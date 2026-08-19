namespace Physiquinator.Core.Services;

/// <summary>
/// Coordinates first-launch theme setup and one-time demo data seeding before pages load.
/// </summary>
public sealed class AppInitializationService(
    IThemeInitialization theme,
    DemoDataSeeder demoSeeder,
    IDemoSeedPreferences preferences,
    WorkoutScheduleService scheduleService)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Task? _initializationTask;

    public bool IsReady { get; private set; }

    public bool ShowSetupOverlay { get; private set; }

    public event Action? ProgressChanged;

    public Task EnsureInitializedAsync() =>
        _initializationTask ??= InitializeCoreAsync();

    private async Task InitializeCoreAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (IsReady)
            {
                return;
            }

            await Task.WhenAll(
                theme.EnsureInitializedAsync(),
                scheduleService.EnsureLoadedAsync()
            ).ConfigureAwait(false);

            if (preferences.IsDefaultProfile)
            {
                var didSeedPlans = await demoSeeder.SeedDemoDataIfNeededAsync().ConfigureAwait(false);

                if (didSeedPlans)
                {
                    ShowSetupOverlay = true;
                    NotifyProgress();
                }

                // History and extras are independent of each other; run in
                // parallel so the total first-launch seeding time is the max
                // rather than the sum.
                var historyTask = demoSeeder.SeedDemoHistoryIfNeededAsync();
                var extrasTask = demoSeeder.SeedDemoExtrasIfNeededAsync();

                await Task.WhenAll(historyTask, extrasTask).ConfigureAwait(false);

                var didSeedHistory = historyTask.Result;
                var didSeedExtras = extrasTask.Result;

                if (didSeedPlans || didSeedHistory || didSeedExtras)
                {
                    // Set flag to show onboarding modal explaining that demo data was seeded
                    preferences.Set(PreferenceKeys.ShowFirstTimeSeedModal, true);

                    ShowSetupOverlay = false;
                    NotifyProgress();
                }
            }

            IsReady = true;
            NotifyProgress();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ReinitializeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            IsReady = false;
            _initializationTask = null;
        }
        finally
        {
            _gate.Release();
        }
        await EnsureInitializedAsync().ConfigureAwait(false);
    }

    private void NotifyProgress() => ProgressChanged?.Invoke();
}
