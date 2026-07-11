using Physiquinator.Models;
using Physiquinator.Services;
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
    public void PauseRest_freezes_remaining_resume_restores_wall_clock()
    {
        var clock = new ManualTimeProvider();
        clock.SetUtcNow(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var svc = new WorkoutSessionService(clock);
        svc.StartWorkout(SamplePlan());
        svc.StartRest(60);

        clock.Advance(TimeSpan.FromSeconds(20));
        Assert.Equal(40, svc.RestSecondsRemaining);

        svc.PauseRest();
        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(40, svc.RestSecondsRemaining);

        Assert.False(svc.ResumeRest());
        Assert.True(svc.RestEndsAtUtc.HasValue);
        Assert.Equal(40, svc.RestSecondsRemaining);

        clock.Advance(TimeSpan.FromSeconds(40));
        Assert.True(svc.TickRest());
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
        var plan = SamplePlan();
        var completed = new[] { new SetCompletion(0, 0), new SetCompletion(0, 1) };

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

        Assert.True(svc.TryUndoLastSet(out var removed));
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
}
