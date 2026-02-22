using System.Timers;
using Physiquinator.Models;

namespace Physiquinator.Services;

public class WorkoutSessionService : IDisposable
{
    private System.Timers.Timer? _restTimer;
    private int _restSecondsRemaining;
    private int _restSecondsTotal;
    private int _lastRestIntervalSeconds;
    private DateTime? _suspendedAt;
    private Action? _onRestTick;
    private Action? _onRestComplete;

    public WorkoutPlan? CurrentPlan { get; private set; }
    public List<SetCompletion> CompletedSets { get; } = new();
    public int RestSecondsRemaining => _restSecondsRemaining;
    public bool IsResting => _restTimer != null;
    public bool IsRestPaused => IsResting && !(_restTimer?.Enabled ?? false);

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

        // Reset timer to last rest interval when completing a set
        if (_restTimer != null)
        {
            _restSecondsRemaining = _lastRestIntervalSeconds;
            _restSecondsTotal = _lastRestIntervalSeconds;
            if (!_restTimer.Enabled)
            {
                _restTimer.Start();
            }
            _onRestTick?.Invoke();
        }
    }

    public void StartRest(int restIntervalSeconds, Action onTick, Action onComplete)
    {
        if (CurrentPlan == null) return;
        _onRestTick = onTick;
        _onRestComplete = onComplete;
        _restSecondsRemaining = restIntervalSeconds;
        _restSecondsTotal = restIntervalSeconds;
        _lastRestIntervalSeconds = restIntervalSeconds;
        _onRestTick?.Invoke();

        _restTimer?.Dispose();
        _restTimer = new System.Timers.Timer(1000);
        _restTimer.Elapsed += RestTimer_Elapsed;
        _restTimer.Start();
    }

    public void PauseRest()
    {
        _restTimer?.Stop();
        _onRestTick?.Invoke();
    }

    public void ResumeRest()
    {
        if (_restTimer == null) return;

        if (_suspendedAt.HasValue)
        {
            var elapsed = (int)(DateTime.UtcNow - _suspendedAt.Value).TotalSeconds;
            _suspendedAt = null;
            _restSecondsRemaining = Math.Max(0, _restSecondsRemaining - elapsed);

            if (_restSecondsRemaining <= 0)
            {
                _restTimer.Stop();
                try { _onRestTick?.Invoke(); } catch { }
                try { _onRestComplete?.Invoke(); } catch { }
                return;
            }
        }

        if (!_restTimer.Enabled && _restSecondsRemaining > 0)
        {
            _restTimer.Start();
            try { _onRestTick?.Invoke(); } catch { }
        }
    }

    public void ResetRest()
    {
        if (_restTimer != null)
        {
            _restSecondsRemaining = _restSecondsTotal;
            // Restart the timer if it was stopped at zero
            if (!_restTimer.Enabled)
            {
                _restTimer.Start();
            }
            _onRestTick?.Invoke();
        }
    }

    public void SkipRest()
    {
        StopRestTimer();
        _onRestComplete?.Invoke();
    }

    /// <summary>Pause the rest timer when the app is backgrounded to avoid battery-optimizer kills.</summary>
    public void SuspendRest()
    {
        if (_restTimer?.Enabled == true)
        {
            _suspendedAt = DateTime.UtcNow;
            _restTimer.Stop();
        }
    }

    public void UnregisterTimerCallbacks()
    {
        _onRestTick = null;
        _onRestComplete = null;
    }

    private void RestTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        _restSecondsRemaining--;
        try { _onRestTick?.Invoke(); } catch { }
        if (_restSecondsRemaining <= 0)
        {
            // Stop timer but don't clear it - keep it visible at 0:00
            _restTimer?.Stop();
            try { _onRestComplete?.Invoke(); } catch { }
        }
    }

    private void StopRestTimer()
    {
        _restTimer?.Stop();
        _restTimer?.Dispose();
        _restTimer = null;
        _restSecondsRemaining = 0;
        _restSecondsTotal = 0;
        _suspendedAt = null;
    }

    public void Dispose()
    {
        _restTimer?.Stop();
        _restTimer?.Dispose();
        _restTimer = null;
    }
}
