using Microsoft.AspNetCore.Identity;

namespace AutoCheck.Data;

// ── Identity user ─────────────────────────────────────────────────────────────

public class AppUser : IdentityUser
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
}

// ── Lab definitions ───────────────────────────────────────────────────────────

public class LabDef
{
    public int Id { get; set; }
    public int Number { get; set; }
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Goal { get; set; }
    public string? BranchName { get; set; }
    public string? SourceDir { get; set; }   // e.g. "sandbox/intro" or "src"
    public bool MergesMain { get; set; }
    public int OrderIndex { get; set; }
    public string? FullMarkdown { get; set; }
    public DateTime? Deadline { get; set; }
    public int AttemptsMax { get; set; } = 3;

    public List<LabTask> Tasks { get; set; } = [];
    public List<Submission> Submissions { get; set; } = [];
}

public class LabTask
{
    public int Id { get; set; }
    public int LabDefId { get; set; }
    public LabDef LabDef { get; set; } = null!;
    public int Number { get; set; }
    public string Title { get; set; } = "";
    public string? Brief { get; set; }
    public int Difficulty { get; set; }

    public List<TaskResult> Results { get; set; } = [];
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

// ── Submissions ───────────────────────────────────────────────────────────────

public class Submission
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public StudentRecord Student { get; set; } = null!;
    public int LabDefId { get; set; }
    public LabDef LabDef { get; set; } = null!;
    public int Status { get; set; }          // LabStatus enum values
    public int? AutoScore { get; set; }
    public int? DefenseScore { get; set; }
    public int? FinalScore { get; set; }
    public int AttemptsUsed { get; set; }
    public int AttemptsMax { get; set; } = 3;
    public string? Deadline { get; set; }
    public bool IsCurrent { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? BranchOverride { get; set; }
    public string? CommitMappingJson { get; set; }
    public int? Attempt1Score { get; set; }
    public int? Attempt2Score { get; set; }
    public int? Attempt3Score { get; set; }

    // Plagiarism gate: submission matched another student's checked work
    public bool PlagiarismFlag { get; set; }
    public string? PlagiarismNote { get; set; }
    /// <summary>Teacher explicitly allowed resubmission despite the match.</summary>
    public bool PlagiarismApproved { get; set; }

    public List<TaskResult> TaskResults { get; set; } = [];
    public List<LabComment> Comments { get; set; } = [];
}

public class TaskResult
{
    public int Id { get; set; }
    public int SubmissionId { get; set; }
    public Submission Submission { get; set; } = null!;
    public int LabTaskId { get; set; }
    public LabTask LabTask { get; set; } = null!;
    public int AttemptNo { get; set; } = 1;   // 1..AttemptsMax — results are kept for every attempt
    public string State { get; set; } = "fail";
    public int Score { get; set; }
    public string? Feedback { get; set; }
    public int TestsPassed { get; set; }
    public int TestsTotal { get; set; }

    public List<DiffEntry> DiffLines { get; set; } = [];
}

public class DiffEntry
{
    public int Id { get; set; }
    public int TaskResultId { get; set; }
    public TaskResult TaskResult { get; set; } = null!;
    public int OrderIndex { get; set; }
    public string Type { get; set; } = "ctx";
    public int? N1 { get; set; }
    public int? N2 { get; set; }
    public string Text { get; set; } = "";
}

// ── Groups ────────────────────────────────────────────────────────────────

public class GroupRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int OrderIndex { get; set; }
}

// ── Comments ──────────────────────────────────────────────────────────────────

public class LabComment
{
    public int Id { get; set; }
    public int SubmissionId { get; set; }
    public Submission Submission { get; set; } = null!;
    public int? TaskResultId { get; set; }   // null = comment on whole lab
    public string AuthorRole { get; set; } = "";   // "teacher" | "student"
    public string AuthorName { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

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
