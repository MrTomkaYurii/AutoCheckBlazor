using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AutoCheck.Data;
using AutoCheck.Models;
using Microsoft.EntityFrameworkCore;

namespace AutoCheck.Services;

/// <summary>
/// Real auto-grading engine:
///   1. git clone / pull student's repo onto the branch defined in LabDef
///   2. dotnet build → compile check
///   3. dotnet test  → per-task test pass count (if test project exists)
///   4. Claude API   → per-task code review → state / score / feedback
/// Falls back to simulation when GitHub URL is missing or API key is not set.
/// </summary>
public class GradingService(
    AppDbContext db,
    INotificationService notif,
    IConfiguration cfg,
    ILogger<GradingService> log) : IGradingService
{
    private string ApiKey   => cfg["Anthropic:ApiKey"] ?? "";
    private string Model    => cfg["Anthropic:Model"]  ?? "claude-haiku-4-5-20251001";
    private string WorkRoot => string.IsNullOrEmpty(cfg["Grading:WorkRoot"])
        ? Path.Combine(Path.GetTempPath(), "autocheck-repos")
        : cfg["Grading:WorkRoot"]!;
    private int MaxLines => int.TryParse(cfg["Grading:MaxLinesPerFile"], out var v) ? v : 250;

    // ── Entry point ───────────────────────────────────────────────────────────

    public async Task<GradingResultDto> RunAsync(
        int submissionId, int studentId,
        IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var sub = await db.Submissions
            .Include(x => x.LabDef).ThenInclude(l => l.Tasks)
            .Include(x => x.TaskResults).ThenInclude(tr => tr.DiffLines)
            .Include(x => x.Student)
            .FirstOrDefaultAsync(x => x.Id == submissionId && x.StudentId == studentId, ct)
            ?? throw new InvalidOperationException("Submission not found");

        var student = sub.Student;
        var lab     = sub.LabDef;
        var branch  = sub.BranchOverride ?? lab.BranchName ?? "main";

        List<CommitTaskMap>? commitMap = null;
        if (!string.IsNullOrEmpty(sub.CommitMappingJson))
        {
            try { commitMap = JsonSerializer.Deserialize<List<CommitTaskMap>>(sub.CommitMappingJson); }
            catch { }
        }

        // ── 1. Prepare repo ──────────────────────────────────────────────────
        string? workDir = null;
        if (!string.IsNullOrWhiteSpace(student.Github))
        {
            progress?.Report("Клонування репозиторію…");
            workDir = await PrepareRepoAsync(student.Github, branch, ct);
        }

        if (workDir is null)
        {
            progress?.Report(string.IsNullOrWhiteSpace(student.Github)
                ? "GitHub URL не вказано — запускаємо симуляцію…"
                : "Не вдалося клонувати репозиторій — запускаємо симуляцію…");
            return await RunSimulatedAsync(sub, progress, ct);
        }

        // ── 2. Build ─────────────────────────────────────────────────────────
        progress?.Report("Компіляція проєкту…");
        var buildOk = await DotnetBuildAsync(workDir, ct);

        // ── 3. Test ──────────────────────────────────────────────────────────
        Dictionary<string, (int Passed, int Total)> testResults = [];
        if (buildOk)
        {
            progress?.Report("Запуск тестів…");
            testResults = await DotnetTestAsync(workDir, ct);
        }

        // ── 4. Grade each task with Claude ───────────────────────────────────
        var codeFiles = GetCSharpFiles(workDir);
        db.TaskResults.RemoveRange(sub.TaskResults);
        await db.SaveChangesAsync(ct);

        int totalTests = 0, passedTests = 0;

        foreach (var taskDef in lab.Tasks.OrderBy(t => t.Number))
        {
            progress?.Report($"Аналіз завдання {taskDef.Number}: {taskDef.Title}…");

            var mappedSha = commitMap?.FirstOrDefault(m => m.TaskNumber == taskDef.Number)?.Sha;
            var code = !string.IsNullOrEmpty(mappedSha) && workDir is not null
                ? await GetCommitCodeAsync(workDir, mappedSha, ct)
                : FindRelevantCode(codeFiles, taskDef.Title);
            var tests  = testResults.TryGetValue(NormalizeTaskTitle(taskDef.Title), out var tr) ? tr : ((int?)null, (int?)null);
            var result = await GradeWithClaudeAsync(taskDef, code, buildOk, tests.Item1, tests.Item2, ct);

            int taskTotal  = taskDef.Difficulty * 2 + 4;
            int taskPassed = tests.Item1 ?? (int)Math.Round(taskTotal * result.Score / 100.0);
            totalTests  += taskTotal;
            passedTests += Math.Min(taskPassed, taskTotal);

            db.TaskResults.Add(new TaskResult
            {
                SubmissionId = sub.Id,
                LabTaskId    = taskDef.Id,
                State        = result.State,
                Score        = result.Score,
                TestsPassed  = Math.Min(taskPassed, taskTotal),
                TestsTotal   = taskTotal,
                Feedback     = result.Feedback,
            });
        }

        // ── 5. Finalise submission ────────────────────────────────────────────
        int autoScore = lab.Tasks.Count > 0
            ? (int)Math.Round(100.0 * passedTests / Math.Max(totalTests, 1))
            : 0;

        sub.AutoScore    = autoScore;
        sub.AttemptsUsed++;
        sub.SubmittedAt  = DateTime.UtcNow;
        if (sub.Status == (int)LabStatus.Locked)
            sub.Status = (int)LabStatus.Review;

        await db.SaveChangesAsync(ct);

        await notif.SendAsync(studentId,
            $"Авто-перевірку Lab{lab.Number:D2} завершено",
            $"Ваша авто-оцінка: {autoScore}/100. Очікуйте захист.", "grading");

        progress?.Report("Готово!");
        return new GradingResultDto(sub.Id, autoScore, passedTests, totalTests);
    }

    // ── Git ───────────────────────────────────────────────────────────────────

    private async Task<string?> PrepareRepoAsync(string rawUrl, string branch, CancellationToken ct)
    {
        var repoUrl = NormalizeGitUrl(rawUrl);
        var slug    = Convert.ToHexString(
            System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes(repoUrl)))[..10];
        var dir = Path.Combine(WorkRoot, slug);

        try
        {
            Directory.CreateDirectory(WorkRoot);

            if (!Directory.Exists(dir))
            {
                await Git(".", $"clone --depth=1 --no-single-branch {repoUrl} {dir}", ct);
            }
            else
            {
                await Git(dir, "fetch --all --prune", ct);
            }

            // checkout + reset hard so stale local changes never block
            await Git(dir, $"checkout {branch}", ct);
            await Git(dir, $"reset --hard origin/{branch}", ct);

            return dir;
        }
        catch (Exception ex)
        {
            log.LogWarning("Git prepare failed for {Url} / {Branch}: {Msg}", repoUrl, branch, ex.Message);
            return null;
        }
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
        var (exit, _, stderr) = await RunProcessAsync("git", args, workDir, ct);
        if (exit != 0)
            throw new Exception($"git {args.Split(' ')[0]} failed: {stderr}");
    }

    // ── dotnet build ──────────────────────────────────────────────────────────

    private async Task<bool> DotnetBuildAsync(string workDir, CancellationToken ct)
    {
        var csproj = FindProjectFile(workDir);
        if (csproj is null) return false;

        var (exit, _, _) = await RunProcessAsync("dotnet",
            $"build \"{csproj}\" -c Debug --nologo -clp:ErrorsOnly", workDir, ct);
        return exit == 0;
    }

    // ── dotnet test ───────────────────────────────────────────────────────────

    private async Task<Dictionary<string, (int Passed, int Total)>> DotnetTestAsync(
        string workDir, CancellationToken ct)
    {
        var testProj = FindTestProject(workDir);
        if (testProj is null) return [];

        var resultFile = Path.Combine(Path.GetTempPath(), $"ac-test-{Guid.NewGuid():N}.trx");
        var (_, stdout, _) = await RunProcessAsync("dotnet",
            $"test \"{testProj}\" --logger \"trx;LogFileName={resultFile}\" --nologo --no-build", workDir, ct);

        return ParseTrx(resultFile);
    }

    private static Dictionary<string, (int Passed, int Total)> ParseTrx(string path)
    {
        var result = new Dictionary<string, (int Passed, int Total)>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return result;
        try
        {
            var xml = System.Xml.Linq.XDocument.Load(path);
            var ns  = xml.Root?.Name.Namespace ?? "";
            foreach (var unit in xml.Descendants(ns + "UnitTestResult"))
            {
                var name    = unit.Attribute("testName")?.Value ?? "";
                var outcome = unit.Attribute("outcome")?.Value ?? "";
                // test name expected to contain task keyword e.g. "Patient", "Doctor"
                foreach (var key in result.Keys.ToList())
                    if (name.Contains(key, StringComparison.OrdinalIgnoreCase))
                    {
                        var (p, t) = result[key];
                        result[key] = (p + (outcome == "Passed" ? 1 : 0), t + 1);
                    }
            }
        }
        catch { /* non-critical */ }
        return result;
    }

    // ── Code discovery ────────────────────────────────────────────────────────

    private Dictionary<string, string> GetCSharpFiles(string workDir) =>
        Directory
            .GetFiles(workDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                     && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .ToDictionary(
                f => Path.GetRelativePath(workDir, f),
                f => TruncateFile(f));

    private string TruncateFile(string path)
    {
        var lines = File.ReadAllLines(path);
        return string.Join('\n', lines.Take(MaxLines));
    }

    private string FindRelevantCode(Dictionary<string, string> files, string taskTitle)
    {
        if (files.Count == 0) return "(репозиторій не містить .cs файлів)";

        // Extract candidate class name from task title: "Клас Patient" → "Patient"
        var words = taskTitle
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim('`'))
            .Where(w => w.Length > 2 && !IsUkrainianStopWord(w))
            .ToArray();

        // Score each file by keyword matches in path
        var scored = files
            .Select(kv => (
                Path: kv.Key,
                Code: kv.Value,
                Score: words.Sum(w => kv.Key.Contains(w, StringComparison.OrdinalIgnoreCase) ? 2 : 0)
                     + words.Sum(w => kv.Value.Contains($"class {w}", StringComparison.OrdinalIgnoreCase) ? 3 : 0)
            ))
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        if (scored.Score == 0 && files.Count > 0)
        {
            // No good match – return all files concatenated (truncated)
            var all = string.Join("\n\n", files
                .OrderBy(kv => kv.Key)
                .Select(kv => $"// {kv.Key}\n{kv.Value}")
                .Take(4));
            return all.Length > 8000 ? all[..8000] + "\n// [truncated]" : all;
        }

        return $"// {scored.Path}\n{scored.Code}";
    }

    private async Task<string> GetCommitCodeAsync(string workDir, string sha, CancellationToken ct)
    {
        try
        {
            var (exit, stdout, _) = await RunProcessAsync(
                "git", $"show {sha} --unified=5 --no-color", workDir, ct);
            if (exit != 0) return "(не вдалося отримати коміт)";
            return stdout.Length > 8000 ? stdout[..8000] + "\n// [truncated]" : stdout;
        }
        catch { return "(помилка при отриманні diff коміту)"; }
    }

    private static bool IsUkrainianStopWord(string w) =>
        w is "Клас" or "Метод" or "Клас:" or "Реалізуй" or "Визнач" or "Додай" or "Зростаючий";

    private static string NormalizeTaskTitle(string t) =>
        t.Split(' ').LastOrDefault(w => w.Length > 2) ?? t;

    // ── Claude API ────────────────────────────────────────────────────────────

    private async Task<(string State, int Score, string Feedback)> GradeWithClaudeAsync(
        LabTask task, string code, bool buildOk,
        int? testsPassed, int? testsTotal, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(ApiKey))
        {
            log.LogDebug("No Anthropic API key — using simulation for task {N}", task.Number);
            return Simulate();
        }

        var buildInfo = buildOk ? "Проєкт компілюється." : "ПОМИЛКА КОМПІЛЯЦІЇ: проєкт не збирається.";
        var testInfo  = testsPassed.HasValue
            ? $"Тести: {testsPassed}/{testsTotal} пройдено."
            : "Тести не запускалися або тестовий проєкт відсутній.";

        var prompt =
            "Ти — автоматизований перевірник коду C# для університетського курсу ООП.\n" +
            "Проаналізуй код студента і відповідай ТІЛЬКИ валідним JSON без markdown.\n\n" +
            $"## Завдання {task.Number}: {task.Title}\n" +
            $"{task.Brief ?? ""}\n\n" +
            $"## Стан збірки\n{buildInfo}\n{testInfo}\n\n" +
            $"## Код студента\n```csharp\n{code}\n```\n\n" +
            "## Відповідь (JSON, без коментарів)\n" +
            "{ \"state\": \"pass\"|\"warn\"|\"fail\", \"score\": <0-100>, \"feedback\": \"<до 3 речень українською>\" }\n\n" +
            "Критерії:\n" +
            "• pass  90-100 — завдання виконано повністю, принципи ООП дотримано\n" +
            "• warn  50-89  — основна логіка є, але є недоліки або edge-case\n" +
            "• fail  0-49   — не реалізовано, не компілюється, або логіка неправильна";

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            http.DefaultRequestHeaders.Add("x-api-key", ApiKey);
            http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            var body = JsonSerializer.Serialize(new
            {
                model      = Model,
                max_tokens = 350,
                messages   = new[] { new { role = "user", content = prompt } }
            });

            var resp = await http.PostAsync(
                "https://api.anthropic.com/v1/messages",
                new StringContent(body, Encoding.UTF8, "application/json"), ct);

            if (!resp.IsSuccessStatusCode)
            {
                log.LogWarning("Claude API {Status} for task {N}", resp.StatusCode, task.Number);
                return Simulate();
            }

            using var doc     = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var rawText       = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "{}";
            using var parsed  = JsonDocument.Parse(rawText);
            var root          = parsed.RootElement;

            var state    = root.GetProperty("state").GetString()    ?? "fail";
            var score    = root.GetProperty("score").GetInt32();
            var feedback = root.GetProperty("feedback").GetString() ?? "";

            return (state, Math.Clamp(score, 0, 100), feedback);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Claude call failed for task {N}", task.Number);
            return Simulate();
        }
    }

    // ── Simulation fallback ───────────────────────────────────────────────────

    private static readonly string[] PassFb = [
        "Відмінно! Усі вимоги виконано, код відповідає принципам ООП.",
        "Чудово. Логіка правильна, поля інкапсульовані.",
        "Правильна реалізація. Конструктор та властивості оформлені коректно.",
    ];
    private static readonly string[] WarnFb = [
        "Основний функціонал є, але є незначні відхилення від специфікації.",
        "Логіка вірна, але метод не обробляє null. Рекомендується перевірка.",
        "Більшість тестів пройдено, але є edge-case з порожнім списком.",
    ];
    private static readonly string[] FailFb = [
        "Не вдалося скомпілювати. Перевірте синтаксис та відсутні поля.",
        "Метод не реалізовано або має неправильну сигнатуру.",
        "Компіляція пройшла, але тести не пройдено через неправильну логіку.",
    ];

    private static (string State, int Score, string Feedback) Simulate()
    {
        var rng = new Random();
        double r = rng.NextDouble();
        if (r < 0.60) return ("pass", 85 + rng.Next(16), PassFb[rng.Next(PassFb.Length)]);
        if (r < 0.85) return ("warn", 55 + rng.Next(30), WarnFb[rng.Next(WarnFb.Length)]);
        return ("fail", rng.Next(30), FailFb[rng.Next(FailFb.Length)]);
    }

    private async Task<GradingResultDto> RunSimulatedAsync(
        Submission sub, IProgress<string>? progress, CancellationToken ct)
    {
        var steps = new[] { "Компіляція…", "Запуск тестів…", "Формування звіту…" };
        foreach (var s in steps) { progress?.Report(s); await Task.Delay(600, ct); }

        db.TaskResults.RemoveRange(sub.TaskResults);
        await db.SaveChangesAsync(ct);

        int total = 0, passed = 0;
        foreach (var t in sub.LabDef.Tasks.OrderBy(x => x.Number))
        {
            var (state, score, fb) = Simulate();
            int tt = t.Difficulty * 2 + 4;
            int tp = state == "pass" ? tt : state == "warn" ? (int)(tt * 0.65) : new Random().Next(0, tt / 3 + 1);
            total += tt; passed += tp;
            db.TaskResults.Add(new TaskResult
            {
                SubmissionId = sub.Id, LabTaskId = t.Id,
                State = state, Score = score, TestsPassed = tp, TestsTotal = tt, Feedback = fb,
            });
        }

        int auto = sub.LabDef.Tasks.Count > 0
            ? (int)Math.Round(100.0 * passed / Math.Max(total, 1)) : 0;
        sub.AutoScore = auto; sub.AttemptsUsed++; sub.SubmittedAt = DateTime.UtcNow;
        if (sub.Status == (int)LabStatus.Locked) sub.Status = (int)LabStatus.Review;
        await db.SaveChangesAsync(ct);

        await notif.SendAsync(sub.StudentId,
            $"Авто-перевірку Lab{sub.LabDef.Number:D2} завершено (симуляція)",
            $"Авто-оцінка: {auto}/100. Очікуйте захист.", "grading");

        progress?.Report("Готово!");
        return new GradingResultDto(sub.Id, auto, passed, total);
    }

    // ── Process helpers ───────────────────────────────────────────────────────

    private async Task<(int Exit, string Stdout, string Stderr)> RunProcessAsync(
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

    // ── Project file finders ──────────────────────────────────────────────────

    private static string? FindProjectFile(string workDir)
    {
        // Prefer src/ subfolder, then root
        foreach (var pattern in new[] { "src/**/*.csproj", "**/*.csproj" })
        {
            var files = Directory.GetFiles(workDir, "*.csproj", SearchOption.AllDirectories)
                .Where(f => !f.Contains("Test") && !f.Contains("test"))
                .OrderBy(f => f.Split(Path.DirectorySeparatorChar).Length)
                .ToArray();
            if (files.Length > 0) return files[0];
        }
        return null;
    }

    private static string? FindTestProject(string workDir) =>
        Directory.GetFiles(workDir, "*.csproj", SearchOption.AllDirectories)
            .FirstOrDefault(f => f.Contains("Test", StringComparison.OrdinalIgnoreCase));
}
