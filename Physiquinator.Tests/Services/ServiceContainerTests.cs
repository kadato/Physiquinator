using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Physiquinator.Core.Data;
using Physiquinator.Core.Services;
using Physiquinator.Core.Services.Ai;
using Physiquinator.Tests.TestDoubles;
using Xunit;

namespace Physiquinator.Tests.Services;

/// <summary>
/// Guards the production DI graph built by <see cref="PhysiquinatorServiceCollectionExtensions.AddPhysiquinatorServices"/>:
/// every service the extension registers must resolve without circular
/// dependencies (regression: RestNotificationService and RestAlertSettingsService
/// were mutually dependent via constructors) and with the lifetimes the
/// extension declares.
/// </summary>
public class ServiceContainerTests
{
    private sealed class NoopNotificationService(RestAlertSettingsService settings) : INotificationService
    {
        // Mirrors the production RestNotificationService dependency so the DI
        // container graph exercises the same constructor cycle it does.
        public RestAlertSettingsService Settings { get; } = settings;

        public Task EnsurePermissionAsync() => Task.CompletedTask;

        public bool SupportsNotifications => false;

        public bool SupportsOverlay => false;

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
    }

    private sealed class NoopFileTransferService : IFileTransferService
    {
        public Task ExportJsonAsync(string fileName, string json, string shareTitle = "Export Workout Plan") => Task.CompletedTask;

        public Task ExportImageAsync(string fileName, byte[] pngBytes, string shareTitle = "Share") => Task.CompletedTask;

        public Task<string?> PickJsonAsync(string pickerTitle) => Task.FromResult<string?>(null);
    }

    private static ServiceProvider BuildContainer()
    {
        var dbPathProvider = new TempDbPathProvider(
            Path.Combine(Path.GetTempPath(), $"physiq-test-{Guid.NewGuid():N}.db3"));
        var services = new ServiceCollection();
        services.AddPhysiquinatorServices(new InMemoryPreferences(), dbPathProvider);
        // Platform services the extension intentionally leaves to each host
        // (MauiProgram / Web Program), plus IJSRuntime that the hosts get from
        // the Blazor framework.
        services.AddSingleton<INotificationService, NoopNotificationService>();
        services.AddSingleton<IVibrationService>(new NoopVibration());
        services.AddSingleton<IFileTransferService>(new NoopFileTransferService());
        services.AddSingleton<IJSRuntime>(new NoopJSRuntime());
        return services.BuildServiceProvider();
    }

    private static readonly Type[] RegisteredServiceTypes =
    [
        typeof(AppDatabase),
        typeof(WorkoutPlanRepository),
        typeof(WorkoutHistoryRepository),
        typeof(WorkoutHistoryService),
        typeof(WorkoutPlanService),
        typeof(WorkoutStatsService),
        typeof(WorkoutSessionService),
        typeof(WorkoutQuickActionService),
        typeof(RestAlertSettingsService),
        typeof(AppUpdateSettingsService),
        typeof(WorkoutScheduleService),
        typeof(RestTimerCoordinator),
        typeof(IDemoSeedPreferences),
        typeof(DemoDataSeeder),
        typeof(AppDataResetService),
        typeof(BackupRestoreService),
        typeof(ThemeService),
        typeof(IThemeInitialization),
        typeof(AppInitializationService),
        typeof(WorkoutTimerInterop),
        typeof(UserProfileService),
        typeof(WeightUnitService),
        typeof(OpenAiCompatibleClient),
        typeof(AiToolRegistry),
        typeof(AiAssistantService),
    ];

    [Fact]
    public async Task EveryServiceRegisteredByExtension_Resolves()
    {
        await using ServiceProvider provider = BuildContainer();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();

        foreach (Type type in RegisteredServiceTypes)
        {
            Assert.NotNull(scope.ServiceProvider.GetRequiredService(type));
        }

        var tools = scope.ServiceProvider.GetRequiredService<IEnumerable<IAiTool>>().ToList();
        Assert.NotEmpty(tools);
        Assert.All(tools, Assert.NotNull);
    }

    [Fact]
    public void StatefulServices_RegisteredAsSingletons_ShareOneInstance()
    {
        using ServiceProvider provider = BuildContainer();

        Assert.Same(provider.GetRequiredService<AppDatabase>(), provider.GetRequiredService<AppDatabase>());
        Assert.Same(provider.GetRequiredService<WorkoutSessionService>(), provider.GetRequiredService<WorkoutSessionService>());
        Assert.Same(provider.GetRequiredService<UserProfileService>(), provider.GetRequiredService<UserProfileService>());
        Assert.Same(provider.GetRequiredService<WorkoutPlanService>(), provider.GetRequiredService<WorkoutPlanService>());
        Assert.Same(provider.GetRequiredService<WorkoutPlanRepository>(), provider.GetRequiredService<WorkoutPlanRepository>());
        Assert.Same(provider.GetRequiredService<WorkoutHistoryRepository>(), provider.GetRequiredService<WorkoutHistoryRepository>());
        Assert.Same(provider.GetRequiredService<RestAlertSettingsService>(), provider.GetRequiredService<RestAlertSettingsService>());
        Assert.Same(provider.GetRequiredService<RestTimerCoordinator>(), provider.GetRequiredService<RestTimerCoordinator>());
    }

    [Fact]
    public async Task ScopedServices_GetFreshInstancePerScope()
    {
        await using ServiceProvider provider = BuildContainer();
        await using AsyncServiceScope scope1 = provider.CreateAsyncScope();
        await using AsyncServiceScope scope2 = provider.CreateAsyncScope();

        ThemeService theme1 = scope1.ServiceProvider.GetRequiredService<ThemeService>();
        Assert.Same(theme1, scope1.ServiceProvider.GetRequiredService<ThemeService>());
        Assert.NotSame(theme1, scope2.ServiceProvider.GetRequiredService<ThemeService>());

        AiToolRegistry registry1 = scope1.ServiceProvider.GetRequiredService<AiToolRegistry>();
        Assert.NotSame(registry1, scope2.ServiceProvider.GetRequiredService<AiToolRegistry>());

        AiAssistantService assistant1 = scope1.ServiceProvider.GetRequiredService<AiAssistantService>();
        Assert.NotSame(assistant1, scope2.ServiceProvider.GetRequiredService<AiAssistantService>());

        AppDataResetService reset1 = scope1.ServiceProvider.GetRequiredService<AppDataResetService>();
        Assert.NotSame(reset1, scope2.ServiceProvider.GetRequiredService<AppDataResetService>());
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
