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
///   3. Gemini API   → ONE batch request for all tasks → state / score / feedback
/// </summary>
public class GradingService(
    AppDbContext db,
    INotificationService notif,
    IConfiguration cfg,
    IWebHostEnvironment env,
    ILogger<GradingService> log,
    GeminiQuotaService quota) : IGradingService
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

        // ── 0. Pre-flight ────────────────────────────────────────────────────
        if (sub.AttemptsUsed >= sub.AttemptsMax)
            throw new InvalidOperationException(
                $"Ліміт спроб вичерпано ({sub.AttemptsMax} з {sub.AttemptsMax}). Зверніться до викладача.");

        if (quota.IsExhausted)
            throw new InvalidOperationException(
                $"Денний ліміт перевірок вичерпано ({quota.DailyLimit} з {quota.DailyLimit} використано сьогодні). Спробуйте завтра.");

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
            string code;
            if (!string.IsNullOrEmpty(mappedSha))
            {
                var rawDiff = await GetCommitCodeAsync(workDir, mappedSha, ct);
                code = FilterDiffToTask(rawDiff, taskDef.Number);
            }
            else
            {
                code = FindRelevantCode(codeFiles, taskDef.Title);
            }
            var checks = LoadTaskChecks(lab.Number, taskDef.Number);
            return (Task: taskDef, Code: code, Checks: checks);
        }));

        // ── 3. Grade ALL tasks in ONE Gemini call ────────────────────────────────
        progress?.Report("Аналіз коду через Gemini…");
        var gradeResults = await GradeAllWithGeminiAsync(lab, taskInputs, ct);
        var graded = taskInputs
            .Zip(gradeResults, (inp, res) => (inp.Task, inp.Code, Result: res))
            .ToArray();

        // ── 4. Persist results ────────────────────────────────────────────────
        db.TaskResults.RemoveRange(sub.TaskResults);
        await db.SaveChangesAsync(ct);

        foreach (var (taskDef, code, result) in graded)
        {
            var tr = new TaskResult
            {
                SubmissionId = sub.Id,
                LabTaskId    = taskDef.Id,
                State        = result.State,
                Score        = result.Score,
                TestsPassed  = 0,
                TestsTotal   = 0,
                Feedback     = JsonSerializer.Serialize(new { done = result.Done, issues = result.Issues, analysis = result.Analysis }),
            };
            var diffLines = ParseDiff(code);
            for (int i = 0; i < diffLines.Count; i++)
            {
                var (dtype, n1, n2, text) = diffLines[i];
                tr.DiffLines.Add(new DiffEntry { OrderIndex = i, Type = dtype, N1 = n1, N2 = n2, Text = text });
            }
            db.TaskResults.Add(tr);
        }

        // ── 5. Finalise submission ────────────────────────────────────────────
        int autoScore = graded.Length > 0
            ? (int)Math.Round(graded.Average(g => (double)g.Result.Score))
            : 0;

        // Store score in the corresponding attempt slot (AttemptsUsed = index before increment)
        var attemptIdx = sub.AttemptsUsed;
        if      (attemptIdx == 0) sub.Attempt1Score = autoScore;
        else if (attemptIdx == 1) sub.Attempt2Score = autoScore;
        else                      sub.Attempt3Score = autoScore;

        // AutoScore = найкраща спроба серед усіх; статус залежить від порогу 50
        var bestScore = new[] { sub.Attempt1Score ?? 0, sub.Attempt2Score ?? 0, sub.Attempt3Score ?? 0 }.Max();
        if (bestScore >= 50)
        {
            sub.AutoScore = bestScore;
            sub.Status    = (int)LabStatus.Review;
        }
        else
        {
            sub.AutoScore = null;   // оцінка не виставляється — лаба відхилена до захисту
            sub.Status    = (int)LabStatus.Rejected;
        }

        sub.AttemptsUsed++;
        sub.SubmittedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        await notif.SendAsync(studentId,
            $"Авто-перевірку Lab{lab.Number:D2} завершено",
            $"Ваша авто-оцінка: {autoScore}/100. Очікуйте захист.", "grading");

        progress?.Report("Готово!");
        return new GradingResultDto(sub.Id, autoScore, 0, 0);
    }

    // ── Gemini API — batch (all tasks in one request) ─────────────────────────

    private async Task<GradeResult[]> GradeAllWithGeminiAsync(
        LabDef lab,
        (LabTask Task, string Code, TaskCheckInfo? Checks)[] inputs,
        CancellationToken ct)
    {
        GradeResult Fail(string msg) => new("fail", 0, [], [], msg);
        var failAll = inputs.Select(_ => Fail("Помилка відповіді системи перевірки.")).ToArray();
        if (inputs.Length == 0) return failAll;

        var sb = new StringBuilder();
        sb.AppendLine("Ти — суворий, але справедливий перевірник коду C# для університетського курсу ООП.");
        sb.AppendLine("Відповідай ТІЛЬКИ валідним JSON-масивом без markdown-огорток і без тексту поза ним.");
        sb.AppendLine();
        sb.AppendLine($"## Лаба: {lab.Title}");
        if (!string.IsNullOrWhiteSpace(lab.Goal))
            sb.AppendLine($"**Мета:** {lab.Goal}");
        sb.AppendLine();
        sb.AppendLine("Перевір кожне завдання нижче і поверни JSON-масив з оцінкою для кожного.");
        sb.AppendLine();

        foreach (var (task, code, checks) in inputs)
        {
            sb.AppendLine("---");
            sb.AppendLine($"### Завдання {task.Number}: {task.Title}  [{new string('⭐', task.Difficulty)}]");
            if (!string.IsNullOrWhiteSpace(task.Brief))
                sb.AppendLine(task.Brief);
            sb.AppendLine();
            if (checks?.ExpectedOutputs.Length > 0)
            {
                sb.AppendLine("Очікувані виводи:");
                foreach (var exp in checks.ExpectedOutputs.Take(4))
                    sb.AppendLine($"- `{exp}`");
                sb.AppendLine();
            }
            sb.AppendLine("Код студента:");
            sb.AppendLine("```csharp");
            sb.AppendLine(code);
            sb.AppendLine("```");
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine("## Відповідь — ТІЛЬКИ JSON-масив, без пояснень поза ним:");
        sb.AppendLine("""
[
  {
    "n": <номер завдання>,
    "done": ["<конкретна вимога з умови — виконана>", "..."],
    "issues": ["<конкретна вимога з умови — НЕ виконана або виконана з помилкою>", "..."],
    "analysis": "<2-3 речення загальної оцінки коду>"
  },
  ...
]
""");
        sb.AppendLine("Правила:");
        sb.AppendLine("• Перерахуй у done/issues ВСІ конкретні вимоги з умови — кожна має потрапити або в done, або в issues");
        sb.AppendLine("• done/issues — лише конкретні вимоги з умови, не загальні фрази на кшталт 'код написаний' чи 'є помилки'");
        sb.AppendLine("• Оцінка буде розрахована автоматично: score = done.length / (done.length + issues.length) × 100");
        sb.AppendLine($"• Поверни масив рівно з {inputs.Length} елементами (n=1..{inputs.Length})");

        try
        {
            quota.RecordCall();
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };

            var body = JsonSerializer.Serialize(new
            {
                contents = new[]
                {
                    new { role = "user", parts = new[] { new { text = sb.ToString() } } }
                },
                generationConfig = new { responseMimeType = "application/json", temperature = 0.1 }
            });

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent?key={ApiKey}";

            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var resp    = await http.PostAsync(url, content, ct);
            if ((int)resp.StatusCode == 429)
            {
                log.LogInformation("Gemini 429 on batch, waiting 30s");
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
                content = new StringContent(body, Encoding.UTF8, "application/json");
                resp    = await http.PostAsync(url, content, ct);
            }

            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync(ct);
                log.LogWarning("Gemini batch {Status}: {Err}", resp.StatusCode, err.Length > 300 ? err[..300] : err);
                return failAll;
            }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var rawText   = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? "[]";

            using var parsed = JsonDocument.Parse(rawText);
            if (parsed.RootElement.ValueKind != JsonValueKind.Array)
            {
                log.LogWarning("Gemini batch returned non-array");
                return failAll;
            }

            static string[] Arr(JsonElement r, string key) =>
                r.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.Array
                    ? el.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => s.Length > 0).ToArray()
                    : [];

            var byN = new Dictionary<int, GradeResult>();
            foreach (var item in parsed.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("n", out var nEl)) continue;
                var n        = nEl.GetInt32();
                var done     = Arr(item, "done");
                var issues   = Arr(item, "issues");
                var analysis = item.TryGetProperty("analysis", out var an) ? an.GetString() ?? "" : "";

                // Score is calculated from done/issues ratio for transparency.
                // Gemini's own "score" field is ignored to avoid arbitrary numbers.
                var total = done.Length + issues.Length;
                var score = total > 0
                    ? Math.Clamp((int)Math.Round((double)done.Length / total * 100), 0, 100)
                    : (item.TryGetProperty("score", out var sc) ? Math.Clamp(sc.GetInt32(), 0, 100) : 0);
                var state = score >= 80 ? "pass" : score >= 50 ? "warn" : "fail";

                byN[n] = new GradeResult(state, score, done, issues, analysis);
            }

            return inputs
                .Select(inp => byN.TryGetValue(inp.Task.Number, out var r) ? r
                    : Fail("Gemini не повернув результат для цього завдання."))
                .ToArray();
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Gemini batch call failed");
            return failAll;
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
            {
                // Full clone — we need full history so git show works for any mapped commit
                await Git(".", $"clone --no-single-branch {repoUrl} {dir}", ct);
            }
            else
            {
                // Unshallow if previously cloned with --depth; ignore error if already full
                await RunProcessAsync("git", "fetch --unshallow --all --prune", dir, ct);
                await Git(dir, "fetch --all --prune", ct);
            }

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
            var (exit, stdout, stderr) = await RunProcessAsync(
                "git", $"show {sha} --unified=5 --no-color", workDir, ct);
            if (exit != 0)
            {
                log.LogWarning("git show {Sha} failed: {Err}", sha, stderr);
                return "";
            }
            return stdout.Length > 8000 ? stdout[..8000] + "\n// [truncated]" : stdout;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "GetCommitCode failed for {Sha}", sha);
            return "";
        }
    }

    private static bool IsUkrainianStopWord(string w) =>
        w is "Клас" or "Метод" or "Клас:" or "Реалізуй" or "Визнач" or "Додай" or "Зростаючий";

    // ── Diff filter — keeps only the hunk(s) for Task{N}.cs ─────────────────

    private static string FilterDiffToTask(string diffOutput, int taskNumber)
    {
        if (string.IsNullOrWhiteSpace(diffOutput)) return diffOutput;

        var trimmed = diffOutput.TrimStart();
        if (!trimmed.StartsWith("commit ") && !trimmed.StartsWith("diff --git"))
            return diffOutput; // plain file content, not a git diff

        var pattern  = $"task{taskNumber}";
        var lines    = diffOutput.Split('\n');
        var header   = new List<string>(); // commit / Author / Date / message lines
        var taskSec  = new List<string>(); // lines for this task's file
        bool inDiff  = false;
        bool inTask  = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (line.StartsWith("diff --git"))
            {
                inDiff = true;
                inTask = line.Contains(pattern, StringComparison.OrdinalIgnoreCase);
                if (inTask) taskSec.Add(line);
            }
            else if (!inDiff)
            {
                header.Add(line);
            }
            else if (inTask)
            {
                taskSec.Add(line);
            }
        }

        return taskSec.Count > 0
            ? string.Join('\n', header.Concat(taskSec))
            : diffOutput; // fallback: show full diff if no task-specific file found
    }

    // ── Diff parser ───────────────────────────────────────────────────────────

    private static List<(string Type, int? N1, int? N2, string Text)> ParseDiff(string raw)
    {
        var result = new List<(string, int?, int?, string)>();
        if (string.IsNullOrWhiteSpace(raw)) return result;

        bool isDiff = raw.TrimStart().StartsWith("commit ") ||
                      raw.TrimStart().StartsWith("diff --git");

        if (!isDiff)
        {
            int n = 1;
            foreach (var line in raw.Split('\n').Take(600))
                result.Add(("ctx", n, n++, line.TrimEnd('\r')));
            return result;
        }

        int oldLine = 0, newLine = 0;
        bool inHunk = false;

        foreach (var rawLine in raw.Split('\n').Take(1200))
        {
            var line = rawLine.TrimEnd('\r');

            if (line.StartsWith("diff --git") || line.StartsWith("index ") ||
                line.StartsWith("--- ") || line.StartsWith("+++ ") ||
                line.StartsWith("Binary "))
            {
                result.Add(("hdr", null, null, line));
                inHunk = false;
                continue;
            }
            if (line.StartsWith("commit ") || line.StartsWith("Author:") ||
                line.StartsWith("Date:") || line.StartsWith("Merge:") ||
                (line.StartsWith("    ") && !inHunk))
            {
                result.Add(("hdr", null, null, line));
                continue;
            }
            if (line.StartsWith("@@"))
            {
                var parts = line.Split(' ');
                foreach (var p in parts)
                {
                    if (p.StartsWith("-") && p.Length > 1 && !p.StartsWith("---"))
                    { int.TryParse(p[1..].Split(',')[0], out oldLine); }
                    else if (p.StartsWith("+") && p.Length > 1 && !p.StartsWith("+++"))
                    { int.TryParse(p[1..].Split(',')[0], out newLine); }
                }
                inHunk = true;
                result.Add(("hdr", null, null, line));
                continue;
            }
            if (!inHunk) continue;

            if (line.StartsWith("+"))
            {
                result.Add(("add", null, newLine, line[1..]));
                newLine++;
            }
            else if (line.StartsWith("-"))
            {
                result.Add(("del", oldLine, null, line[1..]));
                oldLine++;
            }
            else if (line.StartsWith(" ") || line.Length == 0)
            {
                result.Add(("ctx", oldLine, newLine, line.Length > 0 ? line[1..] : ""));
                oldLine++;
                newLine++;
            }
        }

        return result;
    }

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
