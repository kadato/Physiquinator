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
        var planEntities = await _database.Database.Table<WorkoutPlanEntity>().ToListAsync();
        var plans = new List<WorkoutPlan>();

        foreach (var planEntity in planEntities)
        {
            var exercises = await _database.Database.Table<ExercisePlanEntity>()
                .Where(e => e.WorkoutPlanId == planEntity.Id)
                .OrderBy(e => e.Order)
                .ToListAsync();

            var plan = new WorkoutPlan
            {
                Id = Guid.Parse(planEntity.Id),
                Name = planEntity.Name,
                RestIntervalSeconds = planEntity.RestIntervalSeconds,
                DefaultSetCount = planEntity.DefaultSetCount,
                CreatedAt = planEntity.CreatedAt,
                Exercises = exercises.Select(e => new ExercisePlan
                {
                    Id = Guid.Parse(e.Id),
                    Name = e.Name,
                    SetCount = e.SetCount,
                    Order = e.Order,
                    RestIntervalSeconds = e.RestIntervalSeconds
                }).ToList()
            };

            plans.Add(plan);
        }

        return plans;
    }

    public async Task<WorkoutPlan?> GetPlanAsync(Guid id)
    {
        var planEntity = await _database.Database.Table<WorkoutPlanEntity>()
            .Where(p => p.Id == id.ToString())
            .FirstOrDefaultAsync();

        if (planEntity == null)
            return null;

        var exercises = await _database.Database.Table<ExercisePlanEntity>()
            .Where(e => e.WorkoutPlanId == planEntity.Id)
            .OrderBy(e => e.Order)
            .ToListAsync();

        return new WorkoutPlan
        {
            Id = Guid.Parse(planEntity.Id),
            Name = planEntity.Name,
            RestIntervalSeconds = planEntity.RestIntervalSeconds,
            DefaultSetCount = planEntity.DefaultSetCount,
            CreatedAt = planEntity.CreatedAt,
            Exercises = exercises.Select(e => new ExercisePlan
            {
                Id = Guid.Parse(e.Id),
                Name = e.Name,
                SetCount = e.SetCount,
                Order = e.Order,
                RestIntervalSeconds = e.RestIntervalSeconds
            }).ToList()
        };
    }

    public async Task SavePlanAsync(WorkoutPlan plan)
    {
        var planEntity = new WorkoutPlanEntity
        {
            Id = plan.Id.ToString(),
            Name = plan.Name,
            RestIntervalSeconds = plan.RestIntervalSeconds,
            DefaultSetCount = plan.DefaultSetCount,
            CreatedAt = plan.CreatedAt
        };

        await _database.Database.InsertOrReplaceAsync(planEntity);

        await _database.Database.Table<ExercisePlanEntity>()
            .Where(e => e.WorkoutPlanId == plan.Id.ToString())
            .DeleteAsync();

        foreach (var exercise in plan.Exercises)
        {
            var exerciseEntity = new ExercisePlanEntity
            {
                Id = exercise.Id.ToString(),
                WorkoutPlanId = plan.Id.ToString(),
                Name = exercise.Name,
                SetCount = exercise.SetCount,
                Order = exercise.Order,
                RestIntervalSeconds = exercise.RestIntervalSeconds
            };

            await _database.Database.InsertAsync(exerciseEntity);
        }
    }

    public async Task DeletePlanAsync(Guid id)
    {
        await _database.Database.Table<ExercisePlanEntity>()
            .Where(e => e.WorkoutPlanId == id.ToString())
            .DeleteAsync();

        await _database.Database.Table<WorkoutPlanEntity>()
            .Where(p => p.Id == id.ToString())
            .DeleteAsync();
    }
}
