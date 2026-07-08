using AutoCheck.Data;
using AutoCheck.Models;
using Microsoft.EntityFrameworkCore;

namespace AutoCheck.Services;

/// <summary>
/// Hourly background check: labs whose deadline is within the next 24 hours →
/// students that have not passed them yet get a one-time reminder
/// (in-app + email via NotificationService).
/// </summary>
public class DeadlineReminderService(
    IServiceScopeFactory scopeFactory,
    ILogger<DeadlineReminderService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // small startup delay so seeding finishes first
        await Task.Delay(TimeSpan.FromSeconds(30), ct);

        while (!ct.IsCancellationRequested)
        {
            try { await CheckAsync(ct); }
            catch (Exception ex) { log.LogWarning(ex, "Deadline reminder pass failed"); }

            await Task.Delay(TimeSpan.FromHours(1), ct);
        }
    }

    private async Task CheckAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db    = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notif = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var now  = DateTime.UtcNow;
        var soon = now.AddHours(24);

        var labs = await db.Labs.AsNoTracking()
            .Where(l => l.Deadline != null && l.Deadline > now && l.Deadline <= soon)
            .ToListAsync(ct);

        foreach (var lab in labs)
        {
            var title = $"Дедлайн Lab{lab.Number:D2} — завтра";

            // students who haven't passed auto-check yet (not Done / not Review)
            var pending = await db.Submissions.AsNoTracking()
                .Where(s => s.LabDefId == lab.Id
                         && s.Status != (int)LabStatus.Done
                         && s.Status != (int)LabStatus.Review)
                .Select(s => s.StudentId)
                .ToListAsync(ct);

            // one-time: skip students already reminded about this lab
            var reminded = await db.Notifications.AsNoTracking()
                .Where(n => n.Type == "deadline" && n.Title == title)
                .Select(n => n.StudentId)
                .ToListAsync(ct);

            foreach (var studentId in pending.Except(reminded))
            {
                await notif.SendAsync(studentId, title,
                    $"Нагадування: дедлайн здачі «{lab.Title}» — " +
                    $"{lab.Deadline!.Value.ToLocalTime():dd.MM.yyyy HH:mm}. " +
                    "Не забудьте здати роботу на авто-перевірку.",
                    "deadline");
            }
        }
    }
}
