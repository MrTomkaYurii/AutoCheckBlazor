using Microsoft.AspNetCore.DataProtection;

namespace AutoCheck.Services;

/// <summary>
/// Encrypts student GitHub tokens at rest (ASP.NET Data Protection).
/// Legacy plaintext values pass through Unprotect unchanged and get encrypted
/// on the next profile save.
/// </summary>
public class TokenProtector(IDataProtectionProvider provider)
{
    private const string Prefix = "enc:v1:";
    private IDataProtector Protector => provider.CreateProtector("AutoCheck.GithubTokens");

    public string Protect(string? plain) =>
        string.IsNullOrEmpty(plain) ? "" : Prefix + Protector.Protect(plain);

    public string Unprotect(string? stored)
    {
        if (string.IsNullOrEmpty(stored)) return "";
        if (!stored.StartsWith(Prefix)) return stored;   // legacy plaintext
        try
        {
            return Protector.Unprotect(stored[Prefix.Length..]);
        }
        catch
        {
            // key ring lost (e.g. dp-keys deleted) — token unusable, treat as unset
            return "";
        }
    }
}
