using Physiquinator.Models;
using Physiquinator.Services;
using Xunit;

namespace Physiquinator.Tests.Services;

public class WorkoutSessionServiceTests : IDisposable
{
    private readonly WorkoutSessionService _sut = new();

    public void Dispose() => _sut.Dispose();

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────

    private static WorkoutPlan MakePlan(int exerciseCount = 1, int setsPerExercise = 3) => new()
    {
        Exercises = Enumerable.Range(0, exerciseCount)
            .Select(i => new ExercisePlan { Name = $"Ex{i}", SetCount = setsPerExercise })
            .ToList()
    };

    // ──────────────────────────────────────────────────────────────
    // StartWorkout
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void StartWorkout_SetsPlan()
    {
        var plan = MakePlan();

        _sut.StartWorkout(plan);

        Assert.Equal(plan, _sut.CurrentPlan);
    }

    [Fact]
    public void StartWorkout_ClearsCompletedSets()
    {
        var plan = MakePlan();
        _sut.StartWorkout(plan);
        _sut.CompleteSet(0, 0);

        _sut.StartWorkout(plan);

        Assert.Empty(_sut.CompletedSets);
    }

    [Fact]
    public void StartWorkout_StopsRestTimer()
    {
        var plan = MakePlan();
        _sut.StartWorkout(plan);
        _sut.StartRest(30, () => { }, () => { });

        _sut.StartWorkout(plan);

        Assert.False(_sut.IsResting);
    }

    // ──────────────────────────────────────────────────────────────
    // EndWorkout
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void EndWorkout_ClearsPlan()
    {
        _sut.StartWorkout(MakePlan());

        _sut.EndWorkout();

        Assert.Null(_sut.CurrentPlan);
    }

    [Fact]
    public void EndWorkout_ClearsCompletedSets()
    {
        var plan = MakePlan();
        _sut.StartWorkout(plan);
        _sut.CompleteSet(0, 0);

        _sut.EndWorkout();

        Assert.Empty(_sut.CompletedSets);
    }

    [Fact]
    public void EndWorkout_StopsRestTimer()
    {
        var plan = MakePlan();
        _sut.StartWorkout(plan);
        _sut.StartRest(30, () => { }, () => { });

        _sut.EndWorkout();

        Assert.False(_sut.IsResting);
    }

    // ──────────────────────────────────────────────────────────────
    // IsSetCompleted
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void IsSetCompleted_ReturnsFalse_WhenSetNotCompleted()
    {
        _sut.StartWorkout(MakePlan());

        Assert.False(_sut.IsSetCompleted(0, 0));
    }

    [Fact]
    public void IsSetCompleted_ReturnsTrue_AfterSetCompleted()
    {
        _sut.StartWorkout(MakePlan());
        _sut.CompleteSet(0, 0);

        Assert.True(_sut.IsSetCompleted(0, 0));
    }

    [Fact]
    public void IsSetCompleted_ReturnsFalse_ForDifferentSet()
    {
        _sut.StartWorkout(MakePlan(setsPerExercise: 3));
        _sut.CompleteSet(0, 0);

        Assert.False(_sut.IsSetCompleted(0, 1));
    }

    // ──────────────────────────────────────────────────────────────
    // CompleteSet
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void CompleteSet_AddsToCompletedSets()
    {
        _sut.StartWorkout(MakePlan(setsPerExercise: 3));

        _sut.CompleteSet(0, 1);

        Assert.Contains(new SetCompletion(0, 1), _sut.CompletedSets);
    }

    [Fact]
    public void CompleteSet_DoesNothing_WhenNoPlan()
    {
        _sut.CompleteSet(0, 0);

        Assert.Empty(_sut.CompletedSets);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(5, 0)]
    public void CompleteSet_DoesNothing_WhenExerciseIndexOutOfRange(int exerciseIndex, int setIndex)
    {
        _sut.StartWorkout(MakePlan(exerciseCount: 1));

        _sut.CompleteSet(exerciseIndex, setIndex);

        Assert.Empty(_sut.CompletedSets);
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(0, 5)]
    public void CompleteSet_DoesNothing_WhenSetIndexOutOfRange(int exerciseIndex, int setIndex)
    {
        _sut.StartWorkout(MakePlan(setsPerExercise: 3));

        _sut.CompleteSet(exerciseIndex, setIndex);

        Assert.Empty(_sut.CompletedSets);
    }

    // ──────────────────────────────────────────────────────────────
    // StartRest
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void StartRest_SetsIsResting()
    {
        _sut.StartWorkout(MakePlan());

        _sut.StartRest(30, () => { }, () => { });

        Assert.True(_sut.IsResting);
    }

    [Fact]
    public void StartRest_SetsRestSecondsRemaining()
    {
        _sut.StartWorkout(MakePlan());

        _sut.StartRest(45, () => { }, () => { });

        Assert.Equal(45, _sut.RestSecondsRemaining);
    }

    [Fact]
    public void StartRest_InvokesOnTickImmediately()
    {
        _sut.StartWorkout(MakePlan());
        var tickCount = 0;

        _sut.StartRest(30, () => tickCount++, () => { });

        Assert.Equal(1, tickCount);
    }

    [Fact]
    public void StartRest_DoesNothing_WhenNoPlan()
    {
        _sut.StartRest(30, () => { }, () => { });

        Assert.False(_sut.IsResting);
    }

    [Fact]
    public void StartRest_IsNotPaused_AfterStart()
    {
        _sut.StartWorkout(MakePlan());

        _sut.StartRest(30, () => { }, () => { });

        Assert.False(_sut.IsRestPaused);
    }

    // ──────────────────────────────────────────────────────────────
    // PauseRest
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void PauseRest_SetsIsRestPaused()
    {
        _sut.StartWorkout(MakePlan());
        _sut.StartRest(30, () => { }, () => { });

        _sut.PauseRest();

        Assert.True(_sut.IsRestPaused);
    }

    [Fact]
    public void PauseRest_InvokesOnTick()
    {
        _sut.StartWorkout(MakePlan());
        var tickCount = 0;
        _sut.StartRest(30, () => tickCount++, () => { });
        tickCount = 0; // reset after StartRest's immediate tick

        _sut.PauseRest();

        Assert.Equal(1, tickCount);
    }

    [Fact]
    public void PauseRest_KeepsIsResting_True()
    {
        _sut.StartWorkout(MakePlan());
        _sut.StartRest(30, () => { }, () => { });

        _sut.PauseRest();

        Assert.True(_sut.IsResting);
    }

    // ──────────────────────────────────────────────────────────────
    // ResumeRest
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void ResumeRest_ClearsIsRestPaused()
    {
        _sut.StartWorkout(MakePlan());
        _sut.StartRest(30, () => { }, () => { });
        _sut.PauseRest();

        _sut.ResumeRest();

        Assert.False(_sut.IsRestPaused);
    }

    [Fact]
    public void ResumeRest_InvokesOnTick()
    {
        _sut.StartWorkout(MakePlan());
        var tickCount = 0;
        _sut.StartRest(30, () => tickCount++, () => { });
        _sut.PauseRest();
        tickCount = 0;

        _sut.ResumeRest();

        Assert.Equal(1, tickCount);
    }

    [Fact]
    public void ResumeRest_DoesNothing_WhenNotResting()
    {
        // Should not throw
        _sut.ResumeRest();

        Assert.False(_sut.IsResting);
    }

    // ──────────────────────────────────────────────────────────────
    // ResetRest
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void ResetRest_InvokesOnTick()
    {
        _sut.StartWorkout(MakePlan());
        var tickCount = 0;
        _sut.StartRest(30, () => tickCount++, () => { });
        tickCount = 0;

        _sut.ResetRest();

        Assert.Equal(1, tickCount);
    }

    [Fact]
    public void ResetRest_RestoresRestSecondsRemaining()
    {
        _sut.StartWorkout(MakePlan());
        _sut.StartRest(30, () => { }, () => { });

        _sut.ResetRest();

        Assert.Equal(30, _sut.RestSecondsRemaining);
    }

    [Fact]
    public void ResetRest_RestartsTimer_WhenPaused()
    {
        _sut.StartWorkout(MakePlan());
        _sut.StartRest(30, () => { }, () => { });
        _sut.PauseRest();

        _sut.ResetRest();

        Assert.False(_sut.IsRestPaused);
    }

    // ──────────────────────────────────────────────────────────────
    // SkipRest
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void SkipRest_SetsIsResting_False()
    {
        _sut.StartWorkout(MakePlan());
        _sut.StartRest(30, () => { }, () => { });

        _sut.SkipRest();

        Assert.False(_sut.IsResting);
    }

    [Fact]
    public void SkipRest_InvokesOnComplete()
    {
        _sut.StartWorkout(MakePlan());
        var completed = false;
        _sut.StartRest(30, () => { }, () => completed = true);

        _sut.SkipRest();

        Assert.True(completed);
    }

    [Fact]
    public void SkipRest_SetsRestSecondsRemaining_ToZero()
    {
        _sut.StartWorkout(MakePlan());
        _sut.StartRest(30, () => { }, () => { });

        _sut.SkipRest();

        Assert.Equal(0, _sut.RestSecondsRemaining);
    }

    // ──────────────────────────────────────────────────────────────
    // CancelRest
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void CancelRest_SetsIsResting_False()
    {
        _sut.StartWorkout(MakePlan());
        _sut.StartRest(30, () => { }, () => { });

        _sut.CancelRest();

        Assert.False(_sut.IsResting);
    }

    [Fact]
    public void CancelRest_DoesNotInvokeOnComplete()
    {
        _sut.StartWorkout(MakePlan());
        var completed = false;
        _sut.StartRest(30, () => { }, () => completed = true);

        _sut.CancelRest();

        Assert.False(completed);
    }

    // ──────────────────────────────────────────────────────────────
    // SuspendRest / UnregisterTimerCallbacks
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void SuspendRest_PausesTimer_WhenResting()
    {
        _sut.StartWorkout(MakePlan());
        _sut.StartRest(30, () => { }, () => { });

        _sut.SuspendRest();

        Assert.True(_sut.IsRestPaused);
    }

    [Fact]
    public void SuspendRest_DoesNothing_WhenNotResting()
    {
        // Should not throw
        _sut.SuspendRest();

        Assert.False(_sut.IsResting);
    }

    [Fact]
    public void UnregisterTimerCallbacks_PreventsOnTickFromBeingCalled()
    {
        _sut.StartWorkout(MakePlan());
        var tickCount = 0;
        _sut.StartRest(30, () => tickCount++, () => { });
        tickCount = 0;

        _sut.UnregisterTimerCallbacks();
        _sut.PauseRest(); // would normally call onTick

        Assert.Equal(0, tickCount);
    }

    [Fact]
    public void UnregisterTimerCallbacks_PreventsOnCompleteFromBeingCalled()
    {
        _sut.StartWorkout(MakePlan());
        var completed = false;
        _sut.StartRest(30, () => { }, () => completed = true);

        _sut.UnregisterTimerCallbacks();
        _sut.SkipRest(); // would normally call onComplete

        Assert.False(completed);
    }

    // ──────────────────────────────────────────────────────────────
    // Timer tick behavior (async — waits for real 1-second ticks)
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void RestTimer_DecrementsRestSecondsRemaining_AfterOneTick()
    {
        _sut.StartWorkout(MakePlan());
        _sut.StartRest(5, () => { }, () => { });

        _sut.TickRest();

        Assert.Equal(4, _sut.RestSecondsRemaining);
    }

    [Fact]
    public void RestTimer_CallsOnTick_OnEachTick()
    {
        _sut.StartWorkout(MakePlan());
        var tickCount = 0;
        _sut.StartRest(5, () => tickCount++, () => { });
        tickCount = 0; // reset the immediate tick from StartRest

        _sut.TickRest();
        _sut.TickRest();

        Assert.Equal(2, tickCount);
    }

    [Fact]
    public void RestTimer_CallsOnComplete_WhenTimeReachesZero()
    {
        _sut.StartWorkout(MakePlan());
        var completed = false;
        _sut.StartRest(2, () => { }, () => completed = true);

        _sut.TickRest();
        _sut.TickRest();

        Assert.True(completed);
    }

    [Fact]
    public void RestTimer_RestSecondsRemaining_IsZero_WhenTimeExpires()
    {
        _sut.StartWorkout(MakePlan());
        _sut.StartRest(1, () => { }, () => { });

        _sut.TickRest();

        Assert.Equal(0, _sut.RestSecondsRemaining);
    }

    [Fact]
    public void RestTimer_DoesNotDecrementWhilePaused()
    {
        _sut.StartWorkout(MakePlan());
        _sut.StartRest(30, () => { }, () => { });
        _sut.PauseRest();
        var secondsAfterPause = _sut.RestSecondsRemaining;

        _sut.TickRest();

        Assert.Equal(secondsAfterPause, _sut.RestSecondsRemaining);
    }
}
