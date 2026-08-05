namespace Physiquinator.Core.Models;

/// <summary>
/// Snapshot of the active workout pushed to platform surfaces (ongoing
/// notification, floating overlay) whenever it changes. Rest fields describe
/// the running countdown when resting; the next-exercise fields describe the
/// upcoming set so quick actions can log it from the background.
/// </summary>
public sealed record WorkoutTimerState(
    string? PlanName,
    DateTime? RestEndsAtUtc,
    int RestRemainingSeconds,
    string? NextExerciseName,
    int? NextExerciseIndex,
    int? NextSetIndex,
    int? NextSetTotal);
