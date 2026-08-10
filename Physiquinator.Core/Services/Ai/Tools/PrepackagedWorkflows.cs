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
                BodyweightPercent = e.BodyweightPercent,
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
            IReadOnlyList<ExerciseProgressRow> progressRows = await repository.GetExercisesSessionProgressAsync(plan.Id);
            var latestByExercise = progressRows
                .GroupBy(r => r.ExerciseName)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (ExercisePlan exercise in plan.Exercises)
            {
                if (!latestByExercise.TryGetValue(exercise.Name, out ExerciseProgressRow? latestSession) || latestSession is null) continue;

                var recommendedWeight = latestSession.BestWeightKg.HasValue
                    ? Math.Round(latestSession.BestWeightKg.Value * 1.025, 1)
                    : exercise.DefaultWeightKg;

                recommendations.Add(new
                {
                    planName = plan.Name,
                    exerciseName = exercise.Name,
                    lastLoggedWeightKg = latestSession.BestWeightKg,
                    lastLoggedTotalReps = latestSession.TotalReps,
                    targetRepsConfigured = exercise.DefaultReps,
                    recommendedWeightKg = recommendedWeight,
                    recommendationReason = latestSession.BestWeightKg.HasValue
                        ? $"Increase weight by +2.5% from {latestSession.BestWeightKg.Value}kg to {recommendedWeight}kg for next session."
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
