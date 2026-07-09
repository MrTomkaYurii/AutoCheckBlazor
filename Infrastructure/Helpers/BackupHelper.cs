using AutoCheck.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoCheck.Services;

/// <summary>Shared backup logic: hot SQLite copy via VACUUM INTO + rotation.</summary>
public static class BackupHelper
{
    public static async Task<string> BackupNowAsync(
        AppDbContext db, IWebHostEnvironment env, IConfiguration cfg,
        ILogger? log = null, CancellationToken ct = default)
    {
        var dir = cfg["Backup:Dir"];
        if (string.IsNullOrEmpty(dir)) dir = Path.Combine(env.ContentRootPath, "backups");
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, $"autocheck-{DateTime.Now:yyyyMMdd-HHmmss}.db");
        // VACUUM INTO is a consistent hot backup — safe while the app is running.
        // The target filename is a literal that SQLite cannot bind as a parameter, and
        // `path` is derived only from server config + a timestamp (never user input),
        // with single quotes doubled — so EF1002 (SQL-injection) does not apply here.
#pragma warning disable EF1002
        await db.Database.ExecuteSqlRawAsync($"VACUUM INTO '{path.Replace("'", "''")}'", ct);
#pragma warning restore EF1002

        var keep = int.TryParse(cfg["Backup:KeepLast"], out var k) ? Math.Max(1, k) : 14;
        foreach (var old in Directory.GetFiles(dir, "autocheck-*.db")
                                     .OrderByDescending(f => f, StringComparer.Ordinal)
                                     .Skip(keep))
            File.Delete(old);

        // Off-host mirror — no-op unless Backup:Git:RemoteUrl/Token are configured
        await GitBackupSync.SyncAsync(path, cfg, log ?? NullLogger.Instance, ct);

        return path;
    }
}
