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
        IDatabasePathProvider databasePathProvider,
        bool scopeStatefulServicesPerCircuit = false)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(databasePathProvider);

        services.AddSingleton(TimeProvider.System);

        if (scopeStatefulServicesPerCircuit)
        {
            services.AddScoped<IAppPreferences>(_ => CreatePerScopeInstance(preferences));
            services.AddScoped<IDatabasePathProvider>(_ => CreatePerScopeInstance(databasePathProvider));
            services.AddScoped(sp => new AppDatabase(
                sp.GetRequiredService<IDatabasePathProvider>().GetDatabasePath(
                    GetActiveProfileId(sp.GetRequiredService<IAppPreferences>()))));
        }
        else
        {
            services.AddSingleton(preferences);
            services.AddSingleton(databasePathProvider);
            services.AddSingleton(new AppDatabase(
                databasePathProvider.GetDatabasePath(GetActiveProfileId(preferences))));
        }

        AddStatefulService<WorkoutPlanRepository>();
        AddStatefulService<WorkoutHistoryRepository>();
        AddStatefulService<WorkoutHistoryService>();
        AddStatefulService<WorkoutPlanService>();
        AddStatefulService<WorkoutStatsService>();
        AddStatefulService<WorkoutSessionService>();
        AddStatefulService<WorkoutQuickActionService>();
        AddStatefulService<RestAlertSettingsService>();
        AddStatefulService<AppUpdateSettingsService>();
        AddStatefulService<WorkoutScheduleService>();
        AddStatefulService<RestTimerCoordinator>();
        AddStatefulServicePair<IDemoSeedPreferences, ScopedDemoSeedPreferences>();
        AddStatefulService<DemoDataSeeder>();
        services.AddScoped<AppDataResetService>();
        AddStatefulService<BackupRestoreService>();
        services.AddScoped<ThemeService>();
        services.AddScoped<IThemeInitialization>(sp => sp.GetRequiredService<ThemeService>());
        services.AddScoped<AppInitializationService>();
        services.AddScoped<WorkoutTimerInterop>();
        AddStatefulService<UserProfileService>();
        AddStatefulService<WeightUnitService>();

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

        void AddStatefulService<TService>() where TService : class
        {
            if (scopeStatefulServicesPerCircuit)
            {
                services.AddScoped<TService>();
            }
            else
            {
                services.AddSingleton<TService>();
            }
        }

        void AddStatefulServicePair<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService
        {
            if (scopeStatefulServicesPerCircuit)
            {
                services.AddScoped<TService, TImplementation>();
            }
            else
            {
                services.AddSingleton<TService, TImplementation>();
            }
        }
    }

    private static TService CreatePerScopeInstance<TService>(TService seed) where TService : class
        => (TService)Activator.CreateInstance(seed.GetType())!;

    private static Guid GetActiveProfileId(IAppPreferences preferences)
    {
        var activeIdStr = preferences.Get(PreferenceKeys.ActiveProfileId, string.Empty);
        return Guid.TryParse(activeIdStr, out Guid g) ? g : UserProfileService.DemoProfileId;
    }
}
