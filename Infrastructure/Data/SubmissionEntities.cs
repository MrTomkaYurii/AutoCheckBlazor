namespace AutoCheck.Data;

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

    // Plagiarism gate: submission matched another student's checked work → auto-rejected
    public bool PlagiarismFlag { get; set; }
    public string? PlagiarismNote { get; set; }
    /// <summary>Teacher explicitly allowed resubmission despite the match.</summary>
    public bool PlagiarismApproved { get; set; }

    // Soft plagiarism suspicion (structural similarity below the hard-reject bar):
    // teacher-only, never shown to the student, does NOT block or consume an attempt.
    public bool PlagiarismSuspect { get; set; }
    public string? PlagiarismSuspectNote { get; set; }

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
