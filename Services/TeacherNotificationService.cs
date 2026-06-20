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
    private readonly object _lock  = new();
    private readonly List<TeacherNotif> _list = [];
    private int _nextId = 1;

    public void Add(string title, string body, string type = "comment")
    {
        lock (_lock)
            _list.Add(new TeacherNotif { Id = _nextId++, Title = title, Body = body, Type = type });
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
