using Physiquinator.Models;

namespace Physiquinator.Services;

public class DemoDataSeeder
{
    private readonly WorkoutPlanService _planService;

    public DemoDataSeeder(WorkoutPlanService planService)
    {
        _planService = planService;
    }

    public async Task SeedDemoDataIfNeededAsync()
    {
        var existingPlans = await _planService.GetAllPlansAsync();
        
        // Only seed if there are no plans yet
        if (existingPlans.Any())
            return;

        var demoPlans = new List<WorkoutPlan>
        {
            CreatePushDayPlan(),
            CreatePullDayPlan(),
            CreateLegDayPlan(),
            CreateFullBodyPlan()
        };

        foreach (var plan in demoPlans)
        {
            await _planService.SavePlanAsync(plan);
        }
    }

    private static WorkoutPlan CreatePushDayPlan()
    {
        return new WorkoutPlan
        {
            Id = Guid.NewGuid(),
            Name = "Push Day",
            RestIntervalSeconds = 90,
            DefaultSetCount = 4,
            Exercises = new List<ExercisePlan>
            {
                new() { Id = Guid.NewGuid(), Name = "Bench Press", SetCount = 4, Order = 0, RestIntervalSeconds = 120 },
                new() { Id = Guid.NewGuid(), Name = "Overhead Press", SetCount = 4, Order = 1, RestIntervalSeconds = 90 },
                new() { Id = Guid.NewGuid(), Name = "Incline Dumbbell Press", SetCount = 3, Order = 2, RestIntervalSeconds = 90 },
                new() { Id = Guid.NewGuid(), Name = "Lateral Raises", SetCount = 3, Order = 3, RestIntervalSeconds = 60 },
                new() { Id = Guid.NewGuid(), Name = "Tricep Pushdowns", SetCount = 3, Order = 4, RestIntervalSeconds = 60 },
                new() { Id = Guid.NewGuid(), Name = "Overhead Tricep Extension", SetCount = 3, Order = 5, RestIntervalSeconds = 60 }
            }
        };
    }

    private static WorkoutPlan CreatePullDayPlan()
    {
        return new WorkoutPlan
        {
            Id = Guid.NewGuid(),
            Name = "Pull Day",
            RestIntervalSeconds = 90,
            DefaultSetCount = 4,
            Exercises = new List<ExercisePlan>
            {
                new() { Id = Guid.NewGuid(), Name = "Deadlift", SetCount = 3, Order = 0, RestIntervalSeconds = 180 },
                new() { Id = Guid.NewGuid(), Name = "Pull-Ups", SetCount = 4, Order = 1, RestIntervalSeconds = 90 },
                new() { Id = Guid.NewGuid(), Name = "Barbell Rows", SetCount = 4, Order = 2, RestIntervalSeconds = 90 },
                new() { Id = Guid.NewGuid(), Name = "Face Pulls", SetCount = 3, Order = 3, RestIntervalSeconds = 60 },
                new() { Id = Guid.NewGuid(), Name = "Bicep Curls", SetCount = 3, Order = 4, RestIntervalSeconds = 60 },
                new() { Id = Guid.NewGuid(), Name = "Hammer Curls", SetCount = 3, Order = 5, RestIntervalSeconds = 60 }
            }
        };
    }

    private static WorkoutPlan CreateLegDayPlan()
    {
        return new WorkoutPlan
        {
            Id = Guid.NewGuid(),
            Name = "Leg Day",
            RestIntervalSeconds = 120,
            DefaultSetCount = 4,
            Exercises = new List<ExercisePlan>
            {
                new() { Id = Guid.NewGuid(), Name = "Squats", SetCount = 4, Order = 0, RestIntervalSeconds = 180 },
                new() { Id = Guid.NewGuid(), Name = "Romanian Deadlift", SetCount = 4, Order = 1, RestIntervalSeconds = 120 },
                new() { Id = Guid.NewGuid(), Name = "Leg Press", SetCount = 3, Order = 2, RestIntervalSeconds = 120 },
                new() { Id = Guid.NewGuid(), Name = "Leg Curls", SetCount = 3, Order = 3, RestIntervalSeconds = 90 },
                new() { Id = Guid.NewGuid(), Name = "Calf Raises", SetCount = 4, Order = 4, RestIntervalSeconds = 60 },
                new() { Id = Guid.NewGuid(), Name = "Leg Extensions", SetCount = 3, Order = 5, RestIntervalSeconds = 90 }
            }
        };
    }

    private static WorkoutPlan CreateFullBodyPlan()
    {
        return new WorkoutPlan
        {
            Id = Guid.NewGuid(),
            Name = "Full Body Workout",
            RestIntervalSeconds = 90,
            DefaultSetCount = 3,
            Exercises = new List<ExercisePlan>
            {
                new() { Id = Guid.NewGuid(), Name = "Squats", SetCount = 3, Order = 0, RestIntervalSeconds = 120 },
                new() { Id = Guid.NewGuid(), Name = "Bench Press", SetCount = 3, Order = 1, RestIntervalSeconds = 120 },
                new() { Id = Guid.NewGuid(), Name = "Barbell Rows", SetCount = 3, Order = 2, RestIntervalSeconds = 90 },
                new() { Id = Guid.NewGuid(), Name = "Overhead Press", SetCount = 3, Order = 3, RestIntervalSeconds = 90 },
                new() { Id = Guid.NewGuid(), Name = "Pull-Ups", SetCount = 3, Order = 4, RestIntervalSeconds = 90 },
                new() { Id = Guid.NewGuid(), Name = "Plank", SetCount = 3, Order = 5, RestIntervalSeconds = 45 }
            }
        };
    }
}
