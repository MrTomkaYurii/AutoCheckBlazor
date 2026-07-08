using AutoCheck.Data;
using Microsoft.EntityFrameworkCore;

namespace AutoCheck.Services;

public class NotificationService(AppDbContext db, EmailService email) : INotificationService
{
    public async Task SendAsync(int studentId, string title, string body, string type = "info")
    {
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
                _ = email.SendAsync(to, $"AutoCheck · {title}", body);
        }
    }

    public Task<List<Notification>> GetUnreadAsync(int studentId) =>
        db.Notifications
          .Where(n => n.StudentId == studentId && !n.IsRead)
          .OrderByDescending(n => n.CreatedAt)
          .ToListAsync();

    public Task<int> GetUnreadCountAsync(int studentId) =>
        db.Notifications.CountAsync(n => n.StudentId == studentId && !n.IsRead);

    public async Task MarkReadAsync(int id)
    {
        var n = await db.Notifications.FindAsync(id);
        if (n is null) return;
        n.IsRead = true;
        await db.SaveChangesAsync();
    }

    public async Task MarkAllReadAsync(int studentId)
    {
        var unread = await db.Notifications
            .Where(n => n.StudentId == studentId && !n.IsRead)
            .ToListAsync();
        foreach (var n in unread) n.IsRead = true;
        await db.SaveChangesAsync();
    }
}
