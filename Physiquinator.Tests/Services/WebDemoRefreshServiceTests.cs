using Microsoft.Extensions.Logging.Abstractions;
using Physiquinator.Core.Data;
using Physiquinator.Core.Services;
using Physiquinator.Tests.TestDoubles;
using Physiquinator.Web.Services;
using SQLite;
using Xunit;

namespace Physiquinator.Tests.Services;

public class WebDemoRefreshServiceTests : IAsyncLifetime
{
    static WebDemoRefreshServiceTests() => SQLitePCL.Batteries_V2.Init();

    private string _tempDir = null!;
    private WebUserStore _users = null!;
    private WebDemoRefreshService _sut = null!;

    private AppDatabase _db = null!;
    private WorkoutPlanService _planService = null!;
    private WorkoutHistoryRepository _historyRepo = null!;
    private DemoDataSeeder _seeder = null!;

    public async Task InitializeAsync()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "physiquinator-demo-refresh", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _users = new WebUserStore(Path.Combine(_tempDir, "physiquinator-users.db3"));
        WebUser? demo = await _users.FindByUsernameAsync(_users.DemoUsername);
        Assert.NotNull(demo);

        var demoPath = Path.Combine(_tempDir, $"physiquinator_{demo.Id}.db3");
        _db = new AppDatabase(demoPath);
        await _db.EnsureInitializedAsync();

        var planRepo = new WorkoutPlanRepository(_db);
        _planService = new WorkoutPlanService(planRepo);
        _historyRepo = new WorkoutHistoryRepository(_db, TimeProvider.System);
        var appPrefs = new InMemoryPreferences();
        var profileService = new UserProfileService(
            _db,
            new WorkoutSessionService(TimeProvider.System),
            appPrefs,
            new TempDbPathProvider(demoPath),
            TimeProvider.System);
        var scheduleService = new WorkoutScheduleService(appPrefs, profileService, _db);
        _seeder = new DemoDataSeeder(
            _planService, _db, _historyRepo, scheduleService, profileService,
            new MemoryDemoSeedPreferences(), TimeProvider.System);

        _sut = new WebDemoRefreshService(_users, TimeProvider.System, NullLogger<WebDemoRefreshService>.Instance);
    }

    public async Task DisposeAsync()
    {
        try
        {
            await _db.Database.CloseAsync();
        }
        catch
        {
            // Ignore close failures in test teardown.
        }

        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Ignore cleanup failures leaving temp files behind.
        }
    }

    private async Task SeedDemoAsync()
    {
        Assert.True(await _seeder.SeedDemoDataIfNeededAsync());
        var (seededHistory, _) = await _seeder.SeedDemoHistoryAndExtrasIfNeededAsync();
        Assert.True(seededHistory);
    }

    private static void BackdateAllSessions(string demoPath, int days)
    {
        using var db = new SQLiteConnection(demoPath);
        var sessions = db.Table<WorkoutSessionLogEntity>().ToList();
        Assert.NotEmpty(sessions);
        foreach (var s in sessions)
        {
            s.StartedAtUtc = s.StartedAtUtc.AddDays(-days);
            if (s.EndedAtUtc is not null)
                s.EndedAtUtc = s.EndedAtUtc.Value.AddDays(-days);
        }
        db.UpdateAll(sessions);
    }

    [Fact]
    public async Task FreshSeed_IsNotWiped()
    {
        await SeedDemoAsync();

        Assert.False(await _sut.RefreshDemoIfStaleAsync(force: false, directoryOverride: _tempDir));
        Assert.Equal(4, (await _planService.GetAllPlansAsync()).Count);
        Assert.True(await _historyRepo.GetSessionCountAsync() > 0);
    }

    [Fact]
    public async Task YesterdaySeed_IsWiped()
    {
        await SeedDemoAsync();
        WebUser? demo = await _users.FindByUsernameAsync(_users.DemoUsername);
        Assert.NotNull(demo);
        BackdateAllSessions(Path.Combine(_tempDir, $"physiquinator_{demo.Id}.db3"), days: 1);

        Assert.True(await _sut.RefreshDemoIfStaleAsync(force: false, directoryOverride: _tempDir));
        _planService.InvalidatePlanCache();
        Assert.Empty(await _planService.GetAllPlansAsync());
        Assert.Equal(0, await _historyRepo.GetSessionCountAsync());
    }

    [Fact]
    public async Task Force_WipesFreshSeed()
    {
        await SeedDemoAsync();

        Assert.True(await _sut.RefreshDemoIfStaleAsync(force: true, directoryOverride: _tempDir));
        _planService.InvalidatePlanCache();
        Assert.Empty(await _planService.GetAllPlansAsync());
    }

    [Fact]
    public async Task MissingDemoFile_ReturnsFalse()
    {
        var emptyDir = Path.Combine(_tempDir, "empty");
        Directory.CreateDirectory(emptyDir);
        Assert.False(await _sut.RefreshDemoIfStaleAsync(force: false, directoryOverride: emptyDir));
    }

    [Fact]
    public async Task DeletedPlans_AreWipedAsStale()
    {
        await SeedDemoAsync();
        await _db.Database.ExecuteAsync("DELETE FROM WorkoutPlans");

        Assert.True(await _sut.RefreshDemoIfStaleAsync(force: false, directoryOverride: _tempDir));
    }
}
