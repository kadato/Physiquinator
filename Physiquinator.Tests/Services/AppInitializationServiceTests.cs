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
    public async Task EnsureInitializedAsync_WhenDbAlreadyHasData_SkipsDemoSeedAndOverlay()
    {
        // Pre-populate the DB so the seeders see existing data and skip
        await _seeder.SeedDemoDataIfNeededAsync();
        await _seeder.SeedDemoHistoryIfNeededAsync();
        var planCountBefore = (await _planService.GetAllPlansAsync()).Count;
        var sessionCountBefore = await _historyRepo.GetSessionCountAsync();

        var sut = CreateSut();
        await sut.EnsureInitializedAsync();

        Assert.True(sut.IsReady);
        Assert.False(sut.ShowSetupOverlay);
        Assert.Equal(planCountBefore, (await _planService.GetAllPlansAsync()).Count);
        Assert.Equal(sessionCountBefore, await _historyRepo.GetSessionCountAsync());
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
    public async Task EnsureInitializedAsync_OnFirstInstall_WhenNotDefaultProfile_SkipsSeeding()
    {
        _prefs.IsDefaultProfile = false;

        var sut = CreateSut();
        await sut.EnsureInitializedAsync();

        Assert.True(sut.IsReady);
        Assert.False(sut.ShowSetupOverlay);
        Assert.Equal(1, _theme.InitializeCallCount);
        Assert.Empty(await _planService.GetAllPlansAsync());
        Assert.Equal(0, await _historyRepo.GetSessionCountAsync());
    }

    [Fact]
    public async Task EnsureInitializedAsync_AfterDbClearedWithSeedFlagsSet_DoesNotReseed()
    {
        // First init seeds the DB
        var sut = CreateSut();
        await sut.EnsureInitializedAsync();
        Assert.NotEmpty(await _planService.GetAllPlansAsync());

        // Clear the DB
        await _db.ClearAllUserDataAsync();

        // Re-init with a fresh AppInitializationService — should NOT re-seed because preference flags are still true
        var sut2 = CreateSut();
        await sut2.EnsureInitializedAsync();

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

        public bool IsDefaultProfile { get; set; } = true;
    }
}
