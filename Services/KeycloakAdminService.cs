using System.Net.Http.Headers;
using System.Text.Json;

namespace AutoCheck.Services;

/// <summary>
/// Calls the Keycloak Admin REST API to change a user's password.
/// Requires Keycloak:AdminUsername + Keycloak:AdminPassword in appsettings.json.
/// </summary>
public class KeycloakAdminService(IHttpClientFactory http, IConfiguration cfg)
{
    private string Authority  => cfg["Keycloak:Authority"] ?? "";
    private string ClientId   => cfg["Keycloak:ClientId"]  ?? "autocheck-blazor";

    private string BaseUrl
    {
        get
        {
            var idx = Authority.LastIndexOf("/realms/");
            return idx > 0 ? Authority[..idx] : Authority;
        }
    }

    private string RealmName
    {
        get
        {
            var idx = Authority.LastIndexOf("/realms/");
            return idx > 0 ? Authority[(idx + 8)..] : "autocheck";
        }
    }

    /// <summary>Verify the user's current password via Resource Owner Password Credentials grant.</summary>
    public async Task VerifyPasswordAsync(string email, string password)
    {
        var client = http.CreateClient();
        var resp = await client.PostAsync(
            $"{Authority}/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"]  = ClientId,
                ["username"]   = email,
                ["password"]   = password,
            }));

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException("Поточний пароль невірний.");
    }

    /// <summary>Change a Keycloak user's password via the Admin REST API.</summary>
    public async Task ChangePasswordAsync(string keycloakSub, string newPassword)
    {
        var adminToken = await GetAdminTokenAsync();
        var client     = http.CreateClient();

        using var req = new HttpRequestMessage(
            HttpMethod.Put,
            $"{BaseUrl}/admin/realms/{RealmName}/users/{keycloakSub}/reset-password");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        req.Content = JsonContent.Create(new { type = "password", value = newPassword, temporary = false });

        var resp = await client.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Помилка Keycloak {(int)resp.StatusCode}: {body[..Math.Min(body.Length, 200)]}");
        }
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var adminUser = cfg["Keycloak:AdminUsername"] ?? "admin";
        var adminPass = cfg["Keycloak:AdminPassword"] ?? "";

        if (string.IsNullOrEmpty(adminPass))
            throw new InvalidOperationException(
                "Keycloak:AdminPassword не налаштовано. Додайте його до appsettings.json.");

        var client = http.CreateClient();
        var resp = await client.PostAsync(
            $"{BaseUrl}/realms/master/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"]  = "admin-cli",
                ["username"]   = adminUser,
                ["password"]   = adminPass,
            }));

        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Не вдалося отримати admin-токен Keycloak ({(int)resp.StatusCode}). " +
                "Перевірте Keycloak:AdminUsername та Keycloak:AdminPassword в appsettings.json.");

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }
}
