using Physiquinator.Core.Models;

namespace Physiquinator.Core.Data;

public sealed class WorkoutPlanRepository(AppDatabase database)
{
    private readonly AppDatabase _database = database;

    private static WorkoutPlan ToModel(WorkoutPlanEntity planEntity, List<ExercisePlanEntity> exercises) => new()
    {
        Id = Guid.Parse(planEntity.Id),
        Name = planEntity.Name,
        RestIntervalSeconds = planEntity.RestIntervalSeconds,
        DefaultSetCount = planEntity.DefaultSetCount,
        CreatedAt = planEntity.CreatedAt,
        SortOrder = planEntity.SortOrder,
        Exercises = [.. exercises.Select(ToModel)]
    };

    private static ExercisePlan ToModel(ExercisePlanEntity e) => new()
    {
        Id = Guid.Parse(e.Id),
        Name = e.Name,
        SetCount = e.SetCount,
        WarmupSetCount = e.WarmupSetCount,
        SupersetGroupId = e.SupersetGroupId,
        Order = e.Order,
        RestIntervalSeconds = e.RestIntervalSeconds,
        DefaultReps = e.DefaultReps,
        DefaultWeightKg = e.DefaultWeightKg,
        BodyweightPercent = e.BodyweightPercent,
        LogType = (ExerciseLogType)e.LogType
    };

    private static WorkoutPlanEntity ToEntity(WorkoutPlan plan) => new()
    {
        Id = plan.Id.ToString(),
        Name = plan.Name,
        RestIntervalSeconds = plan.RestIntervalSeconds,
        DefaultSetCount = plan.DefaultSetCount,
        CreatedAt = plan.CreatedAt,
        SortOrder = plan.SortOrder
    };

    private static ExercisePlanEntity ToEntity(ExercisePlan exercise, string planIdString) => new()
    {
        Id = exercise.Id.ToString(),
        WorkoutPlanId = planIdString,
        Name = exercise.Name,
        SetCount = exercise.SetCount,
        WarmupSetCount = exercise.WarmupSetCount,
        SupersetGroupId = exercise.SupersetGroupId,
        Order = exercise.Order,
        RestIntervalSeconds = exercise.RestIntervalSeconds,
        DefaultReps = exercise.DefaultReps,
        DefaultWeightKg = exercise.DefaultWeightKg,
        BodyweightPercent = exercise.BodyweightPercent,
        LogType = (int)exercise.LogType
    };

    public async Task<List<WorkoutPlan>> GetAllPlansAsync()
    {
        await _database.EnsureInitializedAsync().ConfigureAwait(false);

        List<WorkoutPlanEntity> planEntities = await _database.Database.Table<WorkoutPlanEntity>().ToListAsync().ConfigureAwait(false);
        if (planEntities.Count == 0)
            return [];

        List<ExercisePlanEntity> allExercises = await _database.Database.Table<ExercisePlanEntity>().ToListAsync().ConfigureAwait(false);

        var exercisesGrouped = allExercises.GroupBy(e => e.WorkoutPlanId)
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.Order).ToList(), StringComparer.Ordinal);

        var plans = new List<WorkoutPlan>(planEntities.Count);

        foreach (WorkoutPlanEntity planEntity in planEntities)
        {
            exercisesGrouped.TryGetValue(planEntity.Id, out List<ExercisePlanEntity>? exercises);
            plans.Add(ToModel(planEntity, exercises ?? []));
        }

        return [.. plans.OrderBy(p => p.SortOrder).ThenByDescending(p => p.CreatedAt)];
    }

    public async Task<WorkoutPlan?> GetPlanAsync(Guid id)
    {
        await _database.EnsureInitializedAsync().ConfigureAwait(false);
        var idString = id.ToString();
        WorkoutPlanEntity planEntity = await _database.Database.Table<WorkoutPlanEntity>()
            .Where(p => p.Id == idString)
            .FirstOrDefaultAsync().ConfigureAwait(false);

        if (planEntity == null)
            return null;

        List<ExercisePlanEntity> exercises = await _database.Database.Table<ExercisePlanEntity>()
            .Where(e => e.WorkoutPlanId == planEntity.Id)
            .OrderBy(e => e.Order)
            .ToListAsync().ConfigureAwait(false);

        return ToModel(planEntity, exercises);
    }

    public async Task SavePlanAsync(WorkoutPlan plan)
    {
        await _database.EnsureInitializedAsync().ConfigureAwait(false);
        WorkoutPlanEntity planEntity = ToEntity(plan);

        var planIdString = plan.Id.ToString();

        await _database.Database.RunInTransactionAsync(conn =>
        {
            conn.InsertOrReplace(planEntity);

            conn.Execute("DELETE FROM ExercisePlans WHERE WorkoutPlanId = ?", planIdString);

            foreach (ExercisePlan exercise in plan.Exercises)
            {
                conn.Insert(ToEntity(exercise, planIdString));
            }
        }).ConfigureAwait(false);
    }

    /// <summary>Persists all plans and their exercises in a single transaction.</summary>
    public async Task SavePlansAsync(IReadOnlyList<WorkoutPlan> plans)
    {
        ArgumentNullException.ThrowIfNull(plans);
        await _database.EnsureInitializedAsync().ConfigureAwait(false);

        await _database.Database.RunInTransactionAsync(conn =>
        {
            foreach (WorkoutPlan plan in plans)
            {
                var planIdString = plan.Id.ToString();
                conn.InsertOrReplace(ToEntity(plan));

                conn.Execute("DELETE FROM ExercisePlans WHERE WorkoutPlanId = ?", planIdString);

                foreach (ExercisePlan exercise in plan.Exercises)
                {
                    conn.Insert(ToEntity(exercise, planIdString));
                }
            }
        }).ConfigureAwait(false);
    }

    public async Task DeletePlanAsync(Guid id)
    {
        await _database.EnsureInitializedAsync().ConfigureAwait(false);
        var idString = id.ToString();
        await _database.Database.Table<ExercisePlanEntity>()
            .Where(e => e.WorkoutPlanId == idString)
            .DeleteAsync().ConfigureAwait(false);

        await _database.Database.Table<WorkoutPlanEntity>()
            .Where(p => p.Id == idString)
            .DeleteAsync().ConfigureAwait(false);
    }

    /// <summary>Persists a new relative ordering for the given plans in a single transaction.</summary>
    public async Task ReorderPlansAsync(IReadOnlyList<(Guid Id, int SortOrder)> orderedPlans)
    {
        ArgumentNullException.ThrowIfNull(orderedPlans);
        await _database.EnsureInitializedAsync().ConfigureAwait(false);

        await _database.Database.RunInTransactionAsync(conn =>
        {
            foreach ((Guid id, var sortOrder) in orderedPlans)
            {
                conn.Execute("UPDATE WorkoutPlans SET SortOrder = ? WHERE Id = ?", sortOrder, id.ToString());
            }
        }).ConfigureAwait(false);
    }
}
