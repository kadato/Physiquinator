using Physiquinator.Data;
using Physiquinator.Services;
using Xunit;

namespace Physiquinator.Tests.Services;

public class AppInitializationServiceTests : IAsyncLifetime
{
    private AppDatabase _db = null!;
    private WorkoutPlanRepository _planRepo = null!;
    private WorkoutPlanService _planService = null!;
    private WorkoutHistoryRepository _historyRepo = null!;
    private DemoDataSeeder _seeder = null!;
    private MemoryDemoSeedPreferences _prefs = null!;
    private RecordingThemeInitialization _theme = null!;

    static AppInitializationServiceTests() => SQLitePCL.Batteries_V2.Init();

    public async Task InitializeAsync()
    {
        _db = new AppDatabase(":memory:");
        await _db.EnsureInitializedAsync();
        _planRepo = new WorkoutPlanRepository(_db);
        _planService = new WorkoutPlanService(_planRepo);
        _historyRepo = new WorkoutHistoryRepository(_db);
        _prefs = new MemoryDemoSeedPreferences();
        _seeder = new DemoDataSeeder(_planService, _db, _historyRepo, _prefs);
        _theme = new RecordingThemeInitialization();
    }

    public async Task DisposeAsync() => await _db.Database.CloseAsync();

    [Fact]
    public async Task EnsureInitializedAsync_WhenSeedFlagsSet_SkipsDemoSeedAndOverlay()
    {
        _prefs.Set(DemoDataSeeder.InitialDemoSeedCompletedKey, true);
        _prefs.Set(DemoDataSeeder.DemoHistorySeedCompletedKey, true);

        var sut = CreateSut();
        await sut.EnsureInitializedAsync();

        Assert.True(sut.IsReady);
        Assert.False(sut.ShowSetupOverlay);
        Assert.Equal(0, await _historyRepo.GetSessionCountAsync());
        Assert.Empty(await _planService.GetAllPlansAsync());
        Assert.Equal(1, _theme.InitializeCallCount);
    }

    [Fact]
    public async Task EnsureInitializedAsync_OnFirstInstall_InitializesThemeBeforeSeeding()
    {
        var sut = CreateSut();
        await sut.EnsureInitializedAsync();

        Assert.True(sut.IsReady);
        Assert.False(sut.ShowSetupOverlay);
        Assert.Equal(1, _theme.InitializeCallCount);
        Assert.Equal(4, (await _planService.GetAllPlansAsync()).Count);
        Assert.InRange(await _historyRepo.GetSessionCountAsync(), 100, 120);
    }

    [Fact]
    public async Task EnsureInitializedAsync_AfterDbClearedWithSeedFlagsSet_DoesNotReseed()
    {
        _prefs.Set(DemoDataSeeder.InitialDemoSeedCompletedKey, true);
        _prefs.Set(DemoDataSeeder.DemoHistorySeedCompletedKey, true);
        await _seeder.SeedDemoDataIfNeededAsync();
        await _seeder.SeedDemoHistoryIfNeededAsync();
        await _db.ClearAllUserDataAsync();

        var sut = CreateSut();
        await sut.EnsureInitializedAsync();

        Assert.Empty(await _planService.GetAllPlansAsync());
        Assert.Equal(0, await _historyRepo.GetSessionCountAsync());
    }

    private AppInitializationService CreateSut() =>
        new(_theme, _seeder, _prefs);

    private sealed class RecordingThemeInitialization : IThemeInitialization
    {
        public int InitializeCallCount { get; private set; }

        public Task EnsureInitializedAsync()
        {
            InitializeCallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryDemoSeedPreferences : IDemoSeedPreferences
    {
        private readonly Dictionary<string, bool> _values = new();

        public bool Get(string key, bool defaultValue) =>
            _values.TryGetValue(key, out var v) ? v : defaultValue;

        public void Set(string key, bool value) => _values[key] = value;
    }
}
