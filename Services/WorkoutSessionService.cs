using Physiquinator.Models;

namespace Physiquinator.Services;

/// <summary>
/// Manages workout session state. Rest countdown uses wall-clock end time
/// (<see cref="RestEndsAtUtc"/>) while ticking is still driven by the UI/JS bridge
/// (<see cref="TickRest"/>) to avoid background threads in the WebView.
/// </summary>
public class WorkoutSessionService : IDisposable
{
    private readonly TimeProvider _time;

    private DateTime? _restEndsAtUtc;
    private int _activeRestDurationSeconds;
    private bool _isResting;
    private bool _userPaused;
    private int? _pausedRemainingSeconds;

    public WorkoutSessionService(TimeProvider time) => _time = time;

    public WorkoutPlan? CurrentPlan { get; private set; }
    public List<SetCompletion> CompletedSets { get; } = new();

    /// <summary>UTC instant when the current rest period ends, if running on wall clock.</summary>
    public DateTime? RestEndsAtUtc => _isResting && !_userPaused ? _restEndsAtUtc : null;

    public int RestSecondsRemaining
    {
        get
        {
            if (!_isResting) return 0;
            if (_userPaused && _pausedRemainingSeconds.HasValue)
                return Math.Max(0, _pausedRemainingSeconds.Value);
            if (_restEndsAtUtc.HasValue)
                return Math.Max(0, (int)Math.Ceiling((_restEndsAtUtc.Value - UtcNow).TotalSeconds));
            return 0;
        }
    }

    public bool IsResting => _isResting;
    public bool IsRestPaused => _isResting && _userPaused;

    /// <summary>Fired when rest expires while the app was not driving JS ticks (e.g. after resume from background).</summary>
    public event EventHandler? RestCompletedWhileBackground;

    private DateTime UtcNow => _time.GetUtcNow().UtcDateTime;

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

    public void StartRest(int restIntervalSeconds)
    {
        if (CurrentPlan == null) return;

        _activeRestDurationSeconds = Math.Max(0, restIntervalSeconds);
        _userPaused = false;
        _pausedRemainingSeconds = null;

        if (_activeRestDurationSeconds == 0)
        {
            StopRest();
            return;
        }

        _restEndsAtUtc = UtcNow.AddSeconds(_activeRestDurationSeconds);
        _isResting = true;
    }

    /// <summary>
    /// Returns <c>true</c> when the rest period just finished.
    /// Must be called only from the UI timer loop (single thread).
    /// </summary>
    public bool TickRest()
    {
        if (!_isResting || _userPaused || !_restEndsAtUtc.HasValue) return false;

        if (UtcNow >= _restEndsAtUtc.Value)
        {
            StopRest();
            return true;
        }

        return false;
    }

    public void PauseRest()
    {
        if (!_isResting || _userPaused) return;

        _pausedRemainingSeconds = RestSecondsRemaining;
        _userPaused = true;
        _restEndsAtUtc = null;
    }

    /// <summary>User tapped Resume after pausing rest.</summary>
    public bool ResumeRest()
    {
        if (!_isResting || !_userPaused) return false;

        var remaining = _pausedRemainingSeconds ?? 0;
        _userPaused = false;
        _pausedRemainingSeconds = null;

        if (remaining <= 0)
        {
            StopRest();
            return true;
        }

        _restEndsAtUtc = UtcNow.AddSeconds(remaining);
        return false;
    }

    /// <summary>Called when the app window becomes active. Completes rest if wall-clock end passed.</summary>
    public void NotifyAppActivated()
    {
        if (!TryCompleteRestIfExpired())
            return;

        RestCompletedWhileBackground?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Used by tests and <see cref="NotifyAppActivated"/>.</summary>
    public bool TryCompleteRestIfExpired()
    {
        if (!_isResting || _userPaused || !_restEndsAtUtc.HasValue) return false;
        if (UtcNow < _restEndsAtUtc.Value) return false;

        StopRest();
        return true;
    }

    public void ResetRest()
    {
        if (_activeRestDurationSeconds <= 0) return;

        _userPaused = false;
        _pausedRemainingSeconds = null;
        _restEndsAtUtc = UtcNow.AddSeconds(_activeRestDurationSeconds);
        _isResting = true;
    }

    public void SkipRest() => StopRest();

    /// <summary>Stop the rest timer without firing any completion callback.</summary>
    public void CancelRest() => StopRest();

    /// <summary>App window deactivated. Wall clock still applies; JS ticks may stop in WebView.</summary>
    public void SuspendRest()
    {
        // Intentionally no-op: rest continues in real time via RestEndsAtUtc.
    }

    private void StopRest()
    {
        _isResting = false;
        _userPaused = false;
        _pausedRemainingSeconds = null;
        _restEndsAtUtc = null;
        _activeRestDurationSeconds = 0;
    }

    public void Dispose() { }
}
