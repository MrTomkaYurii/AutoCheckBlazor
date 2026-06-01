using AutoCheck.Grading.Models;
using AutoCheck.Grading.Pipeline.Steps;

namespace AutoCheck.Grading.Pipeline;

/// <summary>
/// Оркестратор перевірки. Запускає кроки послідовно.
/// Якщо будь-який крок виставив HasError — зупиняється.
/// </summary>
public class GradingPipeline
{
    private readonly ILogger<GradingPipeline> _log;
    private readonly IEnumerable<IGradingStep> _steps;

    public GradingPipeline(ILogger<GradingPipeline> log, IEnumerable<IGradingStep> steps)
    {
        _log   = log;
        _steps = steps;
    }

    public async Task<GradingResult> RunAsync(
        GradingContext context,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        foreach (var step in _steps)
        {
            if (ct.IsCancellationRequested) break;

            progress?.Report($"{step.Name}…");
            _log.LogInformation("Grading [{Id}] step: {Step}", context.SubmissionId, step.Name);

            try
            {
                context = await step.RunAsync(context, ct);
            }
            catch (NotImplementedException)
            {
                _log.LogWarning("Step {Step} not implemented — skipping", step.Name);
                continue;
            }
            catch (Exception ex)
            {
                context.HasError     = true;
                context.ErrorMessage = $"{step.Name}: {ex.Message}";
                _log.LogError(ex, "Step {Step} failed", step.Name);
            }

            if (context.HasError)
            {
                _log.LogWarning("Pipeline stopped at {Step}: {Error}", step.Name, context.ErrorMessage);
                break;
            }
        }

        return BuildResult(context);
    }

    private static GradingResult BuildResult(GradingContext ctx) => new()
    {
        SubmissionId = ctx.SubmissionId,
        BuildPassed  = ctx.BuildPassed,
        TestsPassed  = ctx.TestsPassed,
        GitHubRunUrl = ctx.GitHubRunUrl,
        FinishedAt   = DateTime.UtcNow,
        Status = ctx.HasError       ? GradingStatus.Error
               : !ctx.BuildPassed   ? GradingStatus.Failed
               : ctx.TestsPassed    ? GradingStatus.Passed
                                    : GradingStatus.Failed,
        Summary = ctx.HasError
            ? $"Помилка: {ctx.ErrorMessage}"
            : ctx.BuildPassed
                ? "Проект компілюється ✓"
                : $"Помилка компіляції:\n{ctx.BuildOutput}",
    };
}
