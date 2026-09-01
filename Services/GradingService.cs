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
    IDbContextFactory<AppDbContext> dbf,
    INotificationService notif,
    IConfiguration cfg,
    IWebHostEnvironment env,
    ILogger<GradingService> log,
    GeminiQuotaService quota,
    GradingQueueService queue,
    PlagiarismService plagiarism,
    TokenProtector tokens,
    IHttpClientFactory httpFactory) : IGradingService
{
    private string ApiKey   => cfg["Gemini:ApiKey"]  ?? "";
    private string Model    => cfg["Gemini:Model"]   ?? "gemini-2.5-flash";
    private string WorkRoot => GradingPaths.WorkRoot(cfg);
    private int MaxLines => int.TryParse(cfg["Grading:MaxLinesPerFile"], out var v) ? v : 250;

    // ── Entry point ───────────────────────────────────────────────────────────

    public async Task<GradingResultDto> RunAsync(
        int submissionId, int studentId,
        IProgress<string>? progress = null, CancellationToken ct = default)
    {
        // Dedicated context for the whole grading run — not shared with the Blazor
        // circuit, so the minutes-long clone + Gemini work can't collide with UI reads.
        await using var db = await dbf.CreateDbContextAsync(ct);

        var sub = await db.Submissions
            .Include(x => x.LabDef).ThenInclude(l => l.Tasks)
            .Include(x => x.TaskResults).ThenInclude(tr => tr.DiffLines)
            .Include(x => x.Student)
            .FirstOrDefaultAsync(x => x.Id == submissionId && x.StudentId == studentId, ct)
            ?? throw new InvalidOperationException("Submission not found");

        // ── 0. Pre-flight ────────────────────────────────────────────────────
        // Лабу вимкнено з курсу — студент не мав дістатися сюди через UI, це страховка
        if (!sub.LabDef.IsActive)
            throw new InvalidOperationException("Лабораторну вимкнено — здача закрита.");

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

        // ── Repository-substitution guard (fail fast, before the queue/Gemini) ──
        // If another student's profile points at the SAME repo (submitting with a
        // classmate's repository), this is a hard integrity fail: reject the lab, zero
        // the defense and final grade, and consume the attempt. Teacher approval
        // (PlagiarismApproved) bypasses it — used to clear an innocent owner whose URL
        // was copied by a cheater (both collide, so the teacher decides who is legit).
        if (!sub.PlagiarismApproved && !string.IsNullOrWhiteSpace(sub.Student.Github))
        {
            var repoConflict = await plagiarism.FindSharedRepoAsync(studentId, sub.Student.Github);
            if (repoConflict != null)
            {
                var idx = sub.AttemptsUsed;
                if      (idx == 0) sub.Attempt1Score = 0;
                else if (idx == 1) sub.Attempt2Score = 0;
                else if (idx == 2) sub.Attempt3Score = 0;
                sub.AttemptsUsed++;
                sub.SubmittedAt  = DateTime.UtcNow;
                sub.Status       = (int)LabStatus.Rejected;
                sub.AutoScore    = null;
                sub.DefenseScore = 0;   // fail the defense
                sub.FinalScore   = 0;
                sub.PlagiarismFlag = true;
                sub.PlagiarismNote = $"Підміна репозиторію — той самий репозиторій, що й у: {repoConflict.StudentName} ({repoConflict.Group})";
                db.GradeAudits.Add(new GradeAudit
                {
                    SubmissionId = sub.Id, Actor = "система", Action = "repo-conflict",
                    NewValue = sub.PlagiarismNote,
                });
                await db.SaveChangesAsync(ct);
                await notif.SendAsync(studentId,
                    $"Lab{sub.LabDef.Number:D2}: здачу відхилено",
                    "Ваш GitHub-репозиторій збігається з репозиторієм іншого студента. " +
                    "Лабораторну відхилено, захист провалено. Зверніться до викладача.",
                    "grading");
                throw new InvalidOperationException(
                    "Здачу відхилено: вказаний репозиторій уже використовує інший студент. " +
                    "Лабораторну відхилено, захист провалено. Спробу витрачено. Зверніться до викладача.");
            }
        }

        // One grading at a time — the rest wait in line
        using var _queueSlot = await queue.EnterAsync(progress, ct);

        // Re-check attempt limit AND deadline after waiting: a parallel submit (second
        // tab) may have consumed the last attempt, and the queue wait itself (clone +
        // Gemini calls for everyone ahead in line) can take long enough for the
        // deadline to pass while this submission was sitting in the queue.
        await db.Entry(sub).ReloadAsync(ct);
        if (sub.AttemptsUsed >= sub.AttemptsMax)
            throw new InvalidOperationException(
                $"Ліміт спроб вичерпано ({sub.AttemptsMax} з {sub.AttemptsMax}). Зверніться до викладача.");
        if (sub.LabDef.Deadline is DateTime queuedDeadline && DateTime.UtcNow > queuedDeadline)
            throw new InvalidOperationException(
                "Дедлайн минув — здача цієї лаби закрита. Зверніться до викладача.");

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
        // the student's token (decrypted) lets us clone private repos
        var ghToken = tokens.Unprotect(student.GithubToken);
        var workDir = await PrepareRepoAsync(student.Github, branch, ghToken, ct)
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
                code = GitDiff.FilterToTask(rawDiff, taskDef.Number);
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
            // DiffLines use, otherwise commit-mapped submissions never match.
            // Use "add" lines (what the student wrote); fall back to "ctx" for
            // non-mapped, full-file submissions — mirrors the corpus in PlagiarismService.
            var candidateLines = taskInputs.SelectMany(t =>
            {
                var parsed = GitDiff.Parse(t.Code);
                var add = parsed.Where(d => d.Type == "add").Select(d => d.Text).ToList();
                return add.Count > 0 ? add : parsed.Where(d => d.Type == "ctx").Select(d => d.Text).ToList();
            });
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
                // Don't erase a legitimately earned score from an earlier, unrelated
                // attempt — only null it out if no prior attempt scored >= 50 (mirrors
                // the "best-of-attempts" logic in the normal grading path below).
                var priorBest = new[] { sub.Attempt1Score ?? 0, sub.Attempt2Score ?? 0, sub.Attempt3Score ?? 0 }.Max();
                sub.AutoScore = priorBest >= 50 ? priorBest : null;
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
            var diffLines = GitDiff.Parse(code);
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
            var http = httpFactory.CreateClient("gemini");

            var body = JsonSerializer.Serialize(new
            {
                contents = new[]
                {
                    new { role = "user", parts = new[] { new { text = sb.ToString() } } }
                },
                generationConfig = new { responseMimeType = "application/json", temperature = 0.1 }
            });

            // API key travels in the x-goog-api-key header, never in the URL/query
            // (query strings leak into proxy and access logs).
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent";

            var resp = await PostGeminiAsync(http, url, body, ct);
            if ((int)resp.StatusCode == 429)
            {
                log.LogInformation("Gemini 429 on batch, waiting 30s");
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
                quota.RecordCall();   // the retry is a second real API call — count it
                resp = await PostGeminiAsync(http, url, body, ct);
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

    private async Task<string?> PrepareRepoAsync(string rawUrl, string branch, string? token, CancellationToken ct)
    {
        var repoUrl = GitDiff.NormalizeUrl(rawUrl);
        var slug    = Convert.ToHexString(
            System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes(repoUrl)))[..10];
        var dir = Path.Combine(WorkRoot, slug);

        // Private repos: embed the student's token into the fetch URL.
        // Never log authUrl — log repoUrl instead.
        var authUrl = string.IsNullOrEmpty(token)
            ? repoUrl
            : repoUrl.Replace("https://", $"https://x-access-token:{token}@");

        try
        {
            Directory.CreateDirectory(WorkRoot);

            if (!Directory.Exists(dir))
            {
                // Full clone — we need full history so git show works for any mapped commit
                await Git(".", $"clone --no-single-branch {authUrl} {dir}", ct);
            }
            else
            {
                // Refresh the remote URL (token may have been added or rotated)
                await RunProcessAsync("git", $"remote set-url origin {authUrl}", dir, ct);
                // Unshallow if previously cloned with --depth; ignore error if already full
                await RunProcessAsync("git", "fetch --unshallow --all --prune", dir, ct);
                await Git(dir, "fetch --all --prune", ct);
            }

            await Git(dir, $"checkout {branch}", ct);
            await Git(dir, $"reset --hard origin/{branch}", ct);
            // Mark the clone as used now, so RepoCleanupService's "idle for N days"
            // prune reflects real submission activity rather than filesystem quirks
            // (a re-grade with no working-tree change wouldn't touch the dir otherwise).
            try { Directory.SetLastWriteTimeUtc(dir, DateTime.UtcNow); } catch { /* best-effort */ }
            return dir;
        }
        catch (Exception ex)
        {
            // git error text may echo the fetch URL — mask the token before logging
            var msg = string.IsNullOrEmpty(token) ? ex.Message : ex.Message.Replace(token, "***");
            log.LogWarning("Git prepare failed {Url}/{Branch}: {Msg}", repoUrl, branch, msg);
            return null;
        }
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

    // ── Gemini health check ───────────────────────────────────────────────────

    /// <summary>POSTs a JSON body to Gemini with the API key in the x-goog-api-key header.</summary>
    private async Task<HttpResponseMessage> PostGeminiAsync(
        HttpClient http, string url, string jsonBody, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("x-goog-api-key", ApiKey);
        return await http.SendAsync(req, ct);
    }

    private async Task CheckGeminiAsync(CancellationToken ct)
    {
        try
        {
            var http = httpFactory.CreateClient("gemini-health");
            var body = """{"contents":[{"role":"user","parts":[{"text":"ping"}]}],"generationConfig":{"maxOutputTokens":1}}""";
            var url  = $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent";
            quota.RecordCall();   // the health ping is a real API call — count it against the daily quota
            var resp = await PostGeminiAsync(http, url, body, ct);
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

        // Drain both pipes concurrently. Reading stdout fully before touching stderr
        // can deadlock: if the child fills stderr's ~64KB OS buffer before closing
        // stdout, it blocks on the write while we wait on a stream that never ends.
        // Cancellation is driven by WaitForExitAsync(ct); the reads complete once the
        // pipes close (on exit or Kill), so they don't need the token themselves.
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();

        try
        {
            await proc.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // Don't leave an orphaned git process holding the work dir or a network
            // fetch open; kill the whole tree, then drain the reads so they settle.
            try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { /* already gone */ }
            try { await Task.WhenAll(stdoutTask, stderrTask); } catch { /* ignore */ }
            throw;
        }

        return (proc.ExitCode, await stdoutTask, await stderrTask);
    }

}
