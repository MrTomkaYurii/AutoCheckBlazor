namespace AutoCheck.Services;

/// <summary>
/// New-account email-domain rule. Configured via Registration:AllowedEmailDomain
/// (comma-separated list; empty ⇒ any domain). Only gates account CREATION —
/// login and existing accounts are never affected.
/// </summary>
public static class RegistrationPolicy
{
    private static string[] Allowed(IConfiguration cfg) =>
        (cfg["Registration:AllowedEmailDomain"] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static bool EmailAllowed(string? email, IConfiguration cfg)
    {
        var allowed = Allowed(cfg);
        if (allowed.Length == 0) return true;
        var at = email?.LastIndexOf('@') ?? -1;
        if (at < 0) return false;
        var domain = email![(at + 1)..].Trim();
        return allowed.Any(d => string.Equals(d, domain, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>User-facing hint, e.g. "Реєстрація лише з поштою @chnu.edu.ua." — empty when unrestricted.</summary>
    public static string Hint(IConfiguration cfg)
    {
        var allowed = Allowed(cfg);
        return allowed.Length == 0 ? "" : $"Реєстрація лише з поштою @{string.Join(" / @", allowed)}.";
    }
}
