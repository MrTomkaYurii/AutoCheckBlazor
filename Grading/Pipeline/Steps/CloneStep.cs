using AutoCheck.Grading.Models;

namespace AutoCheck.Grading.Pipeline.Steps;

/// <summary>
/// Клонує репозиторій студента у тимчасову папку.
/// Якщо вже клоновано — робить fetch.
/// TODO: реалізувати git clone/fetch
/// </summary>
public class CloneStep : IGradingStep
{
    public string Name => "Clone";

    public Task<GradingContext> RunAsync(GradingContext context, CancellationToken ct = default)
    {
        // TODO:
        // 1. Визначити тимчасову папку: /tmp/autocheck/{submissionId}
        // 2. Якщо не існує → git clone --depth=1 {RepoUrl}
        // 3. Якщо існує → git fetch
        // 4. Після роботи → видалити папку (або залишити для кешу)

        throw new NotImplementedException("CloneStep not implemented yet");
    }
}
