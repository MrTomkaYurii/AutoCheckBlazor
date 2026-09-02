namespace AutoCheck.Models;

public enum LabStatus { Done, Review, Rejected, Locked }

public class Lab
{
    public int Id;
    public string Title = "";
    public LabStatus Status;
    public int? Auto;
    public int? Attempt1;
    public int? Attempt2;
    public int? Attempt3;
    public int? Defense;
    public int? Final;
    public bool Current;
    public DateTime? DeadlineAt;

    public bool IsOverdue => DeadlineAt.HasValue && DateTime.UtcNow > DeadlineAt.Value;
    public bool IsDueSoon => DeadlineAt.HasValue && !IsOverdue
        && (DeadlineAt.Value - DateTime.UtcNow).TotalHours <= 24;
}

// ----- Lab 03 detailed submission view -----
public class LabDetail
{
    public int Id;
    public string Title = "";
    public string Branch = "";
    public DateTime? DeadlineAt;
    public int Auto;
    public int AttemptsUsed;
    public int AttemptsMax;
    public string Intro = "";
    public List<TaskItem> Tasks = new();

    /// <summary>Attempts that have stored grading results, with their scores.</summary>
    public List<AttemptInfo> Attempts = new();
    /// <summary>Which attempt's results are loaded into Tasks (0 = none yet).</summary>
    public int SelectedAttempt;

    // Plagiarism gate state
    public bool PlagFlag;
    public string? PlagNote;
    public bool PlagApproved;
    // Soft structural-similarity suspicion — teacher UI only, never rendered to students.
    public bool PlagSuspect;
    public string? PlagSuspectNote;

    public bool IsOverdue => DeadlineAt.HasValue && DateTime.UtcNow > DeadlineAt.Value;
}

public class AttemptInfo
{
    public int No;
    public int? Score;
    public bool IsBest;   // the attempt AutoScore was taken from
}

public class TaskItem
{
    public string Id = "";
    public int N;
    public string Title = "";
    public string State = "";       // pass | warn | fail
    public int Score;
    public string Brief = "";
    public string Feedback = "";
    public int TestsPassed;
    public int TestsTotal;
    public List<DiffLine> Diff = new();

    public (string Tone, string Bg, string Icon, string Label) View() => State switch
    {
        "pass" => ("var(--st-green)",  "var(--st-green-bg)",  "check", "Пройдено"),
        "warn" => ("var(--st-yellow)", "var(--st-yellow-bg)", "warn",  "Зауваження"),
        _      => ("var(--st-red)",    "var(--st-red-bg)",    "x",     "Помилка"),
    };
    public string ColorClass => State == "pass" ? "green" : State == "warn" ? "yellow" : "red";
}

public class DiffLine
{
    public string Type = "";   // ctx | add | del
    public int? N1;
    public int? N2;
    public string Text = "";
}
