using Physiquinator.Core.Data;
using System.Text.Json;

namespace Physiquinator.Core.Services.Ai.Tools;

public sealed class GetWorkoutHistoryStatsTool(WorkoutHistoryRepository repository) : IAiTool
{
    public string Name => "get_workout_history_stats";
    public string Description => "Get workout session history summary, total session count, and recent session logs.";

    public object ParametersSchema => new
    {
        type = "object",
        properties = new
        {
            recentLimit = new { type = "integer", description = "Number of recent sessions to retrieve (default 10)" }
        },
        required = Array.Empty<string>()
    };

    public async Task<string> ExecuteAsync(string argumentsJson)
    {
        var limit = 10;
        if (!string.IsNullOrWhiteSpace(argumentsJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(argumentsJson);
                if (doc.RootElement.TryGetProperty("recentLimit", out JsonElement limProp) && limProp.TryGetInt32(out var lVal))
                {
                    limit = lVal;
                }
            }
            catch { /* default fallback */ }
        }

        var totalSessions = await repository.GetSessionCountAsync();
        IReadOnlyList<WorkoutSessionLogEntity> recentSessions = await repository.GetRecentSessionsAsync(limit);

        var result = new
        {
            totalWorkoutsCompleted = totalSessions,
            recentSessions = recentSessions.Select(s => new
            {
                sessionId = s.Id,
                planName = s.PlanName,
                startedAtUtc = s.StartedAtUtc,
                endedAtUtc = s.EndedAtUtc,
                durationMinutes = s.EndedAtUtc.HasValue ? Math.Round((s.EndedAtUtc.Value - s.StartedAtUtc).TotalMinutes, 1) : (double?)null
            })
        };

        return JsonSerializer.Serialize(result);
    }
}

public sealed class GetExerciseProgressionTool(WorkoutHistoryRepository repository) : IAiTool
{
    public string Name => "get_exercise_progression";
    public string Description => "Get session-by-session progression metrics for a specific exercise (volume, best weight, total reps per session).";

    public object ParametersSchema => new
    {
        type = "object",
        properties = new
        {
            exerciseName = new { type = "string", description = "Name of the exercise (e.g. 'Bench Press', 'Squat')" }
        },
        required = new[] { "exerciseName" }
    };

    public async Task<string> ExecuteAsync(string argumentsJson)
    {
        using var doc = JsonDocument.Parse(argumentsJson);
        JsonElement root = doc.RootElement;

        if (!root.TryGetProperty("exerciseName", out JsonElement exProp) || string.IsNullOrWhiteSpace(exProp.GetString()))
        {
            return JsonSerializer.Serialize(new { success = false, error = "Missing exerciseName parameter" });
        }

        var exerciseName = exProp.GetString()!;
        IReadOnlyList<ExerciseSessionProgressEntry> allProgress = await repository.GetExerciseSessionProgressAcrossPlansAsync(exerciseName);

        var result = new
        {
            exerciseName,
            totalSessions = allProgress.Count,
            sessions = allProgress.Select(p => new
            {
                p.SessionId,
                p.StartedAtUtc,
                p.BestWeightKg,
                p.TotalReps,
                p.SetCount,
                p.TotalVolumeKg
            })
        };

        return JsonSerializer.Serialize(result);
    }
}
