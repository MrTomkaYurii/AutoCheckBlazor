namespace AutoCheck.Data;

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

// ── Groups ────────────────────────────────────────────────────────────────

public class GroupRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int OrderIndex { get; set; }
}
