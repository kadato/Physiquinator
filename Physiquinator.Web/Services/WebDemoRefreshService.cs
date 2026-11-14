using Microsoft.Extensions.Logging;
using Physiquinator.Core.Data;
using SQLite;

namespace Physiquinator.Web.Services;

/// <summary>
/// Keeps the shared demo account fresh. The demo login is one account every
/// portfolio visitor shares, so visitor edits pile up and the seeded dates drift:
/// after a day the "today" session sits in the past and charts end early.
/// Before signing a demo session in, the auth endpoint calls
/// <see cref="RefreshDemoIfStaleAsync"/>: when the demo database holds no demo
/// plans, no sessions, or its newest session is not from today (UTC), the user
/// tables are wiped and the next circuit seeds a fresh demo relative to today.
/// Pass <c>force: true</c> (POST /api/auth/demo?fresh=true) to wipe on demand.
/// A wipe only clears tables. The next circuit repopulates through the normal
/// <c>DemoDataSeeder</c> path, so schema and <c>user_version</c> stay intact and
/// open circuits on the same file keep working.
/// </summary>
public sealed class WebDemoRefreshService(
    WebUserStore users,
    TimeProvider time,
    ILogger<WebDemoRefreshService> logger)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Wipes demo user data when stale. Returns true when a wipe ran.</summary>
    public async Task<bool> RefreshDemoIfStaleAsync(bool force = false, string? directoryOverride = null, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            WebUser? demo;
            try
            {
                demo = await users.FindByUsernameAsync(users.DemoUsername).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Demo refresh skipped: demo account lookup failed.");
                return false;
            }

            if (demo is null)
                return false;

            var directory = directoryOverride ?? WebDatabasePathProvider.ResolveDatabaseDirectory();
            var path = Path.Combine(directory, $"physiquinator_{demo.Id}.db3");
            if (!File.Exists(path))
                return false;

            return await Task.Run(() => RefreshFileIfStale(path, force), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool RefreshFileIfStale(string path, bool force)
    {
        SQLiteConnection? db = null;
        try
        {
            db = new SQLiteConnection(path);
            // PRAGMAs can return row values, so read them back like the async
            // AppDatabase setup does. Plain Execute treats the returned row as
            // "not an error" and throws SQLiteException on some platforms.
            db.ExecuteScalar<string>("PRAGMA busy_timeout = 5000;");

            if (!TableExists(db, "WorkoutPlans") || !TableExists(db, "WorkoutSessionLogs"))
                return false;

            if (!force && !IsStale(db))
                return false;

            db.RunInTransaction(() =>
            {
                db.Execute("DELETE FROM WorkoutScheduleHistory");
                db.Execute("DELETE FROM BodyweightLogs");
                db.Execute("DELETE FROM WorkoutSetLogs");
                db.Execute("DELETE FROM WorkoutSessionLogs");
                db.Execute("DELETE FROM ExercisePlans");
                db.Execute("DELETE FROM WorkoutPlans");
            });

            logger.LogInformation("Demo database wiped for refresh (force: {Force}).", force);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Demo refresh failed for {Path}; continuing with existing data.", path);
            return false;
        }
        finally
        {
            try
            {
                db?.Close();
            }
            catch
            {
                // Ignore close failures. The wipe already committed or failed safe.
            }
        }
    }

    private bool IsStale(SQLiteConnection db)
    {
        try
        {
            var today = time.GetUtcNow().UtcDateTime.Date;

            var planIds = db.Query<IdRow>(
                "SELECT Id FROM WorkoutPlans WHERE Id IN (?,?,?,?)",
                DemoDataIds.PushPlan.ToString(),
                DemoDataIds.PullPlan.ToString(),
                DemoDataIds.LegPlan.ToString(),
                DemoDataIds.FullBodyPlan.ToString());
            if (planIds.Count != 4)
                return true;

            var sessionCount = db.ExecuteScalar<int>("SELECT COUNT(*) FROM WorkoutSessionLogs");
            if (sessionCount == 0)
                return true;

            var latest = db.Table<WorkoutSessionLogEntity>()
                .OrderByDescending(s => s.StartedAtUtc)
                .FirstOrDefault();
            if (latest is null)
                return true;

            return latest.StartedAtUtc.Date != today;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Demo staleness check failed; leaving data in place.");
            return false;
        }
    }

    private static bool TableExists(SQLiteConnection db, string table)
    {
        var count = db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=?", table);
        return count > 0;
    }

#pragma warning disable S3459 // Row type is populated via reflection by sqlite-net.
    private sealed class IdRow
    {
        public string Id { get; set; } = string.Empty;
    }
#pragma warning restore S3459
}
