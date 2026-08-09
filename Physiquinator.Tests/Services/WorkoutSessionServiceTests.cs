using Physiquinator.Core.Models;
using Physiquinator.Core.Services;
using Xunit;

namespace Physiquinator.Tests.Services;

public class WorkoutSessionServiceTests
{
    private static WorkoutPlan SamplePlan() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test",
        Exercises =
        [
            new ExercisePlan { Name = "Squat", SetCount = 2, Order = 0, RestIntervalSeconds = 60 }
        ]
    };

    [Fact]
    public void GetFirstUncompletedSetIndex_CountsWarmupSetsInFront()
    {
        var svc = new WorkoutSessionService(new ManualTimeProvider());
        svc.StartWorkout(new WorkoutPlan
        {
            Name = "Warm",
            Exercises =
            [
                new ExercisePlan { Name = "Bench", SetCount = 3, WarmupSetCount = 2, Order = 0 }
            ]
        });

        // Total set span is 0..4 (2 warm-ups + 3 working).
        Assert.Equal(0, svc.GetFirstUncompletedSetIndex(0));

        svc.CompleteSet(0, 0);
        svc.CompleteSet(0, 1);
        Assert.Equal(2, svc.GetFirstUncompletedSetIndex(0));
        Assert.False(svc.IsExerciseDone(0));

        svc.CompleteSet(0, 2);
        svc.CompleteSet(0, 3);
        svc.CompleteSet(0, 4);
        Assert.True(svc.IsExerciseDone(0));
        Assert.Equal(-1, svc.GetFirstUncompletedSetIndex(0));
    }

    [Fact]
    public void WouldCompleteWorkout_CountsWarmupSetsInFront()
    {
        var svc = new WorkoutSessionService(new ManualTimeProvider());
        svc.StartWorkout(new WorkoutPlan
        {
            Name = "Warm",
            Exercises =
            [
                new ExercisePlan { Name = "Bench", SetCount = 1, WarmupSetCount = 1, Order = 0 }
            ]
        });

        Assert.False(svc.WouldCompleteWorkout(0, 0));
        svc.CompleteSet(0, 0);
        Assert.True(svc.WouldCompleteWorkout(0, 1));
    }

    [Fact]
    public void StartRest_sets_remaining_from_wall_clock()
    {
        var clock = new ManualTimeProvider();
        clock.SetUtcNow(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var svc = new WorkoutSessionService(clock);
        svc.StartWorkout(SamplePlan());

        svc.StartRest(90);

        Assert.Equal(90, svc.RestSecondsRemaining);
        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(60, svc.RestSecondsRemaining);
    }

    [Fact]
    public void TickRest_returns_true_when_wall_clock_passed_end()
    {
        var clock = new ManualTimeProvider();
        clock.SetUtcNow(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var svc = new WorkoutSessionService(clock);
        svc.StartWorkout(SamplePlan());
        svc.StartRest(5);

        Assert.False(svc.TickRest());
        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.True(svc.TickRest());
        Assert.False(svc.IsResting);
    }

    [Fact]
    public void TryCompleteRestIfExpired_true_after_background_elapsed()
    {
        var clock = new ManualTimeProvider();
        clock.SetUtcNow(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var svc = new WorkoutSessionService(clock);
        svc.StartWorkout(SamplePlan());
        svc.StartRest(10);

        clock.Advance(TimeSpan.FromSeconds(11));
        Assert.True(svc.TryCompleteRestIfExpired());
        Assert.False(svc.IsResting);
    }

    [Fact]
    public void ResetRest_restarts_full_duration()
    {
        var clock = new ManualTimeProvider();
        clock.SetUtcNow(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var svc = new WorkoutSessionService(clock);
        svc.StartWorkout(SamplePlan());
        svc.StartRest(60);
        clock.Advance(TimeSpan.FromSeconds(45));
        Assert.Equal(15, svc.RestSecondsRemaining);
        svc.ResetRest();
        Assert.Equal(60, svc.RestSecondsRemaining);
    }

    [Fact]
    public void AddRestSeconds_extends_running_rest()
    {
        var clock = new ManualTimeProvider();
        clock.SetUtcNow(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var svc = new WorkoutSessionService(clock);
        svc.StartWorkout(SamplePlan());
        svc.StartRest(60);
        clock.Advance(TimeSpan.FromSeconds(20));
        Assert.Equal(40, svc.RestSecondsRemaining);

        svc.AddRestSeconds(30);
        Assert.Equal(70, svc.RestSecondsRemaining);

        clock.Advance(TimeSpan.FromSeconds(70));
        Assert.True(svc.TickRest());
    }

    [Fact]
    public void AddRestSeconds_is_noop_when_not_resting()
    {
        var clock = new ManualTimeProvider();
        var svc = new WorkoutSessionService(clock);
        svc.StartWorkout(SamplePlan());
        svc.AddRestSeconds(30);
        Assert.False(svc.IsResting);
        Assert.Equal(0, svc.RestSecondsRemaining);
    }

    [Fact]
    public void CompleteSet_ignores_invalid_indices()
    {
        var clock = new ManualTimeProvider();
        var svc = new WorkoutSessionService(clock);
        svc.StartWorkout(SamplePlan());
        svc.CompleteSet(-1, 0);
        svc.CompleteSet(99, 0);
        svc.CompleteSet(0, 99);
        Assert.Empty(svc.CompletedSets);
    }

    [Fact]
    public void ResumeWorkout_restores_completed_sets()
    {
        var clock = new ManualTimeProvider();
        var svc = new WorkoutSessionService(clock);
        WorkoutPlan plan = SamplePlan();
        SetCompletion[] completed = [new SetCompletion(0, 0), new SetCompletion(0, 1)];

        svc.ResumeWorkout(plan, completed);

        Assert.Same(plan, svc.CurrentPlan);
        Assert.Equal(2, svc.CompletedSets.Count);
        Assert.True(svc.IsSetCompleted(0, 0));
        Assert.True(svc.IsSetCompleted(0, 1));
        Assert.False(svc.IsResting);
    }

    [Fact]
    public void ActiveRestDurationSeconds_tracks_start_rest()
    {
        var clock = new ManualTimeProvider();
        var svc = new WorkoutSessionService(clock);
        svc.StartWorkout(SamplePlan());
        Assert.Equal(0, svc.ActiveRestDurationSeconds);

        svc.StartRest(45);
        Assert.Equal(45, svc.ActiveRestDurationSeconds);

        svc.SkipRest();
        Assert.Equal(0, svc.ActiveRestDurationSeconds);
    }

    [Fact]
    public void TryUndoLastSet_removes_last_completion()
    {
        var clock = new ManualTimeProvider();
        var svc = new WorkoutSessionService(clock);
        svc.StartWorkout(SamplePlan());
        svc.CompleteSet(0, 0);
        svc.CompleteSet(0, 1);

        Assert.True(svc.TryUndoLastSet(out SetCompletion? removed));
        Assert.Equal(new SetCompletion(0, 1), removed);
        Assert.Single(svc.CompletedSets);

        Assert.True(svc.TryUndoLastSet(out removed));
        Assert.Equal(new SetCompletion(0, 0), removed);
        Assert.False(svc.TryUndoLastSet(out _));
    }

    [Fact]
    public void WouldCompleteWorkout_tracks_completion_correctly_out_of_order()
    {
        var plan = new WorkoutPlan
        {
            Id = Guid.NewGuid(),
            Name = "Multi-Exercise Plan",
            Exercises =
            [
                new ExercisePlan { Name = "Squat", SetCount = 2, Order = 0 },
                new ExercisePlan { Name = "Bench Press", SetCount = 1, Order = 1 }
            ]
        };

        var clock = new ManualTimeProvider();
        var svc = new WorkoutSessionService(clock);
        svc.StartWorkout(plan);

        // Squat: 2 sets, Bench: 1 set.
        // Initially, completing Squat set 0 should not complete the workout.
        Assert.False(svc.WouldCompleteWorkout(0, 0));
        svc.CompleteSet(0, 0);

        // Completing Squat set 1 (last set of first exercise) should not complete the workout because Bench set 0 is not completed.
        Assert.False(svc.WouldCompleteWorkout(0, 1));

        // If we do it out of order:
        // Let's complete Bench set 0.
        // Completing Bench set 0 should not complete the workout because Squat set 1 is still incomplete.
        Assert.False(svc.WouldCompleteWorkout(1, 0));
        svc.CompleteSet(1, 0);

        // Now Squat set 0 and Bench set 0 are completed.
        // Completing Squat set 1 (the remaining incomplete set) should complete the workout!
        Assert.True(svc.WouldCompleteWorkout(0, 1));
    }

    [Fact]
    public void GetFirstUncompletedSetIndex_returns_first_open_set()
    {
        var plan = new WorkoutPlan
        {
            Exercises =
            [
                new ExercisePlan { Name = "Squat", SetCount = 3, Order = 0 }
            ]
        };
        var svc = new WorkoutSessionService(new ManualTimeProvider());
        svc.StartWorkout(plan);

        Assert.Equal(0, svc.GetFirstUncompletedSetIndex(0));

        svc.CompleteSet(0, 0);
        svc.CompleteSet(0, 2);
        Assert.Equal(1, svc.GetFirstUncompletedSetIndex(0));

        svc.CompleteSet(0, 1);
        Assert.Equal(-1, svc.GetFirstUncompletedSetIndex(0));
    }

    [Fact]
    public void IsExerciseDone_and_first_uncompleted_exercise_track_progress()
    {
        var plan = new WorkoutPlan
        {
            Exercises =
            [
                new ExercisePlan { Name = "Squat", SetCount = 2, Order = 0 },
                new ExercisePlan { Name = "Bench Press", SetCount = 1, Order = 1 }
            ]
        };
        var svc = new WorkoutSessionService(new ManualTimeProvider());
        svc.StartWorkout(plan);

        Assert.False(svc.IsExerciseDone(0));
        Assert.Equal(0, svc.GetFirstUncompletedExerciseIndex());

        svc.CompleteSet(0, 0);
        svc.CompleteSet(0, 1);
        Assert.True(svc.IsExerciseDone(0));
        Assert.False(svc.IsExerciseDone(1));
        Assert.Equal(1, svc.GetFirstUncompletedExerciseIndex());

        svc.CompleteSet(1, 0);
        Assert.True(svc.IsExerciseDone(1));
        Assert.Equal(-1, svc.GetFirstUncompletedExerciseIndex());
    }

    [Fact]
    public void Completion_helpers_return_safe_defaults_without_plan()
    {
        var svc = new WorkoutSessionService(new ManualTimeProvider());

        Assert.Equal(-1, svc.GetFirstUncompletedSetIndex(0));
        Assert.True(svc.IsExerciseDone(0));
        Assert.Equal(-1, svc.GetFirstUncompletedExerciseIndex());
    }

    [Fact]
    public void RestoreRestState_restores_wall_clock_countdown()
    {
        var clock = new ManualTimeProvider();
        clock.SetUtcNow(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var svc = new WorkoutSessionService(clock);
        svc.StartWorkout(SamplePlan());

        Assert.True(svc.RestoreRestState(clock.GetUtcNow().UtcDateTime.AddSeconds(50), 90));
        Assert.True(svc.IsResting);
        Assert.Equal(50, svc.RestSecondsRemaining);
        Assert.Equal(90, svc.ActiveRestDurationSeconds);

        clock.Advance(TimeSpan.FromSeconds(50));
        Assert.True(svc.TickRest());
    }

    [Fact]
    public void RestoreRestState_rejects_expired_or_empty_snapshot()
    {
        var clock = new ManualTimeProvider();
        clock.SetUtcNow(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var svc = new WorkoutSessionService(clock);

        Assert.False(svc.RestoreRestState(clock.GetUtcNow().UtcDateTime.AddSeconds(-1), 90));
        Assert.False(svc.RestoreRestState(clock.GetUtcNow().UtcDateTime.AddSeconds(10), 0));
        Assert.False(svc.IsResting);
    }

    [Fact]
    public void RestStateChanged_fires_on_material_rest_changes_only()
    {
        var clock = new ManualTimeProvider();
        clock.SetUtcNow(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var svc = new WorkoutSessionService(clock);
        svc.StartWorkout(SamplePlan());

        var fired = 0;
        svc.RestStateChanged += (_, _) => fired++;

        svc.StartRest(60);
        Assert.Equal(1, fired);

        // A steady countdown must not fire per tick (the UI countdown is
        // JS-driven; the coordinator must not re-run platform work).
        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.False(svc.TickRest());
        Assert.Equal(1, fired);

        svc.AddRestSeconds(30);
        Assert.Equal(2, fired);

        // A reset that lands on the same wall-clock end time as the extended
        // rest is not a material change (same alarm, same snapshot, same
        // remaining), so it stays silent.
        svc.ResetRest();
        Assert.Equal(2, fired);

        clock.Advance(TimeSpan.FromSeconds(10));
        svc.ResetRest();
        Assert.Equal(3, fired);

        svc.SkipRest();
        Assert.Equal(4, fired);

        // No-op mutations must stay silent.
        svc.CancelRest();
        svc.SkipRest();
        Assert.Equal(4, fired);

        // Workout load must not tear down a persisted rest countdown.
        svc.StartWorkout(SamplePlan());
        Assert.Equal(4, fired);

        svc.ResumeWorkout(SamplePlan(), []);
        Assert.Equal(4, fired);

        // Ending a workout that already stopped is not a rest change.
        svc.EndWorkout();
        Assert.Equal(4, fired);

        // Ending a workout while resting is.
        svc.StartWorkout(SamplePlan());
        svc.StartRest(60);
        Assert.Equal(5, fired);
        svc.EndWorkout();
        Assert.Equal(6, fired);
    }

    [Fact]
    public void WorkoutStateChanged_fires_on_workout_load_and_end()
    {
        var clock = new ManualTimeProvider();
        var svc = new WorkoutSessionService(clock);

        var fired = 0;
        svc.WorkoutStateChanged += (_, _) => fired++;

        svc.StartWorkout(SamplePlan());
        Assert.Equal(1, fired);

        svc.ResumeWorkout(SamplePlan(), []);
        Assert.Equal(2, fired);

        svc.EndWorkout();
        Assert.Equal(3, fired);
    }

    [Fact]
    public void RestoreRestState_fires_RestStateChanged()
    {
        var clock = new ManualTimeProvider();
        clock.SetUtcNow(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var svc = new WorkoutSessionService(clock);

        var fired = 0;
        svc.RestStateChanged += (_, _) => fired++;

        Assert.True(svc.RestoreRestState(clock.GetUtcNow().UtcDateTime.AddSeconds(30), 60));
        Assert.Equal(1, fired);

        Assert.False(svc.RestoreRestState(clock.GetUtcNow().UtcDateTime.AddSeconds(-1), 60));
        Assert.Equal(1, fired);
    }

    [Fact]
    public void RemapExerciseIndexes_moves_completed_sets_between_exercises()
    {
        var plan = new WorkoutPlan
        {
            Exercises =
            [
                new ExercisePlan { Name = "Squat", SetCount = 2, Order = 0 },
                new ExercisePlan { Name = "Bench Press", SetCount = 2, Order = 1 }
            ]
        };
        var svc = new WorkoutSessionService(new ManualTimeProvider());
        svc.StartWorkout(plan);
        svc.CompleteSet(0, 0);
        svc.CompleteSet(1, 1);

        // Swap the two exercises: old 0 -> 1, old 1 -> 0.
        svc.RemapExerciseIndexes(new Dictionary<int, int> { [0] = 1, [1] = 0 });

        Assert.False(svc.IsSetCompleted(0, 0));
        Assert.True(svc.IsSetCompleted(1, 0));
        Assert.True(svc.IsSetCompleted(0, 1));
        Assert.False(svc.IsSetCompleted(1, 1));
        Assert.Equal(2, svc.CompletedSets.Count);
        Assert.Contains(new SetCompletion(1, 0), svc.CompletedSets);
        Assert.Contains(new SetCompletion(0, 1), svc.CompletedSets);
    }

    [Fact]
    public void RemapExerciseIndexes_is_noop_for_empty_or_identity_mapping()
    {
        var plan = new WorkoutPlan
        {
            Exercises =
            [
                new ExercisePlan { Name = "Squat", SetCount = 2, Order = 0 },
                new ExercisePlan { Name = "Bench Press", SetCount = 1, Order = 1 }
            ]
        };
        var svc = new WorkoutSessionService(new ManualTimeProvider());
        svc.StartWorkout(plan);
        svc.CompleteSet(0, 0);
        svc.CompleteSet(1, 0);

        svc.RemapExerciseIndexes(new Dictionary<int, int>());
        Assert.True(svc.IsSetCompleted(0, 0));
        Assert.True(svc.IsSetCompleted(1, 0));

        svc.RemapExerciseIndexes(new Dictionary<int, int> { [0] = 0, [1] = 1 });
        Assert.True(svc.IsSetCompleted(0, 0));
        Assert.True(svc.IsSetCompleted(1, 0));
    }
}
