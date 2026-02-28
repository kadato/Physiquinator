using Physiquinator.Models;

namespace Physiquinator.Services;

/// <summary>
/// Manages workout session state. Rest-timer ticking is driven externally by the UI
/// component via <see cref="TickRest"/> — there is no internal background timer thread,
/// which avoids cross-thread WebView interactions that caused native crashes on Android.
/// </summary>
public class WorkoutSessionService : IDisposable
{
    private int _restSecondsRemaining;
    private int _restSecondsTotal;
    private int _lastRestIntervalSeconds;
    private bool _isResting;
    private bool _isRestPaused;
    private DateTime? _suspendedAt;
    private Action? _onRestTick;
    private Action? _onRestComplete;

    public WorkoutPlan? CurrentPlan { get; private set; }
    public List<SetCompletion> CompletedSets { get; } = new();
    public int RestSecondsRemaining => _restSecondsRemaining;
    public bool IsResting => _isResting;
    public bool IsRestPaused => _isResting && _isRestPaused;

    public void StartWorkout(WorkoutPlan plan)
    {
        CurrentPlan = plan;
        CompletedSets.Clear();
        StopRest();
    }

    public void EndWorkout()
    {
        CurrentPlan = null;
        CompletedSets.Clear();
        StopRest();
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

    public void StartRest(int restIntervalSeconds, Action onTick, Action onComplete)
    {
        if (CurrentPlan == null) return;
        _onRestTick = onTick;
        _onRestComplete = onComplete;
        _restSecondsRemaining = restIntervalSeconds;
        _restSecondsTotal = restIntervalSeconds;
        _lastRestIntervalSeconds = restIntervalSeconds;
        _isRestPaused = false;
        _suspendedAt = null;
        _isResting = true;

        try { onTick.Invoke(); } catch { }
    }

    /// <summary>
    /// Decrements the countdown by one second, fires <c>onTick</c>, and fires
    /// <c>onComplete</c> when the countdown reaches zero.
    /// Returns <c>true</c> when the rest period just finished.
    /// Must be called only from the UI timer loop (single thread).
    /// </summary>
    public bool TickRest()
    {
        if (!_isResting || _isRestPaused) return false;

        _restSecondsRemaining--;
        if (_restSecondsRemaining <= 0)
        {
            _isResting = false;
            try { _onRestTick?.Invoke(); } catch { }
            try { _onRestComplete?.Invoke(); } catch { }
            return true;
        }

        try { _onRestTick?.Invoke(); } catch { }
        return false;
    }

    public void PauseRest()
    {
        _isRestPaused = true;
        try { _onRestTick?.Invoke(); } catch { }
    }

    public void ResumeRest()
    {
        if (!_isResting) return;

        if (_suspendedAt.HasValue)
        {
            var elapsed = (int)(DateTime.UtcNow - _suspendedAt.Value).TotalSeconds;
            _suspendedAt = null;
            _restSecondsRemaining = Math.Max(0, _restSecondsRemaining - elapsed);

            if (_restSecondsRemaining <= 0)
            {
                _isResting = false;
                try { _onRestTick?.Invoke(); } catch { }
                try { _onRestComplete?.Invoke(); } catch { }
                return;
            }
        }

        _isRestPaused = false;
        try { _onRestTick?.Invoke(); } catch { }
    }

    public void ResetRest()
    {
        if (_isResting)
        {
            _restSecondsRemaining = _restSecondsTotal;
            _isRestPaused = false;
            try { _onRestTick?.Invoke(); } catch { }
        }
    }

    public void SkipRest()
    {
        StopRest();
        try { _onRestComplete?.Invoke(); } catch { }
    }

    /// <summary>Stop the rest timer without firing the onComplete callback.</summary>
    public void CancelRest() => StopRest();

    /// <summary>Pause the countdown when the app is backgrounded.</summary>
    public void SuspendRest()
    {
        if (_isResting && !_isRestPaused)
        {
            _suspendedAt = DateTime.UtcNow;
            _isRestPaused = true;
        }
    }

    public void UnregisterTimerCallbacks()
    {
        _onRestTick = null;
        _onRestComplete = null;
    }

    private void StopRest()
    {
        _isResting = false;
        _isRestPaused = false;
        _restSecondsRemaining = 0;
        _restSecondsTotal = 0;
        _suspendedAt = null;
    }

    public void Dispose() { }
}
