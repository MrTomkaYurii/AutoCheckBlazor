using AutoCheck.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoCheck.Services;

/// <summary>Shared backup logic: hot SQLite copy via VACUUM INTO + rotation.</summary>
public static class BackupHelper
{
    public static async Task<string> BackupNowAsync(
        AppDbContext db, IWebHostEnvironment env, IConfiguration cfg, ILogger? log = null)
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

        // Off-host mirror — no-op unless Backup:Git:RemoteUrl/Token are configured
        await GitBackupSync.SyncAsync(path, cfg, log ?? NullLogger.Instance);

        return path;
    }
}
