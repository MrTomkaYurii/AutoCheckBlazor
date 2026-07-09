using AutoCheck.Data;
using Microsoft.EntityFrameworkCore;

namespace AutoCheck.Services;

/// <summary>Daily automatic SQLite backup (config: Backup:Dir, Backup:KeepLast).</summary>
public class BackupService(
    IServiceScopeFactory scopeFactory,
    IWebHostEnvironment env,
    IConfiguration cfg,
    ILogger<BackupService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromMinutes(2), ct);   // let startup/seeding finish

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var path = await BackupHelper.BackupNowAsync(db, env, cfg, log);
                log.LogInformation("DB backup created: {Path}", path);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "DB backup failed");
            }

            await Task.Delay(TimeSpan.FromHours(24), ct);
        }
    }
}
