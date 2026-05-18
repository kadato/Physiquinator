namespace Physiquinator.Data;

public class WorkoutHistoryRepository
{
    private readonly AppDatabase _db;

    public WorkoutHistoryRepository(AppDatabase db) => _db = db;

    public async Task<string> BeginSessionAsync(Guid planId, string planName)
    {
        await _db.EnsureInitializedAsync();
        var id = Guid.NewGuid().ToString();
        await _db.Database.InsertAsync(new WorkoutSessionLogEntity
        {
            Id = id,
            WorkoutPlanId = planId.ToString(),
            PlanName = planName,
            StartedAtUtc = DateTime.UtcNow
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
}
