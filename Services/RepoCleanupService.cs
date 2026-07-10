namespace AutoCheck.Services;

/// <summary>
/// Daily janitor for cached student repo clones. <see cref="GradingService"/> clones
/// each repo (full history) under <c>Grading:WorkRoot</c>, keyed by a hash of the repo
/// URL, and reuses it across submissions — but never deletes it. Over a semester
/// (~100 students, dead/renamed repos) that grows without bound and fills the disk.
///
/// This removes clone dirs not used within <c>Grading:RepoRetentionDays</c> (default 7);
/// a pruned clone is simply re-cloned on the student's next submission, so it only
/// costs a one-off clone, never correctness.
/// </summary>
public class RepoCleanupService(
    IConfiguration cfg,
    ILogger<RepoCleanupService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromMinutes(3), ct);   // let startup/seeding finish

        while (!ct.IsCancellationRequested)
        {
            try { Sweep(); }
            catch (Exception ex) { log.LogWarning(ex, "Repo cleanup pass failed"); }

            await Task.Delay(TimeSpan.FromHours(24), ct);
        }
    }

    private void Sweep()
    {
        var root = GradingPaths.WorkRoot(cfg);
        if (!Directory.Exists(root)) return;

        var days   = int.TryParse(cfg["Grading:RepoRetentionDays"], out var d) && d > 0 ? d : 7;
        var cutoff = DateTime.UtcNow.AddDays(-days);

        foreach (var dir in Directory.GetDirectories(root))
        {
            try
            {
                // PrepareRepoAsync stamps LastWriteTime on every use, so an actively
                // submitted repo keeps its clone; only genuinely idle ones fall behind.
                if (Directory.GetLastWriteTimeUtc(dir) >= cutoff) continue;
                DeleteRepoDir(dir);
                log.LogInformation("Pruned idle repo clone: {Dir}", Path.GetFileName(dir));
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Failed to prune repo clone {Dir}", dir);
            }
        }
    }

    /// <summary>
    /// Recursively deletes a clone dir. Git pack files are marked read-only, which makes
    /// a plain Directory.Delete throw on Windows — clear the attribute first.
    /// </summary>
    private static void DeleteRepoDir(string dir)
    {
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            var attr = File.GetAttributes(file);
            if (attr.HasFlag(FileAttributes.ReadOnly))
                File.SetAttributes(file, attr & ~FileAttributes.ReadOnly);
        }
        Directory.Delete(dir, recursive: true);
    }
}
