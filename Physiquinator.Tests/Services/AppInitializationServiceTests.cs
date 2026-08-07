using Physiquinator.Core.Data;
using Physiquinator.Core.Services;
using Physiquinator.Tests.TestDoubles;
using Xunit;

namespace Physiquinator.Tests.Services;

public class AppInitializationServiceTests : IAsyncLifetime
{
    private AppDatabase _db = null!;
    private WorkoutPlanRepository _planRepo = null!;
    private WorkoutPlanService _planService = null!;
    private WorkoutHistoryRepository _historyRepo = null!;
    private InMemoryPreferences _appPreferences = null!;
    private DemoDataSeeder _seeder = null!;
    private MemoryDemoSeedPreferences _prefs = null!;
    private RecordingThemeInitialization _theme = null!;
    private WorkoutScheduleService _scheduleService = null!;

    static AppInitializationServiceTests() => SQLitePCL.Batteries_V2.Init();

    public async Task InitializeAsync()
    {
        _db = new AppDatabase(":memory:");
        await _db.EnsureInitializedAsync();
        _planRepo = new WorkoutPlanRepository(_db);
        _planService = new WorkoutPlanService(_planRepo);
        _historyRepo = new WorkoutHistoryRepository(_db, TimeProvider.System);
        _prefs = new MemoryDemoSeedPreferences();
        _appPreferences = new InMemoryPreferences();
        var profiles = new UserProfileService(
            _db,
            new WorkoutSessionService(TimeProvider.System),
            _appPreferences,
            new TempDbPathProvider(":memory:"),
            TimeProvider.System);
        _scheduleService = new WorkoutScheduleService(_appPreferences, profiles, _db);
        _seeder = new DemoDataSeeder(
            _planService,
            _db,
            _historyRepo,
            _scheduleService,
            profiles,
            _prefs,
            TimeProvider.System);
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

        AppInitializationService sut = CreateSut();
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
        AppInitializationService sut = CreateSut();
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

        AppInitializationService sut = CreateSut();
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
        AppInitializationService sut = CreateSut();
        await sut.EnsureInitializedAsync();
        Assert.NotEmpty(await _planService.GetAllPlansAsync());

        // Clear the DB
        await _db.ClearAllUserDataAsync();

        // Re-init with a fresh AppInitializationService — should NOT re-seed because preference flags are still true
        AppInitializationService sut2 = CreateSut();
        await sut2.EnsureInitializedAsync();

        Assert.Empty(await _planService.GetAllPlansAsync());
        Assert.Equal(0, await _historyRepo.GetSessionCountAsync());
    }

    private AppInitializationService CreateSut() =>
        new(_theme, _seeder, _prefs, _scheduleService);

    private sealed class RecordingThemeInitialization : IThemeInitialization
    {
        public int InitializeCallCount { get; private set; }

        public Task EnsureInitializedAsync()
        {
            InitializeCallCount++;
            return Task.CompletedTask;
        }
    }
}
