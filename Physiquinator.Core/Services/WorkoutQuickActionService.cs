using Physiquinator.Core.Data;
using Physiquinator.Core.Models;

namespace Physiquinator.Core.Services;

/// <summary>Status of a quick-action set log.</summary>
public enum QuickActionStatus
{
    /// <summary>No uncompleted set existed, nothing was logged.</summary>
    NothingToLog,

    /// <summary>Set logged. The workout continues.</summary>
    Logged,

    /// <summary>Set logged. It was the last set of the workout.</summary>
    WorkoutCompleted
}

/// <summary>
/// Result of <see cref="WorkoutQuickActionService.LogNextSetAsync"/> with the
/// set details needed for confirmation UI.
/// </summary>
public sealed record QuickActionResult(
    QuickActionStatus Status,
    string? ExerciseName = null,
    int? LoggedSetIndex = null,
    int? SetTotal = null);

/// <summary>
/// Logs the next set from the background surfaces (ongoing notification
/// quick actions and the floating overlay) through the same session flow as
/// the workout page: complete the set, persist the history row in the open
/// session, then start the rest countdown.
/// </summary>
public sealed class WorkoutQuickActionService(
    WorkoutSessionService session,
    WorkoutHistoryRepository history)
{
    private const int DefaultReps = 10;

    /// <summary>Logs the next uncompleted set using the exercise defaults for weight and reps.</summary>
    public Task<QuickActionResult> LogNextSetAsync() => LogNextSetAsync(null, null);

    /// <summary>Logs the next uncompleted set. Explicit weight and reps override the exercise defaults.</summary>
    public async Task<QuickActionResult> LogNextSetAsync(double? weightKg, int? reps)
    {
        WorkoutPlan? plan = session.CurrentPlan;
        if (plan == null)
            return new QuickActionResult(QuickActionStatus.NothingToLog);

        var exerciseIndex = session.GetFirstUncompletedExerciseIndex();
        if (exerciseIndex < 0 || exerciseIndex >= plan.Exercises.Count)
            return new QuickActionResult(QuickActionStatus.NothingToLog);

        ExercisePlan exercise = plan.Exercises[exerciseIndex];
        var setIndex = session.GetFirstUncompletedSetIndex(exerciseIndex);
        if (setIndex < 0)
            return new QuickActionResult(QuickActionStatus.NothingToLog);

        var duration = exercise.LogType == ExerciseLogType.Duration;
        double? loggedWeight = duration ? null : weightKg ?? exercise.DefaultWeightKg ?? 0.0;
        int? loggedReps = reps ?? exercise.DefaultReps ?? DefaultReps;

        // Completing the last set ends the workout. Stop any running rest.
        if (session.WouldCompleteWorkout(exerciseIndex, setIndex))
            session.SkipRest();

        session.CompleteSet(exerciseIndex, setIndex);

        WorkoutSessionLogEntity? open = await history.GetAnyInProgressSessionAsync();
        if (open != null)
            await history.LogSetAsync(open.Id, exerciseIndex, exercise.Name, setIndex, loggedReps, loggedWeight, isWarmup: setIndex < exercise.WarmupSetCount);

        QuickActionStatus status = session.GetFirstUncompletedExerciseIndex() == -1
            ? QuickActionStatus.WorkoutCompleted
            : QuickActionStatus.Logged;

        if (status != QuickActionStatus.WorkoutCompleted && exercise.RestIntervalSeconds > 0)
            session.StartRest(exercise.RestIntervalSeconds);

        return new QuickActionResult(status, exercise.Name, setIndex + 1, exercise.TotalSetCount);
    }

    /// <summary>
    /// Removes the most recently logged set (session state and history row)
    /// and stops any running rest so the set can be re-logged.
    /// </summary>
    public async Task UndoLastSetAsync()
    {
        if (!session.TryUndoLastSet(out _))
            return;

        session.SkipRest();

        WorkoutSessionLogEntity? open = await history.GetAnyInProgressSessionAsync();
        if (open != null)
            await history.TryDeleteLastSetLogAsync(open.Id);
    }
}
