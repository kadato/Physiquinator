using Physiquinator.Core.Data;
using Physiquinator.Core.Models;
using Xunit;

namespace Physiquinator.Tests.Data;

public class WorkoutPlanRepositoryTests : IAsyncLifetime
{
    private AppDatabase _db = null!;
    private WorkoutPlanRepository _sut = null!;

    // Register the native SQLite provider once for the whole test process
    static WorkoutPlanRepositoryTests() => SQLitePCL.Batteries_V2.Init();

    public async Task InitializeAsync()
    {
        _db = new AppDatabase(":memory:");
        await _db.EnsureInitializedAsync();
        _sut = new WorkoutPlanRepository(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.Database.CloseAsync();
    }

    // ------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------

    private static WorkoutPlan MakePlan(string name = "Plan A", int exerciseCount = 2) => new()
    {
        Name = name,
        RestIntervalSeconds = 60,
        DefaultSetCount = 3,
        Exercises = Enumerable.Range(0, exerciseCount)
            .Select(i => new ExercisePlan { Name = $"Ex{i}", SetCount = i + 1, Order = i, RestIntervalSeconds = 30 + (i * 10) })
            .ToList()
    };

    // ------------------------------------------------------------
    // GetAllPlansAsync
    // ------------------------------------------------------------

    [Fact]
    public async Task GetAllPlansAsync_ReturnsEmpty_WhenNoPlansSaved()
    {
        List<WorkoutPlan> result = await _sut.GetAllPlansAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllPlansAsync_ReturnsAllSavedPlans()
    {
        await _sut.SavePlanAsync(MakePlan("Plan A"));
        await _sut.SavePlanAsync(MakePlan("Plan B"));

        List<WorkoutPlan> result = await _sut.GetAllPlansAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAllPlansAsync_ReturnsPlansWithCorrectNames()
    {
        await _sut.SavePlanAsync(MakePlan("Leg Day"));

        List<WorkoutPlan> result = await _sut.GetAllPlansAsync();

        Assert.Equal("Leg Day", result[0].Name);
    }

    [Fact]
    public async Task GetAllPlansAsync_IncludesExercisesForEachPlan()
    {
        await _sut.SavePlanAsync(MakePlan(exerciseCount: 3));

        List<WorkoutPlan> result = await _sut.GetAllPlansAsync();

        Assert.Equal(3, result[0].Exercises.Count);
    }

    [Fact]
    public async Task GetAllPlansAsync_ExercisesReturnedInOrder()
    {
        WorkoutPlan plan = MakePlan(exerciseCount: 3);
        await _sut.SavePlanAsync(plan);

        List<WorkoutPlan> result = await _sut.GetAllPlansAsync();
        List<ExercisePlan> exercises = result[0].Exercises;

        Assert.Equal([0, 1, 2], exercises.Select(e => e.Order));
    }

    // ------------------------------------------------------------
    // GetPlanAsync
    // ------------------------------------------------------------

    [Fact]
    public async Task GetPlanAsync_ReturnsNull_WhenPlanDoesNotExist()
    {
        WorkoutPlan? result = await _sut.GetPlanAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPlanAsync_ReturnsPlan_WhenItExists()
    {
        WorkoutPlan plan = MakePlan("Push Day");
        await _sut.SavePlanAsync(plan);

        WorkoutPlan? result = await _sut.GetPlanAsync(plan.Id);

        Assert.NotNull(result);
        Assert.Equal(plan.Id, result.Id);
        Assert.Equal("Push Day", result.Name);
    }

    [Fact]
    public async Task GetPlanAsync_ReturnsCorrectRestIntervalSeconds()
    {
        WorkoutPlan plan = MakePlan();
        plan.RestIntervalSeconds = 90;
        await _sut.SavePlanAsync(plan);

        WorkoutPlan? result = await _sut.GetPlanAsync(plan.Id);

        Assert.Equal(90, result!.RestIntervalSeconds);
    }

    [Fact]
    public async Task GetPlanAsync_IncludesExercisesInOrder()
    {
        WorkoutPlan plan = MakePlan(exerciseCount: 3);
        await _sut.SavePlanAsync(plan);

        WorkoutPlan? result = await _sut.GetPlanAsync(plan.Id);

        Assert.Equal(3, result!.Exercises.Count);
        Assert.Equal([0, 1, 2], result.Exercises.Select(e => e.Order));
    }

    [Fact]
    public async Task GetPlanAsync_ExercisesHaveCorrectFields()
    {
        WorkoutPlan plan = MakePlan(exerciseCount: 1);
        ExercisePlan expected = plan.Exercises[0];
        expected.DefaultReps = 12;
        expected.DefaultWeightKg = 60.5;
        expected.LogType = ExerciseLogType.BodyweightReps;
        await _sut.SavePlanAsync(plan);

        WorkoutPlan? result = await _sut.GetPlanAsync(plan.Id);
        ExercisePlan actual = result!.Exercises[0];

        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.SetCount, actual.SetCount);
        Assert.Equal(expected.RestIntervalSeconds, actual.RestIntervalSeconds);
        Assert.Equal(12, actual.DefaultReps);
        Assert.Equal(60.5, actual.DefaultWeightKg);
        Assert.Equal(ExerciseLogType.BodyweightReps, actual.LogType);
    }

    // ------------------------------------------------------------
    // SavePlanAsync
    // ------------------------------------------------------------

    [Fact]
    public async Task SavePlanAsync_PersistsPlan()
    {
        WorkoutPlan plan = MakePlan();

        await _sut.SavePlanAsync(plan);

        WorkoutPlan? result = await _sut.GetPlanAsync(plan.Id);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task SavePlanAsync_UpdatesExistingPlan()
    {
        WorkoutPlan plan = MakePlan("Original");
        await _sut.SavePlanAsync(plan);

        plan.Name = "Updated";
        await _sut.SavePlanAsync(plan);

        WorkoutPlan? result = await _sut.GetPlanAsync(plan.Id);
        Assert.Equal("Updated", result!.Name);
    }

    [Fact]
    public async Task SavePlanAsync_ReplacesExercises_WhenUpdating()
    {
        WorkoutPlan plan = MakePlan(exerciseCount: 3);
        await _sut.SavePlanAsync(plan);

        plan.Exercises = [new ExercisePlan { Name = "Only One", SetCount = 5, Order = 0 }];
        await _sut.SavePlanAsync(plan);

        WorkoutPlan? result = await _sut.GetPlanAsync(plan.Id);
        Assert.Single(result!.Exercises);
        Assert.Equal("Only One", result.Exercises[0].Name);
    }

    [Fact]
    public async Task SavePlanAsync_PersistsDefaultSetCount()
    {
        WorkoutPlan plan = MakePlan();
        plan.DefaultSetCount = 5;
        await _sut.SavePlanAsync(plan);

        WorkoutPlan? result = await _sut.GetPlanAsync(plan.Id);

        Assert.Equal(5, result!.DefaultSetCount);
    }

    // ------------------------------------------------------------
    // DeletePlanAsync
    // ------------------------------------------------------------

    [Fact]
    public async Task DeletePlanAsync_RemovesPlan()
    {
        WorkoutPlan plan = MakePlan();
        await _sut.SavePlanAsync(plan);

        await _sut.DeletePlanAsync(plan.Id);

        WorkoutPlan? result = await _sut.GetPlanAsync(plan.Id);
        Assert.Null(result);
    }

    [Fact]
    public async Task DeletePlanAsync_RemovesExercisesOfDeletedPlan()
    {
        WorkoutPlan plan = MakePlan(exerciseCount: 3);
        await _sut.SavePlanAsync(plan);

        await _sut.DeletePlanAsync(plan.Id);

        var planIdString = plan.Id.ToString();
        var exerciseCount = await _db.Database.Table<ExercisePlanEntity>()
            .Where(e => e.WorkoutPlanId == planIdString)
            .CountAsync();
        Assert.Equal(0, exerciseCount);
    }

    [Fact]
    public async Task DeletePlanAsync_DoesNotRemoveOtherPlans()
    {
        WorkoutPlan planA = MakePlan("A");
        WorkoutPlan planB = MakePlan("B");
        await _sut.SavePlanAsync(planA);
        await _sut.SavePlanAsync(planB);

        await _sut.DeletePlanAsync(planA.Id);

        WorkoutPlan? result = await _sut.GetPlanAsync(planB.Id);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task DeletePlanAsync_IsNoOp_WhenPlanDoesNotExist()
    {
        // Should not throw
        await _sut.DeletePlanAsync(Guid.NewGuid());

        List<WorkoutPlan> all = await _sut.GetAllPlansAsync();
        Assert.Empty(all);
    }

    // ------------------------------------------------------------
    // ReorderPlansAsync
    // ------------------------------------------------------------

    [Fact]
    public async Task ReorderPlansAsync_PersistsNewSortOrder()
    {
        WorkoutPlan planA = MakePlan("A");
        WorkoutPlan planB = MakePlan("B");
        WorkoutPlan planC = MakePlan("C");
        await _sut.SavePlanAsync(planA);
        await _sut.SavePlanAsync(planB);
        await _sut.SavePlanAsync(planC);

        // New order: C (0), A (1), B (2)
        await _sut.ReorderPlansAsync([(planC.Id, 0), (planA.Id, 1), (planB.Id, 2)]);

        List<WorkoutPlan> result = await _sut.GetAllPlansAsync();

        Assert.Equal([planC.Id, planA.Id, planB.Id], result.Select(p => p.Id));
        Assert.Equal([0, 1, 2], result.Select(p => p.SortOrder));
    }

    [Fact]
    public async Task ReorderPlansAsync_IsNoOp_WhenPlanIdsDoNotExist()
    {
        WorkoutPlan planA = MakePlan("A");
        await _sut.SavePlanAsync(planA);

        await _sut.ReorderPlansAsync([(Guid.NewGuid(), 0)]);

        List<WorkoutPlan> result = await _sut.GetAllPlansAsync();
        Assert.Single(result);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsWarmupSetsAndSupersetGroup()
    {
        var plan = new WorkoutPlan
        {
            Name = "Superset day",
            Exercises =
            [
                new ExercisePlan { Name = "Bench Press", SetCount = 4, WarmupSetCount = 2, SupersetGroupId = "A", Order = 0 },
                new ExercisePlan { Name = "Barbell Row", SetCount = 4, WarmupSetCount = 1, SupersetGroupId = "A", Order = 1 },
                new ExercisePlan { Name = "Plank", SetCount = 3, LogType = ExerciseLogType.Duration, Order = 2 }
            ]
        };

        await _sut.SavePlanAsync(plan);

        WorkoutPlan? loaded = await _sut.GetPlanAsync(plan.Id);
        Assert.NotNull(loaded);
        Assert.Equal(3, loaded.Exercises.Count);
        Assert.Equal(2, loaded.Exercises[0].WarmupSetCount);
        Assert.Equal("A", loaded.Exercises[0].SupersetGroupId);
        Assert.Equal(1, loaded.Exercises[1].WarmupSetCount);
        Assert.Equal("A", loaded.Exercises[1].SupersetGroupId);
        Assert.Equal(0, loaded.Exercises[2].WarmupSetCount);
        Assert.Null(loaded.Exercises[2].SupersetGroupId);
        Assert.Equal(6, loaded.Exercises[0].TotalSetCount);
    }
}
