using Physiquinator.Core.Data;
using Physiquinator.Core.Models;
using Physiquinator.Core.Services;
using Xunit;

namespace Physiquinator.Tests.Services;

/// <summary>
/// Guards background set logging from the notification quick actions and the
/// floating overlay: defaults, explicit metrics, duration exercises, the
/// last-set completion and undo.
/// </summary>
public class WorkoutQuickActionServiceTests : IAsyncLifetime
{
    private AppDatabase _db = null!;
    private WorkoutHistoryRepository _history = null!;

    static WorkoutQuickActionServiceTests() => SQLitePCL.Batteries_V2.Init();

    public async Task InitializeAsync()
    {
        _db = new AppDatabase(":memory:");
        await _db.EnsureInitializedAsync();
        _history = new WorkoutHistoryRepository(_db, TimeProvider.System);
    }

    public async Task DisposeAsync()
    {
        await _db.Database.CloseAsync();
    }

    private async Task<(WorkoutSessionService Session, WorkoutQuickActionService Actions, string SessionId)> BuildAsync(WorkoutPlan plan)
    {
        var session = new WorkoutSessionService(TimeProvider.System);
        session.StartWorkout(plan);
        var sessionId = await _history.BeginSessionAsync(plan.Id, plan.Name);
        return (session, new WorkoutQuickActionService(session, _history), sessionId);
    }

    private static WorkoutPlan SamplePlan() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Push",
        Exercises =
        [
            new ExercisePlan { Name = "Bench press", SetCount = 3, Order = 0, RestIntervalSeconds = 90, DefaultWeightKg = 60, DefaultReps = 8 },
            new ExercisePlan { Name = "Dips", SetCount = 2, Order = 1, RestIntervalSeconds = 60 }
        ]
    };

    [Fact]
    public async Task LogNextSetAsync_logs_first_set_with_exercise_defaults_and_starts_rest()
    {
        (WorkoutSessionService? session, WorkoutQuickActionService? actions, var sessionId) = await BuildAsync(SamplePlan());

        QuickActionResult result = await actions.LogNextSetAsync();

        Assert.Equal(QuickActionStatus.Logged, result.Status);
        Assert.Equal("Bench press", result.ExerciseName);
        Assert.Equal(1, result.LoggedSetIndex);
        Assert.Equal(3, result.SetTotal);

        Assert.True(session.IsSetCompleted(0, 0));
        Assert.True(session.IsResting);
        Assert.Equal(90, session.ActiveRestDurationSeconds);

        IReadOnlyList<WorkoutSetLogEntity> logged = await _history.GetSetsForSessionAsync(sessionId);
        WorkoutSetLogEntity set = Assert.Single(logged);
        Assert.Equal(0, set.ExerciseIndex);
        Assert.Equal("Bench press", set.ExerciseName);
        Assert.Equal(8, set.Reps);
        Assert.Equal(60, set.WeightKg);
    }

    [Fact]
    public async Task LogNextSetAsync_uses_explicit_metrics_when_provided()
    {
        (WorkoutSessionService _, WorkoutQuickActionService? actions, var sessionId) = await BuildAsync(SamplePlan());

        QuickActionResult result = await actions.LogNextSetAsync(72.5, 6);

        Assert.Equal(QuickActionStatus.Logged, result.Status);

        IReadOnlyList<WorkoutSetLogEntity> logged = await _history.GetSetsForSessionAsync(sessionId);
        WorkoutSetLogEntity set = Assert.Single(logged);
        Assert.Equal(6, set.Reps);
        Assert.Equal(72.5, set.WeightKg);
    }

    [Fact]
    public async Task LogNextSetAsync_advances_exercise_and_reports_one_based_set_index()
    {
        (WorkoutSessionService _, WorkoutQuickActionService? actions, var _) = await BuildAsync(SamplePlan());

        await actions.LogNextSetAsync();
        await actions.LogNextSetAsync();
        QuickActionResult result = await actions.LogNextSetAsync();

        Assert.Equal(QuickActionStatus.Logged, result.Status);
        Assert.Equal("Bench press", result.ExerciseName);
        Assert.Equal(3, result.LoggedSetIndex);
        Assert.Equal(3, result.SetTotal);
    }

    [Fact]
    public async Task LogNextSetAsync_moves_to_next_exercise_when_current_is_done()
    {
        (WorkoutSessionService _, WorkoutQuickActionService? actions, var _) = await BuildAsync(SamplePlan());

        for (var i = 0; i < 3; i++)
            await actions.LogNextSetAsync();

        QuickActionResult result = await actions.LogNextSetAsync();

        Assert.Equal(QuickActionStatus.Logged, result.Status);
        Assert.Equal("Dips", result.ExerciseName);
        Assert.Equal(1, result.LoggedSetIndex);
        Assert.Equal(2, result.SetTotal);
    }

    [Fact]
    public async Task LogNextSetAsync_returns_WorkoutCompleted_and_skips_rest_on_last_set()
    {
        (WorkoutSessionService? session, WorkoutQuickActionService? actions, var _) = await BuildAsync(SamplePlan());

        QuickActionResult result = new(QuickActionStatus.Logged);
        for (var i = 0; i < 5; i++)
            result = await actions.LogNextSetAsync();

        Assert.Equal(QuickActionStatus.WorkoutCompleted, result.Status);
        Assert.Equal("Dips", result.ExerciseName);
        Assert.Equal(2, result.LoggedSetIndex);
        Assert.False(session.IsResting);
    }

    [Fact]
    public async Task LogNextSetAsync_returns_NothingToLog_when_workout_is_done()
    {
        (WorkoutSessionService _, WorkoutQuickActionService? actions, var _) = await BuildAsync(SamplePlan());

        for (var i = 0; i < 5; i++)
            await actions.LogNextSetAsync();

        QuickActionResult result = await actions.LogNextSetAsync();

        Assert.Equal(QuickActionStatus.NothingToLog, result.Status);
        Assert.Null(result.ExerciseName);
    }

    [Fact]
    public async Task LogNextSetAsync_duration_exercise_logs_without_weight()
    {
        var plan = new WorkoutPlan
        {
            Id = Guid.NewGuid(),
            Name = "Mobility",
            Exercises =
            [
                new ExercisePlan { Name = "Plank", SetCount = 2, Order = 0, RestIntervalSeconds = 45, LogType = ExerciseLogType.Duration, DefaultReps = 3 }
            ]
        };
        (WorkoutSessionService _, WorkoutQuickActionService? actions, var sessionId) = await BuildAsync(plan);

        QuickActionResult result = await actions.LogNextSetAsync();

        Assert.Equal(QuickActionStatus.Logged, result.Status);

        IReadOnlyList<WorkoutSetLogEntity> logged = await _history.GetSetsForSessionAsync(sessionId);
        WorkoutSetLogEntity set = Assert.Single(logged);
        Assert.Null(set.WeightKg);
        Assert.Equal(3, set.Reps);
    }

    [Fact]
    public async Task UndoLastSetAsync_removes_set_and_history_row_and_stops_rest()
    {
        (WorkoutSessionService? session, WorkoutQuickActionService? actions, var sessionId) = await BuildAsync(SamplePlan());
        await actions.LogNextSetAsync();
        Assert.True(session.IsResting);

        await actions.UndoLastSetAsync();

        Assert.False(session.IsSetCompleted(0, 0));
        Assert.False(session.IsResting);
        Assert.Empty(await _history.GetSetsForSessionAsync(sessionId));
    }

    [Fact]
    public async Task UndoLastSetAsync_is_noop_without_sets()
    {
        (WorkoutSessionService? session, WorkoutQuickActionService? actions, var _) = await BuildAsync(SamplePlan());

        await actions.UndoLastSetAsync();

        Assert.Empty(session.CompletedSets);
    }
}
