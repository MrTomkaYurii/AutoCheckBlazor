using AutoCheck.Grading.Models;

namespace AutoCheck.Grading.Pipeline.Steps;

/// <summary>
/// Забирає результат GitHub Actions для конкретного коміту.
/// GitHub сам білдить і тестує — ми тільки читаємо результат через API.
/// TODO: реалізувати запит до GitHub API
/// </summary>
public class GitHubActionsStep : IGradingStep
{
    public string Name => "GitHubActions";

    public Task<GradingContext> RunAsync(GradingContext context, CancellationToken ct = default)
    {
        // TODO:
        // 1. Розпарсити owner/repo з context.RepoUrl
        // 2. GET https://api.github.com/repos/{owner}/{repo}/commits/{sha}/check-runs
        //    Headers: Authorization: Bearer {GitHubToken}
        // 3. Знайти run з name що відповідає нашому workflow
        // 4. status == "completed" && conclusion == "success" → context.TestsPassed = true
        // 5. Зберегти context.GitHubRunUrl для посилання студенту
        // 6. Якщо ще "in_progress" → чекати або повернути Pending

        throw new NotImplementedException("GitHubActionsStep not implemented yet");
    }
}
