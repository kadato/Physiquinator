using Microsoft.Extensions.DependencyInjection;
using Physiquinator.Core.Data;

namespace Physiquinator.Core.Services;

/// <summary>
/// Registers the shared Physiquinator services used by both the MAUI and Blazor Web hosts.
/// Platform-specific services (notifications, vibration, file transfer) are registered by each host.
/// </summary>
public static class PhysiquinatorServiceCollectionExtensions
{
    public static IServiceCollection AddPhysiquinatorServices(
        this IServiceCollection services,
        IAppPreferences preferences,
        IDatabasePathProvider databasePathProvider)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(databasePathProvider);

        services.AddSingleton(preferences);
        services.AddSingleton(databasePathProvider);
        services.AddSingleton(TimeProvider.System);

        var activeIdStr = preferences.Get(PreferenceKeys.ActiveProfileId, string.Empty);
        Guid activeId = Guid.TryParse(activeIdStr, out Guid g) ? g : UserProfileService.DemoProfileId;
        services.AddSingleton(new AppDatabase(databasePathProvider.GetDatabasePath(activeId)));

        services.AddSingleton<WorkoutPlanRepository>();
        services.AddSingleton<WorkoutHistoryRepository>();
        services.AddSingleton<WorkoutHistoryService>();
        services.AddSingleton<WorkoutPlanService>();
        services.AddSingleton<WorkoutStatsService>();
        services.AddSingleton<WorkoutSessionService>();
        services.AddSingleton<RestAlertSettingsService>();
        services.AddSingleton<IDemoSeedPreferences, ScopedDemoSeedPreferences>();
        services.AddSingleton<DemoDataSeeder>();
        services.AddScoped<AppDataResetService>();
        services.AddScoped<ThemeService>();
        services.AddScoped<IThemeInitialization>(sp => sp.GetRequiredService<ThemeService>());
        services.AddScoped<AppInitializationService>();
        services.AddScoped<WorkoutTimerInterop>();
        services.AddSingleton<UserProfileService>();

        return services;
    }
}
