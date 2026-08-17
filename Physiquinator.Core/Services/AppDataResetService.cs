using Physiquinator.Core.Data;

namespace Physiquinator.Core.Services;

/// <summary>Clears local SQLite data, in-memory workout state, and the saved theme preference. Does not re-enable demo seeding.</summary>
public sealed class AppDataResetService(
    AppDatabase database,
    WorkoutSessionService sessionService,
    ThemeService themeService,
    RestAlertSettingsService restAlertSettings,
    WorkoutScheduleService scheduleService,
    WorkoutPlanService planService)
{
    public async Task ClearAllLocalDataAsync()
    {
        sessionService.EndWorkout();
        await database.ClearAllUserDataAsync().ConfigureAwait(false);
        await scheduleService.ResetCacheAsync().ConfigureAwait(false);
        planService.InvalidatePlanCache();
        await themeService.ResetStoredPreferenceToSystemAsync().ConfigureAwait(true);
        restAlertSettings.SetEnabled(true);
    }
}
