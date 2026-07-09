using System.Security.Claims;
using AutoCheck.Components;
using AutoCheck.Data;
using AutoCheck.Services;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Razor + MudBlazor ─────────────────────────────────────────────────────────
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddMudServices();

// ── EF Core — SQLite ──────────────────────────────────────────────────────────
// Blazor Server keeps a DI scope alive for the whole circuit, so a single scoped
// DbContext would be shared across concurrent renders, background timers and long
// grading runs → "A second operation started on this context" crashes. Use a
// factory so every logical operation gets its own short-lived context, and expose
// a scoped shim resolved from that factory purely so ASP.NET Identity (which needs
// a scoped AppDbContext) keeps working inside the per-request auth endpoints.
builder.Services.AddDbContextFactory<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<AppDbContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

// ── Auth — ASP.NET Core Identity (+ optional Google) ─────────────────────────
builder.Services.AddIdentity<AppUser, IdentityRole>(opt =>
{
    opt.User.RequireUniqueEmail = true;
    opt.SignIn.RequireConfirmedAccount = false;
    opt.Password.RequiredLength = 6;
    opt.Password.RequireNonAlphanumeric = false;
    opt.Password.RequireUppercase = false;
    opt.Password.RequireLowercase = false;
    opt.Password.RequireDigit = false;
    opt.Lockout.MaxFailedAccessAttempts = 8;
    opt.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// Validate the security stamp on every request: cookies that survive a DB reset
// (user no longer exists) get rejected and signed out instead of hanging the UI.
builder.Services.Configure<SecurityStampValidatorOptions>(o =>
    o.ValidationInterval = TimeSpan.Zero);

builder.Services.ConfigureApplicationCookie(opt =>
{
    opt.Cookie.HttpOnly = true;
    // Behind a reverse proxy in production, TLS is terminated upstream —
    // ForwardedHeaders below makes the request look HTTPS so this is safe.
    opt.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    opt.LoginPath = "/";
    opt.AccessDeniedPath = "/";
    opt.ExpireTimeSpan = TimeSpan.FromHours(8);
    opt.SlidingExpiration = true;
});

// Trust X-Forwarded-For/-Proto from the reverse proxy (Nginx/Caddy/Traefik) so
// the app sees the real client scheme/IP instead of the proxy's local connection.
// SECURITY: only trust the proxy network(s) listed in ForwardedHeaders:KnownNetworks
// (CIDR, comma/space separated) — otherwise any client could spoof its IP/scheme.
// If none are configured we fall back to trusting all proxies (single-host Docker
// default) but log a warning so production deployments tighten it.
builder.Services.Configure<ForwardedHeadersOptions>(opt =>
{
    opt.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    opt.KnownIPNetworks.Clear();
    opt.KnownProxies.Clear();

    var cidrs = (builder.Configuration["ForwardedHeaders:KnownNetworks"] ?? "")
        .Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    foreach (var cidr in cidrs)
    {
        var parts = cidr.Split('/');
        if (parts.Length == 2
            && System.Net.IPAddress.TryParse(parts[0], out var prefix)
            && int.TryParse(parts[1], out var len))
            opt.KnownIPNetworks.Add(new System.Net.IPNetwork(prefix, len));
    }
    if (opt.KnownIPNetworks.Count == 0)
        Console.Error.WriteLine(
            "[warn] ForwardedHeaders: no KnownNetworks configured — trusting X-Forwarded-* " +
            "from any source. Set ForwardedHeaders:KnownNetworks to your proxy subnet in production.");
});

// Google login is optional — enabled only when ClientId is configured
var googleCfg = builder.Configuration.GetSection("Authentication:Google");
if (!string.IsNullOrEmpty(googleCfg["ClientId"]))
{
    builder.Services.AddAuthentication().AddGoogle(opt =>
    {
        opt.ClientId     = googleCfg["ClientId"]!;
        opt.ClientSecret = googleCfg["ClientSecret"] ?? "";
        opt.SignInScheme = IdentityConstants.ExternalScheme;
    });
}

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

// Data Protection with a fixed key folder — encrypted GitHub tokens survive restarts
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(
        Path.Combine(builder.Environment.ContentRootPath, "dp-keys")))
    .SetApplicationName("AutoCheck");

// ── App services ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<DatabaseSeeder>();
builder.Services.AddScoped<IDataService,           DbDataService>();
builder.Services.AddScoped<IAuthService,           AuthService>();
builder.Services.AddScoped<ILabManagementService,  LabManagementService>();
builder.Services.AddScoped<IGradingService,        GradingService>();
builder.Services.AddScoped<INotificationService,   NotificationService>();
builder.Services.AddScoped<ICommentService,        CommentService>();
builder.Services.AddSingleton<GeminiQuotaService>();
builder.Services.AddSingleton<GradingQueueService>();
builder.Services.AddSingleton<EmailService>();
builder.Services.AddSingleton<TokenProtector>();
builder.Services.AddScoped<PlagiarismService>();
builder.Services.AddHostedService<DeadlineReminderService>();
builder.Services.AddHostedService<BackupService>();
builder.Services.AddSingleton<TeacherNotificationService>();
builder.Services.AddScoped<AppState>();
builder.Services.AddScoped<GitHubService>();
builder.Services.AddHttpClient("github");
// Pooled clients for Gemini — avoids socket exhaustion from `new HttpClient()` per grade.
builder.Services.AddHttpClient("gemini", c => c.Timeout = TimeSpan.FromSeconds(120));
builder.Services.AddHttpClient("gemini-health", c => c.Timeout = TimeSpan.FromSeconds(10));

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

// Seed DB at startup
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
}

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error", createScopeForErrors: true);

app.MapGet("/health", () => Results.Ok("healthy")).AllowAnonymous();

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// ── Auth endpoints (must be outside Blazor circuit — they set/clear cookies) ──

static string SafeReturnUrl(string? url) =>
    !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//") ? url : "/";

// Email + password login (plain form POST from the login page)
app.MapPost("/account/login", async (HttpContext ctx,
    SignInManager<AppUser> signIn, UserManager<AppUser> users) =>
{
    var form      = await ctx.Request.ReadFormAsync();
    var email     = form["email"].ToString().Trim();
    var password  = form["password"].ToString();
    var returnUrl = SafeReturnUrl(form["returnUrl"]);

    string Fail(string msg) =>
        $"/?error={Uri.EscapeDataString(msg)}&email={Uri.EscapeDataString(email)}" +
        $"&returnUrl={Uri.EscapeDataString(returnUrl)}";

    if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        return Results.Redirect(Fail("Введіть email і пароль."));

    var user = await users.FindByEmailAsync(email);
    if (user == null)
        return Results.Redirect(Fail("Користувача з таким email не знайдено."));

    var result = await signIn.PasswordSignInAsync(user, password,
        isPersistent: true, lockoutOnFailure: true);

    if (result.IsLockedOut)
        return Results.Redirect(Fail("Забагато невдалих спроб — акаунт тимчасово заблоковано. Спробуйте за 5 хвилин."));
    if (!result.Succeeded)
        return Results.Redirect(Fail(user.PasswordHash == null
            ? "Для цього акаунта пароль не встановлено — увійдіть через Google."
            : "Невірний пароль."));

    return Results.Redirect(returnUrl);
}).AllowAnonymous().DisableAntiforgery();

// Student self-registration (plain form POST from /register)
app.MapPost("/account/register", async (HttpContext ctx,
    SignInManager<AppUser> signIn, UserManager<AppUser> users, IAuthService auth,
    AppDbContext db) =>
{
    var form      = await ctx.Request.ReadFormAsync();
    var firstName = form["firstName"].ToString().Trim();
    var lastName  = form["lastName"].ToString().Trim();
    var email     = form["email"].ToString().Trim();
    var group     = form["group"].ToString().Trim();
    var password  = form["password"].ToString();
    var confirm   = form["confirm"].ToString();

    string Fail(string msg) =>
        $"/register?error={Uri.EscapeDataString(msg)}" +
        $"&firstName={Uri.EscapeDataString(firstName)}&lastName={Uri.EscapeDataString(lastName)}" +
        $"&email={Uri.EscapeDataString(email)}&group={Uri.EscapeDataString(group)}";

    if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName))
        return Results.Redirect(Fail("Вкажіть ім'я та прізвище."));
    if (string.IsNullOrEmpty(email) || !email.Contains('@'))
        return Results.Redirect(Fail("Вкажіть коректний email."));
    if (string.IsNullOrEmpty(group) || !await db.Groups.AnyAsync(g => g.Name == group))
        return Results.Redirect(Fail("Оберіть групу зі списку."));
    if (password.Length < 6)
        return Results.Redirect(Fail("Пароль має містити щонайменше 6 символів."));
    if (password != confirm)
        return Results.Redirect(Fail("Паролі не збігаються."));
    if (await users.FindByEmailAsync(email) != null)
        return Results.Redirect(Fail("Користувач з таким email вже зареєстрований — увійдіть."));

    var user = new AppUser
    {
        UserName = email, Email = email, EmailConfirmed = true,
        FirstName = firstName, LastName = lastName,
    };
    var created = await users.CreateAsync(user, password);
    if (!created.Succeeded)
        return Results.Redirect(Fail(string.Join(" ", created.Errors.Select(e => e.Description))));

    await users.AddToRoleAsync(user, "student");
    await auth.LinkStudentAsync(user, group);
    await signIn.SignInAsync(user, isPersistent: true);
    return Results.Redirect("/student");
}).AllowAnonymous().DisableAntiforgery();

// Google sign-in / sign-up
app.MapGet("/account/google-login", (SignInManager<AppUser> signIn, string? returnUrl) =>
{
    var redirect = "/account/google-callback?returnUrl=" + Uri.EscapeDataString(SafeReturnUrl(returnUrl));
    var props = signIn.ConfigureExternalAuthenticationProperties(
        GoogleDefaults.AuthenticationScheme, redirect);
    return Results.Challenge(props, [GoogleDefaults.AuthenticationScheme]);
}).AllowAnonymous();

app.MapGet("/account/google-callback", async (
    SignInManager<AppUser> signIn, UserManager<AppUser> users, IAuthService auth,
    string? returnUrl) =>
{
    var dest = SafeReturnUrl(returnUrl);
    string Fail(string msg) => "/?error=" + Uri.EscapeDataString(msg);

    var info = await signIn.GetExternalLoginInfoAsync();
    if (info == null)
        return Results.Redirect(Fail("Не вдалося увійти через Google. Спробуйте ще раз."));

    // Known Google account → straight sign-in
    var result = await signIn.ExternalLoginSignInAsync(
        info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: true);
    if (result.Succeeded)
        return Results.Redirect(dest);
    // A locked-out account (e.g. too many failed password attempts) must stay locked
    // out here too — falling through to the new-account/sign-in path below would let
    // Google login bypass the lockout entirely, since SignInAsync doesn't check it.
    if (result.IsLockedOut)
        return Results.Redirect(Fail("Акаунт тимчасово заблоковано через забагато невдалих спроб входу. Спробуйте пізніше."));

    var email = info.Principal.FindFirstValue(ClaimTypes.Email);
    if (string.IsNullOrEmpty(email))
        return Results.Redirect(Fail("Google не надав email. Використайте вхід з паролем."));

    var user = await users.FindByEmailAsync(email);
    var isNew = user == null;
    if (user == null)
    {
        user = new AppUser
        {
            UserName = email, Email = email, EmailConfirmed = true,
            FirstName = info.Principal.FindFirstValue(ClaimTypes.GivenName) ?? "",
            LastName  = info.Principal.FindFirstValue(ClaimTypes.Surname)   ?? "",
        };
        var created = await users.CreateAsync(user);
        if (!created.Succeeded)
            return Results.Redirect(Fail(string.Join(" ", created.Errors.Select(e => e.Description))));
        await users.AddToRoleAsync(user, "student");
        await auth.LinkStudentAsync(user, "");   // group is chosen at /onboarding
    }

    // Link the Google login to the account. A failure here means the external login
    // wasn't attached, so a later ExternalLoginSignInAsync would keep missing — surface
    // it instead of silently signing in a half-linked account (existing users can still
    // fall back to password login).
    var linked = await users.AddLoginAsync(user, info);
    if (!linked.Succeeded)
        return Results.Redirect(Fail(isNew
            ? "Не вдалося звʼязати акаунт Google. Спробуйте ще раз."
            : "Цей email уже має акаунт. Увійдіть з паролем, потім привʼяжіть Google у профілі."));

    await signIn.SignInAsync(user, isPersistent: true);
    return Results.Redirect(isNew ? "/onboarding" : dest);
}).AllowAnonymous();

app.MapPost("/account/logout", async (SignInManager<AppUser> signIn) =>
{
    await signIn.SignOutAsync();
    return Results.Redirect("/");
});

// Re-issues the auth cookie after a security-stamp change (e.g. password change)
app.MapGet("/account/refresh-signin", async (HttpContext ctx,
    SignInManager<AppUser> signIn, UserManager<AppUser> users, string? returnUrl) =>
{
    var user = await users.GetUserAsync(ctx.User);
    if (user != null) await signIn.RefreshSignInAsync(user);
    return Results.Redirect(SafeReturnUrl(returnUrl));
}).RequireAuthorization();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
