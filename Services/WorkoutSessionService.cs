using System.Timers;
using Physiquinator.Models;

namespace Physiquinator.Services;

public class WorkoutSessionService
{
    private System.Timers.Timer? _restTimer;
    private int _restSecondsRemaining;
    private Action? _onRestTick;
    private Action? _onRestComplete;

    public WorkoutPlan? CurrentPlan { get; private set; }
    public List<SetCompletion> CompletedSets { get; } = new();
    public int RestSecondsRemaining => _restSecondsRemaining;
    public bool IsResting => _restTimer?.Enabled ?? false;

    public void StartWorkout(WorkoutPlan plan)
    {
        CurrentPlan = plan;
        CompletedSets.Clear();
        StopRestTimer();
    }

    public void EndWorkout()
    {
        CurrentPlan = null;
        CompletedSets.Clear();
        StopRestTimer();
    }

    public bool IsSetCompleted(int exerciseIndex, int setIndex) =>
        CompletedSets.Contains(new SetCompletion(exerciseIndex, setIndex));

    public void CompleteSet(int exerciseIndex, int setIndex)
    {
        if (CurrentPlan == null) return;
        if (exerciseIndex < 0 || exerciseIndex >= CurrentPlan.Exercises.Count) return;
        var ex = CurrentPlan.Exercises[exerciseIndex];
        if (setIndex < 0 || setIndex >= ex.SetCount) return;

        CompletedSets.Add(new SetCompletion(exerciseIndex, setIndex));
    }

    public void StartRest(Action onTick, Action onComplete)
    {
        if (CurrentPlan == null) return;
        _onRestTick = onTick;
        _onRestComplete = onComplete;
        _restSecondsRemaining = CurrentPlan.RestIntervalSeconds;
        _onRestTick?.Invoke();

        _restTimer?.Dispose();
        _restTimer = new System.Timers.Timer(1000);
        _restTimer.Elapsed += RestTimer_Elapsed;
        _restTimer.Start();
    }

    public void SkipRest()
    {
        StopRestTimer();
        _onRestComplete?.Invoke();
    }

    private void RestTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        _restSecondsRemaining--;
        _onRestTick?.Invoke();
        if (_restSecondsRemaining <= 0)
        {
            StopRestTimer();
            _onRestComplete?.Invoke();
        }
    }

    private void StopRestTimer()
    {
        _restTimer?.Stop();
        _restTimer?.Dispose();
        _restTimer = null;
        _restSecondsRemaining = 0;
    }
}
