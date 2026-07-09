namespace AutoCheck.Data;

// ── Grade audit ───────────────────────────────────────────────────────────────

/// <summary>Who changed what in grading — for disputed-grade situations.</summary>
public class GradeAudit
{
    public int Id { get; set; }
    public int SubmissionId { get; set; }
    public string Actor { get; set; } = "";      // teacher display name
    public string Action { get; set; } = "";     // "grade" | "reject" | "extra-attempt"
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}

// ── Notifications ─────────────────────────────────────────────────────────────

public class Notification
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public StudentRecord Student { get; set; } = null!;
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Type { get; set; } = "info";  // "grade" | "grading" | "info"
}
