using Microsoft.AspNetCore.Identity;

namespace AutoCheck.Data;

// ── Identity user ─────────────────────────────────────────────────────────────

public class AppUser : IdentityUser
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
}

// ── People ────────────────────────────────────────────────────────────────────

public class StudentRecord
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Group { get; set; } = "";
    public string Email { get; set; } = "";
    public string Github      { get; set; } = "";
    public string GithubToken { get; set; } = "";   // optional, для вищого rate limit
    public string Initials { get; set; } = "";

    public List<Submission> Submissions { get; set; } = [];
    public List<Notification> Notifications { get; set; } = [];
    public UserLink? UserLink { get; set; }
}

public class TeacherRecord
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Initials { get; set; } = "";
    public string Email { get; set; } = "";
    public string Title { get; set; } = "";
    public string Course { get; set; } = "";

    public UserLink? UserLink { get; set; }
}

// ── Auth link ─────────────────────────────────────────────────────────────────

/// <summary>Links an Identity user (AppUser.Id) to a StudentRecord or TeacherRecord.</summary>
public class UserLink
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";   // "student" | "teacher"
    public int? StudentId { get; set; }
    public StudentRecord? Student { get; set; }
    public int? TeacherId { get; set; }
    public TeacherRecord? Teacher { get; set; }
}
