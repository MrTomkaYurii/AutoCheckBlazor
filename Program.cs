using System.Security.Claims;
using System.Text.Json;
using AutoCheck.Components;
using AutoCheck.Data;
using AutoCheck.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Razor + MudBlazor ─────────────────────────────────────────────────────────
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddMudServices();

// ── EF Core — SQLite ──────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("Default")));

// ── Auth — Keycloak OIDC + Cookie ─────────────────────────────────────────────
var kc = builder.Configuration.GetSection("Keycloak");

builder.Services.AddAuthentication(opt =>
{
    opt.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    opt.DefaultSignInScheme       = CookieAuthenticationDefaults.AuthenticationScheme;
    opt.DefaultChallengeScheme    = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(opt =>
{
    opt.Cookie.HttpOnly = true;
    opt.LoginPath    = "/account/login";
    opt.LogoutPath   = "/account/logout";
    opt.ExpireTimeSpan = TimeSpan.FromHours(8);
    opt.SlidingExpiration = true;
})
.AddOpenIdConnect(opt =>
{
    opt.Authority           = kc["Authority"];
    opt.MetadataAddress     = kc["Authority"] + "/.well-known/openid-configuration";
    opt.ClientId            = kc["ClientId"];
    opt.ClientSecret        = kc["ClientSecret"];
    opt.ResponseType        = OpenIdConnectResponseType.Code;
    opt.SaveTokens          = true;
    opt.GetClaimsFromUserInfoEndpoint = true;
    opt.RequireHttpsMetadata = false;  // Keycloak runs on HTTP locally
    opt.BackchannelTimeout  = TimeSpan.FromSeconds(30);
    // Refresh metadata every 5 min so stale config is auto-recovered
    opt.ConfigurationManager = new Microsoft.IdentityModel.Protocols.ConfigurationManager<
        Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration>(
        opt.MetadataAddress,
        new Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfigurationRetriever(),
        new Microsoft.IdentityModel.Protocols.HttpDocumentRetriever { RequireHttps = false })
    {
        AutomaticRefreshInterval  = TimeSpan.FromMinutes(5),
        RefreshInterval           = TimeSpan.FromSeconds(30),
    };

    opt.Scope.Add("openid");
    opt.Scope.Add("profile");
    opt.Scope.Add("email");

    opt.TokenValidationParameters.NameClaimType = "preferred_username";
    opt.TokenValidationParameters.RoleClaimType = ClaimTypes.Role;

    opt.Events = new OpenIdConnectEvents
    {
        OnRedirectToIdentityProviderForSignOut = async ctx =>
        {
            // Keycloak 24 requires id_token_hint; read it from the saved session token
            var idToken = await ctx.HttpContext.GetTokenAsync("id_token");
            if (!string.IsNullOrEmpty(idToken))
                ctx.ProtocolMessage.IdTokenHint = idToken;
        },
        OnTokenValidated = ctx =>
        {
            var identity = (ClaimsIdentity)ctx.Principal!.Identity!;

            // Keycloak sends roles as a JSON array claim named "roles" (via custom mapper)
            foreach (var c in ctx.Principal.FindAll("roles"))
                if (!string.IsNullOrEmpty(c.Value))
                    identity.AddClaim(new Claim(ClaimTypes.Role, c.Value));

            // Fallback: parse realm_access.roles from JWT
            var ra = ctx.Principal.FindFirst("realm_access")?.Value;
            if (!string.IsNullOrEmpty(ra))
            {
                try
                {
                    using var doc = JsonDocument.Parse(ra);
                    if (doc.RootElement.TryGetProperty("roles", out var roles))
                        foreach (var r in roles.EnumerateArray())
                            if (r.GetString() is { Length: > 0 } role
                                && !identity.HasClaim(ClaimTypes.Role, role))
                                identity.AddClaim(new Claim(ClaimTypes.Role, role));
                }
                catch { /* ignore malformed JSON */ }
            }
            return Task.CompletedTask;
        },
    };
});

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

// ── App services ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<DatabaseSeeder>();
builder.Services.AddScoped<IDataService,           DbDataService>();
builder.Services.AddScoped<IAuthService,           AuthService>();
builder.Services.AddScoped<ILabManagementService,  LabManagementService>();
builder.Services.AddScoped<IGradingService,        GradingService>();
builder.Services.AddScoped<INotificationService,   NotificationService>();
builder.Services.AddScoped<ICommentService,        CommentService>();
builder.Services.AddSingleton<GeminiQuotaService>();
builder.Services.AddSingleton<TeacherNotificationService>();
builder.Services.AddScoped<AppState>();
builder.Services.AddScoped<GitHubService>();
builder.Services.AddScoped<KeycloakAdminService>();
builder.Services.AddHttpClient("github");

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

// Seed DB at startup
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error", createScopeForErrors: true);

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// ── Auth endpoints (must be outside Blazor circuit) ───────────────────────────
app.MapGet("/account/login", async (HttpContext ctx, string? returnUrl) =>
{
    var props = new AuthenticationProperties { RedirectUri = returnUrl ?? "/" };
    await ctx.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme, props);
});

app.MapPost("/account/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme,
        new AuthenticationProperties { RedirectUri = "/" });
});

// Public endpoint so Keycloak registration page can load groups via fetch
app.MapGet("/api/groups", async (AppDbContext db) =>
    await db.Groups.OrderBy(g => g.OrderIndex).ThenBy(g => g.Name).Select(g => g.Name).ToListAsync())
    .AllowAnonymous();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
