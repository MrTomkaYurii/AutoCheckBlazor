using AutoCheck.Models;
using AutoCheck.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AutoCheck.Data;

/// <summary>
/// Seeds the SQLite database from MD files and static demo data.
/// Runs at startup; skips tables that already have rows.
/// </summary>
public class DatabaseSeeder(
    AppDbContext db,
    UserManager<AppUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IAuthService auth,
    IWebHostEnvironment env,
    ILogger<DatabaseSeeder> log)
{
    public async Task SeedAsync()
    {
        // In development: if the schema is stale (missing tables), drop and recreate.
        // NOTE: new columns are added via soft migration below — do NOT add them here
        // or existing data will be wiped on every upgrade.
        if (env.IsDevelopment())
        {
            try
            {
                await db.UserLinks.AnyAsync();
                await db.Notifications.AnyAsync();
                await db.Comments.AnyAsync();
                _ = await db.Users.AnyAsync();                                        // Identity tables
                _ = await db.UserLinks.Select(l => l.UserId).FirstOrDefaultAsync();   // Keycloak → Identity rename
                _ = await db.Submissions.Select(s => s.BranchOverride).FirstOrDefaultAsync();
                _ = await db.Submissions.Select(s => s.SubmittedAt).FirstOrDefaultAsync();
            }
            catch (Exception)
            {
                log.LogWarning("DB schema is outdated — recreating database…");
                await db.Database.EnsureDeletedAsync();
            }
        }

        await db.Database.EnsureCreatedAsync();

        // Soft migration: add new columns without dropping data.
        // SQLite ALTER TABLE ADD COLUMN is idempotent via try/catch.
        await AddColumnIfMissingAsync("Submissions", "Attempt1Score", "INTEGER");
        await AddColumnIfMissingAsync("Submissions", "Attempt2Score", "INTEGER");
        await AddColumnIfMissingAsync("Submissions", "Attempt3Score", "INTEGER");
        await AddColumnIfMissingAsync("Labs",    "AttemptsMax", "INTEGER NOT NULL DEFAULT 3");
        await AddColumnIfMissingAsync("Teachers", "Email",       "TEXT NOT NULL DEFAULT ''");
        await AddColumnIfMissingAsync("TaskResults", "AttemptNo", "INTEGER NOT NULL DEFAULT 1");
        await AddColumnIfMissingAsync("Submissions", "PlagiarismFlag",     "INTEGER NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("Submissions", "PlagiarismNote",     "TEXT");
        await AddColumnIfMissingAsync("Submissions", "PlagiarismApproved", "INTEGER NOT NULL DEFAULT 0");

        // Soft migration: new tables (EnsureCreated does nothing when the DB already exists)
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "GradeAudits" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_GradeAudits" PRIMARY KEY AUTOINCREMENT,
                "SubmissionId" INTEGER NOT NULL,
                "Actor" TEXT NOT NULL,
                "Action" TEXT NOT NULL,
                "OldValue" TEXT NULL,
                "NewValue" TEXT NULL,
                "At" TEXT NOT NULL
            )
            """);

        if (!await db.Labs.AnyAsync())     await SeedLabsAsync();
        if (!await db.Groups.AnyAsync())   await SeedGroupsAsync();
        if (!await db.Teachers.AnyAsync()) await SeedTeachersAsync();
        if (!await db.Students.AnyAsync()) await SeedStudentsAsync();

        await SeedIdentityAsync();

        // Backfill: ensure every submission has AttemptsMax = 3
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE Submissions SET AttemptsMax = 3 WHERE AttemptsMax != 3");

        // Backfill: treat existing AutoScore as the first attempt for all submissions
        // that already have a score but no attempt slots filled yet.
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE Submissions SET Attempt1Score = AutoScore WHERE AutoScore IS NOT NULL AND Attempt1Score IS NULL");

        // Backfill: якщо лаба відхилена (Status=2) — авто-оцінка не виставляється
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE Submissions SET AutoScore = NULL WHERE Status = 2 AND AutoScore IS NOT NULL");
    }

    // ── Identity: roles + built-in accounts ───────────────────────────────

    private async Task SeedIdentityAsync()
    {
        foreach (var role in new[] { "teacher", "student" })
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        if (env.IsDevelopment())
        {
            // well-known test accounts — dev only, they are shown on the login page
            await EnsureAccountAsync("teacher@test.com", "Test1234!", "teacher", "Олена", "Ковальчук");
            await EnsureAccountAsync("student@test.com", "Test1234!", "student", "Петро", "Іваненко");
        }
        else if ((await userManager.GetUsersInRoleAsync("teacher")).Count == 0)
        {
            // production bootstrap: one teacher with a random password, printed once
            var password = "adm-" + Guid.NewGuid().ToString("N")[..10];
            await EnsureAccountAsync("teacher@test.com", password, "teacher", "Олена", "Ковальчук");
            log.LogWarning(
                "Створено початковий акаунт викладача: teacher@test.com / {Password} — " +
                "увійдіть і одразу змініть email та пароль у Кабінеті.", password);
        }
    }

    private async Task EnsureAccountAsync(string email, string password, string role,
        string firstName, string lastName)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new AppUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                FirstName = firstName, LastName = lastName,
            };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                log.LogWarning("Failed to seed account {Email}: {Errors}",
                    email, string.Join("; ", result.Errors.Select(e => e.Description)));
                return;
            }
            log.LogInformation("Seeded {Role} account {Email}", role, email);
        }

        if (!await userManager.IsInRoleAsync(user, role))
            await userManager.AddToRoleAsync(user, role);

        if (role == "teacher") await auth.LinkTeacherAsync(user);
        else                   await auth.LinkStudentAsync(user, "КІ-31");
    }

    private async Task AddColumnIfMissingAsync(string table, string column, string type)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {type}");
            log.LogInformation("Soft-migrated: added {Column} to {Table}", column, table);
        }
        catch { /* column already exists — that's fine */ }
    }

    // ── Labs ─────────────────────────────────────────────────────────────

    private async Task SeedLabsAsync()
    {
        var dir = Path.Combine(env.ContentRootPath, "content", "labs");
        if (!Directory.Exists(dir))
        {
            log.LogWarning("Labs directory not found at {Dir}", dir);
            return;
        }

        var slugs = Directory.GetDirectories(dir)
            .Select(Path.GetFileName)
            .Where(s => s?.StartsWith("lab-") == true)
            .OrderBy(s => s)
            .ToArray();

        for (int i = 0; i < slugs.Length; i++)
        {
            var slug = slugs[i]!;
            var mdPath = Path.Combine(dir, slug, "instructions.md");
            if (!File.Exists(mdPath)) continue;

            var markdown = await File.ReadAllTextAsync(mdPath);
            var parsed = LabMdParser.Parse(markdown);
            int num = int.Parse(slug.Split('-')[1]);

            var lab = new LabDef
            {
                Number = num, Slug = slug, OrderIndex = i,
                Title = parsed.Title, Goal = parsed.Goal,
                BranchName = parsed.BranchName, MergesMain = parsed.MergesMain,
                FullMarkdown = markdown,
                Deadline = null,   // set by teacher via UI
            };
            foreach (var t in parsed.Tasks)
                lab.Tasks.Add(new LabTask
                {
                    Number = t.Number, Title = t.Title,
                    Brief = t.Brief, Difficulty = t.Difficulty,
                });

            db.Labs.Add(lab);
            log.LogInformation("Lab {Num}: {Title} ({Count} tasks)", num, parsed.Title, parsed.Tasks.Count);
        }
        await db.SaveChangesAsync();
    }

    // ── Groups ───────────────────────────────────────────────────────────

    private async Task SeedGroupsAsync()
    {
        var groups = new[] { "КІ-31", "КІ-32", "КІ-33", "КН-31", "КН-32", "СП-31" };
        for (int i = 0; i < groups.Length; i++)
            db.Groups.Add(new GroupRecord { Name = groups[i], OrderIndex = i });
        await db.SaveChangesAsync();
    }

    // ── Teacher ───────────────────────────────────────────────────────────

    private async Task SeedTeachersAsync()
    {
        db.Teachers.Add(new TeacherRecord
        {
            FirstName = "Олена", LastName = "Ковальчук",
            Initials = "ОК", Title = "Доцент кафедри КН", Course = "ООП на C#",
            // email matches the seeded teacher@test.com account (linked in SeedIdentityAsync)
            Email = "teacher@test.com",
        });
        await db.SaveChangesAsync();
    }

    // ── Students + Submissions ────────────────────────────────────────────

    private record Spec(string Last, string First, string Group,
        int Lvl = 0, int Done = 0, bool Review = false, int Rej = 0, bool Custom = false);

    private static Func<double> Rng(uint a) => () =>
    {
        a += 0x6D2B79F5u;
        uint t = a;
        t = (uint)((t ^ (t >> 15)) * (t | 1u));
        t ^= t + (uint)((t ^ (t >> 7)) * (t | 61u));
        return ((t ^ (t >> 14)) & 0xFFFFFFFFu) / 4294967296.0;
    };
    private static int Clamp(int v) => Math.Max(0, Math.Min(100, v));
    private static int Final(int a, int d) => Clamp((int)Math.Round(0.4 * a + 0.6 * d));

    // Demo statuses for student 0 (Петро Іваненко), covering the first 13 labs.
    // Tuple: (status, autoScore, defenseScore, isCurrent)
    private static readonly (LabStatus Status, int Auto, int Def, bool Current)[] CustomStatuses =
    [
        (LabStatus.Done,     96, 90, false),
        (LabStatus.Done,     88, 85, false),
        (LabStatus.Review,   74, -1, true),   // Lab03 — current
        (LabStatus.Done,     95, 94, false),
        (LabStatus.Done,     82, 88, false),
        (LabStatus.Done,     91, 89, false),
        (LabStatus.Rejected, 41, -1, false),
        (LabStatus.Locked,    0,  0, false),
        (LabStatus.Locked,    0,  0, false),
        (LabStatus.Locked,    0,  0, false),
        (LabStatus.Locked,    0,  0, false),
        (LabStatus.Locked,    0,  0, false),
        (LabStatus.Locked,    0,  0, false),
    ];

    private async Task SeedStudentsAsync()
    {
        var labs = await db.Labs.OrderBy(l => l.Number).ToListAsync();

        var specs = new List<Spec>
        {
            new("Іваненко",   "Петро",      "КІ-31", Custom: true),
            new("Коваленко",  "Олександра", "КІ-31", 94, 8),
            new("Бондаренко", "Андрій",     "КІ-31", 78, 6, true),
            new("Ткаченко",   "Софія",      "КІ-31", 88, 7, true),
            new("Мельник",    "Дмитро",     "КІ-31", 64, 5, true, 4),
            new("Шевченко",   "Марія",      "КІ-31", 91, 8),
            new("Кравчук",    "Назар",      "КІ-31", 72, 4, true),
            new("Поліщук",    "Вікторія",   "КІ-32", 85, 7),
            new("Савченко",   "Артем",      "КІ-32", 58, 3, true, 3),
            new("Руденко",    "Юлія",       "КІ-32", 96, 9),
            new("Захарчук",   "Богдан",     "КІ-32", 69, 5),
            new("Лисенко",    "Катерина",   "КІ-32", 81, 6, true),
            new("Гончаренко", "Максим",     "КІ-32", 75, 5, true, 5),
            new("Марченко",   "Дарина",     "КІ-31", 89, 7),
        };

        for (int si = 0; si < specs.Count; si++)
        {
            var s = specs[si];
            var rec = new StudentRecord
            {
                FirstName = s.First, LastName = s.Last, Group = s.Group,
                // student@test.com matches the seeded test account → auto-links by email
                Email  = si == 0 ? "student@test.com" : "",
                Github = si == 0 ? "github.com/petro-iv" : "",
                Initials = "" + s.First[0] + s.Last[0],
            };
            db.Students.Add(rec);
            await db.SaveChangesAsync();  // get rec.Id

            var rng = Rng((uint)(1000 + si * 7));
            double Noise(double sp) => (rng() - 0.5) * 2 * sp;

            for (int li = 0; li < labs.Count; li++)
            {
                var lab = labs[li];
                int j = li + 1; // 1-based within first 13 labs

                Submission sub;
                if (s.Custom)
                {
                    if (li < CustomStatuses.Length)
                    {
                        var (status, auto, def, cur) = CustomStatuses[li];
                        var hasAttempt = status != LabStatus.Locked && auto > 0;
                        // Demo: show 2 attempts for first two labs to illustrate attempt history
                        int? a1 = hasAttempt ? (li == 0 ? 82 : li == 1 ? 71 : auto) : null;
                        int? a2 = hasAttempt && li <= 1 ? auto : null;
                        int? a3 = null;
                        // AutoScore виставляється лише якщо >= 50 та статус не Rejected
                        var autoScore = (status != LabStatus.Locked && status != LabStatus.Rejected && auto >= 50)
                            ? auto : (int?)null;
                        sub = new Submission
                        {
                            StudentId = rec.Id, LabDefId = lab.Id,
                            Status = (int)status, IsCurrent = cur,
                            Attempt1Score = a1, Attempt2Score = a2, Attempt3Score = a3,
                            AutoScore    = autoScore,
                            DefenseScore = status == LabStatus.Done && def > 0 ? def : null,
                            FinalScore   = status == LabStatus.Done && def > 0 ? Final(auto, def) : null,
                            SubmittedAt  = hasAttempt ? DateTime.UtcNow.AddDays(-(13 - li) * 4) : null,
                            Deadline = null,
                            AttemptsUsed = a3.HasValue ? 3 : a2.HasValue ? 2 : a1.HasValue ? 1 : 0,
                            AttemptsMax = 3,
                        };
                    }
                    else
                    {
                        sub = new Submission { StudentId = rec.Id, LabDefId = lab.Id, Status = (int)LabStatus.Locked, AttemptsMax = 3 };
                    }
                }
                else if (s.Rej > 0 && j == s.Rej)
                {
                    // Відхилена лаба: оцінка < 50, AutoScore = null (не виставляється)
                    int a = Math.Min(47, Math.Max(20, Clamp((int)Math.Round(s.Lvl - 35 + Noise(8)))));
                    sub = new Submission
                    {
                        StudentId = rec.Id, LabDefId = lab.Id,
                        Status = (int)LabStatus.Rejected,
                        AutoScore = null, Attempt1Score = a,
                        SubmittedAt = DateTime.UtcNow.AddDays(-(rng() * 14 + 1)),
                        AttemptsUsed = 1, AttemptsMax = 3,
                    };
                }
                else if (j <= s.Done)
                {
                    int a = Math.Max(50, Clamp((int)Math.Round(s.Lvl + Noise(9))));
                    int d = Math.Max(50, Clamp((int)Math.Round(s.Lvl + Noise(7))));
                    sub = new Submission
                    {
                        StudentId = rec.Id, LabDefId = lab.Id,
                        Status = (int)LabStatus.Done,
                        AutoScore = a, Attempt1Score = a,
                        DefenseScore = d, FinalScore = Final(a, d),
                        SubmittedAt = DateTime.UtcNow.AddDays(-(s.Done - j + 1) * 5 - 2),
                        AttemptsUsed = 1, AttemptsMax = 3,
                    };
                }
                else if (j == s.Done + 1 && s.Review)
                {
                    // Review: оцінка >= 50 — студент пройшов автоперевірку і чекає на захист
                    int a = Math.Max(52, Clamp((int)Math.Round(s.Lvl + Noise(9))));
                    sub = new Submission
                    {
                        StudentId = rec.Id, LabDefId = lab.Id,
                        Status = (int)LabStatus.Review,
                        AutoScore = a, Attempt1Score = a,
                        SubmittedAt = DateTime.UtcNow.AddDays(-(rng() * 5 + 1)),
                        AttemptsUsed = 1, AttemptsMax = 3,
                    };
                }
                else
                {
                    sub = new Submission { StudentId = rec.Id, LabDefId = lab.Id, Status = (int)LabStatus.Locked, AttemptsMax = 3 };
                }

                db.Submissions.Add(sub);
            }
            await db.SaveChangesAsync();

            // Detailed task results for the demo submission of student 0, lab 3 (index 2)
            if (si == 0)
            {
                var lab3 = labs[2];
                var lab3Sub = await db.Submissions.FirstAsync(x => x.StudentId == rec.Id && x.LabDefId == lab3.Id);
                var lab3Tasks = await db.LabTasks.Where(t => t.LabDefId == lab3.Id).OrderBy(t => t.Number).ToListAsync();
                if (lab3Tasks.Count >= 3)
                    await SeedLab3TaskResultsAsync(lab3Sub.Id, lab3Tasks);

                // Attempt history demo: labs 1-2 mають по 2 спроби (82→96, 71→88) —
                // сідяться результати обох, щоб перемикач спроб було видно одразу
                if (labs.Count >= 2)
                {
                    await SeedAttemptHistoryAsync(rec.Id, labs[0], (1, 82), (2, 96));
                    await SeedAttemptHistoryAsync(rec.Id, labs[1], (1, 71), (2, 88));
                }
            }
        }
    }

    /// <summary>Seeds per-attempt TaskResults so the attempt switcher has demo data.</summary>
    private async Task SeedAttemptHistoryAsync(int studentId, LabDef lab, params (int No, int Avg)[] attempts)
    {
        var sub = await db.Submissions.FirstAsync(s => s.StudentId == studentId && s.LabDefId == lab.Id);
        var tasks = await db.LabTasks.Where(t => t.LabDefId == lab.Id).OrderBy(t => t.Number).ToListAsync();
        int[] offsets = [-6, 4, -2, 8, 0, -4, 6, 2];

        foreach (var (no, avg) in attempts)
        {
            for (int i = 0; i < tasks.Count; i++)
            {
                int score = Math.Clamp(avg + offsets[i % offsets.Length], 0, 100);
                string state = score >= 80 ? "pass" : score >= 50 ? "warn" : "fail";

                string[] done = score >= 80
                    ? ["Клас реалізовано за умовою", "Валідація вхідних даних присутня", "Іменування відповідає конвенціям C#"]
                    : ["Базову структуру класу створено"];
                string[] issues = score >= 80
                    ? []
                    : score >= 50
                        ? ["Бракує обробки граничних випадків", "Публічні поля замість властивостей"]
                        : ["Код не компілюється — відсутній конструктор"];
                string analysis = $"Спроба {no}: " + (score >= 80
                    ? "вимоги виконано, код чистий і читабельний."
                    : score >= 50
                        ? "рішення працює, але є зауваження до якості коду."
                        : "суттєві проблеми, потрібно виправити та здати повторно.");

                db.TaskResults.Add(new TaskResult
                {
                    SubmissionId = sub.Id, LabTaskId = tasks[i].Id, AttemptNo = no,
                    State = state, Score = score,
                    Feedback = System.Text.Json.JsonSerializer.Serialize(new { done, issues, analysis }),
                });
            }
        }
        await db.SaveChangesAsync();
    }

    private async Task SeedLab3TaskResultsAsync(int subId, List<LabTask> tasks)
    {
        db.TaskResults.AddRange(
        [
            new TaskResult
            {
                SubmissionId = subId, LabTaskId = tasks[0].Id,
                State = "pass", Score = 100, TestsPassed = 8, TestsTotal = 8,
                Feedback = "Чудово. Усі поля інкапсульовані, конструктор валідує вхідні дані, ToString() повертає читабельний формат. 8/8 тестів пройдено.",
                DiffLines =
                [
                    new() { OrderIndex=0, Type="ctx", N1=10, N2=10, Text="public class Book" },
                    new() { OrderIndex=1, Type="ctx", N1=11, N2=11, Text="{" },
                    new() { OrderIndex=2, Type="del", N1=12, Text="    public string title;" },
                    new() { OrderIndex=3, Type="add", N2=12, Text="    public string Title { get; private set; }" },
                    new() { OrderIndex=4, Type="del", N1=13, Text="    public int year;" },
                    new() { OrderIndex=5, Type="add", N2=13, Text="    public int Year { get; private set; }" },
                    new() { OrderIndex=6, Type="ctx", N1=14, N2=14, Text="" },
                    new() { OrderIndex=7, Type="add", N2=15, Text="    public override string ToString() =>" },
                    new() { OrderIndex=8, Type="add", N2=16, Text="        $\"{Title} — {Author} ({Year})\";" },
                    new() { OrderIndex=9, Type="ctx", N1=15, N2=17, Text="}" },
                ],
            },
            new TaskResult
            {
                SubmissionId = subId, LabTaskId = tasks[1].Id,
                State = "warn", Score = 68, TestsPassed = 5, TestsTotal = 8,
                Feedback = "Логіка працює, але FindByAuthor чутливий до регістру і не обробляє null. Рекомендується StringComparison.OrdinalIgnoreCase. 5/8 тестів пройдено.",
                DiffLines =
                [
                    new() { OrderIndex=0, Type="ctx", N1=22, N2=22, Text="public Book FindByAuthor(string author)" },
                    new() { OrderIndex=1, Type="ctx", N1=23, N2=23, Text="{" },
                    new() { OrderIndex=2, Type="del", N1=24, Text="    return books.First(b => b.Author == author);" },
                    new() { OrderIndex=3, Type="add", N2=24, Text="    return books.FirstOrDefault(b =>" },
                    new() { OrderIndex=4, Type="add", N2=25, Text="        b.Author.Equals(author," },
                    new() { OrderIndex=5, Type="add", N2=26, Text="        StringComparison.OrdinalIgnoreCase));" },
                    new() { OrderIndex=6, Type="ctx", N1=25, N2=27, Text="}" },
                ],
            },
            new TaskResult
            {
                SubmissionId = subId, LabTaskId = tasks[2].Id,
                State = "fail", Score = 0, TestsPassed = 0, TestsTotal = 6,
                Feedback = "Не вдалося скомпілювати: відсутній конструктор за замовчуванням, а BorrowBook посилається на неіснуюче поле isAvailable. Виправте та здайте повторно.",
                DiffLines =
                [
                    new() { OrderIndex=0, Type="ctx", N1=31, N2=31, Text="public void BorrowBook(Book book)" },
                    new() { OrderIndex=1, Type="ctx", N1=32, N2=32, Text="{" },
                    new() { OrderIndex=2, Type="del", N1=33, Text="    if (book.isAvailable)   // CS1061" },
                    new() { OrderIndex=3, Type="del", N1=34, Text="        borrowed.Add(book);" },
                    new() { OrderIndex=4, Type="ctx", N1=35, N2=33, Text="}" },
                ],
            },
        ]);
        await db.SaveChangesAsync();
    }
}
