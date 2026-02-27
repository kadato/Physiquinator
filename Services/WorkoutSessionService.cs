using System.Timers;
using Physiquinator.Models;

namespace Physiquinator.Services;

public class WorkoutSessionService : IDisposable
{
    private readonly object _lock = new();
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
        Action? onTick = null;
        lock (_lock)
        {
            if (_restTimer != null)
            {
                _restSecondsRemaining = _lastRestIntervalSeconds;
                _restSecondsTotal = _lastRestIntervalSeconds;
                if (!_restTimer.Enabled)
                {
                    _restTimer.Start();
                }
                onTick = _onRestTick;
            }
        }
        onTick?.Invoke();
    }

    public void StartRest(int restIntervalSeconds, Action onTick, Action onComplete)
    {
        if (CurrentPlan == null) return;

        lock (_lock)
        {
            _onRestTick = onTick;
            _onRestComplete = onComplete;
            _restSecondsRemaining = restIntervalSeconds;
            _restSecondsTotal = restIntervalSeconds;
            _lastRestIntervalSeconds = restIntervalSeconds;

            _restTimer?.Dispose();
            _restTimer = new System.Timers.Timer(1000) { AutoReset = false };
            _restTimer.Elapsed += RestTimer_Elapsed;
            _restTimer.Start();
        }

        onTick.Invoke();
    }

    public void PauseRest()
    {
        Action? onTick;
        lock (_lock)
        {
            _restTimer?.Stop();
            onTick = _onRestTick;
        }
        onTick?.Invoke();
    }

    public void ResumeRest()
    {
        Action? onTick = null;
        Action? onComplete = null;

        lock (_lock)
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
                    onTick = _onRestTick;
                    onComplete = _onRestComplete;
                }
            }

            if (onComplete == null && !_restTimer.Enabled && _restSecondsRemaining > 0)
            {
                _restTimer.Start();
                onTick = _onRestTick;
            }
        }

        try { onTick?.Invoke(); } catch { }
        try { onComplete?.Invoke(); } catch { }
    }

    public void ResetRest()
    {
        Action? onTick = null;
        lock (_lock)
        {
            if (_restTimer != null)
            {
                _restSecondsRemaining = _restSecondsTotal;
                if (!_restTimer.Enabled)
                {
                    _restTimer.Start();
                }
                onTick = _onRestTick;
            }
        }
        onTick?.Invoke();
    }

    public void SkipRest()
    {
        Action? onComplete;
        lock (_lock)
        {
            StopRestTimerUnsafe();
            onComplete = _onRestComplete;
        }
        onComplete?.Invoke();
    }

    /// <summary>Pause the rest timer when the app is backgrounded to avoid battery-optimizer kills.</summary>
    public void SuspendRest()
    {
        lock (_lock)
        {
            if (_restTimer?.Enabled == true)
            {
                _suspendedAt = DateTime.UtcNow;
                _restTimer.Stop();
            }
        }
    }

    public void UnregisterTimerCallbacks()
    {
        lock (_lock)
        {
            _onRestTick = null;
            _onRestComplete = null;
        }
    }

    private void RestTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        Action? onTick;
        Action? onComplete = null;

        lock (_lock)
        {
            // Ignore stale callbacks from a timer that was replaced or stopped
            if (_restTimer == null || sender != _restTimer)
                return;

            _restSecondsRemaining--;
            onTick = _onRestTick;

            if (_restSecondsRemaining <= 0)
            {
                onComplete = _onRestComplete;
            }
            else
            {
                try { _restTimer.Start(); }
                catch (ObjectDisposedException) { }
            }
        }

        try { onTick?.Invoke(); } catch { }
        try { onComplete?.Invoke(); } catch { }
    }

    private void StopRestTimer()
    {
        lock (_lock)
        {
            StopRestTimerUnsafe();
        }
    }

    /// <summary>Stops and disposes the timer. Caller must hold <see cref="_lock"/>.</summary>
    private void StopRestTimerUnsafe()
    {
        var timer = _restTimer;
        _restTimer = null;
        _restSecondsRemaining = 0;
        _restSecondsTotal = 0;
        _suspendedAt = null;
        timer?.Stop();
        timer?.Dispose();
    }

    public void Dispose()
    {
        lock (_lock)
        {
            var timer = _restTimer;
            _restTimer = null;
            timer?.Stop();
            timer?.Dispose();
        }
    }
}
