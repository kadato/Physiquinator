using Physiquinator.Data;

namespace Physiquinator.Services;

/// <summary>Clears local SQLite data, demo flags, in-memory workout state, and the saved theme preference.</summary>
public sealed class AppDataResetService
{
    private readonly AppDatabase _database;
    private readonly IDemoSeedPreferences _demoSeedPreferences;
    private readonly WorkoutSessionService _sessionService;
    private readonly ThemeService _themeService;
    private readonly RestAlertSettingsService _restAlertSettings;

    public AppDataResetService(
        AppDatabase database,
        IDemoSeedPreferences demoSeedPreferences,
        WorkoutSessionService sessionService,
        ThemeService themeService,
        RestAlertSettingsService restAlertSettings)
    {
        _database = database;
        _demoSeedPreferences = demoSeedPreferences;
        _sessionService = sessionService;
        _themeService = themeService;
        _restAlertSettings = restAlertSettings;
    }

    public async Task ClearAllLocalDataAsync()
    {
        _sessionService.EndWorkout();
        await _database.ClearAllUserDataAsync().ConfigureAwait(false);
        _demoSeedPreferences.Set(DemoDataSeeder.InitialDemoSeedCompletedKey, true);
        _demoSeedPreferences.Set(DemoDataSeeder.DemoHistorySeedCompletedKey, true);
        await _themeService.ResetStoredPreferenceToSystemAsync().ConfigureAwait(true);
        await _restAlertSettings.SetEnabledAsync(true).ConfigureAwait(true);
    }
}
