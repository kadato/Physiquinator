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
}
