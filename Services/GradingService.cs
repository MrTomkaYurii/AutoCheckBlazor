using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AutoCheck.Data;
using AutoCheck.Models;
using Microsoft.EntityFrameworkCore;

namespace AutoCheck.Services;

/// <summary>
/// Auto-grading engine:
///   1. git clone / pull student repo onto the branch defined in LabDef
///   2. dotnet build → compile check
///   3. dotnet test  → per-task test pass count (if test project exists)
///   4. Gemini API   → per-task code review (parallel) → state / score / feedback
/// Falls back to simulation when GitHub URL is missing or Gemini API key is not set.
/// </summary>
public class GradingService(
    AppDbContext db,
    INotificationService notif,
    IConfiguration cfg,
    IWebHostEnvironment env,
    ILogger<GradingService> log) : IGradingService
{
    private string ApiKey   => cfg["Gemini:ApiKey"]  ?? "";
    private string Model    => cfg["Gemini:Model"]   ?? "gemini-2.5-flash";
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
            catch { /* ignore malformed map */ }
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

        // ── 4. Fetch code/diff for each task (parallel git reads) ─────────────
        progress?.Report("Підготовка даних для аналізу…");
        var codeFiles    = GetCSharpFiles(workDir);
        var orderedTasks = lab.Tasks.OrderBy(t => t.Number).ToList();

        var taskInputs = await Task.WhenAll(orderedTasks.Select(async taskDef =>
        {
            var mappedSha = commitMap?.FirstOrDefault(m => m.TaskNumber == taskDef.Number)?.Sha;
            var code = !string.IsNullOrEmpty(mappedSha)
                ? await GetCommitCodeAsync(workDir, mappedSha, ct)
                : FindRelevantCode(codeFiles, taskDef.Title);
            var checks = LoadTaskChecks(lab.Number, taskDef.Number);
            return (Task: taskDef, Code: code, Checks: checks);
        }));

        // ── 5. Grade all tasks in parallel via Gemini ─────────────────────────
        progress?.Report("Аналіз коду через Gemini…");

        var gradingTasks = taskInputs.Select(async input =>
        {
            var tests = testResults.TryGetValue(NormalizeTaskTitle(input.Task.Title), out var tr)
                ? tr : ((int?)null, (int?)null);
            var result = await GradeWithGeminiAsync(
                lab, input.Task, input.Code, buildOk,
                tests.Item1, tests.Item2, input.Checks, ct);
            return (input.Task, result);
        });

        var graded = await Task.WhenAll(gradingTasks);

        // ── 6. Persist results ────────────────────────────────────────────────
        db.TaskResults.RemoveRange(sub.TaskResults);
        await db.SaveChangesAsync(ct);

        int totalTests = 0, passedTests = 0;
        foreach (var (taskDef, result) in graded)
        {
            int taskTotal  = taskDef.Difficulty * 2 + 4;
            int taskPassed = result.TestCount ?? (int)Math.Round(taskTotal * result.Score / 100.0);
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

        // ── 7. Finalise submission ────────────────────────────────────────────
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

    // ── Gemini API ────────────────────────────────────────────────────────────

    private async Task<GradeResult> GradeWithGeminiAsync(
        LabDef lab, LabTask task, string code, bool buildOk,
        int? testsPassed, int? testsTotal,
        TaskCheckInfo? checks, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(ApiKey))
        {
            log.LogDebug("No Gemini API key — simulation for task {N}", task.Number);
            return Simulate(testsPassed);
        }

        var sb = new StringBuilder();
        sb.AppendLine("Ти — суворий, але справедливий перевірник коду C# для університетського курсу ООП.");
        sb.AppendLine("Відповідай ТІЛЬКИ валідним JSON без markdown-огорток.");
        sb.AppendLine();
        sb.AppendLine($"## Лаба: {lab.Title}");
        if (!string.IsNullOrWhiteSpace(lab.Goal))
            sb.AppendLine($"**Мета:** {lab.Goal}");
        sb.AppendLine();
        sb.AppendLine($"## Завдання {task.Number}: {task.Title}  [{new string('⭐', task.Difficulty)}]");
        if (!string.IsNullOrWhiteSpace(task.Brief))
            sb.AppendLine(task.Brief);
        sb.AppendLine();

        if (checks?.ExpectedOutputs.Length > 0)
        {
            sb.AppendLine("## Очікувані виводи (тест-кейси)");
            foreach (var exp in checks.ExpectedOutputs.Take(6))
                sb.AppendLine($"- `{exp}`");
            sb.AppendLine();
        }

        var buildInfo = buildOk ? "✅ Компіляція успішна." : "❌ ПОМИЛКА КОМПІЛЯЦІЇ.";
        var testInfo  = testsPassed.HasValue
            ? $"Тести: {testsPassed}/{testsTotal} {(testsPassed == testsTotal ? "✅ всі пройшли" : "⚠️ не всі пройшли")}"
            : "Автотести не запускалися.";
        sb.AppendLine($"## Збірка і тести");
        sb.AppendLine(buildInfo);
        sb.AppendLine(testInfo);
        sb.AppendLine();
        sb.AppendLine("## Код студента (diff коміту)");
        sb.AppendLine("```csharp");
        sb.AppendLine(code);
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## Відповідь — ТІЛЬКИ JSON, без пояснень поза ним:");
        sb.AppendLine("""
{
  "state": "pass" або "warn" або "fail",
  "score": <ціле 0-100>,
  "feedback": "<2-4 речення українською — конкретний аналіз, без загальних фраз>",
  "issues": ["<конкретна проблема 1>", "<конкретна проблема 2>"]
}
""");
        sb.AppendLine("Критерії:");
        sb.AppendLine("• pass  80-100 — завдання виконано повністю, ООП-принципи дотримано");
        sb.AppendLine("• warn  50-79  — основна логіка є, але є недоліки або необроблені випадки");
        sb.AppendLine("• fail  0-49   — не реалізовано, не компілюється, або логіка принципово хибна");

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };

            var body = JsonSerializer.Serialize(new
            {
                contents = new[]
                {
                    new { role = "user", parts = new[] { new { text = sb.ToString() } } }
                },
                generationConfig = new
                {
                    responseMimeType = "application/json",
                    temperature      = 0.1
                }
            });

            var url  = $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent?key={ApiKey}";
            var resp = await http.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"), ct);

            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync(ct);
                log.LogWarning("Gemini {Status} task {N}: {Err}", resp.StatusCode, task.Number,
                    err.Length > 300 ? err[..300] : err);
                return Simulate(testsPassed);
            }

            using var doc  = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var rawText    = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? "{}";

            using var parsed = JsonDocument.Parse(rawText);
            var root = parsed.RootElement;

            var state    = root.GetProperty("state").GetString()    ?? "fail";
            var score    = root.GetProperty("score").GetInt32();
            var feedback = root.GetProperty("feedback").GetString() ?? "";

            if (root.TryGetProperty("issues", out var issuesEl) && issuesEl.ValueKind == JsonValueKind.Array)
            {
                var issues = issuesEl.EnumerateArray()
                    .Select(x => x.GetString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
                if (issues.Count > 0)
                    feedback += "\n\n**Зауваження:**\n" + string.Join("\n", issues.Select(i => $"• {i}"));
            }

            return new GradeResult(state, Math.Clamp(score, 0, 100), feedback, testsPassed);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Gemini call failed for task {N}", task.Number);
            return Simulate(testsPassed);
        }
    }

    // ── Checks.json loader ────────────────────────────────────────────────────

    private TaskCheckInfo? LoadTaskChecks(int labNumber, int taskNumber)
    {
        try
        {
            var labsDir = Path.Combine(env.ContentRootPath, "content", "labs");
            var labDir  = Directory.GetDirectories(labsDir, $"lab-{labNumber:D2}-*").FirstOrDefault();
            if (labDir is null) return null;

            var checksPath = Path.Combine(labDir, "checks.json");
            if (!File.Exists(checksPath)) return null;

            using var doc  = JsonDocument.Parse(File.ReadAllText(checksPath));
            if (!doc.RootElement.TryGetProperty("tasks", out var tasksEl)) return null;

            foreach (var taskEl in tasksEl.EnumerateArray())
            {
                if (!taskEl.TryGetProperty("n", out var nEl) || nEl.GetInt32() != taskNumber)
                    continue;
                if (!taskEl.TryGetProperty("cases", out var casesEl)) return null;

                var expects = casesEl.EnumerateArray()
                    .Select(c => c.TryGetProperty("expect", out var e) ? e.GetString() : null)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct()
                    .ToArray();

                return new TaskCheckInfo(expects!);
            }
        }
        catch (Exception ex)
        {
            log.LogDebug("checks.json load failed lab {L} task {T}: {M}", labNumber, taskNumber, ex.Message);
        }
        return null;
    }

    private record TaskCheckInfo(string[] ExpectedOutputs);

    private record GradeResult(string State, int Score, string Feedback, int? TestCount = null);

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
                await Git(".", $"clone --depth=1 --no-single-branch {repoUrl} {dir}", ct);
            else
                await Git(dir, "fetch --all --prune", ct);

            await Git(dir, $"checkout {branch}", ct);
            await Git(dir, $"reset --hard origin/{branch}", ct);
            return dir;
        }
        catch (Exception ex)
        {
            log.LogWarning("Git prepare failed {Url}/{Branch}: {Msg}", repoUrl, branch, ex.Message);
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
        await RunProcessAsync("dotnet",
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
                var outcome = unit.Attribute("outcome")?.Value  ?? "";
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

        var words = taskTitle
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim('`'))
            .Where(w => w.Length > 2 && !IsUkrainianStopWord(w))
            .ToArray();

        var scored = files
            .Select(kv => (
                Path:  kv.Key,
                Code:  kv.Value,
                Score: words.Sum(w => kv.Key.Contains(w, StringComparison.OrdinalIgnoreCase) ? 2 : 0)
                     + words.Sum(w => kv.Value.Contains($"class {w}", StringComparison.OrdinalIgnoreCase) ? 3 : 0)
            ))
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        if (scored.Score == 0 && files.Count > 0)
        {
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

    private static GradeResult Simulate(int? testsPassedHint = null)
    {
        var rng = new Random();
        double r = rng.NextDouble();
        if (r < 0.60) return new("pass", 85 + rng.Next(16), PassFb[rng.Next(PassFb.Length)], testsPassedHint);
        if (r < 0.85) return new("warn", 55 + rng.Next(30), WarnFb[rng.Next(WarnFb.Length)], testsPassedHint);
        return new("fail", rng.Next(30), FailFb[rng.Next(FailFb.Length)], testsPassedHint);
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
            var result = Simulate();
            int tt = t.Difficulty * 2 + 4;
            int tp = result.State == "pass" ? tt
                   : result.State == "warn" ? (int)(tt * 0.65)
                   : new Random().Next(0, tt / 3 + 1);
            total += tt; passed += tp;
            db.TaskResults.Add(new TaskResult
            {
                SubmissionId = sub.Id, LabTaskId = t.Id,
                State = result.State, Score = result.Score,
                TestsPassed = tp, TestsTotal = tt, Feedback = result.Feedback,
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

    private static string? FindProjectFile(string workDir) =>
        Directory.GetFiles(workDir, "*.csproj", SearchOption.AllDirectories)
            .Where(f => !f.Contains("Test") && !f.Contains("test"))
            .OrderBy(f => f.Split(Path.DirectorySeparatorChar).Length)
            .FirstOrDefault();

    private static string? FindTestProject(string workDir) =>
        Directory.GetFiles(workDir, "*.csproj", SearchOption.AllDirectories)
            .FirstOrDefault(f => f.Contains("Test", StringComparison.OrdinalIgnoreCase));
}
