using Physiquinator.Data;

namespace Physiquinator.Services;

/// <summary>Clears local SQLite data, demo flags, in-memory workout state, and the saved theme preference.</summary>
public sealed class AppDataResetService
{
    private readonly AppDatabase _database;
    private readonly IDemoSeedPreferences _demoSeedPreferences;
    private readonly WorkoutSessionService _sessionService;
    private readonly ThemeService _themeService;

    public AppDataResetService(
        AppDatabase database,
        IDemoSeedPreferences demoSeedPreferences,
        WorkoutSessionService sessionService,
        ThemeService themeService)
    {
        _database = database;
        _demoSeedPreferences = demoSeedPreferences;
        _sessionService = sessionService;
        _themeService = themeService;
    }

    public async Task ClearAllLocalDataAsync()
    {
        _sessionService.EndWorkout();
        await _database.ClearAllUserDataAsync().ConfigureAwait(false);
        _demoSeedPreferences.Set(DemoDataSeeder.InitialDemoSeedCompletedKey, true);
        _demoSeedPreferences.Set(DemoDataSeeder.DemoHistorySeedCompletedKey, true);
        await _themeService.ResetStoredPreferenceToSystemAsync().ConfigureAwait(true);
    }
}
