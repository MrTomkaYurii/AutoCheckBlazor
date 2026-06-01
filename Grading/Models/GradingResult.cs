namespace AutoCheck.Grading.Models;

public enum GradingStatus
{
    Pending,    // в черзі
    Running,    // виконується
    Passed,     // все ок
    Failed,     // є помилки
    Error,      // щось пішло не так (не репо, не клонується тощо)
}

/// <summary>
/// Фінальний результат перевірки що зберігається в БД.
/// </summary>
public class GradingResult
{
    public int           SubmissionId { get; set; }
    public GradingStatus Status       { get; set; }
    public bool          BuildPassed  { get; set; }
    public bool          TestsPassed  { get; set; }
    public string        GitHubRunUrl { get; set; } = "";
    public string        Summary      { get; set; } = "";   // короткий текст для студента
    public DateTime      FinishedAt   { get; set; } = DateTime.UtcNow;
}
