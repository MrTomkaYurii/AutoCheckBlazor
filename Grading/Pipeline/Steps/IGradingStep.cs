using AutoCheck.Grading.Models;

namespace AutoCheck.Grading.Pipeline.Steps;

/// <summary>
/// Один крок пайплайну перевірки.
/// Кожен крок отримує контекст, робить свою роботу, повертає його далі.
/// Якщо крок виставив context.HasError = true — пайплайн зупиняється.
/// </summary>
public interface IGradingStep
{
    string Name { get; }
    Task<GradingContext> RunAsync(GradingContext context, CancellationToken ct = default);
}
