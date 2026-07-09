namespace AutoCheck.Services;

/// <summary>
/// Converts stored UTC timestamps to Kyiv wall-clock time for display.
/// The server runs in UTC (Docker), so <c>DateTime.ToLocalTime()</c> would just
/// return UTC — this resolves the real Europe/Kyiv zone instead, cross-platform.
/// </summary>
public static class KyivTime
{
    private static readonly TimeZoneInfo Tz = Resolve();

    private static TimeZoneInfo Resolve()
    {
        // Windows uses "FLE Standard Time"; Linux/macOS use the IANA id.
        foreach (var id in new[] { "Europe/Kyiv", "Europe/Kiev", "FLE Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch { /* try next id */ }
        }
        // Last resort if no tz database is present: fixed UTC+2 (ignores DST).
        return TimeZoneInfo.CreateCustomTimeZone("Kyiv+2", TimeSpan.FromHours(2), "Kyiv", "Kyiv");
    }

    /// <summary>Projects a UTC instant onto Kyiv local time.</summary>
    public static DateTime FromUtc(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Tz);
}
