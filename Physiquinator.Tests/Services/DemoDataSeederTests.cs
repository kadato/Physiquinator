using Physiquinator.Core.Data;
using Physiquinator.Core.Models;
using Physiquinator.Core.Services;
using Physiquinator.Tests.TestDoubles;
using Xunit;

namespace Physiquinator.Tests.Services;

public class DemoDataSeederTests : IAsyncLifetime
{
    private AppDatabase _db = null!;
    private WorkoutPlanRepository _planRepo = null!;
    private WorkoutPlanService _planService = null!;
    private WorkoutHistoryRepository _historyRepo = null!;
    private InMemoryPreferences _appPreferences = null!;
    private UserProfileService _profileService = null!;
    private WorkoutScheduleService _scheduleService = null!;
    private DemoDataSeeder _sut = null!;
    private MemoryDemoSeedPreferences _prefs = null!;

    static DemoDataSeederTests() => SQLitePCL.Batteries_V2.Init();

    public async Task InitializeAsync()
    {
        _db = new AppDatabase(":memory:");
        await _db.EnsureInitializedAsync();
        _planRepo = new WorkoutPlanRepository(_db);
        _planService = new WorkoutPlanService(_planRepo);
        _historyRepo = new WorkoutHistoryRepository(_db, TimeProvider.System);
        _appPreferences = new InMemoryPreferences();
        _profileService = new UserProfileService(
            _db,
            new WorkoutSessionService(TimeProvider.System),
            _appPreferences,
            new TempDbPathProvider(":memory:"),
            TimeProvider.System);
        _scheduleService = new WorkoutScheduleService(_appPreferences, _profileService, _db);
        _prefs = new MemoryDemoSeedPreferences();
        _sut = new DemoDataSeeder(_planService, _db, _historyRepo, _scheduleService, _profileService, _prefs, TimeProvider.System);
    }

    public async Task DisposeAsync() => await _db.Database.CloseAsync();

    [Fact]
    public async Task SeedDemoDataAndHistory_ProducesPlansSessionsAndParseableSnapshots()
    {
        await _sut.SeedDemoDataIfNeededAsync();
        await _sut.SeedDemoHistoryIfNeededAsync();

        List<WorkoutPlan> plans = await _planService.GetAllPlansAsync();
        Assert.Equal(4, plans.Count);
        Assert.Contains(plans, p => p.Id == DemoDataIds.PushPlan);

        var sessionCount = await _historyRepo.GetSessionCountAsync();
        Assert.InRange(sessionCount, 100, 120);

        IReadOnlyList<WorkoutSessionLogEntity> recent = await _historyRepo.GetRecentSessionsAsync(200);
        Assert.NotEmpty(recent);
        Assert.Contains(recent, s => s.PlanName == "Leg Day");
        Assert.Contains(recent, s => s.PlanName == "Full Body Workout");

        WorkoutSessionLogEntity? withSnapshot = recent.FirstOrDefault(s => s.PlanSnapshotJson != null && s.PlanName == "Push Day");
        Assert.NotNull(withSnapshot);
        WorkoutPlan? parsed = WorkoutHistoryRepository.TryParsePlanSnapshot(withSnapshot.PlanSnapshotJson);
        Assert.NotNull(parsed);
        Assert.Equal("Push Day", parsed.Name);
        Assert.NotEmpty(parsed.Exercises);
        Assert.Contains(parsed.Exercises, e => e.Name == "Bench Press" && e.DefaultReps is not null);

        // A fresh demo user must not find an unfinished workout on Home.
        Assert.DoesNotContain(recent, s => s.EndedAtUtc is null);

        IReadOnlyList<ExerciseSessionProgressEntry> benchProgress = await _historyRepo.GetExerciseSessionProgressAsync(DemoDataIds.PushPlan, "Bench Press", 30);
        Assert.True(benchProgress.Count >= 18);
        Assert.True(benchProgress[0].BestWeightKg >= benchProgress[^1].BestWeightKg);
        var benchCompleted = benchProgress.Where(r => r.SetCount >= 3).ToList();
        Assert.True(benchCompleted[0].TotalVolumeKg > benchCompleted[^1].TotalVolumeKg);

        IReadOnlyList<ExerciseSessionProgressEntry> squatProgress = await _historyRepo.GetExerciseSessionProgressAsync(DemoDataIds.LegPlan, "Squats", 30);
        Assert.True(squatProgress.Count >= 12);
        Assert.True(squatProgress[0].TotalVolumeKg > squatProgress[^1].TotalVolumeKg);

        IReadOnlyList<ExerciseSessionProgressEntry> pullUpProgress = await _historyRepo.GetExerciseSessionProgressAsync(DemoDataIds.PullPlan, "Pull-Ups", 30);
        Assert.True(pullUpProgress.Count >= 12);
        Assert.True(pullUpProgress[0].TotalReps > pullUpProgress[^1].TotalReps);
        Assert.Contains(pullUpProgress, p => p.BestWeightKg < 0); // Assisted
        Assert.Contains(pullUpProgress, p => p.BestWeightKg is null); // Bodyweight only
        Assert.Contains(pullUpProgress, p => p.BestWeightKg > 0); // Weighted

        IReadOnlyList<ExerciseSessionProgressEntry> plankProgress = await _historyRepo.GetExerciseSessionProgressAsync(DemoDataIds.FullBodyPlan, "Plank", 30);
        Assert.True(plankProgress.Count >= 8);
        Assert.All(plankProgress, p => Assert.Null(p.BestWeightKg));
        Assert.True(plankProgress[0].TotalReps > plankProgress[^1].TotalReps);

        IReadOnlyList<ExerciseSessionProgressEntry> fbBench = await _historyRepo.GetExerciseSessionProgressAsync(DemoDataIds.FullBodyPlan, "Bench Press", 30);
        Assert.True(fbBench.Count >= 8);

        var endLocal = DateOnly.FromDateTime(DateTime.Today);
        (DateTime utcStart, DateTime utcEndExclusive) = GetHeatmapQueryUtcBounds(endLocal, 53);
        IReadOnlyDictionary<DateOnly, int> activity = await _historyRepo.GetSessionCountsByLocalDayAsync(utcStart, utcEndExclusive);
        var weeksWithActivity = activity.Keys
            .Select(GetMondayOfWeek)
            .Distinct()
            .Count();
        Assert.True(weeksWithActivity >= 20);

        DateOnly gridStart = GetMondayOfWeek(endLocal).AddDays(-7 * 52);
        WorkoutDaySummary summary = WorkoutDayStats.Compute(activity, endLocal, gridStart);
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
    public async Task ClearData_WithPreferencesReset_AllowsReseed()
    {
        await _sut.SeedDemoDataIfNeededAsync();
        await _sut.SeedDemoHistoryIfNeededAsync();
        var expected = await _historyRepo.GetSessionCountAsync();

        await _db.ClearAllUserDataAsync();
        _prefs.Clear(); // Explicitly reset pref flags to mimic clean reinstall or clear-seed-flags action

        await _sut.SeedDemoDataIfNeededAsync();
        await _sut.SeedDemoHistoryIfNeededAsync();

        Assert.Equal(4, (await _planService.GetAllPlansAsync()).Count);
        Assert.Equal(expected, await _historyRepo.GetSessionCountAsync());
    }

    [Fact]
    public async Task ClearData_WithoutPreferencesReset_DoesNotReseed()
    {
        await _sut.SeedDemoDataIfNeededAsync();
        await _sut.SeedDemoHistoryIfNeededAsync();

        await _db.ClearAllUserDataAsync();
        // Do not clear preferences, keeping the completed flags set to true

        await _sut.SeedDemoDataIfNeededAsync();
        await _sut.SeedDemoHistoryIfNeededAsync();

        Assert.Empty(await _planService.GetAllPlansAsync());
        Assert.Equal(0, await _historyRepo.GetSessionCountAsync());
    }

    [Fact]
    public async Task SeedDemoData_SetsWorkoutScheduleDays()
    {
        await _sut.SeedDemoDataIfNeededAsync();

        Assert.Equal(
            [DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday],
            _scheduleService.Days.OrderBy(d => d).ToArray());
    }

    [Fact]
    public async Task SeedDemoExtras_SeedsChangingBodyweightsAndProfileBodyweight()
    {
        await _sut.SeedDemoDataIfNeededAsync();
        await _sut.SeedDemoHistoryIfNeededAsync();
        Assert.True(await _sut.SeedDemoExtrasIfNeededAsync());

        IReadOnlyList<BodyweightLogEntity> rows = await _historyRepo.GetBodyweightLogsAsync(1000);
        Assert.InRange(rows.Count, 150, 190);
        Assert.Equal(rows.Count, rows.Select(r => r.Date).Distinct().Count());
        Assert.All(rows, r => Assert.True(r.BodyweightKg > 0));
        Assert.True(rows[0].BodyweightKg < rows[^1].BodyweightKg, "Bodyweight should trend downward across the demo year.");
        Assert.True(rows[0].BodyweightKg > 80, "Latest bodyweight should stay within a believable range.");

        Assert.NotNull(_profileService.GetActiveProfile().BodyweightKg);
        Assert.Equal(rows[0].BodyweightKg, _profileService.GetActiveProfile().BodyweightKg!.Value, precision: 1);
    }

    [Fact]
    public async Task SeedDemoExtras_IsIdempotent_WhenRerunOnFreshDatabase()
    {
        await _sut.SeedDemoDataIfNeededAsync();
        await _sut.SeedDemoHistoryIfNeededAsync();
        await _sut.SeedDemoExtrasIfNeededAsync();
        IReadOnlyList<BodyweightLogEntity> c1 = await _historyRepo.GetBodyweightLogsAsync(1000);

        await _sut.SeedDemoExtrasIfNeededAsync();
        IReadOnlyList<BodyweightLogEntity> c2 = await _historyRepo.GetBodyweightLogsAsync(1000);

        Assert.Equal(c1.Count, c2.Count);
    }

    [Fact]
    public async Task SeedDemoExtras_DoesNotClobberAnExistingSchedule()
    {
        await _scheduleService.SetDaysAsync([DayOfWeek.Tuesday, DayOfWeek.Thursday]);

        await _sut.SeedDemoDataIfNeededAsync();
        await _sut.SeedDemoHistoryIfNeededAsync();
        await _sut.SeedDemoExtrasIfNeededAsync();

        Assert.Equal(
            [DayOfWeek.Tuesday, DayOfWeek.Thursday],
            _scheduleService.Days.OrderBy(d => d).ToArray());
    }

    [Fact]
    public async Task SeedDemoExtras_SkipsWhenBodyweightsAlreadyExist()
    {
        await _sut.SeedDemoDataIfNeededAsync();
        await _historyRepo.UpsertBodyweightLogAsync(DateOnly.FromDateTime(DateTime.Today), 82.0);

        Assert.False(await _sut.SeedDemoExtrasIfNeededAsync());
        Assert.Null(_profileService.GetActiveProfile().BodyweightKg);
    }

    [Fact]
    public async Task SeedDemoHistory_RecentSessions_KeepSettingPersonalRecords()
    {
        await _sut.SeedDemoDataIfNeededAsync();
        await _sut.SeedDemoHistoryIfNeededAsync();

        IReadOnlyList<ExerciseSetLogRow> benchRows = await _historyRepo.GetExerciseSetLogRowsAsync(DemoDataIds.PushPlan, "Bench Press");
        PersonalRecords records = PersonalRecordCalculator.Compute(benchRows, ExerciseLogType.WeightAndReps);

        PersonalRecordEntry? bestWeight = records.Entries.FirstOrDefault(e => e.Kind == ExerciseRecordKind.BestWeight);
        Assert.NotNull(bestWeight);
        Assert.True(bestWeight.Value > 80, "Demo bench should progress well past the starting weight.");
        Assert.True(
            (DateTime.UtcNow - bestWeight.CompletedAtUtc).TotalDays < 21,
            $"Bench PR should fall in a recent session, was {bestWeight.CompletedAtUtc:O}");
    }

    [Fact]
    public async Task SeedDemoData_PlansDemoAllLogTypesWarmupsAndSupersets()
    {
        await _sut.SeedDemoDataIfNeededAsync();

        List<WorkoutPlan> plans = await _planService.GetAllPlansAsync();

        // All three logging styles are represented across the demo plans.
        Assert.Contains(plans.SelectMany(p => p.Exercises), e => e.LogType == ExerciseLogType.WeightAndReps);
        Assert.Contains(plans.SelectMany(p => p.Exercises), e => e.LogType == ExerciseLogType.BodyweightReps);
        Assert.Contains(plans.SelectMany(p => p.Exercises), e => e.LogType == ExerciseLogType.Duration);

        // Bodyweight exercises carry their bodyweight share.
        ExercisePlan pushUps = plans
            .Single(p => p.Id == DemoDataIds.PushPlan)
            .Exercises.Single(e => e.Name == "Push-Ups");
        Assert.Equal(ExerciseLogType.BodyweightReps, pushUps.LogType);
        Assert.Equal(65, pushUps.BodyweightPercent);

        // Compound lifts include warm-up sets in the plan.
        var push = plans.Single(p => p.Id == DemoDataIds.PushPlan);
        Assert.Equal(2, push.Exercises.Single(e => e.Id == DemoDataIds.PushBench).WarmupSetCount);
        Assert.Equal(1, push.Exercises.Single(e => e.Id == DemoDataIds.PushOhp).WarmupSetCount);
        var pull = plans.Single(p => p.Id == DemoDataIds.PullPlan);
        Assert.Equal(1, pull.Exercises.Single(e => e.Id == DemoDataIds.PullDeadlift).WarmupSetCount);
        Assert.Equal(1, pull.Exercises.Single(e => e.Id == DemoDataIds.PullPullups).WarmupSetCount);
        var leg = plans.Single(p => p.Id == DemoDataIds.LegPlan);
        Assert.Equal(2, leg.Exercises.Single(e => e.Id == DemoDataIds.LegSquat).WarmupSetCount);
        Assert.Equal(1, leg.Exercises.Single(e => e.Id == DemoDataIds.LegRdl).WarmupSetCount);

        // Full Body pairs exercises into supersets.
        var fb = plans.Single(p => p.Id == DemoDataIds.FullBodyPlan);
        var fbExercises = fb.Exercises.ToDictionary(e => e.Id);
        Assert.Equal("A", fbExercises[DemoDataIds.FbBench].SupersetGroupId);
        Assert.Equal("A", fbExercises[DemoDataIds.FbRow].SupersetGroupId);
        Assert.Equal("B", fbExercises[DemoDataIds.FbOhp].SupersetGroupId);
        Assert.Equal("B", fbExercises[DemoDataIds.FbPullup].SupersetGroupId);
        Assert.Null(fbExercises[DemoDataIds.FbSquat].SupersetGroupId);
        Assert.Null(fbExercises[DemoDataIds.FbPlank].SupersetGroupId);
    }

    [Fact]
    public async Task SeedDemoHistory_LogsWarmupSetsAlignedWithPlan()
    {
        await _sut.SeedDemoDataIfNeededAsync();
        await _sut.SeedDemoHistoryIfNeededAsync();

        IReadOnlyList<WorkoutSessionLogEntity> recent = await _historyRepo.GetRecentSessionsAsync(200);
        WorkoutSessionLogEntity pushSession = recent.First(s => s.PlanName == "Push Day" && s.EndedAtUtc != null);

        List<WorkoutSetLogEntity> benchSets = [.. (await _historyRepo.GetSetsForSessionAsync(pushSession.Id))
            .Where(s => s.ExerciseName == "Bench Press")
            .OrderBy(s => s.SetIndex)];

        Assert.Equal(6, benchSets.Count); // 2 warm-ups + 4 working
        Assert.Equal([true, true, false, false, false, false], benchSets.Select(s => s.IsWarmup));
        Assert.Equal([0, 1, 2, 3, 4, 5], benchSets.Select(s => s.SetIndex));
        // Warm-up loads ramp up but stay below the working load.
        Assert.True(benchSets[0].WeightKg < benchSets[1].WeightKg);
        Assert.True(benchSets[1].WeightKg < benchSets[2].WeightKg);
        Assert.Equal(benchSets[2].WeightKg, benchSets[^1].WeightKg);
        Assert.All(benchSets.Where(s => s.IsWarmup), s => Assert.True(s.WeightKg > 0));

        // Warm-up rows are excluded from progress aggregates.
        IReadOnlyList<ExerciseSessionProgressEntry> benchProgress = await _historyRepo.GetExerciseSessionProgressAsync(DemoDataIds.PushPlan, "Bench Press", 30);
        Assert.All(benchProgress, r => Assert.Equal(4, r.SetCount));
    }

    [Fact]
    public async Task SeedDemoHistory_DurationAndSupersetExercisesHaveNoWarmups()
    {
        await _sut.SeedDemoDataIfNeededAsync();
        await _sut.SeedDemoHistoryIfNeededAsync();

        IReadOnlyList<WorkoutSessionLogEntity> recent = await _historyRepo.GetRecentSessionsAsync(200);
        WorkoutSessionLogEntity fbSession = recent.First(s => s.PlanName == "Full Body Workout" && s.EndedAtUtc != null);
        IReadOnlyList<WorkoutSetLogEntity> sets = await _historyRepo.GetSetsForSessionAsync(fbSession.Id);

        Assert.DoesNotContain(sets, s => s.IsWarmup && s.ExerciseName is "Plank" or "Pull-Ups" or "Barbell Rows" or "Overhead Press");
        // Squat and bench warm-ups are present on the Full Body plan.
        Assert.Contains(sets, s => s.IsWarmup && s.ExerciseName == "Squats");
        Assert.Contains(sets, s => s.IsWarmup && s.ExerciseName == "Bench Press");
    }

    private static DateOnly GetMondayOfWeek(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff);
    }

    private static (DateTime UtcStart, DateTime UtcEndExclusive) GetHeatmapQueryUtcBounds(DateOnly endLocal, int weeks)
    {
        weeks = Math.Clamp(weeks, 1, 104);
        TimeZoneInfo tz = TimeZoneInfo.Local;
        DateOnly mondayOfEndWeek = GetMondayOfWeek(endLocal);
        DateOnly gridStartMonday = mondayOfEndWeek.AddDays(-7 * (weeks - 1));

        var startLocalUnspecified = DateTime.SpecifyKind(
            gridStartMonday.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Unspecified);
        var endExclusiveUnspecified = DateTime.SpecifyKind(
            endLocal.AddDays(1).ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Unspecified);

        DateTime utcStart = TimeZoneInfo.ConvertTimeToUtc(startLocalUnspecified, tz);
        DateTime utcEndExclusive = TimeZoneInfo.ConvertTimeToUtc(endExclusiveUnspecified, tz);
        return (utcStart, utcEndExclusive);
    }
}
