using Physiquinator.Core.Models;

namespace Physiquinator.Core.Services;

/// <summary>
/// Manages workout session state. Rest countdown uses wall-clock end time
/// (<see cref="RestEndsAtUtc"/>) while ticking is still driven by the UI/JS bridge
/// (<see cref="TickRest"/>) to avoid background threads in the WebView.
/// </summary>
public sealed class WorkoutSessionService(TimeProvider time) : IDisposable
{
    private readonly TimeProvider _time = time;
    private System.Threading.Timer? _internalTimer;

    private DateTime? _restEndsAtUtc;
    private int _activeRestDurationSeconds;
    private bool _isResting;

    // Last rest state broadcast through RestStateChanged, so the 500 ms
    // internal tick stays silent while the countdown just counts down.
    private DateTime? _lastEmittedRestEndsAtUtc;
    private bool _lastEmittedIsResting;

    private readonly List<SetCompletion> _completedSets = [];
    private readonly HashSet<SetCompletion> _completedSetLookup = [];

    public WorkoutPlan? CurrentPlan { get; private set; }

    /// <summary>Completed sets in chronological (append) order.</summary>
    public IReadOnlyList<SetCompletion> CompletedSets => _completedSets;

    /// <summary>UTC instant when the current rest period ends, if running on wall clock.</summary>
    public DateTime? RestEndsAtUtc => _isResting ? _restEndsAtUtc : null;

    public int RestSecondsRemaining
    {
        get
        {
            if (!_isResting) return 0;
            if (_restEndsAtUtc.HasValue)
                return Math.Max(0, (int)Math.Ceiling((_restEndsAtUtc.Value - UtcNow).TotalSeconds));
            return 0;
        }
    }

    public bool IsResting => _isResting;

    /// <summary>Duration in seconds of the active rest period (0 when not resting).</summary>
    public int ActiveRestDurationSeconds => _isResting ? _activeRestDurationSeconds : 0;

    /// <summary>Fired when rest expires while the app was not driving JS ticks, for example after resume from background.</summary>
    public event EventHandler? RestCompletedWhileBackground;

    /// <summary>
    /// Fired on material rest state changes (start, reset, add, skip, cancel,
    /// restore, expiry) so platform surfaces such as the ongoing
    /// notification, floating overlay and exact alarm can stay in sync.
    /// Not fired by the 500 ms internal tick or no-op mutations. The UI
    /// countdown is driven by the JS bridge instead.
    /// </summary>
    public event EventHandler? RestStateChanged;

    /// <summary>
    /// Fired after a workout is started, resumed or ended so the floating
    /// overlay and ongoing notification follow the whole workout lifecycle,
    /// not just the rest countdown.
    /// </summary>
    public event EventHandler? WorkoutStateChanged;

    private DateTime UtcNow => _time.GetUtcNow().UtcDateTime;

    /// <summary>
    /// Raises <see cref="RestStateChanged"/> only when the rest state actually
    /// changed since the last emission (rest started, stopped, reset, extended, or
    /// restored). The 500 ms internal tick and no-op mutations stay silent, so
    /// subscribers such as <see cref="RestTimerCoordinator"/> do not re-run
    /// platform work (snapshot persist, exact-alarm re-arm, overlay restart)
    /// on every tick. The UI countdown is driven by the JS bridge instead.
    /// </summary>
    private void RaiseRestStateChangedIfChanged()
    {
        DateTime? end = _isResting ? _restEndsAtUtc : null;
        if (_lastEmittedIsResting == _isResting && _lastEmittedRestEndsAtUtc == end)
            return;

        _lastEmittedIsResting = _isResting;
        _lastEmittedRestEndsAtUtc = end;
        RestStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void StartWorkout(WorkoutPlan plan)
    {
        CurrentPlan = plan;
        ClearCompletedSets();
        ResetRestSilently();
        WorkoutStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Restores in-memory workout state from persisted set logs.</summary>
    public void ResumeWorkout(WorkoutPlan plan, IEnumerable<SetCompletion> completedSets)
    {
        CurrentPlan = plan;
        ClearCompletedSets();
        _completedSets.AddRange(completedSets);
        _completedSetLookup.UnionWith(_completedSets);
        ResetRestSilently();
        WorkoutStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void EndWorkout()
    {
        CurrentPlan = null;
        ClearCompletedSets();
        StopRest();
        WorkoutStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool IsSetCompleted(int exerciseIndex, int setIndex) =>
        _completedSetLookup.Contains(new SetCompletion(exerciseIndex, setIndex));

    /// <summary>First uncompleted set index for an exercise, or -1 when the exercise is fully completed.</summary>
    public int GetFirstUncompletedSetIndex(int exerciseIndex)
    {
        if (CurrentPlan == null || exerciseIndex < 0 || exerciseIndex >= CurrentPlan.Exercises.Count)
            return -1;

        ExercisePlan exercise = CurrentPlan.Exercises[exerciseIndex];
        for (var s = 0; s < exercise.TotalSetCount; s++)
        {
            if (!IsSetCompleted(exerciseIndex, s))
                return s;
        }

        return -1;
    }

    /// <summary>True when every set of the exercise has been completed.</summary>
    public bool IsExerciseDone(int exerciseIndex) => GetFirstUncompletedSetIndex(exerciseIndex) == -1;

    /// <summary>First exercise with at least one uncompleted set, or -1 when the whole workout is complete.</summary>
    public int GetFirstUncompletedExerciseIndex()
    {
        if (CurrentPlan == null) return -1;

        for (var e = 0; e < CurrentPlan.Exercises.Count; e++)
        {
            if (!IsExerciseDone(e))
                return e;
        }

        return -1;
    }

    /// <summary>
    /// Checks if completing the specified set would complete the entire workout.
    /// </summary>
    public bool WouldCompleteWorkout(int exerciseIndex, int setIndex)
    {
        if (CurrentPlan == null) return false;

        for (var ei = 0; ei < CurrentPlan.Exercises.Count; ei++)
        {
            ExercisePlan ex = CurrentPlan.Exercises[ei];
            for (var si = 0; si < ex.TotalSetCount; si++)
            {
                if (ei == exerciseIndex && si == setIndex)
                    continue;

                if (!IsSetCompleted(ei, si))
                    return false;
            }
        }

        return true;
    }

    public void CompleteSet(int exerciseIndex, int setIndex)
    {
        if (CurrentPlan == null) return;
        if (exerciseIndex < 0 || exerciseIndex >= CurrentPlan.Exercises.Count) return;
        ExercisePlan ex = CurrentPlan.Exercises[exerciseIndex];
        if (setIndex < 0 || setIndex >= ex.TotalSetCount) return;

        var completion = new SetCompletion(exerciseIndex, setIndex);
        _completedSets.Add(completion);
        _completedSetLookup.Add(completion);
    }

    /// <summary>
    /// Re-indexes every completed set after the plan's exercise order changed
    /// mid-session (old exercise index to new index). Used by mid-workout
    /// reordering so completion state stays attached to the right exercise.
    /// </summary>
    public void RemapExerciseIndexes(IReadOnlyDictionary<int, int> mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        if (mapping.Count == 0) return;

        _completedSetLookup.Clear();
        for (var i = 0; i < _completedSets.Count; i++)
        {
            var completion = _completedSets[i];
            if (!mapping.TryGetValue(completion.ExerciseIndex, out var newIndex))
                continue;

            var remapped = new SetCompletion(newIndex, completion.SetIndex);
            _completedSets[i] = remapped;
            _completedSetLookup.Add(remapped);
        }
    }

    /// <summary>Removes the last completed set (chronological append order). Returns false when none.</summary>
    public bool TryUndoLastSet(out SetCompletion removed)
    {
        removed = default!;
        if (_completedSets.Count == 0) return false;
        var i = _completedSets.Count - 1;
        removed = _completedSets[i];
        _completedSets.RemoveAt(i);
        _completedSetLookup.Remove(removed);
        return true;
    }

    public void StartRest(int restIntervalSeconds)
    {
        if (CurrentPlan == null) return;

        _activeRestDurationSeconds = Math.Max(0, restIntervalSeconds);

        if (_activeRestDurationSeconds == 0)
        {
            StopRest();
            return;
        }

        _restEndsAtUtc = UtcNow.AddSeconds(_activeRestDurationSeconds);
        _isResting = true;
        StartInternalTimer();
        RaiseRestStateChangedIfChanged();
    }

    /// <summary>
    /// Returns <c>true</c> when the rest period just finished.
    /// Must be called only from the UI timer loop (single thread).
    /// </summary>
    public bool TickRest()
    {
        if (!_isResting || !_restEndsAtUtc.HasValue) return false;

        if (UtcNow >= _restEndsAtUtc.Value)
        {
            StopRest();
            return true;
        }

        return false;
    }

    /// <summary>Called when the app window becomes active. Completes rest if wall-clock end passed.</summary>
    public void NotifyAppActivated()
    {
        TryCompleteRestIfExpired();
    }

    /// <summary>Used by tests and <see cref="NotifyAppActivated"/>.</summary>
    public bool TryCompleteRestIfExpired()
    {
        if (!_isResting || !_restEndsAtUtc.HasValue) return false;
        if (UtcNow < _restEndsAtUtc.Value) return false;

        StopRest();
        // Same completion signal the internal timer and JS tick paths raise, so
        // the workout page latches the checkmark instead of unmounting the
        // panel when the exact-alarm path finishes the rest.
        RestCompletedWhileBackground?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void ResetRest()
    {
        if (_activeRestDurationSeconds <= 0) return;

        _restEndsAtUtc = UtcNow.AddSeconds(_activeRestDurationSeconds);
        _isResting = true;
        StartInternalTimer();
        RaiseRestStateChangedIfChanged();
    }

    /// <summary>
    /// Extends the current rest countdown by the given seconds. No-op when
    /// not resting.
    /// </summary>
    public void AddRestSeconds(int seconds)
    {
        if (!_isResting || seconds <= 0) return;

        if (_restEndsAtUtc.HasValue)
        {
            _restEndsAtUtc = _restEndsAtUtc.Value.AddSeconds(seconds);
            RaiseRestStateChangedIfChanged();
        }
    }

    public void SkipRest() => StopRest();

    /// <summary>Stop the rest timer without firing any completion callback.</summary>
    public void CancelRest() => StopRest();

    /// <summary>
    /// Restores a running rest countdown from a persisted snapshot (process
    /// restart). Returns false when the snapshot is no longer usable.
    /// </summary>
    public bool RestoreRestState(DateTime restEndsAtUtc, int activeRestDurationSeconds)
    {
        if (restEndsAtUtc <= UtcNow || activeRestDurationSeconds <= 0)
            return false;

        _activeRestDurationSeconds = activeRestDurationSeconds;
        _restEndsAtUtc = restEndsAtUtc;
        _isResting = true;
        StartInternalTimer();
        RaiseRestStateChangedIfChanged();
        return true;
    }

    private void ClearCompletedSets()
    {
        _completedSets.Clear();
        _completedSetLookup.Clear();
    }

    /// <summary>
    /// Resets rest state without firing <see cref="RestStateChanged"/>. Used by
    /// workout load (StartWorkout and ResumeWorkout) so a rest countdown that
    /// survived process death is not torn down before the page restores it.
    /// </summary>
    private void ResetRestSilently()
    {
        _isResting = false;
        _restEndsAtUtc = null;
        _activeRestDurationSeconds = 0;
        StopInternalTimer();
    }

    private void StopRest()
    {
        ResetRestSilently();
        RaiseRestStateChangedIfChanged();
    }

    private void StartInternalTimer()
    {
        // One-second tick: the JS bridge drives the visible countdown at the
        // same cadence, and the exact rest-end alarm is the precise path, so
        // this timer only needs to notice expiry while the app is not ticking
        // (backgrounded). A slower tick keeps the CPU in deep sleep longer.
        _internalTimer ??= new System.Threading.Timer(OnInternalTimerTick, null, 1000, 1000);
    }

    private void StopInternalTimer()
    {
        _internalTimer?.Dispose();
        _internalTimer = null;
    }

    private void OnInternalTimerTick(object? state)
    {
        if (!_isResting || !_restEndsAtUtc.HasValue)
        {
            StopInternalTimer();
            return;
        }

        if (UtcNow >= _restEndsAtUtc.Value)
        {
            StopRest();
            RestCompletedWhileBackground?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Calculates the time-weighted workout progress percentage (0-100), accounting for each
    /// exercise's estimated execution duration and configured rest intervals.
    /// </summary>
    public int CalculateProgressPercentage()
    {
        if (CurrentPlan == null || CurrentPlan.Exercises.Count == 0)
            return 0;

        double totalDurationSeconds = 0;
        double completedDurationSeconds = 0;
        var totalSets = 0;
        var completedSetsCount = 0;

        for (var ei = 0; ei < CurrentPlan.Exercises.Count; ei++)
        {
            ExercisePlan ex = CurrentPlan.Exercises[ei];
            var workDuration = GetEstimatedWorkDurationSeconds(ex);
            var restDuration = Math.Max(0, ex.RestIntervalSeconds);
            var setDuration = workDuration + restDuration;

            totalSets += ex.TotalSetCount;
            totalDurationSeconds += ex.TotalSetCount * setDuration;

            for (var si = 0; si < ex.TotalSetCount; si++)
            {
                if (IsSetCompleted(ei, si))
                {
                    completedSetsCount++;
                    completedDurationSeconds += setDuration;
                }
            }
        }

        if (totalSets == 0 || totalDurationSeconds <= 0)
            return 0;

        if (completedSetsCount == totalSets)
            return 100;

        // If currently resting, credit the elapsed portion of the active rest interval
        if (_isResting && _activeRestDurationSeconds > 0)
        {
            var remainingRest = RestSecondsRemaining;
            if (remainingRest > 0)
            {
                var unelapsed = Math.Min(remainingRest, _activeRestDurationSeconds);
                completedDurationSeconds = Math.Max(0, completedDurationSeconds - unelapsed);
            }
        }

        var pct = (int)Math.Round((completedDurationSeconds / totalDurationSeconds) * 100.0);
        return Math.Clamp(pct, 0, 99);
    }

    private static double GetEstimatedWorkDurationSeconds(ExercisePlan ex)
    {
        if (ex.LogType == ExerciseLogType.Duration)
            return Math.Max(15, ex.DefaultReps ?? 45);

        if (ex.DefaultReps.HasValue && ex.DefaultReps.Value > 0)
            return Math.Clamp(ex.DefaultReps.Value * 3.5, 20, 60);

        return 40;
    }

    public void Dispose()
    {
        StopInternalTimer();
    }
}
