using AutoCheck.Data;
using Microsoft.EntityFrameworkCore;

namespace AutoCheck.Services;

/// <summary>Shared backup logic: hot SQLite copy via VACUUM INTO + rotation.</summary>
public static class BackupHelper
{
    public static async Task<string> BackupNowAsync(
        AppDbContext db, IWebHostEnvironment env, IConfiguration cfg)
    {
        var dir = cfg["Backup:Dir"];
        if (string.IsNullOrEmpty(dir)) dir = Path.Combine(env.ContentRootPath, "backups");
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, $"autocheck-{DateTime.Now:yyyyMMdd-HHmmss}.db");
        // VACUUM INTO is a consistent hot backup — safe while the app is running
        await db.Database.ExecuteSqlRawAsync($"VACUUM INTO '{path.Replace("'", "''")}'");

        var keep = int.TryParse(cfg["Backup:KeepLast"], out var k) ? Math.Max(1, k) : 14;
        foreach (var old in Directory.GetFiles(dir, "autocheck-*.db")
                                     .OrderByDescending(f => f, StringComparer.Ordinal)
                                     .Skip(keep))
            File.Delete(old);

        return path;
    }
}

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
                var path = await BackupHelper.BackupNowAsync(db, env, cfg);
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
