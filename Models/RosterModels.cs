namespace AutoCheck.Models;

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
