using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AutoCheck.Data;
using AutoCheck.Models;
using Microsoft.EntityFrameworkCore;

namespace AutoCheck.Services;

/// <summary>
/// Auto-grading engine:
///   0. pre-flight: Gemini reachability check
///   1. git clone / pull student repo
///   2. extract code per task (commit diff or file heuristic)
///   3. Gemini API   → per-task code review (parallel) → state / score / feedback
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

        // ── 0. Pre-flight: Gemini availability ───────────────────────────────
        if (string.IsNullOrEmpty(ApiKey))
            throw new InvalidOperationException(
                "Система перевірки наразі недоступна. Зверніться до викладача.");

        progress?.Report("Перевірка системи аналізу…");
        await CheckGeminiAsync(ct);

        var student = sub.Student;
        var lab     = sub.LabDef;
        var branch  = sub.BranchOverride ?? lab.BranchName ?? "main";

        if (string.IsNullOrWhiteSpace(student.Github))
            throw new InvalidOperationException(
                "GitHub репозиторій не вказано у профілі. Оновіть профіль та спробуйте знову.");

        List<CommitTaskMap>? commitMap = null;
        if (!string.IsNullOrEmpty(sub.CommitMappingJson))
        {
            try { commitMap = JsonSerializer.Deserialize<List<CommitTaskMap>>(sub.CommitMappingJson); }
            catch { /* ignore malformed map */ }
        }

        // ── 1. Prepare repo ──────────────────────────────────────────────────
        progress?.Report("Клонування репозиторію…");
        var workDir = await PrepareRepoAsync(student.Github, branch, ct)
            ?? throw new InvalidOperationException(
                "Не вдалося отримати репозиторій. Перевірте URL та права доступу.");

        // ── 2. Fetch code/diff for each task (parallel git reads) ────────────
        progress?.Report("Отримання коду завдань…");
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

        // ── 3. Grade all tasks in parallel via Gemini ─────────────────────────
        progress?.Report("Аналіз коду через Gemini…");

        var gradingTasks = taskInputs.Select(async input =>
        {
            var result = await GradeWithGeminiAsync(lab, input.Task, input.Code, input.Checks, ct);
            return (input.Task, result);
        });

        var graded = await Task.WhenAll(gradingTasks);

        // ── 4. Persist results ────────────────────────────────────────────────
        db.TaskResults.RemoveRange(sub.TaskResults);
        await db.SaveChangesAsync(ct);

        foreach (var (taskDef, result) in graded)
        {
            db.TaskResults.Add(new TaskResult
            {
                SubmissionId = sub.Id,
                LabTaskId    = taskDef.Id,
                State        = result.State,
                Score        = result.Score,
                TestsPassed  = 0,
                TestsTotal   = 0,
                Feedback     = JsonSerializer.Serialize(new { done = result.Done, issues = result.Issues, analysis = result.Analysis }),
            });
        }

        // ── 5. Finalise submission ────────────────────────────────────────────
        int autoScore = graded.Length > 0
            ? (int)Math.Round(graded.Average(g => (double)g.result.Score))
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
        return new GradingResultDto(sub.Id, autoScore, 0, 0);
    }

    // ── Gemini API ────────────────────────────────────────────────────────────

    private async Task<GradeResult> GradeWithGeminiAsync(
        LabDef lab, LabTask task, string code,
        TaskCheckInfo? checks, CancellationToken ct)
    {
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
  "done": ["<конкретна вимога завдання — виконано правильно>", "..."],
  "issues": ["<конкретна вимога завдання — НЕ виконано або має помилку: опис>", "..."],
  "analysis": "<2-3 речення: загальна оцінка того наскільки реалізація відповідає вимогам завдання>"
}
""");
        sb.AppendLine("Правила:");
        sb.AppendLine("• pass  80-100 — завдання виконано повністю, всі вимоги дотримано");
        sb.AppendLine("• warn  50-79  — основна логіка є, але частина вимог відсутня або помилкова");
        sb.AppendLine("• fail  0-49   — не реалізовано, не компілюється, або логіка принципово хибна");
        sb.AppendLine("• У 'done' і 'issues' — лише конкретні вимоги з умови, не загальні фрази");
        sb.AppendLine("• 'done' або 'issues' можуть бути порожніми масивами []");

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
                return new GradeResult("fail", 0, [], [], "Помилка відповіді системи перевірки. Зверніться до викладача.");
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

            var state = root.GetProperty("state").GetString() ?? "fail";
            var score = root.GetProperty("score").GetInt32();

            static string[] ParseArr(JsonElement r, string key) =>
                r.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.Array
                    ? el.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => s.Length > 0).ToArray()
                    : [];

            var done     = ParseArr(root, "done");
            var issues   = ParseArr(root, "issues");
            var analysis = root.TryGetProperty("analysis", out var an) ? an.GetString() ?? "" : "";

            return new GradeResult(state, Math.Clamp(score, 0, 100), done, issues, analysis);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Gemini call failed for task {N}", task.Number);
            return new GradeResult("fail", 0, [], [], "Зв'язок із системою перевірки перервано. Спробуйте пізніше.");
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

    private record GradeResult(string State, int Score, string[] Done, string[] Issues, string Analysis);

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

    // ── Gemini health check ───────────────────────────────────────────────────

    private async Task CheckGeminiAsync(CancellationToken ct)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var body = """{"contents":[{"role":"user","parts":[{"text":"ping"}]}],"generationConfig":{"maxOutputTokens":1}}""";
            var url  = $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent?key={ApiKey}";
            var resp = await http.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"), ct);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync(ct);
                var snippet = err.Length > 300 ? err[..300] : err;
                log.LogWarning("Gemini health check failed {Status}: {Err}", resp.StatusCode, snippet);
                throw new InvalidOperationException(
                    $"Gemini API: {(int)resp.StatusCode} {resp.StatusCode}. {snippet}");
            }
        }
        catch (InvalidOperationException) { throw; }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Gemini health check exception");
            throw new InvalidOperationException($"Не вдалося підключитись до Gemini: {ex.Message}");
        }
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

}
