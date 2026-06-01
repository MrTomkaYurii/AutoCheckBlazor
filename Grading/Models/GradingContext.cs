namespace AutoCheck.Grading.Models;

/// <summary>
/// Передається між кроками пайплайну.
/// Містить все що потрібно для перевірки однієї здачі.
/// </summary>
public class GradingContext
{
    // ── Вхідні дані ───────────────────────────────────────────────────────────
    public int    SubmissionId { get; init; }
    public int    StudentId    { get; init; }
    public string RepoUrl      { get; init; } = "";   // https://github.com/user/repo
    public string CommitSha    { get; init; } = "";   // хеш коміту який здається
    public string Branch       { get; init; } = "";   // наприклад sandbox/intro
    public string SourceDir    { get; init; } = "";   // наприклад sandbox/intro або src
    public int    LabNumber    { get; init; }

    // ── Заповнюється кроками ──────────────────────────────────────────────────
    public bool   BuildPassed       { get; set; }
    public string BuildOutput       { get; set; } = "";
    public bool   TestsPassed       { get; set; }
    public string TestsOutput       { get; set; } = "";
    public string GitHubRunStatus   { get; set; } = "";   // "success" | "failure" | "pending"
    public string GitHubRunUrl      { get; set; } = "";

    // ── Помилка будь-якого кроку ──────────────────────────────────────────────
    public bool   HasError    { get; set; }
    public string ErrorMessage { get; set; } = "";
}
