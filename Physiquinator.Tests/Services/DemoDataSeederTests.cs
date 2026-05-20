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
        Assert.InRange(sessionCount, 100, 120);

        var recent = await _historyRepo.GetRecentSessionsAsync(200);
        Assert.NotEmpty(recent);
        Assert.Contains(recent, s => s.PlanName == "Leg Day");
        Assert.Contains(recent, s => s.PlanName == "Full Body Workout");

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
        Assert.True(benchProgress.Count >= 18);
        Assert.True(benchProgress[0].BestWeightKg >= benchProgress[^1].BestWeightKg);
        var benchCompleted = benchProgress.Where(r => r.SetCount >= 3).ToList();
        Assert.True(benchCompleted[0].TotalVolumeKg > benchCompleted[^1].TotalVolumeKg);

        var squatProgress = await _historyRepo.GetExerciseSessionProgressAsync(DemoDataIds.LegPlan, "Squats", 30);
        Assert.True(squatProgress.Count >= 12);
        Assert.True(squatProgress[0].TotalVolumeKg > squatProgress[^1].TotalVolumeKg);

        var pullUpProgress = await _historyRepo.GetExerciseSessionProgressAsync(DemoDataIds.PullPlan, "Pull-Ups", 30);
        Assert.True(pullUpProgress.Count >= 12);
        Assert.True(pullUpProgress[0].TotalReps > pullUpProgress[^1].TotalReps);

        var fbBench = await _historyRepo.GetExerciseSessionProgressAsync(DemoDataIds.FullBodyPlan, "Bench Press", 30);
        Assert.True(fbBench.Count >= 8);

        var endLocal = DateOnly.FromDateTime(DateTime.Today);
        var (utcStart, utcEndExclusive) = GetHeatmapQueryUtcBounds(endLocal, 53);
        var activity = await _historyRepo.GetSessionCountsByLocalDayAsync(utcStart, utcEndExclusive);
        var weeksWithActivity = activity.Keys
            .Select(d => GetMondayOfWeek(d))
            .Distinct()
            .Count();
        Assert.True(weeksWithActivity >= 20);

        var gridStart = GetMondayOfWeek(endLocal).AddDays(-7 * 52);
        var summary = WorkoutDayStats.Compute(activity, endLocal, gridStart);
        Assert.True(summary.CurrentStreakWorkoutDays >= 1);
        Assert.True(summary.LongestStreakWorkoutDays >= 1);
    }

    [Fact]
    public async Task SeedDemoHistory_IsIdempotent_WhenRerunOnFreshDatabase()
    {
        await _sut.SeedDemoDataIfNeededAsync();
        await _sut.SeedDemoHistoryIfNeededAsync();
        var c1 = await _historyRepo.GetSessionCountAsync();

        await _sut.SeedDemoDataIfNeededAsync();
        await _sut.SeedDemoHistoryIfNeededAsync();
        var c2 = await _historyRepo.GetSessionCountAsync();

        Assert.Equal(c1, c2);
    }

    [Fact]
    public async Task ClearData_AllowsReseed()
    {
        await _sut.SeedDemoDataIfNeededAsync();
        await _sut.SeedDemoHistoryIfNeededAsync();
        var expected = await _historyRepo.GetSessionCountAsync();

        await _db.ClearAllUserDataAsync();
        _prefs.Set(DemoDataSeeder.InitialDemoSeedCompletedKey, false);
        _prefs.Set(DemoDataSeeder.DemoHistorySeedCompletedKey, false);

        await _sut.SeedDemoDataIfNeededAsync();
        await _sut.SeedDemoHistoryIfNeededAsync();

        Assert.Equal(4, (await _planService.GetAllPlansAsync()).Count);
        Assert.Equal(expected, await _historyRepo.GetSessionCountAsync());
    }

    private static DateOnly GetMondayOfWeek(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff);
    }

    private static (DateTime UtcStart, DateTime UtcEndExclusive) GetHeatmapQueryUtcBounds(DateOnly endLocal, int weeks)
    {
        weeks = Math.Clamp(weeks, 1, 104);
        var tz = TimeZoneInfo.Local;
        var mondayOfEndWeek = GetMondayOfWeek(endLocal);
        var gridStartMonday = mondayOfEndWeek.AddDays(-7 * (weeks - 1));

        var startLocalUnspecified = DateTime.SpecifyKind(
            gridStartMonday.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Unspecified);
        var endExclusiveUnspecified = DateTime.SpecifyKind(
            endLocal.AddDays(1).ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Unspecified);

        var utcStart = TimeZoneInfo.ConvertTimeToUtc(startLocalUnspecified, tz);
        var utcEndExclusive = TimeZoneInfo.ConvertTimeToUtc(endExclusiveUnspecified, tz);
        return (utcStart, utcEndExclusive);
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
