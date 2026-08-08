using Physiquinator.Core.Data;
using Physiquinator.Core.Models;
using Physiquinator.Core.Services;
using System.Text.Json;
using Xunit;

namespace Physiquinator.Tests.Services;

public class WorkoutPlanServiceTests : IAsyncLifetime
{
    private AppDatabase _db = null!;
    private WorkoutPlanService _service = null!;

    static WorkoutPlanServiceTests() => SQLitePCL.Batteries_V2.Init();

    public async Task InitializeAsync()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"physiquinator_plan_test_{Guid.NewGuid():N}.db");
        _db = new AppDatabase(dbPath);
        await _db.EnsureInitializedAsync();
        _service = new WorkoutPlanService(new WorkoutPlanRepository(_db));
    }

    public async Task DisposeAsync() => await _db.Database.CloseAsync();

    private static async Task<(AppDatabase Db, WorkoutPlanService Service)> CreateIsolatedContextAsync()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"physiquinator_plan_test_{Guid.NewGuid():N}.db");
        var db = new AppDatabase(dbPath);
        await db.EnsureInitializedAsync();
        return (db, new WorkoutPlanService(new WorkoutPlanRepository(db)));
    }

    private static WorkoutPlan MakePlan(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        RestIntervalSeconds = 60,
        DefaultSetCount = 3,
        Exercises =
        [
            new ExercisePlan
            {
                Id = Guid.NewGuid(),
                Name = $"{name} Exercise",
                SetCount = 4,
                Order = 0,
                RestIntervalSeconds = 120,
                DefaultReps = 8,
                DefaultWeightKg = 80.0
            }
        ]
    };

    [Fact]
    public async Task ExportThenImport_SinglePlan_RoundTripsPlanAndExercises()
    {
        WorkoutPlan plan = MakePlan("Push Hypertrophy");
        plan.Exercises.Add(new ExercisePlan
        {
            Id = Guid.NewGuid(),
            Name = "Overhead Press",
            SetCount = 3,
            Order = 1,
            RestIntervalSeconds = 90,
            LogType = ExerciseLogType.WeightAndReps
        });
        await _service.SavePlanAsync(plan);

        var json = await _service.ExportPlanToJsonAsync(plan.Id);

        (AppDatabase targetDb, WorkoutPlanService targetService) = await CreateIsolatedContextAsync();
        try
        {
            WorkoutPlan imported = await targetService.ImportPlanFromJsonAsync(json);

            Assert.Equal(plan.Id, imported.Id);
            Assert.Equal(plan.Name, imported.Name);
            Assert.Equal(plan.RestIntervalSeconds, imported.RestIntervalSeconds);
            Assert.Equal(plan.DefaultSetCount, imported.DefaultSetCount);

            WorkoutPlan? reloaded = await targetService.GetPlanAsync(plan.Id);
            Assert.NotNull(reloaded);
            Assert.Equal(plan.Exercises.Count, reloaded.Exercises.Count);
            Assert.Equal(plan.Exercises[0].Name, reloaded.Exercises[0].Name);
            Assert.Equal(plan.Exercises[0].SetCount, reloaded.Exercises[0].SetCount);
            Assert.Equal(plan.Exercises[0].RestIntervalSeconds, reloaded.Exercises[0].RestIntervalSeconds);
            Assert.Equal(plan.Exercises[0].DefaultReps, reloaded.Exercises[0].DefaultReps);
            Assert.Equal(plan.Exercises[0].DefaultWeightKg, reloaded.Exercises[0].DefaultWeightKg);
            Assert.Equal(plan.Exercises[1].Name, reloaded.Exercises[1].Name);
        }
        finally
        {
            await targetDb.Database.CloseAsync();
        }
    }

    [Fact]
    public async Task ExportAllThenImportAll_RoundTripsEveryPlan()
    {
        WorkoutPlan planA = MakePlan("Push");
        WorkoutPlan planB = MakePlan("Pull");
        await _service.SavePlanAsync(planA);
        await _service.SavePlanAsync(planB);

        var json = await _service.ExportAllPlansToJsonAsync();

        (AppDatabase targetDb, WorkoutPlanService targetService) = await CreateIsolatedContextAsync();
        try
        {
            List<WorkoutPlan> imported = await targetService.ImportPlansFromJsonAsync(json);

            Assert.Equal(2, imported.Count);
            Assert.Equal(
                new[] { planA.Name, planB.Name }.OrderBy(n => n),
                imported.Select(p => p.Name).OrderBy(n => n));

            List<WorkoutPlan> reloaded = await targetService.GetAllPlansAsync();
            Assert.Equal(imported.Count, reloaded.Count);
            Assert.All(reloaded, p => Assert.NotEmpty(p.Exercises));
        }
        finally
        {
            await targetDb.Database.CloseAsync();
        }
    }

    [Fact]
    public async Task ImportPlanFromJsonAsync_InvalidJson_Throws()
    {
        await Assert.ThrowsAsync<JsonException>(() => _service.ImportPlanFromJsonAsync("{not valid json"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ImportPlanFromJsonAsync("null"));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.ImportPlanFromJsonAsync(""));
    }

    [Fact]
    public async Task ExportPlanToJsonAsync_UnknownPlan_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ExportPlanToJsonAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ReorderPlansAsync_PersistsNewOrder()
    {
        WorkoutPlan planA = MakePlan("A");
        WorkoutPlan planB = MakePlan("B");
        WorkoutPlan planC = MakePlan("C");
        await _service.SavePlanAsync(planA);
        await _service.SavePlanAsync(planB);
        await _service.SavePlanAsync(planC);

        await _service.ReorderPlansAsync([planC.Id, planA.Id, planB.Id]);

        List<WorkoutPlan> result = await _service.GetAllPlansAsync();
        Assert.Equal([planC.Id, planA.Id, planB.Id], result.Select(p => p.Id));
        Assert.Equal([0, 1, 2], result.Select(p => p.SortOrder));
    }

    [Fact]
    public async Task DeletePlanAsync_RemovesOnlyThatPlan()
    {
        WorkoutPlan planA = MakePlan("A");
        WorkoutPlan planB = MakePlan("B");
        await _service.SavePlanAsync(planA);
        await _service.SavePlanAsync(planB);

        await _service.DeletePlanAsync(planA.Id);

        Assert.Null(await _service.GetPlanAsync(planA.Id));
        Assert.NotNull(await _service.GetPlanAsync(planB.Id));
        Assert.Single(await _service.GetAllPlansAsync());
    }

    [Fact]
    public async Task PreviewPlansImportAsync_splits_new_vs_overwritten()
    {
        WorkoutPlan existing = MakePlan("Existing");
        await _service.SavePlanAsync(existing);

        WorkoutPlan newPlan = MakePlan("New");
        WorkoutPlan sameId = MakePlan("Same Id");
        sameId.Id = existing.Id;

        var json = JsonSerializer.Serialize(new[] { newPlan, sameId });

        PlanImportPreview preview = await _service.PreviewPlansImportAsync(json);

        Assert.Equal(2, preview.Total);
        Assert.Equal(1, preview.New);
        Assert.Equal(1, preview.Overwritten);
        Assert.Single(await _service.GetAllPlansAsync()); // nothing imported yet
    }

    [Fact]
    public async Task PreviewPlansImportAsync_accepts_single_plan_json()
    {
        WorkoutPlan plan = MakePlan("Single");
        var json = JsonSerializer.Serialize(plan);

        PlanImportPreview preview = await _service.PreviewPlansImportAsync(json);

        Assert.Equal(1, preview.Total);
        Assert.Equal(1, preview.New);
        Assert.Equal(0, preview.Overwritten);
    }

    [Fact]
    public async Task PreviewPlansImportAsync_rejects_empty_or_invalid_json()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.PreviewPlansImportAsync(""));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.PreviewPlansImportAsync("null"));
    }
}
