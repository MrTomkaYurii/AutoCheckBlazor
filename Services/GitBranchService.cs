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

    // Generates mock commits that simulate a realistic branching history:
    //
    //   M  ← merge feature/lab-03 → main        (parents: C, F)
    //   |\
    //   F  |  Клас Clinic + методи               (parent: E)  [feature]
    //   | C   PatientManager: CRUD               (parent: B)  [main]
    //   E  |  AppointmentManager                 (parent: D)  [feature]
    //   D  |  Клас Appointment                   (parent: B)  [feature, branches from B]
    //   |/
    //   B     Клас Doctor, наслідування          (parent: A)  [main]
    //   A     Клас Patient: поля, конструктор     (no parents) [initial]
    //
    // git log topological order (newest first): M, F, C, E, D, B, A
    private static List<CommitInfo> GenerateMockCommits(int max)
    {
        var now = DateTime.UtcNow;

        // Fixed SHAs so parent references are consistent
        string Sha(string key) => key switch
        {
            "M" => "1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d",
            "F" => "fa1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d",
            "C" => "ca1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d",
            "E" => "ea1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d",
            "D" => "da1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d",
            "B" => "ba1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d",
            "A" => "aa1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d",
            _   => "0000000000000000",
        };

        var all = new List<CommitInfo>
        {
            new(Sha("M"), Sha("M")[..7], "Merge feature/lab-03 → main",       "student", now.AddHours(-1),  [Sha("C"), Sha("F")]),
            new(Sha("F"), Sha("F")[..7], "Клас Clinic: поля та методи",        "student", now.AddHours(-3),  [Sha("E")]),
            new(Sha("C"), Sha("C")[..7], "PatientManager: CRUD та пошук",       "student", now.AddHours(-5),  [Sha("B")]),
            new(Sha("E"), Sha("E")[..7], "AppointmentManager, фільтрація",      "student", now.AddHours(-8),  [Sha("D")]),
            new(Sha("D"), Sha("D")[..7], "Клас Appointment: поля і конструктор","student", now.AddHours(-11), [Sha("B")]),
            new(Sha("B"), Sha("B")[..7], "Клас Doctor, наслідування від Person","student", now.AddHours(-14), [Sha("A")]),
            new(Sha("A"), Sha("A")[..7], "Клас Patient: поля, конструктор",     "student", now.AddHours(-18), Array.Empty<string>()),
        };

        return all.Take(max).ToList();
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
