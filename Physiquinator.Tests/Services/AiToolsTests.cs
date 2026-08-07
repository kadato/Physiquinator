using Physiquinator.Core.Data;
using Physiquinator.Core.Models;
using Physiquinator.Core.Services;
using Physiquinator.Core.Services.Ai.Tools;
using System.Text.Json;
using Xunit;

namespace Physiquinator.Tests.Services;

public class AiToolsTests
{
    private static (AppDatabase db, WorkoutPlanRepository planRepo, WorkoutHistoryRepository historyRepo, WorkoutPlanService planService, UserProfileService profileService) CreateTestContext()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"physiquinator_ai_test_{Guid.NewGuid():N}.db");
        var db = new AppDatabase(dbPath);
        TimeProvider time = TimeProvider.System;
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
        (AppDatabase _, WorkoutPlanRepository _, WorkoutHistoryRepository _, WorkoutPlanService? planService, UserProfileService _) = CreateTestContext();
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

        List<WorkoutPlan> plans = await planService.GetAllPlansAsync();
        Assert.Single(plans);
        Assert.Equal("Push Hypertrophy", plans[0].Name);
        Assert.Single(plans[0].Exercises);
        Assert.Equal("Bench Press", plans[0].Exercises[0].Name);
    }

    [Fact]
    public async Task LogBodyweightTool_SavesBodyweightToHistoryAndProfile()
    {
        (AppDatabase _, WorkoutPlanRepository _, WorkoutHistoryRepository? historyRepo, WorkoutPlanService _, UserProfileService? profileService) = CreateTestContext();
        var tool = new LogBodyweightTool(historyRepo, profileService);

        var args = JsonSerializer.Serialize(new
        {
            bodyweightKg = 84.5,
            date = DateTime.Today.ToString("yyyy-MM-dd")
        });

        var responseJson = await tool.ExecuteAsync(args);
        Assert.Contains("success", responseJson);

        UserProfile activeProfile = profileService.GetActiveProfile();
        Assert.Equal(84.5, activeProfile.BodyweightKg);

        IReadOnlyList<BodyweightLogEntity> logs = await historyRepo.GetBodyweightLogsAsync(10);
        Assert.Single(logs);
        Assert.Equal(84.5, logs[0].BodyweightKg);
    }

    [Fact]
    public async Task GenerateDeloadPlanWorkflowTool_CreatesDeloadPlan()
    {
        (AppDatabase _, WorkoutPlanRepository _, WorkoutHistoryRepository _, WorkoutPlanService? planService, UserProfileService _) = CreateTestContext();
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

        List<WorkoutPlan> plans = await planService.GetAllPlansAsync();
        Assert.Equal(2, plans.Count);

        WorkoutPlan? deloadPlan = plans.FirstOrDefault(p => p.Name.Contains("Deload"));
        Assert.NotNull(deloadPlan);
        Assert.Equal(2, deloadPlan.Exercises[0].SetCount); // 50% volume of 4 sets
        Assert.Equal(108.0, deloadPlan.Exercises[0].DefaultWeightKg); // 90% of 120kg
    }

    [Fact]
    public async Task CalculateProgressiveOverloadWorkflowTool_RecommendsIncreaseFromLatestSession()
    {
        (AppDatabase _, WorkoutPlanRepository _, WorkoutHistoryRepository? historyRepo, WorkoutPlanService? planService, UserProfileService _) = CreateTestContext();

        var plan = new WorkoutPlan
        {
            Id = Guid.NewGuid(),
            Name = "Push",
            Exercises = [new ExercisePlan { Id = Guid.NewGuid(), Name = "Bench Press", SetCount = 3, DefaultReps = 8, DefaultWeightKg = 80.0 }]
        };
        await planService.SavePlanAsync(plan);

        var sessionId = await historyRepo.BeginSessionAsync(plan.Id, plan.Name, null);
        await historyRepo.LogSetAsync(sessionId, 0, "Bench Press", 0, reps: 8, weightKg: 100);
        await historyRepo.LogSetAsync(sessionId, 0, "Bench Press", 1, reps: 8, weightKg: 100);
        await historyRepo.EndSessionAsync(sessionId);

        var tool = new CalculateProgressiveOverloadWorkflowTool(historyRepo, planService);
        var responseJson = await tool.ExecuteAsync("{}");

        using var doc = JsonDocument.Parse(responseJson);
        JsonElement recommendations = doc.RootElement.GetProperty("recommendations");
        Assert.Equal(1, recommendations.GetArrayLength());
        Assert.Equal("Bench Press", recommendations[0].GetProperty("exerciseName").GetString());
        Assert.Equal(16, recommendations[0].GetProperty("lastLoggedTotalReps").GetInt32());
        Assert.Equal(100.0, recommendations[0].GetProperty("lastLoggedWeightKg").GetDouble());
        Assert.Equal(102.5, recommendations[0].GetProperty("recommendedWeightKg").GetDouble());
        Assert.Contains("+2.5%", recommendations[0].GetProperty("recommendationReason").GetString());
    }

    [Fact]
    public async Task GetExerciseProgressionTool_ReturnsSessionsAcrossPlans()
    {
        (AppDatabase _, WorkoutPlanRepository _, WorkoutHistoryRepository? historyRepo, WorkoutPlanService? planService, UserProfileService _) = CreateTestContext();

        var planA = new WorkoutPlan { Id = Guid.NewGuid(), Name = "A" };
        var planB = new WorkoutPlan { Id = Guid.NewGuid(), Name = "B" };
        await planService.SavePlanAsync(planA);
        await planService.SavePlanAsync(planB);

        var s1 = await historyRepo.BeginSessionAsync(planA.Id, planA.Name, null);
        await historyRepo.LogSetAsync(s1, 0, "Squat", 0, reps: 5, weightKg: 100);
        await historyRepo.EndSessionAsync(s1);

        var s2 = await historyRepo.BeginSessionAsync(planB.Id, planB.Name, null);
        await historyRepo.LogSetAsync(s2, 0, "Squat", 0, reps: 8, weightKg: 110);
        await historyRepo.EndSessionAsync(s2);

        var tool = new GetExerciseProgressionTool(historyRepo);
        var args = JsonSerializer.Serialize(new { exerciseName = "Squat" });
        var responseJson = await tool.ExecuteAsync(args);

        using var doc = JsonDocument.Parse(responseJson);
        Assert.Equal(2, doc.RootElement.GetProperty("totalSessions").GetInt32());
        JsonElement sessions = doc.RootElement.GetProperty("sessions");
        Assert.Equal(2, sessions.GetArrayLength());
        Assert.Contains(sessions.EnumerateArray(), s => s.GetProperty("SessionId").GetString() == s1 && Math.Abs(s.GetProperty("BestWeightKg").GetDouble() - 100) < 0.001);
        Assert.Contains(sessions.EnumerateArray(), s => s.GetProperty("SessionId").GetString() == s2 && Math.Abs(s.GetProperty("BestWeightKg").GetDouble() - 110) < 0.001);
    }
}
