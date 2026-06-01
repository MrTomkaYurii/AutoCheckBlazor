using AutoCheck.Models;

namespace AutoCheck.Services;

/// <summary>
/// In-memory mock data mirroring the HTML prototype (data.js / teacher-data.js).
/// Replace with real services / EF Core when wiring the backend.
/// </summary>
public static class MockData
{
    public const double WAuto = 0.4, WDef = 0.6;
    public static int FinalOf(int auto, int def) => Clamp((int)Math.Round(WAuto * auto + WDef * def));
    public static int Clamp(int v, int lo = 0, int hi = 100) => Math.Max(lo, Math.Min(hi, v));

    public static readonly Student Student = new()
    {
        FirstName = "Петро", LastName = "Іваненко", Group = "КІ-31",
        Email = "p.ivanenko@lpnu.ua", Github = "github.com/petro-iv", Initials = "ПІ",
    };

    public static readonly Teacher Teacher = new()
    {
        FirstName = "Олена", LastName = "Ковальчук", Initials = "ОК",
        Title = "Доцент кафедри ПЗ", Course = "ООП на C#",
    };

    public static readonly string[] Groups = { "КІ-31", "КІ-32", "КІ-33", "КН-31", "КН-32", "СП-31" };
    public static readonly string[] TeacherGroups = { "Усі групи", "КІ-31", "КІ-32" };

    // ---- student's own labs ----
    public static readonly List<Lab> Labs = new()
    {
        new() { Id = 1,  Title = "Вступ до C#",                 Status = LabStatus.Done,     Auto = 96, Defense = 90, Final = 92 },
        new() { Id = 2,  Title = "Типи та структури",           Status = LabStatus.Done,     Auto = 88, Defense = 85, Final = 86 },
        new() { Id = 3,  Title = "Класи",                       Status = LabStatus.Review,   Auto = 74, Current = true },
        new() { Id = 4,  Title = "Інкапсуляція",                Status = LabStatus.Done,     Auto = 95, Defense = 94, Final = 94 },
        new() { Id = 5,  Title = "Властивості та індексатори",  Status = LabStatus.Done,     Auto = 82, Defense = 88, Final = 86 },
        new() { Id = 6,  Title = "Спадкування",                 Status = LabStatus.Done,     Auto = 91, Defense = 89, Final = 90 },
        new() { Id = 7,  Title = "Поліморфізм",                 Status = LabStatus.Rejected, Auto = 41 },
        new() { Id = 8,  Title = "Інтерфейси",                  Status = LabStatus.Locked },
        new() { Id = 9,  Title = "Абстрактні класи",            Status = LabStatus.Locked },
        new() { Id = 10, Title = "Колекції та Generics",        Status = LabStatus.Locked },
        new() { Id = 11, Title = "Делегати та події",           Status = LabStatus.Locked },
        new() { Id = 12, Title = "LINQ",                        Status = LabStatus.Locked },
        new() { Id = 13, Title = "Винятки та файли",            Status = LabStatus.Locked },
    };

    // ---- Lab 03 detailed view ----
    public static readonly LabDetail Lab03 = new()
    {
        Id = 3, Title = "Класи", Auto = 74, AttemptsUsed = 1, AttemptsMax = 3,
        Intro = "Реалізуйте набір класів предметної області «Бібліотека». Дотримуйтесь принципів інкапсуляції, коректно описуйте конструктори та переозначення `ToString()`.",
        Tasks = new()
        {
            new TaskItem
            {
                Id = "task1", N = 1, Title = "Клас Book", State = "pass", Score = 100,
                Brief = "Опишіть клас `Book` з полями `Title`, `Author`, `Year`, конструктором та властивостями. Переозначте `ToString()`.",
                Feedback = "Чудово. Усі поля інкапсульовані, конструктор валідує рік видання, ToString() повертає читабельний формат. 8/8 тестів пройдено.",
                TestsPassed = 8, TestsTotal = 8,
                Diff = new()
                {
                    new() { Type="ctx", N1=10, N2=10, Text="public class Book" },
                    new() { Type="ctx", N1=11, N2=11, Text="{" },
                    new() { Type="del", N1=12, N2=null, Text="    public string title;" },
                    new() { Type="add", N1=null, N2=12, Text="    public string Title { get; private set; }" },
                    new() { Type="del", N1=13, N2=null, Text="    public int year;" },
                    new() { Type="add", N1=null, N2=13, Text="    public int Year { get; private set; }" },
                    new() { Type="ctx", N1=14, N2=14, Text="" },
                    new() { Type="add", N1=null, N2=15, Text="    public override string ToString() =>" },
                    new() { Type="add", N1=null, N2=16, Text="        $\"{Title} — {Author} ({Year})\";" },
                    new() { Type="ctx", N1=15, N2=17, Text="}" },
                },
            },
            new TaskItem
            {
                Id = "task2", N = 2, Title = "Клас Library", State = "warn", Score = 68,
                Brief = "Реалізуйте клас `Library` з колекцією книг, методами `Add`, `Remove`, `FindByAuthor`.",
                Feedback = "Логіка працює, але `FindByAuthor` чутливий до регістру і не обробляє null. Рекомендується StringComparison.OrdinalIgnoreCase. 5/8 тестів пройдено.",
                TestsPassed = 5, TestsTotal = 8,
                Diff = new()
                {
                    new() { Type="ctx", N1=22, N2=22, Text="public Book FindByAuthor(string author)" },
                    new() { Type="ctx", N1=23, N2=23, Text="{" },
                    new() { Type="del", N1=24, N2=null, Text="    return books.First(b => b.Author == author);" },
                    new() { Type="add", N1=null, N2=24, Text="    return books.FirstOrDefault(b =>" },
                    new() { Type="add", N1=null, N2=25, Text="        b.Author.Equals(author," },
                    new() { Type="add", N1=null, N2=26, Text="        StringComparison.OrdinalIgnoreCase));" },
                    new() { Type="ctx", N1=25, N2=27, Text="}" },
                },
            },
            new TaskItem
            {
                Id = "task3", N = 3, Title = "Клас Reader та видача", State = "fail", Score = 0,
                Brief = "Опишіть клас `Reader` та механіку видачі книг із контролем доступності.",
                Feedback = "Не вдалося скомпілювати: відсутній конструктор за замовчуванням для `Reader`, а метод `BorrowBook` посилається на неіснуюче поле `isAvailable`. Виправте та здайте повторно.",
                TestsPassed = 0, TestsTotal = 6,
                Diff = new()
                {
                    new() { Type="ctx", N1=31, N2=31, Text="public void BorrowBook(Book book)" },
                    new() { Type="ctx", N1=32, N2=32, Text="{" },
                    new() { Type="del", N1=33, N2=null, Text="    if (book.isAvailable)   // CS1061" },
                    new() { Type="del", N1=34, N2=null, Text="        borrowed.Add(book);" },
                    new() { Type="ctx", N1=35, N2=33, Text="}" },
                },
            },
        },
    };

    public static IEnumerable<Lab> Grades => Labs;

    // ============================================================
    //  Teacher roster — deterministically generated (mirrors teacher-data.js)
    // ============================================================
    public static readonly List<RosterStudent> Students = BuildRoster();
    public static readonly List<(int Id, string Short, string Title)> LabCols =
        Labs.Select(l => (Id: l.Id, Short: "L" + l.Id.ToString("D2"), Title: l.Title)).ToList();

    private static Func<double> Mulberry32(uint a) => () =>
    {
        a += 0x6D2B79F5u;
        uint t = a;
        t = (uint)((t ^ (t >> 15)) * (t | 1u));
        t ^= t + (uint)((t ^ (t >> 7)) * (t | 61u));
        return ((t ^ (t >> 14)) & 0xFFFFFFFFu) / 4294967296.0;
    };

    private record Spec(string Last, string First, string Group, int Lvl = 0, int Done = 0, bool Review = false, int Rej = 0, bool Custom = false);

    private static List<RosterStudent> BuildRoster()
    {
        var spec = new List<Spec>
        {
            new("Іваненко",   "Петро",      "КІ-31", Custom: true),
            new("Коваленко",  "Олександра", "КІ-31", 94, 8, false),
            new("Бондаренко", "Андрій",     "КІ-31", 78, 6, true),
            new("Ткаченко",   "Софія",      "КІ-31", 88, 7, true),
            new("Мельник",    "Дмитро",     "КІ-31", 64, 5, true, 4),
            new("Шевченко",   "Марія",      "КІ-31", 91, 8, false),
            new("Кравчук",    "Назар",      "КІ-31", 72, 4, true),
            new("Поліщук",    "Вікторія",   "КІ-32", 85, 7, false),
            new("Савченко",   "Артем",      "КІ-32", 58, 3, true, 3),
            new("Руденко",    "Юлія",       "КІ-32", 96, 9, false),
            new("Захарчук",   "Богдан",     "КІ-32", 69, 5, false),
            new("Лисенко",    "Катерина",   "КІ-32", 81, 6, true),
            new("Гончаренко", "Максим",     "КІ-32", 75, 5, true, 5),
            new("Марченко",   "Дарина",     "КІ-31", 89, 7, false),
        };

        var list = new List<RosterStudent>();
        for (int i = 0; i < spec.Count; i++)
        {
            var s = spec[i];
            var rng = Mulberry32((uint)(1000 + i * 7));
            double Noise(double spread) => (rng() - 0.5) * 2 * spread;

            List<Cell> cells;
            if (s.Custom)
            {
                cells = Labs.Select(l => l.Status switch
                {
                    LabStatus.Locked   => new Cell { Status = LabStatus.Locked },
                    LabStatus.Review   => new Cell { Status = LabStatus.Review, Auto = l.Auto },
                    LabStatus.Rejected => new Cell { Status = LabStatus.Rejected, Auto = l.Auto },
                    _ => new Cell { Status = LabStatus.Done, Auto = l.Auto, Defense = l.Defense, Final = l.Final },
                }).ToList();
            }
            else
            {
                cells = new List<Cell>();
                for (int idx = 0; idx < Labs.Count; idx++)
                {
                    int j = idx + 1;
                    if (s.Rej > 0 && j == s.Rej)
                    {
                        cells.Add(new Cell { Status = LabStatus.Rejected, Auto = Clamp((int)Math.Round(s.Lvl - 25 + Noise(8))) });
                    }
                    else if (j <= s.Done)
                    {
                        int auto = Clamp((int)Math.Round(s.Lvl + Noise(9)));
                        int def = Clamp((int)Math.Round(s.Lvl + Noise(7)));
                        cells.Add(new Cell { Status = LabStatus.Done, Auto = auto, Defense = def, Final = FinalOf(auto, def) });
                    }
                    else if (j == s.Done + 1 && s.Review)
                    {
                        cells.Add(new Cell { Status = LabStatus.Review, Auto = Clamp((int)Math.Round(s.Lvl + Noise(9))) });
                    }
                    else
                    {
                        cells.Add(new Cell { Status = LabStatus.Locked });
                    }
                }
            }

            var rs = new RosterStudent
            {
                Id = i + 1, Last = s.Last, First = s.First, Group = s.Group,
                Initials = "" + s.First[0] + s.Last[0], Labs = cells,
            };
            rs.Recompute();
            list.Add(rs);
        }
        return list;
    }

    public static TeacherStats Stats()
    {
        var withAvg = Students.Where(s => s.Avg.HasValue).ToList();
        return new TeacherStats
        {
            Students = 25,
            VisibleStudents = Students.Count,
            Submissions = 142,
            PassRate = 89,
            PendingReview = Students.Sum(s => s.ReviewCount),
            AvgGrade = withAvg.Count > 0 ? (int)Math.Round(withAvg.Average(s => s.Avg!.Value)) : 0,
        };
    }

    public static List<LabStat> LabStats()
    {
        var res = new List<LabStat>();
        for (int idx = 0; idx < LabCols.Count; idx++)
        {
            var col = LabCols[idx];
            var cells = Students.Select(s => s.Labs[idx]).ToList();
            var done = cells.Where(c => c.Status == LabStatus.Done).ToList();
            var withAuto = cells.Where(c => c.Auto.HasValue).ToList();
            res.Add(new LabStat
            {
                Id = col.Id, Short = col.Short, Title = col.Title, Total = Students.Count,
                Done = done.Count,
                Review = cells.Count(c => c.Status == LabStatus.Review),
                Rejected = cells.Count(c => c.Status == LabStatus.Rejected),
                Submitted = done.Count + cells.Count(c => c.Status == LabStatus.Review),
                Avg = done.Count > 0 ? (int)Math.Round(done.Average(c => c.Final!.Value)) : null,
                AvgAuto = withAuto.Count > 0 ? (int)Math.Round(withAuto.Average(c => c.Auto!.Value)) : null,
            });
        }
        return res;
    }

    public static List<ReviewItem> ReviewQueue()
    {
        var q = new List<ReviewItem>();
        string[] whens = { "щойно", "12 хв тому", "годину тому", "сьогодні, 09:14", "вчора, 18:40" };
        foreach (var s in Students)
        {
            for (int idx = 0; idx < s.Labs.Count; idx++)
            {
                var c = s.Labs[idx];
                if (c.Status == LabStatus.Review)
                {
                    q.Add(new ReviewItem
                    {
                        StudentId = s.Id, Name = s.Last + " " + s.First, Initials = s.Initials, Group = s.Group,
                        LabId = idx + 1, LabShort = "Lab" + (idx + 1).ToString("D2"), LabTitle = LabCols[idx].Title,
                        Auto = c.Auto ?? 0, When = whens[s.Id % 5],
                    });
                }
            }
        }
        return q;
    }
}
