using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace AutoCheck.Services;

/// <summary>
/// Mirrors each DB backup into a dedicated private git repo — off-host redundancy in case
/// the whole Docker host / volume set is lost, not just the app container.
/// Keeps only the newest N backup files; older ones are removed in the same commit that
/// adds the new one, so the working tree stays small (history still grows, by design —
/// no force-push, so nothing is ever silently lost from git's own history either).
/// No-op if Backup:Git:RemoteUrl / Token aren't configured.
/// </summary>
public static class GitBackupSync
{
    public static async Task SyncAsync(string backupFilePath, IConfiguration cfg, ILogger log, CancellationToken ct = default)
    {
        var remoteUrl = cfg["Backup:Git:RemoteUrl"];
        var token     = cfg["Backup:Git:Token"];
        if (string.IsNullOrEmpty(remoteUrl) || string.IsNullOrEmpty(token))
            return;

        var repoDir   = string.IsNullOrEmpty(cfg["Backup:Git:RepoDir"]) ? "/app/backup-repo" : cfg["Backup:Git:RepoDir"]!;
        var keepLast  = int.TryParse(cfg["Backup:Git:KeepLast"], out var k) ? Math.Max(1, k) : 14;
        var userName  = cfg["Backup:Git:UserName"]  ?? "AutoCheck Backup";
        var userEmail = cfg["Backup:Git:UserEmail"] ?? "backup@autocheck.local";

        // GitHub PAT over HTTPS: any username works, token goes in the password slot.
        // Passed as a transient extraheader (-c) so it's never persisted into .git/config.
        var authHeader = "AUTHORIZATION: basic " +
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"x-access-token:{token}"));

        try
        {
            if (!Directory.Exists(Path.Combine(repoDir, ".git")))
            {
                Directory.CreateDirectory(repoDir);
                var clone = await RunGitAsync(repoDir, authHeader, ["clone", remoteUrl, "."], ct);
                if (clone.ExitCode != 0)
                {
                    log.LogWarning("Git backup: clone failed: {Err}", clone.StdErr);
                    return;
                }
                await RunGitAsync(repoDir, authHeader, ["config", "user.name", userName], ct);
                await RunGitAsync(repoDir, authHeader, ["config", "user.email", userEmail], ct);
            }
            else
            {
                var fetch = await RunGitAsync(repoDir, authHeader, ["fetch", "origin"], ct);
                if (fetch.ExitCode == 0)
                    await RunGitAsync(repoDir, authHeader, ["reset", "--hard", "@{u}"], ct);
            }

            // Compress into the repo working tree — sqlite files compress well and this
            // keeps the mirrored history considerably smaller than raw .db per commit.
            var destName = Path.GetFileName(backupFilePath) + ".gz";
            var destPath = Path.Combine(repoDir, destName);
            await using (var src = File.OpenRead(backupFilePath))
            await using (var dst = File.Create(destPath))
            await using (var gz  = new GZipStream(dst, CompressionLevel.Optimal))
                await src.CopyToAsync(gz, ct);

            foreach (var old in Directory.GetFiles(repoDir, "autocheck-*.db.gz")
                                          .OrderByDescending(f => f, StringComparer.Ordinal)
                                          .Skip(keepLast))
                File.Delete(old);

            await RunGitAsync(repoDir, authHeader, ["add", "-A"], ct);
            var commit = await RunGitAsync(repoDir, authHeader,
                ["commit", "-m", $"backup: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC"], ct);
            // Non-zero + "nothing to commit" just means this backup is byte-identical to the last one.
            if (commit.ExitCode != 0 && !commit.StdOut.Contains("nothing to commit"))
            {
                log.LogWarning("Git backup: commit failed: {Err}", commit.StdErr);
                return;
            }

            var push = await RunGitAsync(repoDir, authHeader, ["push", "origin", "HEAD"], ct);
            if (push.ExitCode != 0)
                log.LogWarning("Git backup: push failed: {Err}", push.StdErr);
            else
                log.LogInformation("Git backup: pushed {File}", destName);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Git backup sync failed");
        }
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunGitAsync(
        string workDir, string authHeader, string[] args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add($"http.extraheader={authHeader}");
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var proc = Process.Start(psi)!;
        var stdOutTask = proc.StandardOutput.ReadToEndAsync(ct);
        var stdErrTask = proc.StandardError.ReadToEndAsync(ct);
        await Task.WhenAll(stdOutTask, stdErrTask);
        await proc.WaitForExitAsync(ct);
        return (proc.ExitCode, stdOutTask.Result, stdErrTask.Result);
    }
}
