using Microsoft.Extensions.DependencyInjection;
using Physiquinator.Core.Data;
using Physiquinator.Core.Services;
using Physiquinator.Tests.TestDoubles;
using Xunit;

namespace Physiquinator.Tests.Services;

/// <summary>
/// Guards the production DI graph: rest-alert services must resolve without
/// circular dependencies (regression: RestNotificationService and
/// RestAlertSettingsService were mutually dependent via constructors).
/// </summary>
public class ServiceContainerTests
{
    private sealed class NoopNotificationService(RestAlertSettingsService settings) : INotificationService
    {
        // Mirrors the production RestNotificationService dependency so the DI
        // container graph exercises the same constructor cycle it does.
        public RestAlertSettingsService Settings { get; } = settings;

        public Task EnsurePermissionAsync() => Task.CompletedTask;

        public bool HasOverlayPermission() => true;

        public Task RequestOverlayPermissionAsync() => Task.CompletedTask;

        public void CancelAllRestNotifications()
        {
        }

        public Task ShowRestCompleteNowAsync(string description) => Task.CompletedTask;

        public Task ShowWorkoutTimerUiAsync(Physiquinator.Core.Models.WorkoutTimerState state) => Task.CompletedTask;

        public Task HideWorkoutTimerUiAsync() => Task.CompletedTask;

        public Task ScheduleRestEndAlarmAsync(DateTime restEndsAtUtc, string title, string description) => Task.CompletedTask;

        public Task CancelRestEndAlarmAsync() => Task.CompletedTask;

        public Task ShowSetLoggedNotificationAsync(string exerciseName, int setIndex, int totalSets) => Task.CompletedTask;

        public Task CancelSetLoggedNotificationAsync() => Task.CompletedTask;
    }

    private static ServiceProvider BuildContainer()
    {
        var dbPathProvider = new TempDbPathProvider(
            Path.Combine(Path.GetTempPath(), $"physiq-test-{Guid.NewGuid():N}.db3"));
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAppPreferences, InMemoryPreferences>();
        services.AddSingleton<IDatabasePathProvider>(dbPathProvider);
        services.AddSingleton(new AppDatabase(dbPathProvider.GetDatabasePath(UserProfileService.DemoProfileId)));
        services.AddSingleton<WorkoutPlanRepository>();
        services.AddSingleton<WorkoutHistoryRepository>();
        services.AddSingleton<WorkoutHistoryService>();
        services.AddSingleton<WorkoutPlanService>();
        services.AddSingleton<WorkoutStatsService>();
        services.AddSingleton<WorkoutSessionService>();
        services.AddSingleton<RestAlertSettingsService>();
        services.AddSingleton<INotificationService, NoopNotificationService>();
        services.AddSingleton<IVibrationService>(_ => new NoopVibration());
        services.AddSingleton<IDemoSeedPreferences>(_ => new MemoryDemoSeedPreferences());
        services.AddSingleton<DemoDataSeeder>();
        services.AddSingleton<UserProfileService>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void RestAlertServices_Resolve_WithoutCircularDependency()
    {
        using ServiceProvider provider = BuildContainer();

        RestAlertSettingsService settings = provider.GetRequiredService<RestAlertSettingsService>();
        INotificationService notifications = provider.GetRequiredService<INotificationService>();

        Assert.True(settings.Enabled);
        Assert.NotNull(notifications);
    }

    [Fact]
    public void AddTimeSeconds_defaults_to_30_and_clamps_to_range()
    {
        using ServiceProvider provider = BuildContainer();
        RestAlertSettingsService settings = provider.GetRequiredService<RestAlertSettingsService>();

        Assert.Equal(30, settings.AddTimeSeconds);

        settings.SetAddTimeSeconds(120);
        Assert.Equal(120, settings.AddTimeSeconds);

        settings.SetAddTimeSeconds(2);
        Assert.Equal(5, settings.AddTimeSeconds);

        settings.SetAddTimeSeconds(9999);
        Assert.Equal(300, settings.AddTimeSeconds);
    }
}
