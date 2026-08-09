using Physiquinator.Core.Models;
using Physiquinator.Core.Serialization;
using System.Text.Json;

namespace Physiquinator.Core.Services;

/// <summary>
/// Keeps the platform surfaces in sync with <see cref="WorkoutSessionService"/>:
/// the ongoing notification with quick actions, the floating overlay and the
/// exact rest-end alarm. The UI follows the whole workout lifecycle (active
/// while a workout is running, not only during rest). Every rest state change
/// is persisted as a snapshot, so a rest countdown survives process death and
/// is restored when the workout page loads again.
/// </summary>
public sealed class RestTimerCoordinator : IDisposable
{
    private readonly WorkoutSessionService _session;
    private readonly INotificationService _notifications;
    private readonly RestAlertSettingsService _settings;
    private readonly IAppPreferences _preferences;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _syncGate = new(1, 1);

    // Last rest state processed by SyncAsync, so per-tick re-syncs skip the
    // platform work (snapshot persist, exact-alarm re-arm, overlay restart).
    private DateTime? _lastSyncedRestEndUtc;
    private bool _lastSyncedResting;
    private bool _hasSynced;

    public RestTimerCoordinator(
        WorkoutSessionService session,
        INotificationService notifications,
        RestAlertSettingsService settings,
        IAppPreferences preferences,
        TimeProvider time)
    {
        _session = session;
        _notifications = notifications;
        _settings = settings;
        _preferences = preferences;
        _time = time;

        _session.RestStateChanged += OnRestStateChanged;
        _session.WorkoutStateChanged += OnWorkoutStateChanged;
        _settings.Changed += OnSettingsChanged;
    }

    public void Dispose()
    {
        _session.RestStateChanged -= OnRestStateChanged;
        _session.WorkoutStateChanged -= OnWorkoutStateChanged;
        _settings.Changed -= OnSettingsChanged;
        _syncGate.Dispose();
    }

    /// <summary>
    /// Hides any workout timer UI left over from a previous process instance.
    /// Called once at app startup; the snapshot and alarm are left untouched
    /// so a still-running rest keeps notifying even after process death.
    /// </summary>
    public void EnsureInitialState()
    {
        if (_session.CurrentPlan != null)
            return;

        _ = HideAsync();
    }

    private async Task HideAsync()
    {
        try
        {
            await _notifications.HideWorkoutTimerUiAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RestTimerCoordinator hide failed: {ex}");
        }
    }

    /// <summary>
    /// Reads the persisted rest snapshot without consuming it. The workout
    /// page calls this before loading a workout (which stops the rest timer
    /// and would otherwise clear the snapshot), then restores it afterwards
    /// via <see cref="RestoreRestIfPending(string)"/>.
    /// </summary>
    public string? CapturePendingSnapshot()
    {
        var raw = _preferences.Get(PreferenceKeys.RestTimerSnapshot, string.Empty);
        return string.IsNullOrEmpty(raw) ? null : raw;
    }

    /// <summary>
    /// Restores the persisted rest countdown into the session. Called by the
    /// workout page after the in-progress workout has been loaded (the
    /// countdown belongs to that workout). Returns true when a running rest
    /// was restored.
    /// </summary>
    public bool RestoreRestIfPending() => RestoreRestIfPending(null);

    /// <summary>
    /// Restores a rest countdown from a snapshot captured before the workout
    /// was loaded (<see cref="CapturePendingSnapshot"/>). Falls back to the
    /// persisted snapshot when no captured value is provided.
    /// </summary>
    public bool RestoreRestIfPending(string? capturedRaw)
    {
        RestTimerSnapshot? snapshot = capturedRaw != null ? TryParseSnapshot(capturedRaw) : ReadSnapshot();
        if (snapshot == null)
            return false;

        DateTime now = _time.GetUtcNow().UtcDateTime;

        if (snapshot.EndUtc == null || snapshot.EndUtc.Value <= now)
        {
            ClearSnapshot();
            return false;
        }

        if (!_session.RestoreRestState(snapshot.EndUtc.Value, snapshot.ActiveRestDurationSeconds))
        {
            ClearSnapshot();
            return false;
        }

        return true;
    }

    /// <summary>
    /// Invoked by the Android exact-alarm receiver when the rest-end alarm
    /// fires, including from a cold start after process death. Completes the
    /// rest when it really expired and shows the completion alert.
    /// </summary>
    public void HandleRestEndAlarmFired()
    {
        if (_session.TryCompleteRestIfExpired())
        {
            ShowRestCompleteIfEnabled();
            return;
        }

        RestTimerSnapshot? snapshot = ReadSnapshot();
        if (snapshot != null && snapshot.EndUtc != null
            && snapshot.EndUtc.Value <= _time.GetUtcNow().UtcDateTime)
        {
            ClearSnapshot();
            ShowRestCompleteIfEnabled();
            return;
        }

        OnRestStateChanged(this, EventArgs.Empty);
    }

    private void ShowRestCompleteIfEnabled()
    {
        if (!_settings.Enabled)
            return;

        _ = _notifications.ShowRestCompleteNowAsync(BuildRestCompleteDescription());
    }

    private void OnRestStateChanged(object? sender, EventArgs e) => _ = SyncAsync();

    private void OnWorkoutStateChanged(object? sender, EventArgs e) => _ = SyncAsync();

    private void OnSettingsChanged() => _ = SyncAsync();

    private async Task SyncAsync()
    {
        await _syncGate.WaitAsync();
        try
        {
            if (_session.CurrentPlan == null)
            {
                _hasSynced = false;
                _lastSyncedRestEndUtc = null;
                _lastSyncedResting = false;
                ClearSnapshot();
                await _notifications.HideWorkoutTimerUiAsync();
                await _notifications.CancelRestEndAlarmAsync();
                return;
            }

            WorkoutTimerState state = BuildState();

            // Only re-run platform work when the rest end time (or the
            // resting flag) actually changed since the last sync. The 500 ms
            // session tick and repeated settings events therefore no longer
            // re-persist the snapshot, re-arm the exact alarm or restart the
            // overlay service for an unchanged countdown.
            DateTime? restEnd = _session.IsResting ? state.RestEndsAtUtc : null;
            if (_hasSynced && restEnd == _lastSyncedRestEndUtc && _session.IsResting == _lastSyncedResting)
                return;

            _hasSynced = true;
            _lastSyncedRestEndUtc = restEnd;
            _lastSyncedResting = _session.IsResting;

            // Rest state survives process death; between-set state is rebuilt
            // from the persisted workout session, so only rest needs a snapshot.
            if (_session.IsResting)
                PersistSnapshot(state);
            else
                ClearSnapshot();

            if (!_settings.Enabled)
            {
                await _notifications.HideWorkoutTimerUiAsync();
                await _notifications.CancelRestEndAlarmAsync();
                return;
            }

            await _notifications.ShowWorkoutTimerUiAsync(state);

            if (state.RestEndsAtUtc is { } end)
                await _notifications.ScheduleRestEndAlarmAsync(end, state.PlanName ?? NotificationConstants.DefaultFallbackPlanName, BuildRestCompleteDescription());
            else
                await _notifications.CancelRestEndAlarmAsync();
        }
        catch (Exception ex)
        {
            // Platform surfaces must never break the workout flow
            System.Diagnostics.Debug.WriteLine($"RestTimerCoordinator sync failed: {ex}");
        }
        finally
        {
            _syncGate.Release();
        }
    }

    private WorkoutTimerState BuildState()
    {
        WorkoutPlan? plan = _session.CurrentPlan;

        var exerciseIndex = _session.GetFirstUncompletedExerciseIndex();
        string? nextExerciseName = null;
        int? nextSetIndex = null;
        int? nextSetTotal = null;
        if (plan != null && exerciseIndex >= 0 && exerciseIndex < plan.Exercises.Count)
        {
            ExercisePlan exercise = plan.Exercises[exerciseIndex];
            nextExerciseName = exercise.Name;
            nextSetTotal = exercise.TotalSetCount;
            var setIndex = _session.GetFirstUncompletedSetIndex(exerciseIndex);
            nextSetIndex = setIndex >= 0 ? setIndex + 1 : null;
        }

        return new WorkoutTimerState(
            plan?.Name,
            _session.RestEndsAtUtc,
            _session.RestSecondsRemaining,
            nextExerciseName,
            exerciseIndex >= 0 ? exerciseIndex : null,
            nextSetIndex,
            nextSetTotal);
    }

    private string BuildRestCompleteDescription()
    {
        var exerciseIndex = _session.GetFirstUncompletedExerciseIndex();
        WorkoutPlan? plan = _session.CurrentPlan;

        if (plan != null && exerciseIndex >= 0 && exerciseIndex < plan.Exercises.Count)
        {
            ExercisePlan exercise = plan.Exercises[exerciseIndex];
            var setIndex = _session.GetFirstUncompletedSetIndex(exerciseIndex);
            var setLabel = setIndex >= 0 ? $" · Set {setIndex + 1}/{exercise.TotalSetCount}" : string.Empty;
            return $"Next up: {exercise.Name}{setLabel}";
        }

        return "Rest done — time for your next set.";
    }

    private void PersistSnapshot(WorkoutTimerState state)
    {
        var snapshot = new RestTimerSnapshot
        {
            EndUtc = state.RestEndsAtUtc,
            ActiveRestDurationSeconds = _session.ActiveRestDurationSeconds
        };

        _preferences.Set(PreferenceKeys.RestTimerSnapshot, JsonSerializer.Serialize(snapshot, PhysiquinatorJsonContext.Default.RestTimerSnapshot));
    }

    private void ClearSnapshot() => _preferences.Set(PreferenceKeys.RestTimerSnapshot, string.Empty);

    private RestTimerSnapshot? ReadSnapshot()
    {
        var raw = _preferences.Get(PreferenceKeys.RestTimerSnapshot, string.Empty);
        return string.IsNullOrEmpty(raw) ? null : TryParseSnapshot(raw);
    }

    private static RestTimerSnapshot? TryParseSnapshot(string raw)
    {
        try
        {
            return JsonSerializer.Deserialize(raw, PhysiquinatorJsonContext.Default.RestTimerSnapshot);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
