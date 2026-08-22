using Physiquinator.Core.Models;
using System.Text.Json;

namespace Physiquinator.Core.Services.Ai.Tools;

public sealed class GetWorkoutPlansTool(WorkoutPlanService planService) : IAiTool
{
    public string Name => "get_workout_plans";
    public string Description => "Get all existing workout plans, including exercise lists, sets, reps, and target weights.";

    public object ParametersSchema => new
    {
        type = "object",
        properties = new { },
        required = Array.Empty<string>()
    };

    public async Task<string> ExecuteAsync(string argumentsJson)
    {
        List<WorkoutPlan> plans = await planService.GetAllPlansAsync();
        var result = plans.Select(p => new
        {
            p.Id,
            p.Name,
            p.RestIntervalSeconds,
            p.DefaultSetCount,
            Exercises = p.Exercises.Select(e => new
            {
                e.Id,
                e.Name,
                e.LogType,
                TargetSets = e.SetCount,
                TargetReps = e.DefaultReps,
                TargetWeightKg = e.DefaultWeightKg,
                RestTimerSeconds = e.RestIntervalSeconds
            })
        });

        return JsonSerializer.Serialize(result);
    }
}

public sealed class CreateWorkoutPlanTool(WorkoutPlanService planService) : IAiTool
{
    private const string NameProperty = "name";
    private const string StringType = "string";

    public string Name => "create_workout_plan";
    public string Description => "Create a new workout plan with a list of exercises.";

    public object ParametersSchema => new
    {
        type = "object",
        properties = new
        {
            name = new { type = StringType, description = "Name of the workout plan (for example, 'Push A', 'Upper Body')" },
            exercises = new
            {
                type = "array",
                description = "List of exercises in the plan",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        name = new { type = StringType, description = "Exercise name (for example, 'Bench Press')" },
                        targetSets = new { type = "integer", description = "Number of sets (for example, 3 or 4)" },
                        targetReps = new { type = "integer", description = "Target reps per set (for example, 8 or 10)" },
                        targetWeightKg = new { type = "number", description = "Target weight in kg (optional, for example, 80.0)" },
                        restTimerSeconds = new { type = "integer", description = "Rest timer seconds (optional, for example, 90)" }
                    },
                    required = new[] { NameProperty }
                }
            }
        },
        required = new[] { NameProperty }
    };

    public async Task<string> ExecuteAsync(string argumentsJson)
    {
        using var doc = JsonDocument.Parse(argumentsJson);
        JsonElement root = doc.RootElement;

        var planName = root.GetProperty(NameProperty).GetString() ?? "New Workout Plan";

        var plan = new WorkoutPlan
        {
            Id = Guid.NewGuid(),
            Name = planName,
            Exercises = ParseExercisesJson(root)
        };

        await planService.SavePlanAsync(plan);
        return JsonSerializer.Serialize(new { success = true, planId = plan.Id, message = $"Created workout plan '{plan.Name}' with {plan.Exercises.Count} exercises." });
    }

    private static List<ExercisePlan> ParseExercisesJson(JsonElement root)
    {
        var exercises = new List<ExercisePlan>();
        if (!root.TryGetProperty("exercises", out JsonElement exArray) || exArray.ValueKind != JsonValueKind.Array)
        {
            return exercises;
        }

        var order = 0;
        foreach (JsonElement exElem in exArray.EnumerateArray())
        {
            exercises.Add(WorkoutPlanToolHelper.ParseExerciseItem(exElem, order++));
        }

        return exercises;
    }
}

public sealed class UpdateWorkoutPlanTool(WorkoutPlanService planService) : IAiTool
{
    private const string PlanIdProperty = "planId";
    private const string NameProperty = "name";
    private const string StringType = "string";

    public string Name => "update_workout_plan";
    public string Description => "Update an existing workout plan (change name or modify exercise list).";

    public object ParametersSchema => new
    {
        type = "object",
        properties = new
        {
            planId = new { type = StringType, description = "GUID ID of the plan to update" },
            name = new { type = StringType, description = "New name of the plan (optional)" },
            exercises = new
            {
                type = "array",
                description = "Updated exercises array (replaces current exercise list if provided)",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        id = new { type = StringType, description = "Existing exercise GUID ID (optional, new GUID generated if omitted)" },
                        name = new { type = StringType, description = "Exercise name" },
                        targetSets = new { type = "integer", description = "Number of sets" },
                        targetReps = new { type = "integer", description = "Target reps" },
                        targetWeightKg = new { type = "number", description = "Target weight in kg" },
                        restTimerSeconds = new { type = "integer", description = "Rest timer seconds" }
                    },
                    required = new[] { NameProperty }
                }
            }
        },
        required = new[] { PlanIdProperty }
    };

    public async Task<string> ExecuteAsync(string argumentsJson)
    {
        using var doc = JsonDocument.Parse(argumentsJson);
        JsonElement root = doc.RootElement;

        if (!root.TryGetProperty(PlanIdProperty, out JsonElement planIdProp) || !Guid.TryParse(planIdProp.GetString(), out Guid planId))
        {
            return JsonSerializer.Serialize(new { success = false, error = "Invalid or missing planId" });
        }

        WorkoutPlan? plan = await planService.GetPlanAsync(planId);
        if (plan == null)
        {
            return JsonSerializer.Serialize(new { success = false, error = $"Plan with ID {planId} not found" });
        }

        if (root.TryGetProperty(NameProperty, out JsonElement nameProp) && !string.IsNullOrWhiteSpace(nameProp.GetString()))
        {
            plan.Name = nameProp.GetString()!;
        }

        if (root.TryGetProperty("exercises", out JsonElement exArray) && exArray.ValueKind == JsonValueKind.Array)
        {
            plan.Exercises = ParseUpdatedExercises(exArray);
        }

        await planService.SavePlanAsync(plan);
        return JsonSerializer.Serialize(new { success = true, message = $"Updated workout plan '{plan.Name}'." });
    }

    private static List<ExercisePlan> ParseUpdatedExercises(JsonElement exArray)
    {
        var updatedExercises = new List<ExercisePlan>();
        var order = 0;

        foreach (JsonElement exElem in exArray.EnumerateArray())
        {
            updatedExercises.Add(WorkoutPlanToolHelper.ParseExerciseItem(exElem, order++));
        }

        return updatedExercises;
    }
}

public sealed class DeleteWorkoutPlanTool(WorkoutPlanService planService) : IAiTool
{
    private const string PlanIdProperty = "planId";
    private const string StringType = "string";

    public string Name => "delete_workout_plan";
    public string Description => "Delete a workout plan by its ID.";

    public object ParametersSchema => new
    {
        type = "object",
        properties = new
        {
            planId = new { type = StringType, description = "GUID ID of the plan to delete" }
        },
        required = new[] { PlanIdProperty }
    };

    public async Task<string> ExecuteAsync(string argumentsJson)
    {
        using var doc = JsonDocument.Parse(argumentsJson);
        JsonElement root = doc.RootElement;

        if (!root.TryGetProperty(PlanIdProperty, out JsonElement planIdProp) || !Guid.TryParse(planIdProp.GetString(), out Guid planId))
        {
            return JsonSerializer.Serialize(new { success = false, error = "Invalid planId" });
        }

        await planService.DeletePlanAsync(planId);
        return JsonSerializer.Serialize(new { success = true, message = $"Deleted workout plan with ID {planId}." });
    }
}

internal static class WorkoutPlanToolHelper
{
    public static ExercisePlan ParseExerciseItem(JsonElement exElem, int order)
    {
        var exName = exElem.GetProperty("name").GetString() ?? "Exercise";
        Guid exId = exElem.TryGetProperty("id", out JsonElement idProp) && Guid.TryParse(idProp.GetString(), out Guid g) ? g : Guid.NewGuid();
        var targetSets = exElem.TryGetProperty("targetSets", out JsonElement ts) && ts.TryGetInt32(out var tsVal) ? tsVal : 4;
        var targetReps = exElem.TryGetProperty("targetReps", out JsonElement tr) && tr.TryGetInt32(out var trVal) ? trVal : 10;
        var targetWeight = exElem.TryGetProperty("targetWeightKg", out JsonElement tw) && tw.TryGetDouble(out var twVal) ? twVal : (double?)null;
        var restSeconds = exElem.TryGetProperty("restTimerSeconds", out JsonElement rt) && rt.TryGetInt32(out var rtVal) ? rtVal : 60;

        return new ExercisePlan
        {
            Id = exId,
            Name = exName,
            SetCount = targetSets,
            DefaultReps = targetReps,
            DefaultWeightKg = targetWeight,
            RestIntervalSeconds = restSeconds,
            Order = order,
            LogType = ExerciseLogType.WeightAndReps
        };
    }
}
