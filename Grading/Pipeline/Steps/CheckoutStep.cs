using AutoCheck.Grading.Models;

namespace AutoCheck.Grading.Pipeline.Steps;

/// <summary>
/// Переключається на конкретний коміт який студент здає.
/// TODO: реалізувати git checkout
/// </summary>
public class CheckoutStep : IGradingStep
{
    public string Name => "Checkout";

    public Task<GradingContext> RunAsync(GradingContext context, CancellationToken ct = default)
    {
        // TODO:
        // 1. git checkout {context.CommitSha}
        // 2. Перевірити що CommitSha існує в репо
        // 3. Якщо не існує → context.HasError = true

        throw new NotImplementedException("CheckoutStep not implemented yet");
    }
}
