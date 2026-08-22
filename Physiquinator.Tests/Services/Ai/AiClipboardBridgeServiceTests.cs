using Physiquinator.Core.Data;
using Physiquinator.Core.Models;
using Physiquinator.Core.Services;
using Physiquinator.Core.Services.Ai;
using Physiquinator.Core.Services.Ai.Tools;
using System.Text.Json;
using Xunit;

namespace Physiquinator.Tests.Services.Ai;

public class AiClipboardBridgeServiceTests
{
    private static (
        AppDatabase db,
        WorkoutPlanRepository planRepo,
        WorkoutHistoryRepository historyRepo,
        WorkoutPlanService planService,
        UserProfileService profileService,
        AiToolRegistry toolRegistry,
        AiClipboardBridgeService bridgeService) CreateTestContext()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"physiquinator_bridge_test_{Guid.NewGuid():N}.db");
        var db = new AppDatabase(dbPath);
        TimeProvider time = TimeProvider.System;
        var planRepo = new WorkoutPlanRepository(db);
        var historyRepo = new WorkoutHistoryRepository(db, time);
        var planService = new WorkoutPlanService(planRepo);

        var prefs = new InMemoryPreferences();
        var dbPathProvider = new TestDatabasePathProvider(dbPath);
        var sessionService = new WorkoutSessionService(time);
        var profileService = new UserProfileService(db, sessionService, prefs, dbPathProvider, time);

        var tools = new IAiTool[]
        {
            new GetWorkoutPlansTool(planService),
            new CreateWorkoutPlanTool(planService),
            new UpdateWorkoutPlanTool(planService),
            new DeleteWorkoutPlanTool(planService),
            new LogBodyweightTool(historyRepo, profileService, time),
            new DeleteBodyweightTool(historyRepo),
            new GetWorkoutHistoryStatsTool(historyRepo)
        };

        var toolRegistry = new AiToolRegistry(tools);
        var bridgeService = new AiClipboardBridgeService(toolRegistry, profileService, planService, historyRepo, time);

        return (db, planRepo, historyRepo, planService, profileService, toolRegistry, bridgeService);
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
    public async Task GeneratePromptAsync_IncludesContextAndSchemas()
    {
        (_, _, _, WorkoutPlanService planService, _, _, AiClipboardBridgeService bridgeService) = CreateTestContext();

        var plan = new WorkoutPlan
        {
            Id = Guid.NewGuid(),
            Name = "Push Day",
            Exercises =
            [
                new() { Name = "Bench Press", SetCount = 3, DefaultReps = 10, DefaultWeightKg = 70.0, RestIntervalSeconds = 90 }
            ]
        };
        await planService.SavePlanAsync(plan);

        var prompt = await bridgeService.GeneratePromptAsync("Create a pull day routine.");

        Assert.Contains("Physiquinator AI", prompt);
        Assert.Contains("Push Day", prompt);
        Assert.Contains("Bench Press", prompt);
        Assert.Contains("create_workout_plan", prompt);
        Assert.Contains("log_bodyweight_entry", prompt);
        Assert.Contains("Create a pull day routine.", prompt);
    }

    [Fact]
    public void ParseResponse_ExtractsJsonFromMarkdownFences()
    {
        (_, _, _, _, _, _, AiClipboardBridgeService bridgeService) = CreateTestContext();

        var llmResponse = """
            Here is your new workout plan for Pull Day!

            ### Summary
            - Focus on back and biceps.
            - Rest 90 seconds between compound sets.

            ```json
            {
              "actions": [
                {
                  "tool": "create_workout_plan",
                  "arguments": {
                    "name": "Pull Day",
                    "exercises": [
                      { "name": "Barbell Row", "targetSets": 4, "targetReps": 8, "targetWeightKg": 60.0, "restTimerSeconds": 90 },
                      { "name": "Bicep Curl", "targetSets": 3, "targetReps": 12, "targetWeightKg": 15.0, "restTimerSeconds": 60 }
                    ]
                  }
                },
                {
                  "tool": "log_bodyweight",
                  "arguments": {
                    "bodyweightKg": 82.5,
                    "date": "2026-08-21"
                  }
                }
              ]
            }
            ```

            Let me know if you want to tweak anything!
            """;

        IReadOnlyList<AiBridgeAction> actions = bridgeService.ParseResponse(llmResponse);

        Assert.Equal(2, actions.Count);

        Assert.True(actions[0].IsValid);
        Assert.Equal("create_workout_plan", actions[0].ToolName);
        Assert.Contains("Pull Day", actions[0].HumanSummary);
        Assert.Contains("2 exercises", actions[0].HumanSummary);

        Assert.True(actions[1].IsValid);
        Assert.Equal("log_bodyweight_entry", actions[1].ToolName);
        Assert.Contains("82.5", actions[1].HumanSummary);
    }

    [Fact]
    public void ParseResponse_HandlesUnknownToolGracefully()
    {
        (_, _, _, _, _, _, AiClipboardBridgeService bridgeService) = CreateTestContext();

        var llmResponse = """
            ```json
            {
              "actions": [
                {
                  "tool": "non_existent_tool",
                  "arguments": { "foo": "bar" }
                }
              ]
            }
            ```
            """;

        IReadOnlyList<AiBridgeAction> actions = bridgeService.ParseResponse(llmResponse);

        var action = Assert.Single(actions);
        Assert.False(action.IsValid);
        Assert.Contains("not recognized", action.ValidationError);
    }

    [Fact]
    public async Task ExecuteActionsAsync_SuccessfullyExecutesActions()
    {
        (_, _, _, WorkoutPlanService planService, UserProfileService profileService, _, AiClipboardBridgeService bridgeService) = CreateTestContext();

        var actions = new List<AiBridgeAction>
        {
            new()
            {
                ToolName = "create_workout_plan",
                ArgumentsJson = JsonSerializer.Serialize(new
                {
                    name = "Leg Day A",
                    exercises = new[]
                    {
                        new { name = "Squat", targetSets = 4, targetReps = 6, targetWeightKg = 100.0, restTimerSeconds = 120 }
                    }
                }),
                IsValid = true
            },
            new()
            {
                ToolName = "log_bodyweight_entry",
                ArgumentsJson = JsonSerializer.Serialize(new
                {
                    bodyweightKg = 83.0,
                    date = "2026-08-21"
                }),
                IsValid = true
            }
        };

        List<AiBridgeActionExecutionResult> results = await bridgeService.ExecuteActionsAsync(actions);

        Assert.Equal(2, results.Count);
        Assert.True(results[0].Success);
        Assert.True(results[1].Success);

        List<WorkoutPlan> plans = await planService.GetAllPlansAsync();
        var plan = Assert.Single(plans);
        Assert.Equal("Leg Day A", plan.Name);

        Assert.Equal(83.0, profileService.GetActiveProfile().BodyweightKg);
    }
}
