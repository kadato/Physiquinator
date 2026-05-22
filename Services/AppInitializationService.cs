namespace Physiquinator.Services;

/// <summary>
/// Coordinates first-launch theme setup and one-time demo data seeding before pages load.
/// </summary>
public sealed class AppInitializationService
{
    private readonly IThemeInitialization _theme;
    private readonly DemoDataSeeder _demoSeeder;
    private readonly IDemoSeedPreferences _preferences;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Task? _initializationTask;

    public AppInitializationService(
        IThemeInitialization theme,
        DemoDataSeeder demoSeeder,
        IDemoSeedPreferences preferences)
    {
        _theme = theme;
        _demoSeeder = demoSeeder;
        _preferences = preferences;
    }

    public bool IsReady { get; private set; }

    public bool ShowSetupOverlay { get; private set; }

    public string? SetupStatusMessage { get; private set; }

    public event Action? InitializationCompleted;

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

            await _theme.EnsureInitializedAsync().ConfigureAwait(false);

            if (_preferences.IsDefaultProfile)
            {
                var didSeedPlans = await _demoSeeder.SeedDemoDataIfNeededAsync().ConfigureAwait(false);

                if (didSeedPlans)
                {
                    ShowSetupOverlay = true;
                    SetupStatusMessage = "Building workout history\u2026";
                    NotifyProgress();
                }

                var didSeedHistory = await _demoSeeder.SeedDemoHistoryIfNeededAsync().ConfigureAwait(false);

                if (didSeedPlans || didSeedHistory)
                {
                    // Set flag to show onboarding modal explaining that demo data was seeded
                    _preferences.Set("Physiquinator.ShowFirstTimeSeedModal", true);

                    ShowSetupOverlay = false;
                    SetupStatusMessage = null;
                    NotifyProgress();
                }
            }

            IsReady = true;
            InitializationCompleted?.Invoke();
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
