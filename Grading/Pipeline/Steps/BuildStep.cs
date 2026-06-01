using AutoCheck.Grading.Models;

namespace AutoCheck.Grading.Pipeline.Steps;

/// <summary>
/// Запускає dotnet build для проекту студента.
/// SourceDir вказує де шукати .csproj (наприклад sandbox/intro або src).
/// TODO: реалізувати dotnet build
/// </summary>
public class BuildStep : IGradingStep
{
    public string Name => "Build";

    public Task<GradingContext> RunAsync(GradingContext context, CancellationToken ct = default)
    {
        // TODO:
        // 1. Знайти .csproj в context.SourceDir
        // 2. dotnet build {csprojPath} --nologo
        // 3. exitCode == 0 → context.BuildPassed = true
        // 4. Зберегти вивід у context.BuildOutput
        // 5. Якщо не збудувалось → context.BuildPassed = false (НЕ HasError — це очікуваний результат)

        throw new NotImplementedException("BuildStep not implemented yet");
    }
}
