using Physiquinator.Core.Data;
using Physiquinator.Core.Models;
using System.Text.Json;

namespace Physiquinator.Core.Services.Ai.Tools;

public sealed class GenerateDeloadPlanWorkflowTool(WorkoutPlanService planService) : IAiTool
{
    public string Name => "generate_deload_plan";
    public string Description => "Pre-packaged workflow: Generate a deload version of an existing plan with 50% reduced set volume and slightly lighter target weights.";

    public object ParametersSchema => new
    {
        type = "object",
        properties = new
        {
            planId = new { type = "string", description = "GUID ID of the base plan to generate deload for" }
        },
        required = new[] { "planId" }
    };

    public async Task<string> ExecuteAsync(string argumentsJson)
    {
        using var doc = JsonDocument.Parse(argumentsJson);
        JsonElement root = doc.RootElement;

        if (!root.TryGetProperty("planId", out JsonElement planIdProp) || !Guid.TryParse(planIdProp.GetString(), out Guid planId))
        {
            return JsonSerializer.Serialize(new { success = false, error = "Invalid planId" });
        }

        WorkoutPlan? basePlan = await planService.GetPlanAsync(planId);
        if (basePlan == null)
        {
            return JsonSerializer.Serialize(new { success = false, error = "Base plan not found" });
        }

        var deloadPlan = new WorkoutPlan
        {
            Id = Guid.NewGuid(),
            Name = $"{basePlan.Name} (Deload Week)",
            Exercises = [.. basePlan.Exercises.Select(e => new ExercisePlan
            {
                Id = Guid.NewGuid(),
                Name = e.Name,
                SetCount = Math.Max(2, (int)Math.Ceiling(e.SetCount * 0.5)),
                DefaultReps = e.DefaultReps,
                DefaultWeightKg = e.DefaultWeightKg.HasValue ? Math.Round(e.DefaultWeightKg.Value * 0.9, 1) : null,
                RestIntervalSeconds = Math.Max(60, e.RestIntervalSeconds - 15),
                Order = e.Order,
                LogType = e.LogType
            })]
        };

        await planService.SavePlanAsync(deloadPlan);
        return JsonSerializer.Serialize(new
        {
            success = true,
            deloadPlanId = deloadPlan.Id,
            message = $"Created deload plan '{deloadPlan.Name}' with {deloadPlan.Exercises.Count} exercises."
        });
    }
}

public sealed class CalculateProgressiveOverloadWorkflowTool(WorkoutHistoryRepository repository, WorkoutPlanService planService) : IAiTool
{
    public string Name => "calculate_progressive_overload";
    public string Description => "Pre-packaged workflow: Analyzes recent completed session performance and calculates recommended progressive overload target weights/reps per exercise.";

    public object ParametersSchema => new
    {
        type = "object",
        properties = new { },
        required = Array.Empty<string>()
    };

    public async Task<string> ExecuteAsync(string argumentsJson)
    {
        List<WorkoutPlan> plans = await planService.GetAllPlansAsync();
        var recommendations = new List<object>();

        foreach (WorkoutPlan plan in plans)
        {
            foreach (ExercisePlan exercise in plan.Exercises)
            {
                IReadOnlyList<ExerciseSessionProgressEntry> progressList = await repository.GetExerciseSessionProgressAsync(plan.Id, exercise.Name);
                if (progressList.Count == 0) continue;

                ExerciseSessionProgressEntry latestSession = progressList[0];
                var currentBestWeight = latestSession.BestWeightKg;
                var currentReps = latestSession.TotalReps;

                var recommendedWeight = currentBestWeight.HasValue
                    ? Math.Round(currentBestWeight.Value * 1.025, 1) // +2.5% increment
                    : exercise.DefaultWeightKg;

                recommendations.Add(new
                {
                    planName = plan.Name,
                    exerciseName = exercise.Name,
                    lastLoggedWeightKg = currentBestWeight,
                    lastLoggedTotalReps = currentReps,
                    targetRepsConfigured = exercise.DefaultReps,
                    recommendedWeightKg = recommendedWeight,
                    recommendationReason = currentBestWeight.HasValue
                        ? $"Increase weight by +2.5% from {currentBestWeight.Value}kg to {recommendedWeight}kg for next session."
                        : "Focus on adding 1 extra rep per set before increasing weight."
                });
            }
        }

        return JsonSerializer.Serialize(new
        {
            success = true,
            totalAnalyzed = recommendations.Count,
            recommendations
        });
    }
}
