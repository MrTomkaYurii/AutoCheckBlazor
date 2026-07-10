namespace AutoCheck.Models;

/// <summary>
/// Course-wide points breakdown for a student. All labs together are worth
/// <see cref="TotalMax"/> (100) points; each lab's share is proportional to the
/// summed difficulty (⭐) of its tasks. A student needs <see cref="Threshold"/>
/// points to be admitted to the exam.
/// </summary>
public class PointsBreakdown
{
    public List<LabPoints> Labs = new();

    /// <summary>Points needed for exam admission.</summary>
    public double Threshold = 50;

    /// <summary>Always 100 by construction (labs' MaxPoints sum to this).</summary>
    public double TotalMax => 100;

    /// <summary>Points actually earned = Σ per-lab earned (from finalised grades).</summary>
    public double TotalEarned => Labs.Sum(l => l.Earned);

    public double Percent => TotalMax > 0 ? 100.0 * TotalEarned / TotalMax : 0;
    public bool Admitted => TotalEarned >= Threshold;
    public double Remaining => Math.Max(0, Threshold - TotalEarned);
}

public class LabPoints
{
    public int Number;
    public string Title = "";
    public LabStatus Status;

    public int TaskCount;
    /// <summary>Σ of task difficulties (the lab's weight in the 100-point pool).</summary>
    public int Weight;

    /// <summary>This lab's share of the 100 points.</summary>
    public double MaxPoints;

    public int? Auto;    // auto-check score 0-100 (informational)
    public int? Final;   // final grade 0-100 (0.4·auto + 0.6·defense)

    /// <summary>Points actually earned = MaxPoints × Final/100 (0 until finalised).</summary>
    public double Earned;

    /// <summary>Share of this lab's own points earned so far (= Final %).</summary>
    public double Percent => MaxPoints > 0 ? 100.0 * Earned / MaxPoints : 0;

    /// <summary>Per-task hypothetical split — for demonstration only; the teacher
    /// grades the whole lab integrally, not each task.</summary>
    public List<TaskPoints> Tasks = new();
}

public class TaskPoints
{
    public int Number;
    public string Title = "";
    public int Difficulty;      // ⭐ 1..4
    public double MaxPoints;    // hypothetical share of the lab's points
}
