using System.Text.Json;
using Physiquinator.Models;

namespace Physiquinator.Data;

/// <summary>Reps and weight from the most recent log row for an exercise (same plan).</summary>
public sealed record LastSetMetrics(int? Reps, double? WeightKg);

public class WorkoutHistoryRepository
{
    private sealed class LastSetMetricsRow
    {
        public int? Reps { get; set; }
        public double? WeightKg { get; set; }
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
        var entries = new List<WorkoutHistoryBackupEntry>(sessions.Count);
        foreach (var session in sessions)
        {
            var sets = await GetSetsForSessionAsync(session.Id);
            entries.Add(new WorkoutHistoryBackupEntry
            {
                Session = session,
                Sets = sets.ToList()
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

        foreach (var entry in backup.Sessions ?? new List<WorkoutHistoryBackupEntry>())
        {
            if (entry is null || entry.Session is null || string.IsNullOrWhiteSpace(entry.Session.Id))
                continue;

            var sessionId = entry.Session.Id;
            await _db.Database.InsertOrReplaceAsync(entry.Session);

            foreach (var set in entry.Sets ?? new List<WorkoutSetLogEntity>())
            {
                if (set is null)
                    continue;
                set.SessionId = sessionId;
                if (string.IsNullOrWhiteSpace(set.Id))
                    set.Id = Guid.NewGuid().ToString();
                await _db.Database.InsertOrReplaceAsync(set);
            }
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
