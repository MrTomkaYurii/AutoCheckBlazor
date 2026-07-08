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
    GeminiQuotaService quota,
    GradingQueueService queue,
    PlagiarismService plagiarism) : IGradingService
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

        // Deadline is enforced server-side, not only by the disabled UI button
        if (sub.LabDef.Deadline is DateTime deadline && DateTime.UtcNow > deadline)
            throw new InvalidOperationException(
                "Дедлайн минув — здача цієї лаби закрита. Зверніться до викладача.");

        if (quota.IsExhausted)
            throw new InvalidOperationException(
                $"Денний ліміт перевірок вичерпано ({quota.DailyLimit} з {quota.DailyLimit} використано сьогодні). Спробуйте завтра.");

        if (string.IsNullOrEmpty(ApiKey))
            throw new InvalidOperationException(
                "Система перевірки наразі недоступна. Зверніться до викладача.");

        // One grading at a time — the rest wait in line
        using var _queueSlot = await queue.EnterAsync(progress, ct);

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

        // ── 2.5 Plagiarism gate: identical to another student's checked work? ──
        // Runs BEFORE Gemini (no quota wasted). Teacher can approve to bypass.
        if (!sub.PlagiarismApproved)
        {
            progress?.Report("Перевірка на збіги з іншими роботами…");
            // ParseDiff strips the +/- diff prefixes — the same form the stored
            // DiffLines use, otherwise commit-mapped submissions never match
            var candidateLines = taskInputs.SelectMany(t =>
                ParseDiff(t.Code).Where(d => d.Type is "add" or "ctx").Select(d => d.Text));
            var match = await plagiarism.FindExactMatchAsync(lab.Id, studentId, candidateLines);
            if (match != null)
            {
                var idx = sub.AttemptsUsed;
                if      (idx == 0) sub.Attempt1Score = 0;
                else if (idx == 1) sub.Attempt2Score = 0;
                else if (idx == 2) sub.Attempt3Score = 0;
                // attempts beyond the 3 slots are tracked only via TaskResults/audit
                sub.AttemptsUsed++;
                sub.SubmittedAt = DateTime.UtcNow;
                sub.Status = (int)LabStatus.Rejected;
                sub.AutoScore = null;
                sub.PlagiarismFlag = true;
                sub.PlagiarismNote =
                    $"Збіг {match.Containment:P0} з роботою: {match.StudentName} ({match.Group})";

                db.GradeAudits.Add(new GradeAudit
                {
                    SubmissionId = sub.Id, Actor = "система", Action = "plagiarism",
                    NewValue = sub.PlagiarismNote,
                });
                await db.SaveChangesAsync(ct);

                await notif.SendAsync(studentId,
                    $"Lab{lab.Number:D2}: здачу відхилено",
                    "Автоперевірка виявила повний збіг вашого коду з роботою іншого студента. " +
                    "Спробу витрачено. Зверніться до викладача.",
                    "grading");

                throw new InvalidOperationException(
                    "Здачу відхилено: код повністю збігається з роботою іншого студента, " +
                    "яка вже пройшла перевірку. Спробу витрачено. Зверніться до викладача.");
            }
        }

        // ── 3. Grade ALL tasks in ONE Gemini call ────────────────────────────────
        progress?.Report("Аналіз коду через Gemini…");
        var gradeResults = await GradeAllWithGeminiAsync(lab, taskInputs, ct);
        var graded = taskInputs
            .Zip(gradeResults, (inp, res) => (inp.Task, inp.Code, Result: res))
            .ToArray();

        // ── 4. Persist results ────────────────────────────────────────────────
        // Results of previous attempts are kept (history); clean up only leftovers
        // of the same attempt slot (e.g. after a teacher reset).
        var attemptNo = sub.AttemptsUsed + 1;
        db.TaskResults.RemoveRange(sub.TaskResults.Where(tr => tr.AttemptNo == attemptNo));
        await db.SaveChangesAsync(ct);

        foreach (var (taskDef, code, result) in graded)
        {
            var tr = new TaskResult
            {
                SubmissionId = sub.Id,
                LabTaskId    = taskDef.Id,
                AttemptNo    = attemptNo,
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
        // Difficulty-weighted score, guarded against zero total weight
        int autoScore = Scoring.Weighted(
            graded.Select(g => (g.Result.Score, g.Task.Difficulty)).ToList());

        // Store score in the corresponding attempt slot (AttemptsUsed = index before increment).
        // Attempts beyond the 3 slots (teacher raised the limit) must NOT overwrite
        // slot 3 — their scores live in the per-attempt TaskResults.
        var attemptIdx = sub.AttemptsUsed;
        if      (attemptIdx == 0) sub.Attempt1Score = autoScore;
        else if (attemptIdx == 1) sub.Attempt2Score = autoScore;
        else if (attemptIdx == 2) sub.Attempt3Score = autoScore;

        // AutoScore = найкраща спроба серед усіх; статус залежить від порогу 50.
        // Includes the current attempt and the previous best (covers attempts 4+
        // that have no slot).
        var bestScore = new[]
        {
            sub.Attempt1Score ?? 0, sub.Attempt2Score ?? 0, sub.Attempt3Score ?? 0,
            autoScore, sub.AutoScore ?? 0,
        }.Max();
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

        // Successfully re-graded after the teacher allowed it — clear the flag AND
        // consume the approval (one-time pass; the next match triggers the gate again).
        // The history stays in GradeAudits.
        if (sub.PlagiarismApproved)
        {
            sub.PlagiarismFlag = false;
            sub.PlagiarismApproved = false;
        }

        await db.SaveChangesAsync(ct);

        await notif.SendAsync(studentId,
            $"Авто-перевірку Lab{lab.Number:D2} завершено",
            $"Ваша авто-оцінка: {autoScore}/100. Очікуйте захист.", "grading");

        progress?.Report("Готово!");
        return new GradingResultDto(sub.Id, autoScore, 0, 0);
    }

    // ── Gemini API — batch (all tasks in one request) ─────────────────────────

    /// <summary>
    /// Grades all tasks in one Gemini call. System failures (API down, malformed
    /// response) THROW so the student's attempt is NOT consumed — only a real
    /// grading result may burn an attempt.
    /// </summary>
    private async Task<GradeResult[]> GradeAllWithGeminiAsync(
        LabDef lab,
        (LabTask Task, string Code, TaskCheckInfo? Checks)[] inputs,
        CancellationToken ct)
    {
        if (inputs.Length == 0) return [];

        static InvalidOperationException SystemFail(string detail) => new(
            "Система перевірки не змогла обробити відповідь — спробу не витрачено. " +
            "Повторіть за кілька хвилин. " + detail);

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
            if (checks?.Requirements.Length > 0)
            {
                sb.AppendLine("Обов'язкові вимоги (перевір кожну — вона має потрапити або в done, або в issues):");
                foreach (var req in checks.Requirements)
                    sb.AppendLine($"- {req}");
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
        sb.AppendLine("• Якщо для завдання наведено 'Обов'язкові вимоги' — перевіряй ТІЛЬКИ їх, не додавай своїх");
        sb.AppendLine("• Якщо вимог немає — сам виведи конкретні вимоги з умови завдання");
        sb.AppendLine("• done/issues — лише конкретні вимоги, не загальні фрази на кшталт 'код написаний' чи 'є помилки'");
        sb.AppendLine("• Кожна вимога потрапляє або в done, або в issues — без пропусків");
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
                throw SystemFail($"(Gemini {(int)resp.StatusCode})");
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
                throw SystemFail("(відповідь не є масивом)");
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

            // Malformed response (missing tasks) is a system fault, not the student's
            var missing = inputs.Where(inp => !byN.ContainsKey(inp.Task.Number))
                                .Select(inp => inp.Task.Number).ToArray();
            if (missing.Length > 0)
            {
                log.LogWarning("Gemini batch missing tasks: {Missing}", string.Join(",", missing));
                throw SystemFail($"(немає результату для завдань: {string.Join(", ", missing)})");
            }

            return inputs.Select(inp => byN[inp.Task.Number]).ToArray();
        }
        catch (InvalidOperationException) { throw; }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Gemini batch call failed");
            throw SystemFail($"({ex.Message})");
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
                string[] reqs = [];
                if (taskEl.TryGetProperty("requirements", out var reqsEl) && reqsEl.ValueKind == JsonValueKind.Array)
                    reqs = reqsEl.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => s.Length > 0).ToArray();

                return new TaskCheckInfo(reqs);
            }
        }
        catch (Exception ex)
        {
            log.LogDebug("checks.json load failed lab {L} task {T}: {M}", labNumber, taskNumber, ex.Message);
        }
        return null;
    }

    private record TaskCheckInfo(string[] Requirements);

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
