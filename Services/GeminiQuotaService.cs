namespace AutoCheck.Services;

/// <summary>
/// Singleton — підраховує виклики Gemini API за поточний UTC-день.
/// Скидається автоматично о опівночі UTC.
/// </summary>
public sealed class GeminiQuotaService(IConfiguration cfg)
{
    private readonly object _lock = new();
    private DateOnly _date  = DateOnly.FromDateTime(DateTime.UtcNow);
    private int      _count = 0;

    public int DailyLimit =>
        int.TryParse(cfg["Gemini:DailyLimit"], out var v) && v > 0 ? v : 50;

    /// <summary>Викликати ПЕРЕД фактичним запитом до Gemini.</summary>
    public void RecordCall()
    {
        lock (_lock)
        {
            ResetIfNewDay();
            _count++;
        }
    }

    public int TodayCount
    {
        get { lock (_lock) { ResetIfNewDay(); return _count; } }
    }

    public int Remaining => Math.Max(0, DailyLimit - TodayCount);

    public bool IsExhausted => Remaining <= 0;

    private void ResetIfNewDay()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (_date != today) { _date = today; _count = 0; }
    }
}
