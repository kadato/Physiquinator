using Microsoft.Extensions.DependencyInjection;
using Physiquinator.Core.Data;
using Physiquinator.Core.Services.Ai;
using Physiquinator.Core.Services.Ai.Tools;

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
        services.AddSingleton<WorkoutQuickActionService>();
        services.AddSingleton<RestAlertSettingsService>();
        services.AddSingleton<AppUpdateSettingsService>();
        services.AddSingleton<WorkoutScheduleService>();
        services.AddSingleton<RestTimerCoordinator>();
        services.AddSingleton<IDemoSeedPreferences, ScopedDemoSeedPreferences>();
        services.AddSingleton<DemoDataSeeder>();
        services.AddScoped<AppDataResetService>();
        services.AddScoped<ThemeService>();
        services.AddScoped<IThemeInitialization>(sp => sp.GetRequiredService<ThemeService>());
        services.AddScoped<AppInitializationService>();
        services.AddScoped<WorkoutTimerInterop>();
        services.AddSingleton<UserProfileService>();

        // AI Services & Tools (Registered as Scoped to safely consume scoped dependencies like ThemeService)
        services.AddScoped(sp => new OpenAiCompatibleClient(sp.GetService<HttpClient>() ?? new HttpClient()));

        services.AddScoped<IAiTool, GetWorkoutPlansTool>();
        services.AddScoped<IAiTool, CreateWorkoutPlanTool>();
        services.AddScoped<IAiTool, UpdateWorkoutPlanTool>();
        services.AddScoped<IAiTool, DeleteWorkoutPlanTool>();
        services.AddScoped<IAiTool, GetBodyweightHistoryTool>();
        services.AddScoped<IAiTool, LogBodyweightTool>();
        services.AddScoped<IAiTool, DeleteBodyweightTool>();
        services.AddScoped<IAiTool, GetWorkoutHistoryStatsTool>();
        services.AddScoped<IAiTool, GetExerciseProgressionTool>();
        services.AddScoped<IAiTool, GetAppSettingsTool>();
        services.AddScoped<IAiTool, ChangeAppThemeTool>();
        services.AddScoped<IAiTool, UpdateRestTimerSettingsTool>();
        services.AddScoped<IAiTool, UpdateWorkoutScheduleTool>();
        services.AddScoped<IAiTool, GenerateDeloadPlanWorkflowTool>();
        services.AddScoped<IAiTool, CalculateProgressiveOverloadWorkflowTool>();
        services.AddScoped<AiToolRegistry>();
        services.AddScoped<AiAssistantService>();

        return services;
    }
}
