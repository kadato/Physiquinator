using Physiquinator.Data;
using Physiquinator.Models;
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

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────

    private static WorkoutPlan MakePlan(string name = "Plan A", int exerciseCount = 2) => new()
    {
        Name = name,
        RestIntervalSeconds = 60,
        DefaultSetCount = 3,
        Exercises = Enumerable.Range(0, exerciseCount)
            .Select(i => new ExercisePlan { Name = $"Ex{i}", SetCount = i + 1, Order = i, RestIntervalSeconds = 30 + i * 10 })
            .ToList()
    };

    // ──────────────────────────────────────────────────────────────
    // GetAllPlansAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllPlansAsync_ReturnsEmpty_WhenNoPlansSaved()
    {
        var result = await _sut.GetAllPlansAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllPlansAsync_ReturnsAllSavedPlans()
    {
        await _sut.SavePlanAsync(MakePlan("Plan A"));
        await _sut.SavePlanAsync(MakePlan("Plan B"));

        var result = await _sut.GetAllPlansAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAllPlansAsync_ReturnsPlansWithCorrectNames()
    {
        await _sut.SavePlanAsync(MakePlan("Leg Day"));

        var result = await _sut.GetAllPlansAsync();

        Assert.Equal("Leg Day", result[0].Name);
    }

    [Fact]
    public async Task GetAllPlansAsync_IncludesExercisesForEachPlan()
    {
        await _sut.SavePlanAsync(MakePlan(exerciseCount: 3));

        var result = await _sut.GetAllPlansAsync();

        Assert.Equal(3, result[0].Exercises.Count);
    }

    [Fact]
    public async Task GetAllPlansAsync_ExercisesReturnedInOrder()
    {
        var plan = MakePlan(exerciseCount: 3);
        await _sut.SavePlanAsync(plan);

        var result = await _sut.GetAllPlansAsync();
        var exercises = result[0].Exercises;

        Assert.Equal([0, 1, 2], exercises.Select(e => e.Order));
    }

    // ──────────────────────────────────────────────────────────────
    // GetPlanAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPlanAsync_ReturnsNull_WhenPlanDoesNotExist()
    {
        var result = await _sut.GetPlanAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPlanAsync_ReturnsPlan_WhenItExists()
    {
        var plan = MakePlan("Push Day");
        await _sut.SavePlanAsync(plan);

        var result = await _sut.GetPlanAsync(plan.Id);

        Assert.NotNull(result);
        Assert.Equal(plan.Id, result.Id);
        Assert.Equal("Push Day", result.Name);
    }

    [Fact]
    public async Task GetPlanAsync_ReturnsCorrectRestIntervalSeconds()
    {
        var plan = MakePlan();
        plan.RestIntervalSeconds = 90;
        await _sut.SavePlanAsync(plan);

        var result = await _sut.GetPlanAsync(plan.Id);

        Assert.Equal(90, result!.RestIntervalSeconds);
    }

    [Fact]
    public async Task GetPlanAsync_IncludesExercisesInOrder()
    {
        var plan = MakePlan(exerciseCount: 3);
        await _sut.SavePlanAsync(plan);

        var result = await _sut.GetPlanAsync(plan.Id);

        Assert.Equal(3, result!.Exercises.Count);
        Assert.Equal([0, 1, 2], result.Exercises.Select(e => e.Order));
    }

    [Fact]
    public async Task GetPlanAsync_ExercisesHaveCorrectFields()
    {
        var plan = MakePlan(exerciseCount: 1);
        var expected = plan.Exercises[0];
        await _sut.SavePlanAsync(plan);

        var result = await _sut.GetPlanAsync(plan.Id);
        var actual = result!.Exercises[0];

        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.SetCount, actual.SetCount);
        Assert.Equal(expected.RestIntervalSeconds, actual.RestIntervalSeconds);
    }

    // ──────────────────────────────────────────────────────────────
    // SavePlanAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SavePlanAsync_PersistsPlan()
    {
        var plan = MakePlan();

        await _sut.SavePlanAsync(plan);

        var result = await _sut.GetPlanAsync(plan.Id);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task SavePlanAsync_UpdatesExistingPlan()
    {
        var plan = MakePlan("Original");
        await _sut.SavePlanAsync(plan);

        plan.Name = "Updated";
        await _sut.SavePlanAsync(plan);

        var result = await _sut.GetPlanAsync(plan.Id);
        Assert.Equal("Updated", result!.Name);
    }

    [Fact]
    public async Task SavePlanAsync_ReplacesExercises_WhenUpdating()
    {
        var plan = MakePlan(exerciseCount: 3);
        await _sut.SavePlanAsync(plan);

        plan.Exercises = [new ExercisePlan { Name = "Only One", SetCount = 5, Order = 0 }];
        await _sut.SavePlanAsync(plan);

        var result = await _sut.GetPlanAsync(plan.Id);
        Assert.Single(result!.Exercises);
        Assert.Equal("Only One", result.Exercises[0].Name);
    }

    [Fact]
    public async Task SavePlanAsync_PersistsDefaultSetCount()
    {
        var plan = MakePlan();
        plan.DefaultSetCount = 5;
        await _sut.SavePlanAsync(plan);

        var result = await _sut.GetPlanAsync(plan.Id);

        Assert.Equal(5, result!.DefaultSetCount);
    }

    // ──────────────────────────────────────────────────────────────
    // DeletePlanAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeletePlanAsync_RemovesPlan()
    {
        var plan = MakePlan();
        await _sut.SavePlanAsync(plan);

        await _sut.DeletePlanAsync(plan.Id);

        var result = await _sut.GetPlanAsync(plan.Id);
        Assert.Null(result);
    }

    [Fact]
    public async Task DeletePlanAsync_RemovesExercisesOfDeletedPlan()
    {
        var plan = MakePlan(exerciseCount: 3);
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
        var planA = MakePlan("A");
        var planB = MakePlan("B");
        await _sut.SavePlanAsync(planA);
        await _sut.SavePlanAsync(planB);

        await _sut.DeletePlanAsync(planA.Id);

        var result = await _sut.GetPlanAsync(planB.Id);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task DeletePlanAsync_IsNoOp_WhenPlanDoesNotExist()
    {
        // Should not throw
        await _sut.DeletePlanAsync(Guid.NewGuid());

        var all = await _sut.GetAllPlansAsync();
        Assert.Empty(all);
    }
}
