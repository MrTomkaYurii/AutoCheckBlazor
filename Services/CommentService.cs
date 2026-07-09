using AutoCheck.Data;
using Microsoft.EntityFrameworkCore;

namespace AutoCheck.Services;

public class CommentService(IDbContextFactory<AppDbContext> dbf) : ICommentService
{
    public async Task<List<LabComment>> GetForSubmissionAsync(int submissionId)
    {
        await using var db = await dbf.CreateDbContextAsync();
        return await db.Comments
          .Where(c => c.SubmissionId == submissionId)
          .OrderBy(c => c.CreatedAt)
          .ToListAsync();
    }

    public async Task AddAsync(int submissionId, int? taskResultId, string authorRole, string authorName, string text)
    {
        await using var db = await dbf.CreateDbContextAsync();
        db.Comments.Add(new LabComment
        {
            SubmissionId = submissionId, TaskResultId = taskResultId,
            AuthorRole = authorRole, AuthorName = authorName,
            Text = text, CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    public async Task<List<CommentThread>> GetAllThreadsAsync()
    {
        await using var db = await dbf.CreateDbContextAsync();
        var comments = await db.Comments
            .Include(c => c.Submission).ThenInclude(s => s.Student)
            .Include(c => c.Submission).ThenInclude(s => s.LabDef)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        return comments
            .GroupBy(c => c.SubmissionId)
            .Select(g =>
            {
                var last = g.Last();
                var sub  = g.First().Submission;
                var preview = last.Text.Length > 70 ? last.Text[..70] + "…" : last.Text;
                return new CommentThread(
                    SubmissionId:   g.Key,
                    StudentName:    sub.Student.LastName + " " + sub.Student.FirstName,
                    Initials:       sub.Student.Initials,
                    Group:          sub.Student.Group,
                    LabShort:       "Lab" + sub.LabDef.Number.ToString("D2"),
                    LabTitle:       sub.LabDef.Title,
                    TotalComments:  g.Count(),
                    LastAt:         last.CreatedAt,
                    LastAuthorRole: last.AuthorRole,
                    LastPreview:    preview
                );
            })
            .OrderByDescending(t => t.LastAt)
            .ToList();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var c = await db.Comments.FindAsync(id);
        if (c is null) return;
        db.Comments.Remove(c);
        await db.SaveChangesAsync();
    }
}
