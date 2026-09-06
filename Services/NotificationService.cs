using AutoCheck.Data;
using Microsoft.EntityFrameworkCore;

namespace AutoCheck.Services;

public class NotificationService(IDbContextFactory<AppDbContext> dbf, EmailService email) : INotificationService
{
    public async Task SendAsync(int studentId, string title, string body, string type = "info", string? emailBody = null)
    {
        await using var db = await dbf.CreateDbContextAsync();
        db.Notifications.Add(new Notification
        {
            StudentId = studentId, Title = title, Body = body,
            Type = type, IsRead = false, CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        // Duplicate to email when SMTP is configured (fire-and-forget, never blocks)
        if (email.Enabled)
        {
            var to = await db.Students.Where(s => s.Id == studentId)
                                      .Select(s => s.Email).FirstOrDefaultAsync();
            if (!string.IsNullOrEmpty(to))
                _ = email.SendAsync(to, $"AutoCheck · {title}", emailBody ?? body);
        }
    }

    public async Task<List<Notification>> GetUnreadAsync(int studentId)
    {
        await using var db = await dbf.CreateDbContextAsync();
        return await db.Notifications
          .Where(n => n.StudentId == studentId && !n.IsRead)
          .OrderByDescending(n => n.CreatedAt)
          .ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(int studentId)
    {
        await using var db = await dbf.CreateDbContextAsync();
        return await db.Notifications.CountAsync(n => n.StudentId == studentId && !n.IsRead);
    }

    public async Task MarkReadAsync(int id)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var n = await db.Notifications.FindAsync(id);
        if (n is null) return;
        n.IsRead = true;
        await db.SaveChangesAsync();
    }

    public async Task MarkAllReadAsync(int studentId)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var unread = await db.Notifications
            .Where(n => n.StudentId == studentId && !n.IsRead)
            .ToListAsync();
        foreach (var n in unread) n.IsRead = true;
        await db.SaveChangesAsync();
    }
}
