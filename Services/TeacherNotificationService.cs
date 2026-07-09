namespace AutoCheck.Services;

public class TeacherNotif
{
    public int      Id        { get; init; }
    public string   Title     { get; init; } = "";
    public string   Body      { get; init; } = "";
    public string   Type      { get; init; } = "comment";
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public bool     IsRead    { get; set; }
}

public sealed class TeacherNotificationService
{
    private const int MaxItems = 200;   // in-memory only — cap so a long uptime can't grow it unbounded

    private readonly object _lock  = new();
    private readonly List<TeacherNotif> _list = [];
    private int _nextId = 1;

    public void Add(string title, string body, string type = "comment")
    {
        lock (_lock)
        {
            _list.Add(new TeacherNotif { Id = _nextId++, Title = title, Body = body, Type = type });
            // Drop the oldest entries once over the cap (list is append-order = oldest first).
            if (_list.Count > MaxItems)
                _list.RemoveRange(0, _list.Count - MaxItems);
        }
    }

    public List<TeacherNotif> GetAll()
    {
        lock (_lock) return [.. _list.OrderByDescending(n => n.CreatedAt)];
    }

    public int UnreadCount
    {
        get { lock (_lock) return _list.Count(n => !n.IsRead); }
    }

    public void MarkAllRead()
    {
        lock (_lock)
            foreach (var n in _list)
                n.IsRead = true;
    }
}
