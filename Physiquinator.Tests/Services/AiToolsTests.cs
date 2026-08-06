using Physiquinator.Core.Data;
using Physiquinator.Core.Models;
using Physiquinator.Core.Services;
using Physiquinator.Core.Services.Ai;
using Physiquinator.Core.Services.Ai.Tools;
using Physiquinator.Tests.TestDoubles;
using System.Text.Json;
using Xunit;

namespace Physiquinator.Tests.Services;

public class AiToolsTests
{
    private static (AppDatabase db, WorkoutPlanRepository planRepo, WorkoutHistoryRepository historyRepo, WorkoutPlanService planService, UserProfileService profileService) CreateTestContext()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"physiquinator_ai_test_{Guid.NewGuid():N}.db");
        var db = new AppDatabase(dbPath);
        var time = TimeProvider.System;
        var planRepo = new WorkoutPlanRepository(db);
        var historyRepo = new WorkoutHistoryRepository(db, time);
        var planService = new WorkoutPlanService(planRepo);

        var prefs = new InMemoryPreferences();
        var dbPathProvider = new TestDatabasePathProvider(dbPath);
        var sessionService = new WorkoutSessionService(time);

        var profileService = new UserProfileService(db, sessionService, prefs, dbPathProvider, time);

        return (db, planRepo, historyRepo, planService, profileService);
    }

    private sealed class TestDatabasePathProvider(string path) : IDatabasePathProvider
    {
        public string GetDatabasePath(Guid profileId) => path;
    }

    private sealed class InMemoryPreferences : IAppPreferences
    {
        private readonly Dictionary<string, string> _strDict = [];
        private readonly Dictionary<string, bool> _boolDict = [];

        public string Get(string key, string defaultValue) => _strDict.TryGetValue(key, out var v) ? v : defaultValue;
        public bool Get(string key, bool defaultValue) => _boolDict.TryGetValue(key, out var v) ? v : defaultValue;
        public void Set(string key, string value) => _strDict[key] = value;
        public void Set(string key, bool value) => _boolDict[key] = value;
    }

    [Fact]
    public async Task CreateWorkoutPlanTool_CreatesPlanInDatabase()
    {
        var (_, _, _, planService, _) = CreateTestContext();
        var tool = new CreateWorkoutPlanTool(planService);

        var args = JsonSerializer.Serialize(new
        {
            name = "Push Hypertrophy",
            exercises = new[]
            {
                new { name = "Bench Press", targetSets = 4, targetReps = 8, targetWeightKg = 80.0, restTimerSeconds = 120 }
            }
        });

        var responseJson = await tool.ExecuteAsync(args);
        Assert.Contains("success", responseJson);

        var plans = await planService.GetAllPlansAsync();
        Assert.Single(plans);
        Assert.Equal("Push Hypertrophy", plans[0].Name);
        Assert.Single(plans[0].Exercises);
        Assert.Equal("Bench Press", plans[0].Exercises[0].Name);
    }

    [Fact]
    public async Task LogBodyweightTool_SavesBodyweightToHistoryAndProfile()
    {
        var (_, _, historyRepo, _, profileService) = CreateTestContext();
        var tool = new LogBodyweightTool(historyRepo, profileService);

        var args = JsonSerializer.Serialize(new
        {
            bodyweightKg = 84.5,
            date = DateTime.Today.ToString("yyyy-MM-dd")
        });

        var responseJson = await tool.ExecuteAsync(args);
        Assert.Contains("success", responseJson);

        var activeProfile = profileService.GetActiveProfile();
        Assert.Equal(84.5, activeProfile.BodyweightKg);

        var logs = await historyRepo.GetBodyweightLogsAsync(10);
        Assert.Single(logs);
        Assert.Equal(84.5, logs[0].BodyweightKg);
    }

    [Fact]
    public async Task GenerateDeloadPlanWorkflowTool_CreatesDeloadPlan()
    {
        var (_, _, _, planService, _) = CreateTestContext();
        var basePlan = new WorkoutPlan
        {
            Id = Guid.NewGuid(),
            Name = "Heavy Legs",
            Exercises =
            [
                new() { Id = Guid.NewGuid(), Name = "Squat", SetCount = 4, DefaultReps = 6, DefaultWeightKg = 120.0 }
            ]

        };
        await planService.SavePlanAsync(basePlan);

        var tool = new GenerateDeloadPlanWorkflowTool(planService);
        var args = JsonSerializer.Serialize(new { planId = basePlan.Id.ToString() });

        var responseJson = await tool.ExecuteAsync(args);
        Assert.Contains("success", responseJson);

        var plans = await planService.GetAllPlansAsync();
        Assert.Equal(2, plans.Count);

        var deloadPlan = plans.FirstOrDefault(p => p.Name.Contains("Deload"));
        Assert.NotNull(deloadPlan);
        Assert.Equal(2, deloadPlan.Exercises[0].SetCount); // 50% volume of 4 sets
        Assert.Equal(108.0, deloadPlan.Exercises[0].DefaultWeightKg); // 90% of 120kg
    }
}
