using AutoCheck.Data;
using AutoCheck.Models;
using Microsoft.EntityFrameworkCore;

namespace AutoCheck.Services;

/// <summary>
/// Hourly background check: for every active lab whose deadline is still ahead,
/// students that have not passed it yet get a one-time reminder as the deadline
/// nears — first ~3 days out, then ~1 day out. In-app line is short; the email is
/// a formal notice addressed by the student's profile name. Each window fires once
/// per student per lab.
/// </summary>
public class DeadlineReminderService(
    IServiceScopeFactory scopeFactory,
    ILogger<DeadlineReminderService> log) : BackgroundService
{
    // Reminder windows, in hours before the deadline. TitleLabel is baked into the
    // notification Title, which is also the one-time-dedup key — so each window is
    // tracked independently (a student pinged at the 72 h mark still gets the 24 h one).
    private static readonly (int Hours, string TitleLabel, string LeftPhrase)[] Windows =
    [
        (72, "за 3 дні", "орієнтовно 3 дні"),
        (24, "завтра",   "менше доби"),
    ];

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

        var now     = DateTime.UtcNow;
        var horizon = now.AddHours(Windows.Max(w => w.Hours));

        var labs = await db.Labs.AsNoTracking()
            .Where(l => l.IsActive && l.Deadline != null && l.Deadline > now && l.Deadline <= horizon)
            .ToListAsync(ct);

        foreach (var lab in labs)
        {
            var hoursLeft = (lab.Deadline!.Value - now).TotalHours;

            // tightest window the deadline has already slipped into
            // (e.g. 20 h left → the 24 h window, not the 72 h one)
            var window = Windows
                .Where(w => hoursLeft <= w.Hours)
                .OrderBy(w => w.Hours)
                .First();

            var title = $"Дедлайн Lab{lab.Number:D2} — {window.TitleLabel}";
            var dl    = KyivTime.FromUtc(lab.Deadline!.Value);

            // students who haven't passed auto-check yet (not Done / not Review)
            var pending = await db.Submissions.AsNoTracking()
                .Where(s => s.LabDefId == lab.Id
                         && s.Status != (int)LabStatus.Done
                         && s.Status != (int)LabStatus.Review)
                .Select(s => new { s.StudentId, s.Student.FirstName, s.Student.LastName })
                .ToListAsync(ct);

            // one-time: skip students already reminded about this lab in this window
            var reminded = await db.Notifications.AsNoTracking()
                .Where(n => n.Type == "deadline" && n.Title == title)
                .Select(n => n.StudentId)
                .ToListAsync(ct);
            var remindedSet = reminded.ToHashSet();

            var inApp =
                $"Нагадування: дедлайн здачі «{lab.Title}» — {dl:dd.MM.yyyy HH:mm}. " +
                "Не забудьте здати роботу на авто-перевірку.";

            foreach (var s in pending.Where(s => !remindedSet.Contains(s.StudentId)))
            {
                var email =
                    $"{EmailText.Greeting(s.FirstName, s.LastName)}\n\n" +
                    $"Нагадуємо, що наближається кінцевий термін здачі лабораторної роботи " +
                    $"№ {lab.Number} «{lab.Title}».\n\n" +
                    $"Кінцевий термін: {dl:dd.MM.yyyy} о {dl:HH:mm} за київським часом " +
                    $"(залишилось {window.LeftPhrase}).\n\n" +
                    "Після завершення цього терміну подати роботу на автоматичну перевірку буде " +
                    "неможливо. Якщо роботу ще не здано або вона не пройшла авто-перевірку, будь ласка, " +
                    "завершіть її та подайте через свій кабінет на сторінці відповідної лабораторної роботи.\n\n" +
                    "Це повідомлення сформовано автоматично, відповідати на нього не потрібно.\n\n" +
                    "З повагою,\n" +
                    "система автоматичної перевірки лабораторних робіт AutoCheck";

                await notif.SendAsync(s.StudentId, title, inApp, "deadline", email);
            }
        }
    }
}
