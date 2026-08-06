using Physiquinator.Core.Models;
using Physiquinator.Core.Serialization;
using System.Globalization;
using System.Text.Json;

namespace Physiquinator.Core.Data;

/// <summary>Reps and weight from the most recent log row for an exercise (same plan).</summary>
public sealed record LastSetMetrics(int? Reps, double? WeightKg);

/// <summary>Per-session aggregates for one exercise under a plan (newest sessions first).</summary>
public sealed record ExerciseSessionProgressEntry(
    string SessionId,
    DateTime StartedAtUtc,
    double? BestWeightKg,
    int TotalReps,
    int SetCount,
    double TotalVolumeKg);

public sealed class WorkoutHistoryRepository(AppDatabase db, TimeProvider time)
{
#pragma warning disable S1144 // Unused private types or members should be removed
#pragma warning disable S3459 // Unassigned members should be removed
    private sealed class LastSetMetricsRow
    {
        public int? Reps { get; set; }
        public double? WeightKg { get; set; }
    }

    private sealed class SessionStartUtcRow
    {
        public DateTime StartedAtUtc { get; set; }
    }

    private sealed class ExerciseProgressAggRow
    {
        public string SessionId { get; set; } = "";
        public DateTime StartedAtUtc { get; set; }
        public double? BestWeightKg { get; set; }
        public int TotalReps { get; set; }
        public int SetCount { get; set; }
        public double TotalVolumeKg { get; set; }
    }

    private sealed class LatestMetricsByExerciseRow
    {
        public string ExerciseName { get; set; } = "";
        public int? Reps { get; set; }
        public double? WeightKg { get; set; }
    }

    private sealed class BackupJoinRow
    {
        public string SessionId { get; set; } = "";
        public string SessionWorkoutPlanId { get; set; } = "";
        public string SessionPlanName { get; set; } = "";
        public DateTime SessionStartedAtUtc { get; set; }
        public DateTime? SessionEndedAtUtc { get; set; }
        public string? SessionPlanSnapshotJson { get; set; }
        public string? SetId { get; set; }
        public string? SetSessionId { get; set; }
        public int SetExerciseIndex { get; set; }
        public string? SetExerciseName { get; set; }
        public int SetSetIndex { get; set; }
        public DateTime SetCompletedAtUtc { get; set; }
        public int? SetReps { get; set; }
        public double? SetWeightKg { get; set; }
    }
#pragma warning restore S1144
#pragma warning restore S3459

    private readonly AppDatabase _db = db;
    private readonly TimeProvider _time = time;

    public async Task<string> BeginSessionAsync(Guid planId, string planName, string? planSnapshotJson = null)
    {
        await _db.EnsureInitializedAsync();
        var id = Guid.NewGuid().ToString();
        await _db.Database.InsertAsync(new WorkoutSessionLogEntity
        {
            Id = id,
            WorkoutPlanId = planId.ToString(),
            PlanName = planName,
            StartedAtUtc = _time.GetUtcNow().UtcDateTime,
            PlanSnapshotJson = planSnapshotJson
        });
        return id;
    }

    public async Task LogSetAsync(string sessionId, int exerciseIndex, string exerciseName, int setIndex, int? reps = null, double? weightKg = null)
    {
        await _db.EnsureInitializedAsync();
        await _db.Database.InsertAsync(new WorkoutSetLogEntity
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            ExerciseIndex = exerciseIndex,
            ExerciseName = exerciseName,
            SetIndex = setIndex,
            CompletedAtUtc = _time.GetUtcNow().UtcDateTime,
            Reps = reps,
            WeightKg = weightKg
        });
    }

    public async Task EndSessionAsync(string? sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return;
        await _db.EnsureInitializedAsync();
        WorkoutSessionLogEntity row = await _db.Database.FindAsync<WorkoutSessionLogEntity>(sessionId);
        if (row == null) return;
        row.EndedAtUtc = _time.GetUtcNow().UtcDateTime;
        await _db.Database.UpdateAsync(row);
    }

    public async Task<IReadOnlyList<WorkoutSessionLogEntity>> GetRecentSessionsAsync(int limit = 100)
    {
        await _db.EnsureInitializedAsync();
        return await _db.Database.Table<WorkoutSessionLogEntity>()
            .OrderByDescending(s => s.StartedAtUtc)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync();
    }

    /// <summary>Most recent open session for a plan, or null if none.</summary>
    public async Task<WorkoutSessionLogEntity?> GetInProgressSessionForPlanAsync(Guid planId)
    {
        await _db.EnsureInitializedAsync();
        var planIdStr = planId.ToString();
        return await _db.Database.Table<WorkoutSessionLogEntity>()
            .Where(s => s.WorkoutPlanId == planIdStr && s.EndedAtUtc == null)
            .OrderByDescending(s => s.StartedAtUtc)
            .FirstOrDefaultAsync();
    }

    /// <summary>Any open session (newest first), for home banner and cross-plan prompts.</summary>
    public async Task<WorkoutSessionLogEntity?> GetAnyInProgressSessionAsync()
    {
        await _db.EnsureInitializedAsync();
        return await _db.Database.Table<WorkoutSessionLogEntity>()
            .Where(s => s.EndedAtUtc == null)
            .OrderByDescending(s => s.StartedAtUtc)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Counts workout sessions started on each local calendar day for rows in
    /// <paramref name="utcRangeStart"/> ≤ StartedAtUtc &lt; <paramref name="utcRangeEndExclusive"/>.
    /// </summary>
    public async Task<IReadOnlyDictionary<DateOnly, int>> GetSessionCountsByLocalDayAsync(
        DateTime utcRangeStart,
        DateTime utcRangeEndExclusive)
    {
        await _db.EnsureInitializedAsync();
        List<SessionStartUtcRow> rows = await _db.Database.QueryAsync<SessionStartUtcRow>(
            "SELECT StartedAtUtc FROM WorkoutSessionLogs WHERE StartedAtUtc >= ? AND StartedAtUtc < ?",
            utcRangeStart,
            utcRangeEndExclusive);

        var map = new Dictionary<DateOnly, int>();
        foreach (SessionStartUtcRow row in rows)
        {
            var localDay = DateOnly.FromDateTime(row.StartedAtUtc.ToLocalTime().Date);
            map.TryGetValue(localDay, out var n);
            map[localDay] = n + 1;
        }

        return map;
    }

    /// <summary>
    /// Sessions whose start time falls on <paramref name="localDay"/> in the device local time zone (newest first).
    /// </summary>
    public async Task<IReadOnlyList<WorkoutSessionLogEntity>> GetSessionsForLocalDayAsync(DateOnly localDay)
    {
        await _db.EnsureInitializedAsync();
        TimeZoneInfo tz = TimeZoneInfo.Local;
        var startLocalUnspecified = DateTime.SpecifyKind(
            localDay.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Unspecified);
        var endExclusiveUnspecified = DateTime.SpecifyKind(
            localDay.AddDays(1).ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Unspecified);
        DateTime utcStart = TimeZoneInfo.ConvertTimeToUtc(startLocalUnspecified, tz);
        DateTime utcEndExclusive = TimeZoneInfo.ConvertTimeToUtc(endExclusiveUnspecified, tz);

        return await _db.Database.Table<WorkoutSessionLogEntity>()
            .Where(s => s.StartedAtUtc >= utcStart && s.StartedAtUtc < utcEndExclusive)
            .OrderByDescending(s => s.StartedAtUtc)
            .ToListAsync();
    }

    /// <summary>
    /// Last <paramref name="maxSessions"/> sessions for the plan that logged <paramref name="exerciseName"/>, newest first.
    /// </summary>
    public async Task<IReadOnlyList<ExerciseSessionProgressEntry>> GetExerciseSessionProgressAsync(
        Guid workoutPlanId,
        string exerciseName,
        int maxSessions = 30,
        double? bodyweightKg = null)
    {
        if (string.IsNullOrWhiteSpace(exerciseName)) return [];
        maxSessions = Math.Clamp(maxSessions, 1, 200);
        await _db.EnsureInitializedAsync();

        var planIdStr = workoutPlanId.ToString();
        string query;
        object[] args;

        if (bodyweightKg.HasValue && bodyweightKg.Value > 0)
        {
            query = @"SELECT sess.Id AS SessionId, sess.StartedAtUtc AS StartedAtUtc,
                             MAX(s.WeightKg) AS BestWeightKg,
                             IFNULL(SUM(s.Reps), 0) AS TotalReps,
                             COUNT(*) AS SetCount,
                             SUM(CASE
                                   WHEN s.Reps IS NOT NULL THEN s.Reps * (? + IFNULL(s.WeightKg, 0))
                                   ELSE 0
                                 END) AS TotalVolumeKg
                      FROM WorkoutSessionLogs sess
                      INNER JOIN WorkoutSetLogs s ON s.SessionId = sess.Id
                      WHERE sess.WorkoutPlanId = ? AND s.ExerciseName = ?
                      GROUP BY sess.Id
                      ORDER BY sess.StartedAtUtc DESC
                      LIMIT ?";
            args = [bodyweightKg.Value, planIdStr, exerciseName, maxSessions];
        }
        else
        {
            query = @"SELECT sess.Id AS SessionId, sess.StartedAtUtc AS StartedAtUtc,
                             MAX(s.WeightKg) AS BestWeightKg,
                             IFNULL(SUM(s.Reps), 0) AS TotalReps,
                             COUNT(*) AS SetCount,
                             SUM(CASE
                                   WHEN s.Reps IS NOT NULL AND s.WeightKg IS NOT NULL THEN s.Reps * s.WeightKg
                                   WHEN s.Reps IS NOT NULL THEN s.Reps
                                   WHEN s.WeightKg IS NOT NULL THEN s.WeightKg
                                   ELSE 0
                                 END) AS TotalVolumeKg
                      FROM WorkoutSessionLogs sess
                      INNER JOIN WorkoutSetLogs s ON s.SessionId = sess.Id
                      WHERE sess.WorkoutPlanId = ? AND s.ExerciseName = ?
                      GROUP BY sess.Id
                      ORDER BY sess.StartedAtUtc DESC
                      LIMIT ?";
            args = [planIdStr, exerciseName, maxSessions];
        }

        List<ExerciseProgressAggRow> rows = await _db.Database.QueryAsync<ExerciseProgressAggRow>(query, args);

        return [.. rows
            .Select(r => new ExerciseSessionProgressEntry(
                r.SessionId,
                r.StartedAtUtc,
                r.BestWeightKg,
                r.TotalReps,
                r.SetCount,
                r.TotalVolumeKg))];
    }

    public async Task<int> GetSessionCountAsync()
    {
        await _db.EnsureInitializedAsync();
        return await _db.Database.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM WorkoutSessionLogs");
    }

    /// <summary>Inserts or replaces the bodyweight log for a local calendar day.</summary>
    public async Task UpsertBodyweightLogAsync(DateOnly localDate, double bodyweightKg)
    {
        if (bodyweightKg <= 0)
            throw new ArgumentOutOfRangeException(nameof(bodyweightKg), "Bodyweight must be positive.");

        await _db.EnsureInitializedAsync();
        var key = ToDateKey(localDate);
        BodyweightLogEntity row = await _db.Database.FindAsync<BodyweightLogEntity>(key);
        var nowUtc = _time.GetUtcNow().UtcDateTime;
        if (row == null)
        {
            await _db.Database.InsertAsync(new BodyweightLogEntity
            {
                Date = key,
                BodyweightKg = bodyweightKg,
                UpdatedAtUtc = nowUtc
            });
        }
        else
        {
            row.BodyweightKg = bodyweightKg;
            row.UpdatedAtUtc = nowUtc;
            await _db.Database.UpdateAsync(row);
        }
    }

    /// <summary>Bodyweight log entries, newest day first (at most <paramref name="limit"/> entries).</summary>
    public async Task<IReadOnlyList<BodyweightLogEntity>> GetBodyweightLogsAsync(int limit = 200)
    {
        await _db.EnsureInitializedAsync();
        return await _db.Database.Table<BodyweightLogEntity>()
            .OrderByDescending(b => b.Date)
            .Take(Math.Clamp(limit, 1, 1000))
            .ToListAsync();
    }

    /// <summary>Removes the bodyweight log entry for a local calendar day.</summary>
    public async Task<bool> DeleteBodyweightLogAsync(DateOnly localDate)
    {
        await _db.EnsureInitializedAsync();
        BodyweightLogEntity row = await _db.Database.FindAsync<BodyweightLogEntity>(ToDateKey(localDate));
        if (row == null) return false;
        await _db.Database.DeleteAsync(row);
        return true;
    }

    private static string ToDateKey(DateOnly localDate) =>
        localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>All sessions, newest first (same ordering as recent list, without a cap).</summary>
    public async Task<IReadOnlyList<WorkoutSessionLogEntity>> GetAllSessionsAsync()
    {
        await _db.EnsureInitializedAsync();
        return await _db.Database.Table<WorkoutSessionLogEntity>()
            .OrderByDescending(s => s.StartedAtUtc)
            .ToListAsync();
    }

    public async Task<WorkoutHistoryBackup> CreateBackupSnapshotAsync()
    {
        await _db.EnsureInitializedAsync();

        List<BackupJoinRow> rows = await _db.Database.QueryAsync<BackupJoinRow>(
            @"SELECT sess.Id AS SessionId,
                     sess.WorkoutPlanId AS SessionWorkoutPlanId,
                     sess.PlanName AS SessionPlanName,
                     sess.StartedAtUtc AS SessionStartedAtUtc,
                     sess.EndedAtUtc AS SessionEndedAtUtc,
                     sess.PlanSnapshotJson AS SessionPlanSnapshotJson,
                     s.Id AS SetId,
                     s.SessionId AS SetSessionId,
                     s.ExerciseIndex AS SetExerciseIndex,
                     s.ExerciseName AS SetExerciseName,
                     s.SetIndex AS SetSetIndex,
                     s.CompletedAtUtc AS SetCompletedAtUtc,
                     s.Reps AS SetReps,
                     s.WeightKg AS SetWeightKg
              FROM WorkoutSessionLogs sess
              LEFT JOIN WorkoutSetLogs s ON s.SessionId = sess.Id
              ORDER BY sess.StartedAtUtc DESC, s.CompletedAtUtc, s.ExerciseIndex, s.SetIndex");

        var entries = new List<WorkoutHistoryBackupEntry>();
        WorkoutSessionLogEntity? currentSession = null;
        List<WorkoutSetLogEntity>? currentSets = null;

        foreach (BackupJoinRow row in rows)
        {
            if (row.SessionId != currentSession?.Id)
            {
                currentSets = [];
                currentSession = new WorkoutSessionLogEntity
                {
                    Id = row.SessionId,
                    WorkoutPlanId = row.SessionWorkoutPlanId,
                    PlanName = row.SessionPlanName,
                    StartedAtUtc = row.SessionStartedAtUtc,
                    EndedAtUtc = row.SessionEndedAtUtc,
                    PlanSnapshotJson = row.SessionPlanSnapshotJson
                };
                entries.Add(new WorkoutHistoryBackupEntry { Session = currentSession, Sets = currentSets });
            }

            if (row.SetId != null)
            {
                currentSets!.Add(new WorkoutSetLogEntity
                {
                    Id = row.SetId,
                    SessionId = row.SetSessionId ?? "",
                    ExerciseIndex = row.SetExerciseIndex,
                    ExerciseName = row.SetExerciseName ?? "",
                    SetIndex = row.SetSetIndex,
                    CompletedAtUtc = row.SetCompletedAtUtc,
                    Reps = row.SetReps,
                    WeightKg = row.SetWeightKg
                });
            }
        }

        return new WorkoutHistoryBackup
        {
            FormatVersion = 1,
            Sessions = entries,
            BodyweightEntries = [.. await GetBodyweightLogsAsync(1000)]
        };
    }

    /// <summary>
    /// Merges backup rows by primary key (insert or replace). Sessions are written first, then sets.
    /// Set rows are tied to <see cref="WorkoutSessionLogEntity.Id"/>; <see cref="WorkoutSetLogEntity.SessionId"/> is normalized from the session.
    /// </summary>
    public async Task ImportBackupAsync(WorkoutHistoryBackup backup)
    {
        ArgumentNullException.ThrowIfNull(backup);
        await _db.EnsureInitializedAsync();

        await _db.Database.RunInTransactionAsync(conn =>
        {
            if (backup.Sessions == null) return;
            foreach (WorkoutHistoryBackupEntry entry in backup.Sessions)
            {
                ImportBackupEntry(conn, entry);
            }

            if (backup.BodyweightEntries != null)
            {
                foreach (BodyweightLogEntity? entry in backup.BodyweightEntries)
                {
                    if (entry is null || string.IsNullOrWhiteSpace(entry.Date))
                        continue;
                    conn.InsertOrReplace(entry);
                }
            }
        });
    }

    private static void ImportBackupEntry(SQLite.SQLiteConnection conn, WorkoutHistoryBackupEntry entry)
    {
        if (entry is null || entry.Session is null || string.IsNullOrWhiteSpace(entry.Session.Id))
            return;

        var sessionId = entry.Session.Id;
        conn.InsertOrReplace(entry.Session);

        if (entry.Sets == null) return;
        foreach (WorkoutSetLogEntity? set in entry.Sets)
        {
            if (set is null)
                continue;
            set.SessionId = sessionId;
            if (string.IsNullOrWhiteSpace(set.Id))
                set.Id = Guid.NewGuid().ToString();
            conn.InsertOrReplace(set);
        }
    }

    public async Task<WorkoutSessionLogEntity?> GetSessionAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return null;
        await _db.EnsureInitializedAsync();
        return await _db.Database.FindAsync<WorkoutSessionLogEntity>(sessionId);
    }

    public async Task<IReadOnlyList<WorkoutSetLogEntity>> GetSetsForSessionAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return [];

        await _db.EnsureInitializedAsync();
        return await _db.Database.Table<WorkoutSetLogEntity>()
            .Where(s => s.SessionId == sessionId)
            .OrderBy(s => s.CompletedAtUtc)
            .ThenBy(s => s.ExerciseIndex)
            .ThenBy(s => s.SetIndex)
            .ToListAsync();
    }

    public async Task<IReadOnlyDictionary<string, LastSetMetrics>> GetLatestSetMetricsByExerciseAsync(Guid workoutPlanId)
    {
        await _db.EnsureInitializedAsync();

        var planIdStr = workoutPlanId.ToString();
        List<LatestMetricsByExerciseRow> rows = await _db.Database.QueryAsync<LatestMetricsByExerciseRow>(
            @"SELECT ExerciseName, Reps, WeightKg
              FROM (
                  SELECT s.ExerciseName AS ExerciseName,
                         s.Reps AS Reps,
                         s.WeightKg AS WeightKg,
                         ROW_NUMBER() OVER (PARTITION BY s.ExerciseName
                                            ORDER BY s.CompletedAtUtc DESC, s.ExerciseIndex DESC, s.SetIndex DESC) AS rn
                  FROM WorkoutSetLogs s
                  INNER JOIN WorkoutSessionLogs sess ON sess.Id = s.SessionId
                  WHERE sess.WorkoutPlanId = ?
              ) ranked
              WHERE rn = 1",
            planIdStr);

        var map = new Dictionary<string, LastSetMetrics>(rows.Count);
        foreach (LatestMetricsByExerciseRow row in rows)
            map[row.ExerciseName] = new LastSetMetrics(row.Reps, row.WeightKg);
        return map;
    }

    /// <summary>
    /// Latest logged reps/weight for this exercise name under the same workout plan (any session, including the current one).
    /// </summary>
    public async Task<LastSetMetrics?> GetLatestSetMetricsForExerciseAsync(Guid workoutPlanId, string exerciseName)
    {
        if (string.IsNullOrWhiteSpace(exerciseName)) return null;
        await _db.EnsureInitializedAsync();

        var planIdStr = workoutPlanId.ToString();
        List<LastSetMetricsRow> rows = await _db.Database.QueryAsync<LastSetMetricsRow>(
            @"SELECT s.Reps AS Reps, s.WeightKg AS WeightKg
              FROM WorkoutSetLogs s
              INNER JOIN WorkoutSessionLogs sess ON sess.Id = s.SessionId
              WHERE sess.WorkoutPlanId = ? AND s.ExerciseName = ?
              ORDER BY s.CompletedAtUtc DESC, s.ExerciseIndex DESC, s.SetIndex DESC
              LIMIT 1",
            planIdStr, exerciseName);

        LastSetMetricsRow? row = rows.FirstOrDefault();
        if (row == null) return null;
        return new LastSetMetrics(row.Reps, row.WeightKg);
    }

    /// <summary>Removes the most recently logged set row for the session (same order as append-only completion).</summary>
    public async Task<bool> TryDeleteLastSetLogAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return false;
        await _db.EnsureInitializedAsync();

        WorkoutSetLogEntity last = await _db.Database.Table<WorkoutSetLogEntity>()
            .Where(s => s.SessionId == sessionId)
            .OrderByDescending(s => s.CompletedAtUtc)
            .ThenByDescending(s => s.ExerciseIndex)
            .ThenByDescending(s => s.SetIndex)
            .FirstOrDefaultAsync();

        if (last == null) return false;

        await _db.Database.DeleteAsync(last);
        return true;
    }

    public async Task DeleteSetLogAsync(string sessionId, int exerciseIndex, int setIndex)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return;
        await _db.EnsureInitializedAsync();

        await _db.Database.Table<WorkoutSetLogEntity>()
            .Where(s => s.SessionId == sessionId && s.ExerciseIndex == exerciseIndex && s.SetIndex == setIndex)
            .DeleteAsync();
    }

    public async Task DeleteSessionAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return;
        await _db.EnsureInitializedAsync();

        await _db.Database.Table<WorkoutSetLogEntity>()
            .Where(s => s.SessionId == sessionId)
            .DeleteAsync();

        await _db.Database.Table<WorkoutSessionLogEntity>()
            .Where(s => s.Id == sessionId)
            .DeleteAsync();
    }

    public static WorkoutPlan? TryParsePlanSnapshot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize(json, PhysiquinatorJsonContext.Default.WorkoutPlan);
        }
        catch
        {
            return null;
        }
    }

    public async Task UpdateSessionSnapshotAsync(string sessionId, string planSnapshotJson)
    {
        await _db.EnsureInitializedAsync();
        WorkoutSessionLogEntity row = await _db.Database.FindAsync<WorkoutSessionLogEntity>(sessionId);
        if (row != null)
        {
            row.PlanSnapshotJson = planSnapshotJson;
            await _db.Database.UpdateAsync(row);
        }
    }
}
