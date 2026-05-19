using Physiquinator.Data;

namespace Physiquinator.Services;

/// <summary>Clears local SQLite data, in-memory workout state, and the saved theme preference. Does not re-enable demo seeding.</summary>
public sealed class AppDataResetService
{
    private readonly AppDatabase _database;
    private readonly WorkoutSessionService _sessionService;
    private readonly ThemeService _themeService;
    private readonly RestAlertSettingsService _restAlertSettings;

    public AppDataResetService(
        AppDatabase database,
        WorkoutSessionService sessionService,
        ThemeService themeService,
        RestAlertSettingsService restAlertSettings)
    {
        _database = database;
        _sessionService = sessionService;
        _themeService = themeService;
        _restAlertSettings = restAlertSettings;
    }

    public async Task ClearAllLocalDataAsync()
    {
        _sessionService.EndWorkout();
        await _database.ClearAllUserDataAsync().ConfigureAwait(false);
        await _themeService.ResetStoredPreferenceToSystemAsync().ConfigureAwait(true);
        await _restAlertSettings.SetEnabledAsync(true).ConfigureAwait(true);
    }
}
