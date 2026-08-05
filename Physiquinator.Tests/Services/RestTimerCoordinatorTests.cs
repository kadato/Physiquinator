using Microsoft.Extensions.DependencyInjection;
using Physiquinator.Core.Data;
using Physiquinator.Core.Models;
using Physiquinator.Core.Services;
using Xunit;

namespace Physiquinator.Tests.Services;

/// <summary>
/// Guards the rest-timer coordinator: persisted snapshot round-trip, restore
/// after process death, expired-snapshot cleanup and alarm handling.
/// </summary>
public class RestTimerCoordinatorTests
{
    private sealed class FakeNotificationService : INotificationService
    {
        public List<WorkoutTimerState> ShownStates { get; } = [];

        public List<DateTime> ScheduledAlarms { get; } = [];

        public int HideUiCalls { get; private set; }

        public int CancelAlarmCalls { get; private set; }

        public int ShowCompleteCalls { get; private set; }

        public Task EnsurePermissionAsync() => Task.CompletedTask;

        public void CancelAllRestNotifications()
        {
        }

        public Task ShowRestCompleteNowAsync(string description)
        {
            ShowCompleteCalls++;
            return Task.CompletedTask;
        }

        public Task ShowWorkoutTimerUiAsync(WorkoutTimerState state)
        {
            ShownStates.Add(state);
            return Task.CompletedTask;
        }

        public Task HideWorkoutTimerUiAsync()
        {
            HideUiCalls++;
            return Task.CompletedTask;
        }

        public Task ScheduleRestEndAlarmAsync(DateTime restEndsAtUtc, string title, string description)
        {
            ScheduledAlarms.Add(restEndsAtUtc);
            return Task.CompletedTask;
        }

        public Task CancelRestEndAlarmAsync()
        {
            CancelAlarmCalls++;
            return Task.CompletedTask;
        }

        public Task ShowSetLoggedNotificationAsync(string exerciseName, int setIndex, int totalSets) => Task.CompletedTask;

        public Task CancelSetLoggedNotificationAsync() => Task.CompletedTask;
    }

    private sealed class InMemoryPreferences : IAppPreferences
    {
        private readonly Dictionary<string, string> _values = [];

        public string Get(string key, string defaultValue) =>
            _values.TryGetValue(key, out var value) ? value : defaultValue;

        public bool Get(string key, bool defaultValue)
        {
            if (!_values.TryGetValue(key, out var value))
                return defaultValue;

            return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
        }

        public void Set(string key, string value) => _values[key] = value;

        public void Set(string key, bool value) => _values[key] = value.ToString();
    }

    private sealed record Fixture(
        ManualTimeProvider Clock,
        WorkoutSessionService Session,
        FakeNotificationService Notifications,
        RestTimerCoordinator Coordinator,
        InMemoryPreferences Preferences);

    private static Fixture Build(DateTime startUtc, bool alertsEnabled = true)
    {
        var clock = new ManualTimeProvider();
        clock.SetUtcNow(new DateTimeOffset(startUtc, TimeSpan.Zero));

        var preferences = new InMemoryPreferences();
        if (!alertsEnabled)
            preferences.Set("rest_alerts_enabled", false);

        var session = new WorkoutSessionService(clock);
        var notifications = new FakeNotificationService();

        var coordinator = new RestTimerCoordinator(session, notifications, CreateSettings(preferences), preferences, clock);
        session.StartWorkout(SamplePlan());

        return new Fixture(clock, session, notifications, coordinator, preferences);
    }

    private static RestAlertSettingsService CreateSettings(InMemoryPreferences preferences)
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        return new RestAlertSettingsService(preferences, CreateUserProfileService(preferences), provider);
    }

    private static UserProfileService CreateUserProfileService(InMemoryPreferences preferences)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"physiq-coord-test-{Guid.NewGuid():N}.db3");
        return new UserProfileService(
            new AppDatabase(dbPath),
            new WorkoutSessionService(TimeProvider.System),
            preferences,
            new TempDbPathProvider(dbPath),
            TimeProvider.System);
    }

    private sealed class TempDbPathProvider(string path) : IDatabasePathProvider
    {
        public string GetDatabasePath(Guid profileId) => path;
    }

    private static WorkoutPlan SamplePlan() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test",
        Exercises =
        [
            new ExercisePlan { Name = "Squat", SetCount = 2, Order = 0, RestIntervalSeconds = 60 }
        ]
    };

    /// <summary>Simulates a process restart with only the persisted snapshot carried over.</summary>
    private static Fixture Restart(Fixture original, DateTime newNowUtc)
    {
        var clock = new ManualTimeProvider();
        clock.SetUtcNow(new DateTimeOffset(newNowUtc, TimeSpan.Zero));

        var preferences = new InMemoryPreferences();
        preferences.Set(PreferenceKeys.RestTimerSnapshot, original.Preferences.Get(PreferenceKeys.RestTimerSnapshot, string.Empty));

        var session = new WorkoutSessionService(clock);
        var notifications = new FakeNotificationService();
        var coordinator = new RestTimerCoordinator(session, notifications, CreateSettings(preferences), preferences, clock);

        return new Fixture(clock, session, notifications, coordinator, preferences);
    }

    [Fact]
    public void StartWorkout_shows_ready_state_without_alarm()
    {
        var fix = Build(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        var state = Assert.Single(fix.Notifications.ShownStates);
        Assert.Equal("Test", state.PlanName);
        Assert.Equal("Squat", state.NextExerciseName);
        Assert.Equal(1, state.NextSetIndex);
        Assert.Equal(2, state.NextSetTotal);
        Assert.Null(state.RestEndsAtUtc);
        Assert.Empty(fix.Notifications.ScheduledAlarms);
    }

    [Fact]
    public void StartRest_shows_timer_ui_and_schedules_alarm()
    {
        var fix = Build(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        fix.Session.StartRest(90);

        var state = fix.Notifications.ShownStates[^1];
        Assert.Equal(90, state.RestRemainingSeconds);
        Assert.NotNull(state.RestEndsAtUtc);
        Assert.Equal("Test", state.PlanName);

        var alarm = Assert.Single(fix.Notifications.ScheduledAlarms);
        Assert.Equal(fix.Clock.GetUtcNow().UtcDateTime.AddSeconds(90), alarm);
    }

    [Fact]
    public void SkipRest_shows_ready_state_and_cancels_alarm()
    {
        var fix = Build(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        fix.Session.StartRest(90);

        fix.Session.SkipRest();

        // Workout is still active, so the ready state is re-shown instead of hidden.
        Assert.Equal(0, fix.Notifications.HideUiCalls);
        Assert.True(fix.Notifications.CancelAlarmCalls > 0);

        var state = fix.Notifications.ShownStates[^1];
        Assert.Null(state.RestEndsAtUtc);
        Assert.Equal("Squat", state.NextExerciseName);
        Assert.Equal(1, state.NextSetIndex);
    }

    [Fact]
    public void EndWorkout_hides_ui_cancels_alarm_and_clears_snapshot()
    {
        var fix = Build(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        fix.Session.StartRest(90);
        int hidesBefore = fix.Notifications.HideUiCalls;

        fix.Session.EndWorkout();

        Assert.True(fix.Notifications.HideUiCalls > hidesBefore);
        Assert.True(fix.Notifications.CancelAlarmCalls > 0);

        // No snapshot left to restore.
        Assert.False(fix.Coordinator.RestoreRestIfPending());
    }

    [Fact]
    public void RestoreRestIfPending_restores_running_rest_from_snapshot()
    {
        var fix = Build(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        fix.Session.StartRest(90);

        // 40 seconds later the process restarts; only the snapshot survives.
        // The page flow: capture, load the workout, restore.
        var restarted = Restart(fix, new DateTime(2026, 1, 1, 12, 0, 40, DateTimeKind.Utc));
        string? captured = restarted.Coordinator.CapturePendingSnapshot();
        restarted.Session.StartWorkout(SamplePlan());

        Assert.True(restarted.Coordinator.RestoreRestIfPending(captured));
        Assert.True(restarted.Session.IsResting);
        Assert.Equal(50, restarted.Session.RestSecondsRemaining);

        // Ready state was shown on load, rest state after the restore.
        Assert.Equal(2, restarted.Notifications.ShownStates.Count);
        Assert.Equal("Test", restarted.Notifications.ShownStates[^1].PlanName);
        Assert.Equal(50, restarted.Notifications.ShownStates[^1].RestRemainingSeconds);
        Assert.Single(restarted.Notifications.ScheduledAlarms);
    }

    [Fact]
    public void RestoreRestIfPending_returns_false_and_clears_expired_snapshot()
    {
        var fix = Build(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        fix.Session.StartRest(10);

        // Rest expired while the process was dead.
        var restarted = Restart(fix, new DateTime(2026, 1, 1, 12, 0, 30, DateTimeKind.Utc));

        Assert.False(restarted.Coordinator.RestoreRestIfPending());
        Assert.False(restarted.Session.IsResting);

        // Snapshot was consumed: a second call finds nothing.
        Assert.False(restarted.Coordinator.RestoreRestIfPending());
    }

    [Fact]
    public void HandleRestEndAlarmFired_completes_rest_and_clears_snapshot_on_cold_start()
    {
        var fix = Build(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        fix.Session.StartRest(10);

        // Fresh process; the OS-held alarm fires after the rest already ended.
        var restarted = Restart(fix, new DateTime(2026, 1, 1, 12, 0, 30, DateTimeKind.Utc));
        restarted.Coordinator.HandleRestEndAlarmFired();

        Assert.Equal(1, restarted.Notifications.ShowCompleteCalls);
        Assert.False(restarted.Session.IsResting);

        // Snapshot consumed: restoring later finds nothing.
        Assert.False(restarted.Coordinator.RestoreRestIfPending());
    }

    [Fact]
    public void Cold_start_page_load_resume_workout_keeps_pending_snapshot()
    {
        var fix = Build(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        fix.Session.StartRest(90);

        // Process restarts; the user opens the workout page. The page captures
        // the snapshot, loads the in-progress workout (ResumeWorkout) and only
        // then restores the rest.
        var restarted = Restart(fix, new DateTime(2026, 1, 1, 12, 0, 40, DateTimeKind.Utc));
        string? captured = restarted.Coordinator.CapturePendingSnapshot();
        Assert.NotNull(captured);

        restarted.Session.ResumeWorkout(SamplePlan(), []);
        Assert.True(restarted.Coordinator.RestoreRestIfPending(captured));
        Assert.True(restarted.Session.IsResting);
        Assert.Equal(50, restarted.Session.RestSecondsRemaining);
    }

    [Fact]
    public void RestoreRestIfPending_falls_back_to_persisted_snapshot()
    {
        var fix = Build(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        fix.Session.StartRest(90);

        var restarted = Restart(fix, new DateTime(2026, 1, 1, 12, 0, 40, DateTimeKind.Utc));
        Assert.True(restarted.Coordinator.RestoreRestIfPending());
        Assert.Equal(50, restarted.Session.RestSecondsRemaining);
    }

    [Fact]
    public void AlertsDisabled_hides_ui_but_keeps_snapshot_for_restore()
    {
        var fix = Build(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc), alertsEnabled: false);
        fix.Session.StartRest(90);

        Assert.Empty(fix.Notifications.ShownStates);
        Assert.Empty(fix.Notifications.ScheduledAlarms);
        Assert.True(fix.Notifications.HideUiCalls > 0);

        // Rest state is still persisted so it survives process death.
        var restarted = Restart(fix, new DateTime(2026, 1, 1, 12, 0, 10, DateTimeKind.Utc));
        Assert.True(restarted.Coordinator.RestoreRestIfPending());
        Assert.True(restarted.Session.IsResting);
    }
}
