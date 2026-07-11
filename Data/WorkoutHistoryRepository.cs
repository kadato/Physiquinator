using System.Text.Json;
using Physiquinator.Models;

namespace Physiquinator.Data;

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

public class WorkoutHistoryRepository
{
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
    private static readonly JsonSerializerOptions s_jsonReadOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly AppDatabase _db;

    public WorkoutHistoryRepository(AppDatabase db) => _db = db;

    public async Task<string> BeginSessionAsync(Guid planId, string planName, string? planSnapshotJson = null)
    {
        await _db.EnsureInitializedAsync();
        var id = Guid.NewGuid().ToString();
        await _db.Database.InsertAsync(new WorkoutSessionLogEntity
        {
            Id = id,
            WorkoutPlanId = planId.ToString(),
            PlanName = planName,
            StartedAtUtc = DateTime.UtcNow,
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
            CompletedAtUtc = DateTime.UtcNow,
            Reps = reps,
            WeightKg = weightKg
        });
    }

    public async Task EndSessionAsync(string? sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return;
        await _db.EnsureInitializedAsync();
        var row = await _db.Database.FindAsync<WorkoutSessionLogEntity>(sessionId);
        if (row == null) return;
        row.EndedAtUtc = DateTime.UtcNow;
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
        var rows = await _db.Database.Table<WorkoutSessionLogEntity>()
            .Where(s => s.WorkoutPlanId == planIdStr && s.EndedAtUtc == null)
            .ToListAsync();

        return rows
            .OrderByDescending(s => s.StartedAtUtc)
            .FirstOrDefault();
    }

    /// <summary>Any open session (newest first), for home banner and cross-plan prompts.</summary>
    public async Task<WorkoutSessionLogEntity?> GetAnyInProgressSessionAsync()
    {
        await _db.EnsureInitializedAsync();
        var rows = await _db.Database.Table<WorkoutSessionLogEntity>()
            .Where(s => s.EndedAtUtc == null)
            .ToListAsync();

        return rows
            .OrderByDescending(s => s.StartedAtUtc)
            .FirstOrDefault();
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
        var rows = await _db.Database.QueryAsync<SessionStartUtcRow>(
            "SELECT StartedAtUtc FROM WorkoutSessionLogs WHERE StartedAtUtc >= ? AND StartedAtUtc < ?",
            utcRangeStart,
            utcRangeEndExclusive);

        var map = new Dictionary<DateOnly, int>();
        foreach (var row in rows)
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
        var tz = TimeZoneInfo.Local;
        var startLocalUnspecified = DateTime.SpecifyKind(
            localDay.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Unspecified);
        var endExclusiveUnspecified = DateTime.SpecifyKind(
            localDay.AddDays(1).ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Unspecified);
        var utcStart = TimeZoneInfo.ConvertTimeToUtc(startLocalUnspecified, tz);
        var utcEndExclusive = TimeZoneInfo.ConvertTimeToUtc(endExclusiveUnspecified, tz);

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
        int maxSessions = 30)
    {
        if (string.IsNullOrWhiteSpace(exerciseName)) return Array.Empty<ExerciseSessionProgressEntry>();
        maxSessions = Math.Clamp(maxSessions, 1, 200);
        await _db.EnsureInitializedAsync();

        var planIdStr = workoutPlanId.ToString();
        var rows = await _db.Database.QueryAsync<ExerciseProgressAggRow>(
            @"SELECT sess.Id AS SessionId, sess.StartedAtUtc AS StartedAtUtc,
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
              LIMIT ?",
            planIdStr,
            exerciseName,
            maxSessions);

        return rows
            .Select(r => new ExerciseSessionProgressEntry(
                r.SessionId,
                r.StartedAtUtc,
                r.BestWeightKg,
                r.TotalReps,
                r.SetCount,
                r.TotalVolumeKg))
            .ToList();
    }

    public async Task<int> GetSessionCountAsync()
    {
        await _db.EnsureInitializedAsync();
        return await _db.Database.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM WorkoutSessionLogs");
    }

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
        var sessions = await GetAllSessionsAsync();

        var allSets = await _db.Database.Table<WorkoutSetLogEntity>().ToListAsync();
        var setsBySession = allSets
            .GroupBy(s => s.SessionId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(s => s.CompletedAtUtc)
                      .ThenBy(s => s.ExerciseIndex)
                      .ThenBy(s => s.SetIndex)
                      .ToList()
            );

        var entries = new List<WorkoutHistoryBackupEntry>(sessions.Count);
        foreach (var session in sessions)
        {
            setsBySession.TryGetValue(session.Id, out var sets);
            entries.Add(new WorkoutHistoryBackupEntry
            {
                Session = session,
                Sets = sets ?? new List<WorkoutSetLogEntity>()
            });
        }

        return new WorkoutHistoryBackup { FormatVersion = 1, Sessions = entries };
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
            foreach (var entry in backup.Sessions ?? new List<WorkoutHistoryBackupEntry>())
            {
                if (entry is null || entry.Session is null || string.IsNullOrWhiteSpace(entry.Session.Id))
                    continue;

                var sessionId = entry.Session.Id;
                conn.InsertOrReplace(entry.Session);

                foreach (var set in entry.Sets ?? new List<WorkoutSetLogEntity>())
                {
                    if (set is null)
                        continue;
                    set.SessionId = sessionId;
                    if (string.IsNullOrWhiteSpace(set.Id))
                        set.Id = Guid.NewGuid().ToString();
                    conn.InsertOrReplace(set);
                }
            }
        });
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
            return Array.Empty<WorkoutSetLogEntity>();

        await _db.EnsureInitializedAsync();
        var rows = await _db.Database.Table<WorkoutSetLogEntity>()
            .Where(s => s.SessionId == sessionId)
            .ToListAsync();

        return rows
            .OrderBy(s => s.CompletedAtUtc)
            .ThenBy(s => s.ExerciseIndex)
            .ThenBy(s => s.SetIndex)
            .ToList();
    }

    /// <summary>
    /// Latest logged reps/weight for this exercise name under the same workout plan (any session, including the current one).
    /// </summary>
    public async Task<LastSetMetrics?> GetLatestSetMetricsForExerciseAsync(Guid workoutPlanId, string exerciseName)
    {
        if (string.IsNullOrWhiteSpace(exerciseName)) return null;
        await _db.EnsureInitializedAsync();

        var planIdStr = workoutPlanId.ToString();
        var rows = await _db.Database.QueryAsync<LastSetMetricsRow>(
            @"SELECT s.Reps AS Reps, s.WeightKg AS WeightKg
              FROM WorkoutSetLogs s
              INNER JOIN WorkoutSessionLogs sess ON sess.Id = s.SessionId
              WHERE sess.WorkoutPlanId = ? AND s.ExerciseName = ?
              ORDER BY s.CompletedAtUtc DESC, s.ExerciseIndex DESC, s.SetIndex DESC
              LIMIT 1",
            planIdStr, exerciseName);

        var row = rows.FirstOrDefault();
        if (row == null) return null;
        return new LastSetMetrics(row.Reps, row.WeightKg);
    }

    /// <summary>Removes the most recently logged set row for the session (same order as append-only completion).</summary>
    public async Task<bool> TryDeleteLastSetLogAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return false;
        await _db.EnsureInitializedAsync();

        var rows = await _db.Database.Table<WorkoutSetLogEntity>()
            .Where(s => s.SessionId == sessionId)
            .ToListAsync();

        var last = rows
            .OrderByDescending(s => s.CompletedAtUtc)
            .ThenByDescending(s => s.ExerciseIndex)
            .ThenByDescending(s => s.SetIndex)
            .FirstOrDefault();

        if (last == null) return false;

        await _db.Database.DeleteAsync(last);
        return true;
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
            return JsonSerializer.Deserialize<WorkoutPlan>(json, s_jsonReadOptions);
        }
        catch
        {
            return null;
        }
    }
}
