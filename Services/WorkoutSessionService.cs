using System.Timers;
using Physiquinator.Models;

namespace Physiquinator.Services;

public class WorkoutSessionService : IDisposable
{
    private readonly Action<Action> _dispatchToMainThread;
    private System.Timers.Timer? _restTimer;
    private int _restSecondsRemaining;
    private int _restSecondsTotal;
    private int _lastRestIntervalSeconds;
    private bool _isRestPaused;
    private DateTime? _suspendedAt;
    private Action? _onRestTick;
    private Action? _onRestComplete;

    /// <param name="dispatchToMainThread">
    /// Dispatcher that runs an action on the UI/main thread.
    /// Pass <c>MainThread.BeginInvokeOnMainThread</c> from MAUI.
    /// Defaults to direct (inline) invocation for unit tests.
    /// </param>
    public WorkoutSessionService(Action<Action>? dispatchToMainThread = null)
    {
        _dispatchToMainThread = dispatchToMainThread ?? (action => action());
    }

    public WorkoutPlan? CurrentPlan { get; private set; }
    public List<SetCompletion> CompletedSets { get; } = new();
    public int RestSecondsRemaining => _restSecondsRemaining;
    public bool IsResting => _restTimer != null;
    public bool IsRestPaused => IsResting && _isRestPaused;

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

    public void StartRest(int restIntervalSeconds, Action onTick, Action onComplete)
    {
        if (CurrentPlan == null) return;
        _onRestTick = onTick;
        _onRestComplete = onComplete;
        _restSecondsRemaining = restIntervalSeconds;
        _restSecondsTotal = restIntervalSeconds;
        _lastRestIntervalSeconds = restIntervalSeconds;

        _restTimer?.Dispose();
        _restTimer = new System.Timers.Timer(1000) { AutoReset = false };
        _restTimer.Elapsed += RestTimer_Elapsed;
        _isRestPaused = false;
        _restTimer.Start();

        try { onTick.Invoke(); } catch { }
    }

    public void PauseRest()
    {
        _restTimer?.Stop();
        _isRestPaused = true;
        try { _onRestTick?.Invoke(); } catch { }
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
            _isRestPaused = false;
            _restTimer.Start();
            try { _onRestTick?.Invoke(); } catch { }
        }
    }

    public void ResetRest()
    {
        if (_restTimer != null)
        {
            _restSecondsRemaining = _restSecondsTotal;
            _isRestPaused = false;
            if (!_restTimer.Enabled)
            {
                try { _restTimer.Start(); }
                catch (ObjectDisposedException) { }
            }
            try { _onRestTick?.Invoke(); } catch { }
        }
    }

    public void SkipRest()
    {
        StopRestTimer();
        try { _onRestComplete?.Invoke(); } catch { }
    }

    /// <summary>Stop the rest timer without firing the onComplete callback.</summary>
    public void CancelRest()
    {
        StopRestTimer();
    }

    /// <summary>Pause the rest timer when the app is backgrounded to avoid battery-optimizer kills.</summary>
    public void SuspendRest()
    {
        if (_restTimer?.Enabled == true)
        {
            _suspendedAt = DateTime.UtcNow;
            _isRestPaused = true;
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
        // Elapsed fires on a ThreadPool thread. An unhandled exception here
        // terminates the process in .NET 6+, so the entire body must be guarded.
        try
        {
            _dispatchToMainThread(() =>
            {
                // Ignore stale callbacks from a timer that was replaced or stopped
                if (_restTimer == null || sender != _restTimer)
                    return;

                _restSecondsRemaining--;

                if (_restSecondsRemaining <= 0)
                {
                    try { _onRestTick?.Invoke(); } catch { }
                    try { _onRestComplete?.Invoke(); } catch { }
                }
                else
                {
                    // Re-arm before notifying UI so IsRestPaused stays false during the re-render
                    try { _restTimer?.Start(); }
                    catch (ObjectDisposedException) { }
                    try { _onRestTick?.Invoke(); } catch { }
                }
            });
        }
        catch
        {
            // If dispatching to the main thread fails (e.g. app is shutting down,
            // Android main looper unavailable), swallow the exception so the process
            // survives. The timer won't re-arm (AutoReset is false) and will stop
            // naturally.
        }
    }

    private void StopRestTimer()
    {
        var timer = _restTimer;
        _restTimer = null;
        _restSecondsRemaining = 0;
        _restSecondsTotal = 0;
        _isRestPaused = false;
        _suspendedAt = null;
        timer?.Stop();
        timer?.Dispose();
    }

    public void Dispose()
    {
        var timer = _restTimer;
        _restTimer = null;
        timer?.Stop();
        timer?.Dispose();
    }
}
