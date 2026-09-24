using Microsoft.Extensions.DependencyInjection;
using Physiquinator.Core.Data;
using Physiquinator.Core.Models;
using Physiquinator.Core.Services;
using Physiquinator.Tests.TestDoubles;
using Xunit;

namespace Physiquinator.Tests.Services;

public class RestAlertSettingsServiceTests
{
    private static (RestAlertSettingsService Settings, InMemoryPreferences Preferences) CreateSettings()
    {
        var preferences = new InMemoryPreferences();
        var dbPath = Path.Combine(Path.GetTempPath(), $"physiq-settings-test-{Guid.NewGuid():N}.db3");
        var userProfileService = new UserProfileService(
            new AppDatabase(dbPath),
            new WorkoutSessionService(TimeProvider.System),
            preferences,
            new TempDbPathProvider(dbPath),
            TimeProvider.System);

        var services = new ServiceCollection();
        services.AddSingleton<INotificationService, NoopNotificationService>();
        var provider = services.BuildServiceProvider();

        var settings = new RestAlertSettingsService(preferences, userProfileService, provider);
        return (settings, preferences);
    }

    private sealed class NoopNotificationService : INotificationService
    {
        public bool SupportsNotifications => false;
        public bool SupportsOverlay => false;
        public bool HasOverlayPermission() => false;
        public Task EnsurePermissionAsync() => Task.CompletedTask;
        public Task RequestOverlayPermissionAsync() => Task.CompletedTask;
        public void CancelAllRestNotifications() { }
        public Task ShowWorkoutTimerUiAsync(WorkoutTimerState state) => Task.CompletedTask;
        public Task HideWorkoutTimerUiAsync() => Task.CompletedTask;
        public Task ScheduleRestEndAlarmAsync(DateTime restEndsAtUtc, string title, string description) => Task.CompletedTask;
        public Task CancelRestEndAlarmAsync() => Task.CompletedTask;
        public Task ShowRestCompleteNowAsync(string description) => Task.CompletedTask;
    }

    [Fact]
    public void WakeLockMode_DefaultsToRestOnly()
    {
        var (settings, _) = CreateSettings();
        Assert.Equal(WorkoutWakeLockMode.RestOnly, settings.WakeLockMode);
    }

    [Theory]
    [InlineData(WorkoutWakeLockMode.Always)]
    [InlineData(WorkoutWakeLockMode.Never)]
    [InlineData(WorkoutWakeLockMode.RestOnly)]
    public void WakeLockMode_RoundTripsAndFiresChanged(WorkoutWakeLockMode mode)
    {
        var (settings, _) = CreateSettings();
        var changedFired = 0;
        settings.Changed += () => changedFired++;

        settings.SetWakeLockMode(mode);

        Assert.Equal(mode, settings.WakeLockMode);
        Assert.Equal(1, changedFired);
    }

    [Fact]
    public void WakeLockMode_InvalidPreference_FallsBackToRestOnly()
    {
        var (settings, preferences) = CreateSettings();
        preferences.Set("workout_wake_lock_mode", "InvalidValue");

        Assert.Equal(WorkoutWakeLockMode.RestOnly, settings.WakeLockMode);
    }
}
