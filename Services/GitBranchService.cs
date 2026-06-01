using System.Diagnostics;
using System.Text;
using AutoCheck.Models;

namespace AutoCheck.Services;

public class GitBranchService(IConfiguration cfg, ILogger<GitBranchService> log) : IGitBranchService
{
    private string WorkRoot => string.IsNullOrEmpty(cfg["Grading:WorkRoot"])
        ? Path.Combine(Path.GetTempPath(), "autocheck-repos")
        : cfg["Grading:WorkRoot"]!;

    public async Task<List<CommitInfo>> GetCommitsAsync(
        string repoUrl, string branch,
        int maxCount = 50, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(repoUrl))
            return GenerateMockCommits(maxCount);

        var url  = NormalizeGitUrl(repoUrl);
        var slug = Convert.ToHexString(
            System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes(url)))[..10];
        var dir  = Path.Combine(WorkRoot, slug);

        try
        {
            Directory.CreateDirectory(WorkRoot);

            if (!Directory.Exists(dir))
                await Git(".", $"clone --no-single-branch {url} {dir}", ct);
            else
                await Git(dir, "fetch --all --prune", ct);

            // format: SHA|short|subject|author|ISO-date|parents(space-separated)
            var output = await GitOutput(dir,
                $"log {branch} --format=%H|%h|%s|%an|%aI|%P -{maxCount}", ct);

            var result = new List<CommitInfo>();
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var p = line.Split('|', 6);
                if (p.Length < 5) continue;
                var parents = p.Length > 5
                    ? p[5].Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    : Array.Empty<string>();
                if (DateTime.TryParse(p[4], out var date))
                    result.Add(new CommitInfo(p[0], p[1], p[2], p[3], date, parents));
            }
            return result;
        }
        catch (Exception ex)
        {
            log.LogWarning("Git log failed for {Url}/{Branch}: {Msg}", repoUrl, branch, ex.Message);
            return GenerateMockCommits(maxCount);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static List<CommitInfo> GenerateMockCommits(int max)
    {
        var messages = new[]
        {
            "Клас Patient: поля, конструктор, ToString",
            "Клас Doctor, наслідування від Person",
            "Інтерфейс IMedical, реалізація Hospital",
            "Метод CalculateSalary у Employee",
            "Додано enum Priority, рефакторинг",
            "Initial commit",
        };
        var now    = DateTime.UtcNow;
        var result = new List<CommitInfo>();
        for (int i = 0; i < Math.Min(messages.Length, max); i++)
        {
            var sha    = $"a{i:D2}b{i:D2}c{i:D2}d{i:D2}e{i:D2}f{i:D2}aa{i:D2}b{i:D2}c{i:D2}";
            var parent = i < messages.Length - 1
                ? $"a{i+1:D2}b{i+1:D2}c{i+1:D2}d{i+1:D2}e{i+1:D2}f{i+1:D2}aa{i+1:D2}b{i+1:D2}c{i+1:D2}"
                : "";
            result.Add(new CommitInfo(
                sha, sha[..7], messages[i], "student",
                now.AddHours(-i * 3),
                string.IsNullOrEmpty(parent) ? Array.Empty<string>() : [parent]));
        }
        return result;
    }

    private static string NormalizeGitUrl(string raw)
    {
        raw = raw.Trim().TrimEnd('/');
        if (!raw.StartsWith("http")) raw = "https://" + raw;
        if (!raw.EndsWith(".git"))   raw += ".git";
        return raw;
    }

    private async Task Git(string workDir, string args, CancellationToken ct)
    {
        var (exit, _, stderr) = await Run("git", args, workDir, ct);
        if (exit != 0) throw new Exception($"git {args.Split(' ')[0]} failed: {stderr}");
    }

    private async Task<string> GitOutput(string workDir, string args, CancellationToken ct)
    {
        var (_, stdout, _) = await Run("git", args, workDir, ct);
        return stdout;
    }

    private static async Task<(int Exit, string Stdout, string Stderr)> Run(
        string cmd, string args, string workDir, CancellationToken ct)
    {
        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName               = cmd,
                Arguments              = args,
                WorkingDirectory       = workDir == "." ? Directory.GetCurrentDirectory() : workDir,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            }
        };
        proc.Start();
        var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
        var stderr = await proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        return (proc.ExitCode, stdout, stderr);
    }
}
