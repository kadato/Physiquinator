using System.Threading;
using System.Timers;
using Physiquinator.Models;

namespace Physiquinator.Services;

public class WorkoutSessionService : IDisposable
{
    private readonly Action<string, Exception> _logError;
    private System.Timers.Timer? _restTimer;
    // volatile so the ThreadPool timer callback and the UI thread always see the
    // up-to-date value without needing a lock.
    private volatile int _restSecondsRemaining;
    private int _restSecondsTotal;
    private int _lastRestIntervalSeconds;
    private bool _isRestPaused;
    private DateTime? _suspendedAt;
    private Action? _onRestTick;
    private Action? _onRestComplete;

    /// <param name="logError">
    /// Optional error logger for exceptions caught inside timer callbacks.
    /// Pass <c>(src, ex) => CrashLogger.Log(src, ex)</c> from MAUI.
    /// Defaults to a no-op for unit tests.
    /// </param>
    public WorkoutSessionService(Action<string, Exception>? logError = null)
    {
        _logError = logError ?? ((_, _) => { });
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
        // Runs on a ThreadPool thread. An unhandled exception here terminates the
        // process in .NET 6+, so the entire body is guarded.
        //
        // We intentionally do NOT dispatch to the main thread before calling
        // _onRestTick/_onRestComplete. Those callbacks use InvokeAsync which
        // correctly posts to the Blazor dispatcher from any thread. Dispatching to
        // the MAUI main thread first and then calling InvokeAsync from within that
        // callback caused InvokeAsync to execute StateHasChanged() synchronously
        // inside a Handler.post() callback, which triggered a native WebView crash
        // on Android release builds.
        try
        {
            // Ignore stale callbacks from a timer that was replaced or stopped
            if (_restTimer == null || sender != _restTimer)
                return;

            var remaining = Interlocked.Decrement(ref _restSecondsRemaining);

            if (remaining <= 0)
            {
                try { _onRestTick?.Invoke(); } catch (Exception ex) { _logError("RestTimer/onTick", ex); }
                try { _onRestComplete?.Invoke(); } catch (Exception ex) { _logError("RestTimer/onComplete", ex); }
            }
            else
            {
                // Re-arm first so IsResting stays true during the UI update
                try { _restTimer?.Start(); }
                catch (ObjectDisposedException) { }
                try { _onRestTick?.Invoke(); } catch (Exception ex) { _logError("RestTimer/onTick", ex); }
            }
        }
        catch (Exception ex)
        {
            _logError("RestTimer/elapsed", ex);
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
