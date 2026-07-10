namespace AutoCheck.Services;

/// <summary>
/// Shared resolution of the grading work root — the folder where student repos are
/// cloned/cached. Used by both <see cref="GradingService"/> (writer) and
/// <see cref="RepoCleanupService"/> (janitor) so the path is defined in one place.
/// </summary>
public static class GradingPaths
{
    public static string WorkRoot(IConfiguration cfg) =>
        string.IsNullOrEmpty(cfg["Grading:WorkRoot"])
            ? Path.Combine(Path.GetTempPath(), "autocheck-repos")
            : cfg["Grading:WorkRoot"]!;
}
