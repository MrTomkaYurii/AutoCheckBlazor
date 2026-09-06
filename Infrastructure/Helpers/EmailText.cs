namespace AutoCheck.Services;

/// <summary>Shared building blocks for the plain-text emails sent to students.</summary>
public static class EmailText
{
    /// <summary>
    /// Formal opener addressed by the student's profile name, e.g.
    /// «Шановний(-а) Іван Петренко!». Falls back to a neutral greeting when the
    /// profile has no name yet.
    /// </summary>
    public static string Greeting(string? firstName, string? lastName)
    {
        var name = $"{firstName} {lastName}".Trim();
        return name.Length > 0 ? $"Шановний(-а) {name}!" : "Доброго дня!";
    }
}
