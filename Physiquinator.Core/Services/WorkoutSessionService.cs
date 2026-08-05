using Physiquinator.Core.Models;

namespace Physiquinator.Core.Services;

/// <summary>
/// Manages workout session state. Rest countdown uses wall-clock end time
/// (<see cref="RestEndsAtUtc"/>) while ticking is still driven by the UI/JS bridge
/// (<see cref="TickRest"/>) to avoid background threads in the WebView.
/// </summary>
public sealed class WorkoutSessionService(TimeProvider time)
{
    private readonly TimeProvider _time = time;

    private DateTime? _restEndsAtUtc;
    private int _activeRestDurationSeconds;
    private bool _isResting;
    private bool _userPaused;
    private int? _pausedRemainingSeconds;

    private readonly List<SetCompletion> _completedSets = [];
    private readonly HashSet<SetCompletion> _completedSetLookup = [];

    public WorkoutPlan? CurrentPlan { get; private set; }

    /// <summary>Completed sets in chronological (append) order.</summary>
    public IReadOnlyList<SetCompletion> CompletedSets => _completedSets;

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

    /// <summary>Duration in seconds of the active rest period (0 when not resting).</summary>
    public int ActiveRestDurationSeconds => _isResting ? _activeRestDurationSeconds : 0;

    /// <summary>Continuous progress fraction [0, 1] based on wall clock, for smooth progress bar animation.</summary>
    public double RestProgressFraction
    {
        get
        {
            if (!_isResting || _activeRestDurationSeconds <= 0) return 0;
            if (_userPaused && _pausedRemainingSeconds.HasValue)
            {
                var elapsed = _activeRestDurationSeconds - Math.Max(0, _pausedRemainingSeconds.Value);
                return Math.Clamp(elapsed / (double)_activeRestDurationSeconds, 0, 1);
            }
            if (_restEndsAtUtc.HasValue)
            {
                var remaining = (_restEndsAtUtc.Value - UtcNow).TotalSeconds;
                var elapsed = _activeRestDurationSeconds - Math.Max(0, remaining);
                return Math.Clamp(elapsed / _activeRestDurationSeconds, 0, 1);
            }
            return 0;
        }
    }

    /// <summary>Fired when rest expires while the app was not driving JS ticks (e.g. after resume from background).</summary>
    public event EventHandler? RestCompletedWhileBackground;

    private DateTime UtcNow => _time.GetUtcNow().UtcDateTime;

    public void StartWorkout(WorkoutPlan plan)
    {
        CurrentPlan = plan;
        ClearCompletedSets();
        StopRest();
    }

    /// <summary>Restores in-memory workout state from persisted set logs.</summary>
    public void ResumeWorkout(WorkoutPlan plan, IEnumerable<SetCompletion> completedSets)
    {
        CurrentPlan = plan;
        ClearCompletedSets();
        _completedSets.AddRange(completedSets);
        _completedSetLookup.UnionWith(_completedSets);
        StopRest();
    }

    public void EndWorkout()
    {
        CurrentPlan = null;
        ClearCompletedSets();
        StopRest();
    }

    public bool IsSetCompleted(int exerciseIndex, int setIndex) =>
        _completedSetLookup.Contains(new SetCompletion(exerciseIndex, setIndex));

    /// <summary>First uncompleted set index for an exercise, or -1 when the exercise is fully completed.</summary>
    public int GetFirstUncompletedSetIndex(int exerciseIndex)
    {
        if (CurrentPlan == null || exerciseIndex < 0 || exerciseIndex >= CurrentPlan.Exercises.Count)
            return -1;

        ExercisePlan exercise = CurrentPlan.Exercises[exerciseIndex];
        for (var s = 0; s < exercise.SetCount; s++)
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
            for (var si = 0; si < ex.SetCount; si++)
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
        if (setIndex < 0 || setIndex >= ex.SetCount) return;

        var completion = new SetCompletion(exerciseIndex, setIndex);
        _completedSets.Add(completion);
        _completedSetLookup.Add(completion);
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

    public bool TryRemoveSet(int exerciseIndex, int setIndex)
    {
        var target = new SetCompletion(exerciseIndex, setIndex);
        if (!_completedSetLookup.Remove(target))
            return false;
        _completedSets.Remove(target);
        return true;
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

    private void ClearCompletedSets()
    {
        _completedSets.Clear();
        _completedSetLookup.Clear();
    }

    private void StopRest()
    {
        _isResting = false;
        _userPaused = false;
        _pausedRemainingSeconds = null;
        _restEndsAtUtc = null;
        _activeRestDurationSeconds = 0;
    }
}
