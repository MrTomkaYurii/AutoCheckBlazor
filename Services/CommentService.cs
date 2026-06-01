using AutoCheck.Data;
using Microsoft.EntityFrameworkCore;

namespace AutoCheck.Services;

public class CommentService(AppDbContext db) : ICommentService
{
    public Task<List<LabComment>> GetForSubmissionAsync(int submissionId) =>
        db.Comments
          .Where(c => c.SubmissionId == submissionId)
          .OrderBy(c => c.CreatedAt)
          .ToListAsync();

    public async Task AddAsync(int submissionId, int? taskResultId, string authorRole, string authorName, string text)
    {
        db.Comments.Add(new LabComment
        {
            SubmissionId = submissionId, TaskResultId = taskResultId,
            AuthorRole = authorRole, AuthorName = authorName,
            Text = text, CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var c = await db.Comments.FindAsync(id);
        if (c is null) return;
        db.Comments.Remove(c);
        await db.SaveChangesAsync();
    }
}
