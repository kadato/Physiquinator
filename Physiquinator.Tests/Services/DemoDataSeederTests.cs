using Physiquinator.Data;
using Physiquinator.Services;
using Xunit;

namespace Physiquinator.Tests.Services;

public class DemoDataSeederTests : IAsyncLifetime
{
    private AppDatabase _db = null!;
    private WorkoutPlanRepository _planRepo = null!;
    private WorkoutPlanService _planService = null!;
    private WorkoutHistoryRepository _historyRepo = null!;
    private DemoDataSeeder _sut = null!;
    private MemoryDemoSeedPreferences _prefs = null!;

    static DemoDataSeederTests() => SQLitePCL.Batteries_V2.Init();

    public async Task InitializeAsync()
    {
        _db = new AppDatabase(":memory:");
        await _db.EnsureInitializedAsync();
        _planRepo = new WorkoutPlanRepository(_db);
        _planService = new WorkoutPlanService(_planRepo);
        _historyRepo = new WorkoutHistoryRepository(_db);
        _prefs = new MemoryDemoSeedPreferences();
        _sut = new DemoDataSeeder(_planService, _db, _historyRepo, _prefs);
    }

    public async Task DisposeAsync() => await _db.Database.CloseAsync();

    [Fact]
    public async Task SeedDemoDataAndHistory_ProducesPlansSessionsAndParseableSnapshots()
    {
        await _sut.SeedDemoDataIfNeededAsync();
        await _sut.SeedDemoHistoryIfNeededAsync();

        var plans = await _planService.GetAllPlansAsync();
        Assert.Equal(4, plans.Count);
        Assert.Contains(plans, p => p.Id == DemoDataIds.PushPlan);

        var sessionCount = await _historyRepo.GetSessionCountAsync();
        Assert.Equal(24, sessionCount);

        var recent = await _historyRepo.GetRecentSessionsAsync(50);
        Assert.NotEmpty(recent);

        var withSnapshot = recent.FirstOrDefault(s => s.PlanSnapshotJson != null && s.PlanName == "Push Day");
        Assert.NotNull(withSnapshot);
        var parsed = WorkoutHistoryRepository.TryParsePlanSnapshot(withSnapshot!.PlanSnapshotJson);
        Assert.NotNull(parsed);
        Assert.Equal("Push Day", parsed!.Name);
        Assert.NotEmpty(parsed.Exercises);
        Assert.Contains(parsed.Exercises, e => e.Name == "Bench Press" && e.DefaultReps is not null);

        var inProgress = recent.First(s => s.EndedAtUtc is null);
        Assert.Equal("Push Day", inProgress.PlanName);
        var inProgressSets = await _historyRepo.GetSetsForSessionAsync(inProgress.Id);
        Assert.True(inProgressSets.Count is >= 1 and <= 4);

        var benchProgress = await _historyRepo.GetExerciseSessionProgressAsync(DemoDataIds.PushPlan, "Bench Press", 30);
        Assert.True(benchProgress.Count >= 10);

        // Newest-first: first session should reflect the strongest recent bench tier from the demo curve.
        Assert.True(benchProgress[0].BestWeightKg >= benchProgress[^1].BestWeightKg);
    }

    [Fact]
    public async Task SeedDemoHistory_IsIdempotent_WhenRerunOnFreshDatabase()
    {
        await _sut.SeedDemoDataIfNeededAsync();
        await _sut.SeedDemoHistoryIfNeededAsync();
        var c1 = await _historyRepo.GetSessionCountAsync();

        _prefs.Clear();
        var db2 = new AppDatabase(":memory:");
        await db2.EnsureInitializedAsync();
        var planRepo2 = new WorkoutPlanRepository(db2);
        var planService2 = new WorkoutPlanService(planRepo2);
        var history2 = new WorkoutHistoryRepository(db2);
        var prefs2 = new MemoryDemoSeedPreferences();
        var seeder2 = new DemoDataSeeder(planService2, db2, history2, prefs2);

        await seeder2.SeedDemoDataIfNeededAsync();
        await seeder2.SeedDemoHistoryIfNeededAsync();
        var c2 = await history2.GetSessionCountAsync();

        Assert.Equal(c1, c2);
        await db2.Database.CloseAsync();
    }

    private sealed class MemoryDemoSeedPreferences : IDemoSeedPreferences
    {
        private readonly Dictionary<string, bool> _values = new();

        public bool Get(string key, bool defaultValue) =>
            _values.TryGetValue(key, out var v) ? v : defaultValue;

        public void Set(string key, bool value) => _values[key] = value;

        public void Clear() => _values.Clear();
    }
}
