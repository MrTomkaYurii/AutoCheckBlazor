namespace AutoCheck.Models;

public record CommitInfo(string Sha, string Short, string Message, string Author, DateTime Date, string[] Parents);
public record CommitTaskMap(string Sha, int TaskNumber);

public enum LabStatus { Done, Review, Rejected, Locked }

public static class StatusMeta
{
    // (label, color-class-suffix, css-tone-var)
    public static (string Label, string Color, string Tone) Of(LabStatus s) => s switch
    {
        LabStatus.Done     => ("Зараховано",  "green",  "var(--st-green)"),
        LabStatus.Review   => ("На перевірці", "yellow", "var(--st-yellow)"),
        LabStatus.Rejected => ("Відхилено",   "red",    "var(--st-red)"),
        _                  => ("Не здано",    "grey",   "var(--st-grey)"),
    };
}

public class Student
{
    public string FirstName = "";
    public string LastName = "";
    public string Group = "";
    public string Email = "";
    public string Github       = "";
    public string GithubToken = "";
    public string Initials = "";
}

public class Teacher
{
    public string FirstName = "";
    public string LastName = "";
    public string Initials = "";
    public string Title = "";
    public string Course = "";
}

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

    public bool IsOverdue => DeadlineAt.HasValue && DateTime.UtcNow > DeadlineAt.Value;
}

public class AttemptInfo
{
    public int No;
    public int? Score;
    public bool IsBest;   // the attempt AutoScore was taken from
}

/// <summary>Single source of truth for the difficulty-weighted attempt score.</summary>
public static class Scoring
{
    public static int Weighted(IReadOnlyCollection<(int Score, int Difficulty)> items)
    {
        if (items.Count == 0) return 0;
        double totalWeight = items.Sum(i => (double)i.Difficulty);
        if (totalWeight <= 0)   // difficulty not set — fall back to a plain average
            return (int)Math.Round(items.Average(i => (double)i.Score));
        return (int)Math.Round(items.Sum(i => i.Score * (double)i.Difficulty) / totalWeight);
    }
}

/// <summary>Parses the Gemini feedback JSON ({done, issues, analysis}) stored in TaskResult.Feedback.</summary>
public static class FeedbackJson
{
    public static (string[] Done, string[] Issues, string Analysis) Parse(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return ([], [], "");
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(raw);
            var r = doc.RootElement;
            static string[] Arr(System.Text.Json.JsonElement root, string key) =>
                root.TryGetProperty(key, out var el) && el.ValueKind == System.Text.Json.JsonValueKind.Array
                    ? el.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => s.Length > 0).ToArray()
                    : [];
            var analysis = r.TryGetProperty("analysis", out var a) ? a.GetString() ?? "" : "";
            return (Arr(r, "done"), Arr(r, "issues"), analysis);
        }
        catch { return ([], [], raw); }   // legacy plain-text feedback
    }
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

// ----- Teacher roster -----
public class Cell
{
    public LabStatus Status = LabStatus.Locked;
    public int? Auto;
    public int? Defense;
    public int? Final;

    // display kind for the gradebook
    public string Kind => Status switch
    {
        LabStatus.Done => "done",
        LabStatus.Review => "review",
        LabStatus.Rejected => "rejected",
        _ => "locked",
    };
    public string Tone => Status switch
    {
        LabStatus.Done => "var(--st-green)",
        LabStatus.Review => "var(--st-yellow)",
        LabStatus.Rejected => "var(--st-red)",
        _ => "rgba(255,255,255,0.08)",
    };
}

public class RosterStudent
{
    public int Id;
    public string Last = "";
    public string First = "";
    public string Group = "";
    public string Initials = "";
    public List<Cell> Labs = new();
    public int? Avg;
    public int DoneCount;
    public int ReviewCount;
    public int Submissions;

    public void Recompute()
    {
        var finals = Labs.Where(c => c.Status == LabStatus.Done && c.Final.HasValue).Select(c => c.Final!.Value).ToList();
        Avg = finals.Count > 0 ? (int)Math.Round(finals.Average()) : null;
        DoneCount = Labs.Count(c => c.Status == LabStatus.Done);
        ReviewCount = Labs.Count(c => c.Status == LabStatus.Review);
        Submissions = Labs.Count(c => c.Status != LabStatus.Locked);
    }
}

public class ReviewItem
{
    public int StudentId;
    public string Name = "";
    public string Initials = "";
    public string Group = "";
    public int LabId;
    public string LabShort = "";
    public string LabTitle = "";
    public int Auto;
    public string When = "";
    public DateTime? SubmittedAt;
}

public class LabStat
{
    public int Id;
    public string Short = "";
    public string Title = "";
    public int Submitted;
    public int Done;
    public int Review;
    public int Rejected;
    public int? Avg;
    public int? AvgAuto;
    public int Total;
}

public class TeacherStats
{
    public int Students;
    public int VisibleStudents;
    public int Submissions;
    public int PassRate;
    public int PendingReview;
    public int AvgGrade;
}
