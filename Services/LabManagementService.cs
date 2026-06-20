using AutoCheck.Data;
using AutoCheck.Models;
using Microsoft.EntityFrameworkCore;

namespace AutoCheck.Services;

public class LabManagementService(AppDbContext db, IWebHostEnvironment env) : ILabManagementService
{
    // ── Lab CRUD ──────────────────────────────────────────────────────────────

    public Task<List<LabDef>> GetAllAsync() =>
        db.Labs.Include(l => l.Tasks).OrderBy(l => l.Number).ToListAsync();

    public Task<LabDef?> GetAsync(int id) =>
        db.Labs.Include(l => l.Tasks).FirstOrDefaultAsync(l => l.Id == id);

    public async Task<LabDef> CreateAsync(CreateLabDto dto)
    {
        var lab = new LabDef
        {
            Number = dto.Number, Title = dto.Title, Goal = dto.Goal,
            BranchName = dto.BranchName, MergesMain = dto.MergesMain,
            Deadline = dto.Deadline,
            Slug = $"lab-{dto.Number:D2}-custom",
            OrderIndex = dto.Number,
        };
        db.Labs.Add(lab);
        await db.SaveChangesAsync();

        // Create locked submissions for all existing students
        var studentIds = await db.Students.Select(s => s.Id).ToListAsync();
        foreach (var sid in studentIds)
            db.Submissions.Add(new Submission { StudentId = sid, LabDefId = lab.Id, Status = (int)LabStatus.Locked, AttemptsMax = lab.AttemptsMax });
        await db.SaveChangesAsync();
        return lab;
    }

    public async Task UpdateAsync(int id, CreateLabDto dto)
    {
        var lab = await db.Labs.FindAsync(id) ?? throw new KeyNotFoundException();
        lab.Number = dto.Number; lab.Title = dto.Title; lab.Goal = dto.Goal;
        lab.BranchName = dto.BranchName; lab.MergesMain = dto.MergesMain;
        lab.Deadline = dto.Deadline;
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var lab = await db.Labs.FindAsync(id) ?? throw new KeyNotFoundException();
        db.Labs.Remove(lab);
        await db.SaveChangesAsync();
    }

    // ── Task CRUD ─────────────────────────────────────────────────────────────

    public Task<List<LabTask>> GetTasksAsync(int labId) =>
        db.LabTasks.Where(t => t.LabDefId == labId).OrderBy(t => t.Number).ToListAsync();

    public async Task<LabTask> CreateTaskAsync(int labId, CreateTaskDto dto)
    {
        var t = new LabTask
        {
            LabDefId = labId, Number = dto.Number, Title = dto.Title,
            Brief = dto.Brief, Difficulty = dto.Difficulty,
        };
        db.LabTasks.Add(t);
        await db.SaveChangesAsync();
        return t;
    }

    public async Task UpdateTaskAsync(int id, CreateTaskDto dto)
    {
        var t = await db.LabTasks.FindAsync(id) ?? throw new KeyNotFoundException();
        t.Number = dto.Number; t.Title = dto.Title; t.Brief = dto.Brief; t.Difficulty = dto.Difficulty;
        await db.SaveChangesAsync();
    }

    public async Task DeleteTaskAsync(int id)
    {
        var t = await db.LabTasks.FindAsync(id) ?? throw new KeyNotFoundException();
        db.LabTasks.Remove(t);
        await db.SaveChangesAsync();
    }

    // ── MD Import ─────────────────────────────────────────────────────────────

    public async Task<List<MdImportPreviewItem>> PreviewImportAsync()
    {
        var dir = Path.Combine(env.ContentRootPath, "content", "labs");
        if (!Directory.Exists(dir)) return [];

        var existingNumbers = await db.Labs.Select(l => l.Number).ToHashSetAsync();
        var result = new List<MdImportPreviewItem>();

        foreach (var slug in Directory.GetDirectories(dir)
                     .Select(Path.GetFileName)
                     .Where(s => s?.StartsWith("lab-") == true)
                     .OrderBy(s => s))
        {
            var mdPath = Path.Combine(dir, slug!, "instructions.md");
            if (!File.Exists(mdPath)) continue;

            var md = await File.ReadAllTextAsync(mdPath);
            var parsed = LabMdParser.Parse(md);
            int num = int.Parse(slug!.Split('-')[1]);

            result.Add(new MdImportPreviewItem(num, slug, parsed.Title, parsed.Tasks.Count, existingNumbers.Contains(num)));
        }
        return result;
    }

    public async Task<(int Labs, int Tasks)> ImportAsync(int[] labNumbers)
    {
        var dir = Path.Combine(env.ContentRootPath, "content", "labs");
        if (!Directory.Exists(dir)) return (0, 0);

        var set = new HashSet<int>(labNumbers);
        int labCount = 0, taskCount = 0;

        var slugs = Directory.GetDirectories(dir)
            .Select(Path.GetFileName)
            .Where(s => s?.StartsWith("lab-") == true)
            .OrderBy(s => s);

        var studentIds = await db.Students.Select(s => s.Id).ToListAsync();

        foreach (var slug in slugs)
        {
            int num = int.Parse(slug!.Split('-')[1]);
            if (!set.Contains(num)) continue;

            var mdPath = Path.Combine(dir, slug, "instructions.md");
            if (!File.Exists(mdPath)) continue;

            var md = await File.ReadAllTextAsync(mdPath);
            var parsed = LabMdParser.Parse(md);

            // Upsert lab
            var existing = await db.Labs.Include(l => l.Tasks).FirstOrDefaultAsync(l => l.Number == num);
            if (existing != null)
            {
                existing.Title = parsed.Title; existing.Goal = parsed.Goal;
                existing.BranchName = parsed.BranchName; existing.MergesMain = parsed.MergesMain;
                existing.FullMarkdown = md;
                db.LabTasks.RemoveRange(existing.Tasks);
            }
            else
            {
                existing = new LabDef
                {
                    Number = num, Slug = slug, OrderIndex = num,
                    Title = parsed.Title, Goal = parsed.Goal,
                    BranchName = parsed.BranchName, MergesMain = parsed.MergesMain,
                    FullMarkdown = md,
                };
                db.Labs.Add(existing);
                await db.SaveChangesAsync();

                foreach (var sid in studentIds)
                    db.Submissions.Add(new Submission { StudentId = sid, LabDefId = existing.Id, Status = (int)LabStatus.Locked, AttemptsMax = existing.AttemptsMax });
            }

            await db.SaveChangesAsync();

            foreach (var t in parsed.Tasks)
            {
                db.LabTasks.Add(new LabTask
                {
                    LabDefId = existing.Id, Number = t.Number,
                    Title = t.Title, Brief = t.Brief, Difficulty = t.Difficulty,
                });
                taskCount++;
            }
            await db.SaveChangesAsync();
            labCount++;
        }
        return (labCount, taskCount);
    }
}
