using Physiquinator.Models;

namespace Physiquinator.Data;

public class WorkoutPlanRepository
{
    private readonly AppDatabase _database;

    public WorkoutPlanRepository(AppDatabase database)
    {
        _database = database;
    }

    public async Task<List<WorkoutPlan>> GetAllPlansAsync()
    {
        await _database.EnsureInitializedAsync().ConfigureAwait(false);
        var planEntities = await _database.Database.Table<WorkoutPlanEntity>().ToListAsync().ConfigureAwait(false);
        if (planEntities.Count == 0)
            return new List<WorkoutPlan>();

        var planIds = planEntities.Select(p => p.Id).ToList();
        var allExercises = await _database.Database.Table<ExercisePlanEntity>()
            .Where(e => planIds.Contains(e.WorkoutPlanId))
            .ToListAsync().ConfigureAwait(false);

        var exercisesGrouped = allExercises.GroupBy(e => e.WorkoutPlanId)
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.Order).ToList());

        var plans = new List<WorkoutPlan>();

        foreach (var planEntity in planEntities)
        {
            exercisesGrouped.TryGetValue(planEntity.Id, out var exercises);
            exercises ??= new List<ExercisePlanEntity>();

            var plan = new WorkoutPlan
            {
                Id = Guid.Parse(planEntity.Id),
                Name = planEntity.Name,
                RestIntervalSeconds = planEntity.RestIntervalSeconds,
                DefaultSetCount = planEntity.DefaultSetCount,
                CreatedAt = planEntity.CreatedAt,
                SortOrder = planEntity.SortOrder,
                Exercises = exercises.Select(e => new ExercisePlan
                {
                    Id = Guid.Parse(e.Id),
                    Name = e.Name,
                    SetCount = e.SetCount,
                    Order = e.Order,
                    RestIntervalSeconds = e.RestIntervalSeconds,
                    DefaultReps = e.DefaultReps,
                    DefaultWeightKg = e.DefaultWeightKg,
                    LogType = (ExerciseLogType)e.LogType
                }).ToList()
            };

            plans.Add(plan);
        }

        return plans.OrderBy(p => p.SortOrder).ThenByDescending(p => p.CreatedAt).ToList();
    }

    public async Task<WorkoutPlan?> GetPlanAsync(Guid id)
    {
        await _database.EnsureInitializedAsync().ConfigureAwait(false);
        var idString = id.ToString();
        var planEntity = await _database.Database.Table<WorkoutPlanEntity>()
            .Where(p => p.Id == idString)
            .FirstOrDefaultAsync().ConfigureAwait(false);

        if (planEntity == null)
            return null;

        var exercises = await _database.Database.Table<ExercisePlanEntity>()
            .Where(e => e.WorkoutPlanId == planEntity.Id)
            .OrderBy(e => e.Order)
            .ToListAsync().ConfigureAwait(false);

        return new WorkoutPlan
        {
            Id = Guid.Parse(planEntity.Id),
            Name = planEntity.Name,
            RestIntervalSeconds = planEntity.RestIntervalSeconds,
            DefaultSetCount = planEntity.DefaultSetCount,
            CreatedAt = planEntity.CreatedAt,
            SortOrder = planEntity.SortOrder,
            Exercises = exercises.Select(e => new ExercisePlan
            {
                Id = Guid.Parse(e.Id),
                Name = e.Name,
                SetCount = e.SetCount,
                Order = e.Order,
                RestIntervalSeconds = e.RestIntervalSeconds,
                DefaultReps = e.DefaultReps,
                DefaultWeightKg = e.DefaultWeightKg,
                LogType = (ExerciseLogType)e.LogType
            }).ToList()
        };
    }

    public async Task SavePlanAsync(WorkoutPlan plan)
    {
        await _database.EnsureInitializedAsync().ConfigureAwait(false);
        var planEntity = new WorkoutPlanEntity
        {
            Id = plan.Id.ToString(),
            Name = plan.Name,
            RestIntervalSeconds = plan.RestIntervalSeconds,
            DefaultSetCount = plan.DefaultSetCount,
            CreatedAt = plan.CreatedAt,
            SortOrder = plan.SortOrder
        };

        var planIdString = plan.Id.ToString();

        await _database.Database.RunInTransactionAsync(conn =>
        {
            conn.InsertOrReplace(planEntity);

            conn.Execute("DELETE FROM ExercisePlans WHERE WorkoutPlanId = ?", planIdString);

            foreach (var exercise in plan.Exercises)
            {
                var exerciseEntity = new ExercisePlanEntity
                {
                    Id = exercise.Id.ToString(),
                    WorkoutPlanId = planIdString,
                    Name = exercise.Name,
                    SetCount = exercise.SetCount,
                    Order = exercise.Order,
                    RestIntervalSeconds = exercise.RestIntervalSeconds,
                    DefaultReps = exercise.DefaultReps,
                    DefaultWeightKg = exercise.DefaultWeightKg,
                    LogType = (int)exercise.LogType
                };

                conn.Insert(exerciseEntity);
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
}
