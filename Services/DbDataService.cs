using AutoCheck.Data;
using AutoCheck.Models;
using Microsoft.EntityFrameworkCore;

namespace AutoCheck.Services;

public class DbDataService(IDbContextFactory<AppDbContext> dbf, TokenProtector tokens) : IDataService
{
    // ── Student ───────────────────────────────────────────────────────────────

    // NOTE: seeding/tests only — returns an arbitrary first student, NOT the
    // logged-in one. Request-scoped identity comes from AppState/AuthService.
    public async Task<Student> GetCurrentStudentAsync()
    {
        await using var db = await dbf.CreateDbContextAsync();
        var s = await db.Students.FirstAsync();
        return Map(s);
    }

    public async Task<List<Lab>> GetStudentLabsAsync(int studentId)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var subs = await db.Submissions
            .Include(x => x.LabDef)
            .Where(x => x.StudentId == studentId)
            .OrderBy(x => x.LabDef.Number)
            .ToListAsync();

        return subs.Select(s => new Lab
        {
            Id         = s.LabDef.Number,
            Title      = s.LabDef.Title,
            Status     = (LabStatus)s.Status,
            Auto       = s.AutoScore,
            Attempt1   = s.Attempt1Score,
            Attempt2   = s.Attempt2Score,
            Attempt3   = s.Attempt3Score,
            Defense    = s.DefenseScore,
            Final      = s.FinalScore,
            Current    = s.IsCurrent,
            DeadlineAt = s.LabDef.Deadline,
        }).ToList();
    }

    public async Task<LabDetail?> GetLabDetailAsync(int labNumber, int studentId, int? attemptNo = null)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var sub = await db.Submissions
            .Include(x => x.LabDef).ThenInclude(l => l.Tasks)
            .Include(x => x.TaskResults).ThenInclude(tr => tr.LabTask)
            .Include(x => x.TaskResults).ThenInclude(tr => tr.DiffLines)
            .FirstOrDefaultAsync(x => x.StudentId == studentId && x.LabDef.Number == labNumber);

        if (sub is null) return null;

        var detail = new LabDetail
        {
            Id           = sub.LabDef.Number,
            Title        = sub.LabDef.Title,
            Branch       = sub.LabDef.BranchName ?? "",
            DeadlineAt   = sub.LabDef.Deadline,
            Auto         = sub.AutoScore ?? 0,
            AttemptsUsed = sub.AttemptsUsed,
            AttemptsMax  = sub.AttemptsMax,
            Intro        = sub.LabDef.Goal ?? "",
            PlagFlag     = sub.PlagiarismFlag,
            PlagNote     = sub.PlagiarismNote,
            PlagApproved = sub.PlagiarismApproved,
        };

        // Attempts that have stored results; each attempt's score is recomputed
        // from its own TaskResults (works for attempts 4+ that have no slot column)
        var attemptScores = sub.TaskResults
            .GroupBy(r => r.AttemptNo)
            .ToDictionary(
                g => g.Key,
                g => Scoring.Weighted(g.Select(r => (r.Score, r.LabTask.Difficulty)).ToList()));
        var attemptNos = attemptScores.Keys.OrderBy(n => n).ToList();
        var bestNo = attemptNos.Count > 0
            ? attemptNos.OrderByDescending(n => attemptScores[n]).ThenByDescending(n => n).First()
            : 0;
        detail.Attempts = attemptNos
            .Select(n => new AttemptInfo { No = n, Score = attemptScores[n], IsBest = n == bestNo })
            .ToList();

        // Selected attempt: requested → latest with results → none
        var selected = attemptNo.HasValue && attemptNos.Contains(attemptNo.Value)
            ? attemptNo.Value
            : attemptNos.Count > 0 ? attemptNos.Max() : 0;
        detail.SelectedAttempt = selected;

        foreach (var taskDef in sub.LabDef.Tasks.OrderBy(t => t.Number))
        {
            var res = sub.TaskResults.FirstOrDefault(r => r.LabTaskId == taskDef.Id && r.AttemptNo == selected);
            detail.Tasks.Add(new TaskItem
            {
                Id          = "task" + taskDef.Number,
                N           = taskDef.Number,
                Title       = taskDef.Title,
                Brief       = taskDef.Brief ?? "",
                State       = res?.State ?? "fail",
                Score       = res?.Score ?? 0,
                Feedback    = res?.Feedback ?? "",
                TestsPassed = res?.TestsPassed ?? 0,
                TestsTotal  = res?.TestsTotal  ?? (taskDef.Difficulty * 3 + 3),
                Diff        = res?.DiffLines
                    .OrderBy(d => d.OrderIndex)
                    .Select(d => new Models.DiffLine { Type = d.Type, N1 = d.N1, N2 = d.N2, Text = d.Text })
                    .ToList() ?? [],
            });
        }
        return detail;
    }

    // ── Teacher ───────────────────────────────────────────────────────────────

    // NOTE: seeding/tests only — returns an arbitrary first teacher. The logged-in
    // teacher comes from AppState/AuthService, not this method.
    public async Task<Teacher> GetTeacherAsync()
    {
        await using var db = await dbf.CreateDbContextAsync();
        var t = await db.Teachers.FirstAsync();
        return new Teacher { FirstName = t.FirstName, LastName = t.LastName, Initials = t.Initials, Title = t.Title, Course = t.Course };
    }

    public async Task<string[]> GetTeacherGroupsAsync()
    {
        await using var db = await dbf.CreateDbContextAsync();
        var groups = await db.Students.Select(s => s.Group).Distinct().OrderBy(g => g).ToListAsync();
        return ["Усі групи", .. groups];
    }

    public async Task<List<(int Id, string Short, string Title)>> GetLabColumnsAsync()
    {
        await using var db = await dbf.CreateDbContextAsync();
        var rows = await db.Labs.OrderBy(l => l.Number).Select(l => new { l.Number, l.Title }).ToListAsync();
        return rows.Select(r => (r.Number, "L" + r.Number.ToString("D2"), r.Title)).ToList();
    }

    public async Task<List<RosterStudent>> GetRosterAsync()
    {
        await using var db = await dbf.CreateDbContextAsync();
        var students = await db.Students.OrderBy(s => s.Id).ToListAsync();
        var labs     = await db.Labs.OrderBy(l => l.Number).ToListAsync();
        var allSubs  = await db.Submissions.ToListAsync();

        // Index submissions by (student, lab) so the per-cell lookup is O(1) instead
        // of scanning the whole list for every student × lab combination.
        var subIndex = allSubs.ToDictionary(x => (x.StudentId, x.LabDefId));

        return students.Select(s =>
        {
            var cells = labs.Select(lab =>
            {
                var sub = subIndex.GetValueOrDefault((s.Id, lab.Id));
                return sub is null
                    ? new Cell { Status = LabStatus.Locked }
                    : new Cell
                    {
                        Status  = (LabStatus)sub.Status,
                        Auto    = sub.AutoScore,
                        Defense = sub.DefenseScore,
                        Final   = sub.FinalScore,
                    };
            }).ToList();

            var rs = new RosterStudent
            {
                Id = s.Id, Last = s.LastName, First = s.FirstName,
                Group = s.Group, Initials = s.Initials, Labs = cells,
            };
            rs.Recompute();
            return rs;
        }).ToList();
    }

    public async Task<TeacherStats> GetStatsAsync()
    {
        await using var db = await dbf.CreateDbContextAsync();
        var subs = await db.Submissions.ToListAsync();
        var done = subs.Where(x => x.Status == (int)LabStatus.Done).ToList();
        var nonLocked = subs.Where(x => x.Status != (int)LabStatus.Locked).ToList();

        return new TeacherStats
        {
            Students        = await db.Students.CountAsync(),
            VisibleStudents = await db.Students.CountAsync(),
            Submissions     = nonLocked.Count,
            PendingReview   = subs.Count(x => x.Status == (int)LabStatus.Review),
            PassRate        = nonLocked.Count > 0 ? (int)Math.Round(100.0 * done.Count / nonLocked.Count) : 0,
            AvgGrade        = done.Any(x => x.FinalScore.HasValue)
                ? (int)Math.Round(done.Where(x => x.FinalScore.HasValue).Average(x => x.FinalScore!.Value))
                : 0,
        };
    }

    public async Task<List<LabStat>> GetLabStatsAsync()
    {
        await using var db = await dbf.CreateDbContextAsync();
        var labs    = await db.Labs.OrderBy(l => l.Number).ToListAsync();
        var allSubs = await db.Submissions.ToListAsync();
        int total   = await db.Students.CountAsync();

        return labs.Select(lab =>
        {
            var cells = allSubs.Where(x => x.LabDefId == lab.Id).ToList();
            var done  = cells.Where(c => c.Status == (int)LabStatus.Done).ToList();
            var withAuto = cells.Where(c => c.AutoScore.HasValue).ToList();

            return new LabStat
            {
                Id        = lab.Number, Short = "L" + lab.Number.ToString("D2"), Title = lab.Title, Total = total,
                Done      = done.Count,
                Review    = cells.Count(c => c.Status == (int)LabStatus.Review),
                Rejected  = cells.Count(c => c.Status == (int)LabStatus.Rejected),
                Submitted = done.Count + cells.Count(c => c.Status == (int)LabStatus.Review),
                Avg       = done.Any(c => c.FinalScore.HasValue)
                    ? (int)Math.Round(done.Where(c => c.FinalScore.HasValue).Average(c => c.FinalScore!.Value)) : null,
                AvgAuto   = withAuto.Count > 0
                    ? (int)Math.Round(withAuto.Average(c => c.AutoScore!.Value)) : null,
            };
        }).ToList();
    }

    public async Task<List<ReviewItem>> GetReviewQueueAsync()
    {
        await using var db = await dbf.CreateDbContextAsync();
        var subs = await db.Submissions
            .Include(x => x.Student).Include(x => x.LabDef)
            .Where(x => x.Status == (int)LabStatus.Review)
            .OrderByDescending(x => x.SubmittedAt)
            .ToListAsync();

        return subs.Select(s => new ReviewItem
        {
            StudentId   = s.StudentId,
            Name        = s.Student.LastName + " " + s.Student.FirstName,
            Initials    = s.Student.Initials,
            Group       = s.Student.Group,
            LabId       = s.LabDef.Number,
            LabShort    = "Lab" + s.LabDef.Number.ToString("D2"),
            LabTitle    = s.LabDef.Title,
            Auto        = s.AutoScore ?? 0,
            SubmittedAt = s.SubmittedAt,
            When        = s.SubmittedAt.HasValue ? FormatWhen(s.SubmittedAt.Value) : "—",
        }).ToList();
    }

    public async Task<List<ReviewItem>> GetRejectedQueueAsync()
    {
        await using var db = await dbf.CreateDbContextAsync();
        var subs = await db.Submissions
            .Include(x => x.Student).Include(x => x.LabDef)
            .Where(x => x.Status == (int)LabStatus.Rejected)
            .OrderByDescending(x => x.SubmittedAt)
            .ToListAsync();

        return subs.Select(s => new ReviewItem
        {
            StudentId   = s.StudentId,
            Name        = s.Student.LastName + " " + s.Student.FirstName,
            Initials    = s.Student.Initials,
            Group       = s.Student.Group,
            LabId       = s.LabDef.Number,
            LabShort    = "Lab" + s.LabDef.Number.ToString("D2"),
            LabTitle    = s.LabDef.Title,
            Auto        = new[] { s.Attempt1Score ?? 0, s.Attempt2Score ?? 0, s.Attempt3Score ?? 0 }.Max(),
            SubmittedAt = s.SubmittedAt,
            When        = s.SubmittedAt.HasValue ? FormatWhen(s.SubmittedAt.Value) : "—",
        }).ToList();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string FormatWhen(DateTime dt)
    {
        var diff = DateTime.UtcNow - dt;
        var kyiv = KyivTime.FromUtc(dt);
        if (diff.TotalMinutes < 2)  return "щойно";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} хв тому";
        if (diff.TotalHours < 2)    return "годину тому";
        if (diff.TotalHours < 24)   return $"сьогодні, {kyiv:HH:mm}";
        if (diff.TotalHours < 48)   return $"вчора, {kyiv:HH:mm}";
        return kyiv.ToString("dd.MM, HH:mm");
    }

    private Student Map(StudentRecord s) =>
        new() { FirstName = s.FirstName, LastName = s.LastName, Group = s.Group, Email = s.Email, Github = s.Github, GithubToken = tokens.Unprotect(s.GithubToken), Initials = s.Initials };
}
