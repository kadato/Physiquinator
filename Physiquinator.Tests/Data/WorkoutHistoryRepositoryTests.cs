using Physiquinator.Core.Data;
using Physiquinator.Core.Models;
using Xunit;

namespace Physiquinator.Tests.Data;

public class WorkoutHistoryRepositoryTests : IAsyncLifetime
{
    private AppDatabase _db = null!;
    private WorkoutHistoryRepository _sut = null!;

    static WorkoutHistoryRepositoryTests() => SQLitePCL.Batteries_V2.Init();

    public async Task InitializeAsync()
    {
        _db = new AppDatabase(":memory:");
        await _db.EnsureInitializedAsync();
        _sut = new WorkoutHistoryRepository(_db, TimeProvider.System);
    }

    public async Task DisposeAsync()
    {
        await _db.Database.CloseAsync();
    }

    [Fact]
    public async Task GetRecentSessionsAsync_ReturnsEmpty_WhenNoSessions()
    {
        IReadOnlyList<WorkoutSessionLogEntity> result = await _sut.GetRecentSessionsAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task Begin_Log_End_GetRecent_GetSets_RoundTrip()
    {
        var planId = Guid.NewGuid();
        const string snapshot = """{"name":"P"}""";

        var sessionId = await _sut.BeginSessionAsync(planId, "Leg day", snapshot);
        Assert.False(string.IsNullOrWhiteSpace(sessionId));

        await _sut.LogSetAsync(sessionId, 0, "Squat", 0, reps: 8, weightKg: 100);
        await _sut.LogSetAsync(sessionId, 0, "Squat", 1, reps: 8, weightKg: 100);
        await _sut.EndSessionAsync(sessionId);

        IReadOnlyList<WorkoutSessionLogEntity> sessions = await _sut.GetRecentSessionsAsync();
        Assert.Single(sessions);
        Assert.Equal("Leg day", sessions[0].PlanName);
        Assert.Equal(planId.ToString(), sessions[0].WorkoutPlanId);
        Assert.Equal(snapshot, sessions[0].PlanSnapshotJson);
        Assert.NotNull(sessions[0].EndedAtUtc);

        IReadOnlyList<WorkoutSetLogEntity> sets = await _sut.GetSetsForSessionAsync(sessionId);
        Assert.Equal(2, sets.Count);
        Assert.Equal([0, 1], sets.Select(s => s.SetIndex));
        Assert.Equal(8, sets[0].Reps);
        Assert.Equal(100, sets[0].WeightKg);
    }

    [Fact]
    public async Task GetSetsForSessionAsync_OrdersByCompletedTime_Chronological()
    {
        var sessionId = await _sut.BeginSessionAsync(Guid.NewGuid(), "Test", null);
        await _sut.LogSetAsync(sessionId, 1, "B", 0);
        await _sut.LogSetAsync(sessionId, 0, "A", 0);

        IReadOnlyList<WorkoutSetLogEntity> sets = await _sut.GetSetsForSessionAsync(sessionId);

        Assert.Equal(["B", "A"], sets.Select(s => s.ExerciseName));
    }

    [Fact]
    public async Task TryDeleteLastSetLogAsync_RemovesLatestOnly()
    {
        var sessionId = await _sut.BeginSessionAsync(Guid.NewGuid(), "Test", null);
        await _sut.LogSetAsync(sessionId, 0, "A", 0);
        await _sut.LogSetAsync(sessionId, 0, "A", 1);

        Assert.True(await _sut.TryDeleteLastSetLogAsync(sessionId));
        IReadOnlyList<WorkoutSetLogEntity> sets = await _sut.GetSetsForSessionAsync(sessionId);
        Assert.Single(sets);
        Assert.Equal(0, sets[0].SetIndex);

        Assert.False(await _sut.TryDeleteLastSetLogAsync("nonexistent"));
    }

    [Fact]
    public async Task DeleteSessionAsync_RemovesSessionAndSets()
    {
        var sessionId = await _sut.BeginSessionAsync(Guid.NewGuid(), "X", null);
        await _sut.LogSetAsync(sessionId, 0, "A", 0);

        await _sut.DeleteSessionAsync(sessionId);

        Assert.Null(await _sut.GetSessionAsync(sessionId));
        Assert.Empty(await _sut.GetSetsForSessionAsync(sessionId));
    }

    [Fact]
    public async Task GetExerciseSetLogRowsAsync_ReturnsChronologicalRows_ScopedByPlanAndName()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var s1 = await _sut.BeginSessionAsync(p1, "A", null);
        await _sut.LogSetAsync(s1, 0, "Bench", 0, reps: 8, weightKg: 50);
        var s2 = await _sut.BeginSessionAsync(p1, "B", null);
        await _sut.LogSetAsync(s2, 0, "Bench", 0, reps: 6, weightKg: 55);
        await _sut.LogSetAsync(s2, 0, "Squat", 0, reps: 5, weightKg: 100);
        var s3 = await _sut.BeginSessionAsync(p2, "Other", null);
        await _sut.LogSetAsync(s3, 0, "Bench", 0, reps: 1, weightKg: 200);

        IReadOnlyList<ExerciseSetLogRow> rows = await _sut.GetExerciseSetLogRowsAsync(p1, "Bench");

        Assert.Equal(2, rows.Count);
        Assert.Equal(s1, rows[0].SessionId);
        Assert.Equal(8, rows[0].Reps);
        Assert.Equal(50, rows[0].WeightKg);
        Assert.Equal(s2, rows[1].SessionId);
        Assert.Equal(6, rows[1].Reps);
        Assert.Equal(55, rows[1].WeightKg);
    }

    [Fact]
    public async Task GetExerciseSetLogRowsAsync_ReturnsEmpty_WhenNoMatches()
    {
        Assert.Empty(await _sut.GetExerciseSetLogRowsAsync(Guid.NewGuid(), "Anything"));
    }

    [Fact]
    public async Task GetLatestSetMetricsForExerciseAsync_UsesMostRecentAcrossSessionsForSamePlan()
    {
        var planId = Guid.NewGuid();
        var s1 = await _sut.BeginSessionAsync(planId, "Day1", null);
        await _sut.LogSetAsync(s1, 0, "Squat", 0, reps: 10, weightKg: 60);
        await _sut.EndSessionAsync(s1);

        LastSetMetrics? m = await _sut.GetLatestSetMetricsForExerciseAsync(planId, "Squat");
        Assert.NotNull(m);
        Assert.Equal(10, m.Reps);
        Assert.Equal(60, m.WeightKg);

        var s2 = await _sut.BeginSessionAsync(planId, "Day2", null);
        await _sut.LogSetAsync(s2, 0, "Squat", 0, reps: 12, weightKg: 62.5);
        m = await _sut.GetLatestSetMetricsForExerciseAsync(planId, "Squat");
        Assert.Equal(12, m!.Reps);
        Assert.Equal(62.5, m.WeightKg);
    }

    [Fact]
    public async Task GetLatestSetMetricsForExerciseAsync_ReturnsNull_WhenNoMatchingLogs()
    {
        Assert.Null(await _sut.GetLatestSetMetricsForExerciseAsync(Guid.NewGuid(), "Anything"));
    }

    [Fact]
    public async Task GetLatestSetMetricsForExerciseAsync_ScopesByPlanId()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var s1 = await _sut.BeginSessionAsync(p1, "A", null);
        await _sut.LogSetAsync(s1, 0, "Lift", 0, reps: 5, weightKg: 40);

        Assert.Null(await _sut.GetLatestSetMetricsForExerciseAsync(p2, "Lift"));
        Assert.NotNull(await _sut.GetLatestSetMetricsForExerciseAsync(p1, "Lift"));
    }

    [Fact]
    public async Task GetLatestSetMetricsByExerciseAsync_ReturnsLatestPerExercise()
    {
        var planId = Guid.NewGuid();
        var s1 = await _sut.BeginSessionAsync(planId, "Day1", null);
        await _sut.LogSetAsync(s1, 0, "Squat", 0, reps: 10, weightKg: 60);
        await _sut.LogSetAsync(s1, 1, "Bench", 0, reps: 8, weightKg: 50);
        await _sut.EndSessionAsync(s1);

        var s2 = await _sut.BeginSessionAsync(planId, "Day2", null);
        await _sut.LogSetAsync(s2, 0, "Squat", 0, reps: 12, weightKg: 62.5);

        IReadOnlyDictionary<string, LastSetMetrics> map = await _sut.GetLatestSetMetricsByExerciseAsync(planId);

        Assert.Equal(2, map.Count);
        Assert.Equal(12, map["Squat"].Reps);
        Assert.Equal(62.5, map["Squat"].WeightKg);
        Assert.Equal(8, map["Bench"].Reps);
        Assert.Equal(50, map["Bench"].WeightKg);
    }

    [Fact]
    public async Task GetLatestSetMetricsByExerciseAsync_ScopesByPlanId()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var s1 = await _sut.BeginSessionAsync(p1, "A", null);
        await _sut.LogSetAsync(s1, 0, "Lift", 0, reps: 5, weightKg: 40);
        var s2 = await _sut.BeginSessionAsync(p2, "B", null);
        await _sut.LogSetAsync(s2, 0, "Lift", 0, reps: 1, weightKg: 1);

        IReadOnlyDictionary<string, LastSetMetrics> map = await _sut.GetLatestSetMetricsByExerciseAsync(p1);

        Assert.Single(map);
        Assert.Equal(5, map["Lift"].Reps);
        Assert.Equal(40, map["Lift"].WeightKg);
    }

    [Fact]
    public async Task GetLatestSetMetricsByExerciseAsync_ReturnsEmpty_WhenNoLogs()
    {
        IReadOnlyDictionary<string, LastSetMetrics> map = await _sut.GetLatestSetMetricsByExerciseAsync(Guid.NewGuid());

        Assert.Empty(map);
    }

    [Fact]
    public void TryParsePlanSnapshot_ReturnsNull_ForInvalidJson()
    {
        Assert.Null(WorkoutHistoryRepository.TryParsePlanSnapshot("{not json"));
    }

    [Fact]
    public void TryParsePlanSnapshot_RoundTripsPlan()
    {
        var plan = new WorkoutPlan
        {
            Name = "Push",
            Exercises = [new ExercisePlan { Name = "Bench", SetCount = 3, Order = 0, RestIntervalSeconds = 90, DefaultReps = 10 }]
        };
        var json = System.Text.Json.JsonSerializer.Serialize(plan);
        WorkoutPlan? parsed = WorkoutHistoryRepository.TryParsePlanSnapshot(json);
        Assert.NotNull(parsed);
        Assert.Equal("Push", parsed.Name);
        Assert.Single(parsed.Exercises);
        Assert.Equal(10, parsed.Exercises[0].DefaultReps);
    }

    [Fact]
    public async Task CreateBackupSnapshotAsync_IncludesSessionsWithoutSets()
    {
        var sessionId = await _sut.BeginSessionAsync(Guid.NewGuid(), "Empty", null);

        WorkoutHistoryBackup backup = await _sut.CreateBackupSnapshotAsync();

        Assert.Single(backup.Sessions);
        Assert.Equal(sessionId, backup.Sessions[0].Session.Id);
        Assert.Empty(backup.Sessions[0].Sets);
    }

    [Fact]
    public async Task CreateBackupSnapshotAsync_OrdersSessionsNewestFirst_SetsChronological()
    {
        var planId = Guid.NewGuid();
        var baseTime = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);
        var olderId = await InsertSessionAtUtcAsync(planId, "Old", baseTime);
        await _sut.LogSetAsync(olderId, 0, "A", 0);
        var newerId = await InsertSessionAtUtcAsync(planId, "New", baseTime.AddHours(1));

        WorkoutHistoryBackup backup = await _sut.CreateBackupSnapshotAsync();

        Assert.Equal(2, backup.Sessions.Count);
        Assert.Equal(newerId, backup.Sessions[0].Session.Id);
        Assert.Equal(olderId, backup.Sessions[1].Session.Id);
        Assert.Empty(backup.Sessions[0].Sets);
        Assert.Single(backup.Sessions[1].Sets);
    }

    [Fact]
    public async Task GetSessionCountAsync_TracksSessions()
    {
        Assert.Equal(0, await _sut.GetSessionCountAsync());

        await _sut.BeginSessionAsync(Guid.NewGuid(), "A", null);
        Assert.Equal(1, await _sut.GetSessionCountAsync());
    }

    [Fact]
    public async Task CreateBackupSnapshotAsync_ThenImportBackupAsync_RoundTrips()
    {
        var planId = Guid.NewGuid();
        const string snapshot = """{"name":"Snap"}""";
        var sessionId = await _sut.BeginSessionAsync(planId, "Leg day", snapshot);
        await _sut.LogSetAsync(sessionId, 0, "Squat", 0, reps: 5, weightKg: 40);
        await _sut.EndSessionAsync(sessionId);

        WorkoutHistoryBackup backup = await _sut.CreateBackupSnapshotAsync();
        Assert.Single(backup.Sessions);
        Assert.Single(backup.Sessions[0].Sets);
        Assert.Equal(snapshot, backup.Sessions[0].Session.PlanSnapshotJson);

        await _sut.DeleteSessionAsync(sessionId);
        Assert.Equal(0, await _sut.GetSessionCountAsync());

        await _sut.ImportBackupAsync(backup);

        Assert.Equal(1, await _sut.GetSessionCountAsync());
        IReadOnlyList<WorkoutSessionLogEntity> sessions = await _sut.GetRecentSessionsAsync();
        Assert.Single(sessions);
        Assert.Equal(sessionId, sessions[0].Id);
        Assert.Equal("Leg day", sessions[0].PlanName);

        IReadOnlyList<WorkoutSetLogEntity> sets = await _sut.GetSetsForSessionAsync(sessionId);
        Assert.Single(sets);
        Assert.Equal(5, sets[0].Reps);
        Assert.Equal(40, sets[0].WeightKg);
    }

    [Fact]
    public async Task UpsertBodyweightLogAsync_InsertsThenReplacesForSameDay()
    {
        var day = new DateOnly(2026, 8, 6);

        await _sut.UpsertBodyweightLogAsync(day, 82.5);
        await _sut.UpsertBodyweightLogAsync(day, 81.8);

        IReadOnlyList<BodyweightLogEntity> rows = await _sut.GetBodyweightLogsAsync();
        Assert.Single(rows);
        Assert.Equal(81.8, rows[0].BodyweightKg);
        Assert.Equal("2026-08-06", rows[0].Date);
    }

    [Fact]
    public async Task UpsertBodyweightLogAsync_RejectsNonPositiveValues()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _sut.UpsertBodyweightLogAsync(new DateOnly(2026, 8, 6), 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _sut.UpsertBodyweightLogAsync(new DateOnly(2026, 8, 6), -5));
    }

    [Fact]
    public async Task GetBodyweightLogsAsync_OrdersNewestFirst_AndRespectsLimit()
    {
        await _sut.UpsertBodyweightLogAsync(new DateOnly(2026, 8, 4), 84);
        await _sut.UpsertBodyweightLogAsync(new DateOnly(2026, 8, 6), 83);
        await _sut.UpsertBodyweightLogAsync(new DateOnly(2026, 8, 5), 83.5);

        IReadOnlyList<BodyweightLogEntity> rows = await _sut.GetBodyweightLogsAsync(2);

        Assert.Equal(2, rows.Count);
        Assert.Equal(["2026-08-06", "2026-08-05"], rows.Select(r => r.Date));
    }

    [Fact]
    public async Task DeleteBodyweightLogAsync_RemovesOnlyThatDay()
    {
        await _sut.UpsertBodyweightLogAsync(new DateOnly(2026, 8, 5), 83.5);
        await _sut.UpsertBodyweightLogAsync(new DateOnly(2026, 8, 6), 83);

        Assert.True(await _sut.DeleteBodyweightLogAsync(new DateOnly(2026, 8, 5)));
        Assert.False(await _sut.DeleteBodyweightLogAsync(new DateOnly(2026, 8, 5)));

        IReadOnlyList<BodyweightLogEntity> rows = await _sut.GetBodyweightLogsAsync();
        Assert.Single(rows);
        Assert.Equal("2026-08-06", rows[0].Date);
    }

    [Fact]
    public async Task CreateBackupSnapshotAsync_IncludesBodyweightEntries_AndImportRestoresThem()
    {
        await _sut.UpsertBodyweightLogAsync(new DateOnly(2026, 8, 5), 83.5);
        await _sut.UpsertBodyweightLogAsync(new DateOnly(2026, 8, 6), 83);

        WorkoutHistoryBackup backup = await _sut.CreateBackupSnapshotAsync();
        Assert.Equal(2, backup.BodyweightEntries.Count);

        await _sut.DeleteBodyweightLogAsync(new DateOnly(2026, 8, 5));
        await _sut.DeleteBodyweightLogAsync(new DateOnly(2026, 8, 6));
        Assert.Empty(await _sut.GetBodyweightLogsAsync());

        await _sut.ImportBackupAsync(backup);

        IReadOnlyList<BodyweightLogEntity> rows = await _sut.GetBodyweightLogsAsync();
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Date == "2026-08-05" && Math.Abs(r.BodyweightKg - 83.5) < 0.001);
        Assert.Contains(rows, r => r.Date == "2026-08-06" && Math.Abs(r.BodyweightKg - 83) < 0.001);
    }

    [Fact]
    public async Task GetSessionCountsByLocalDayAsync_GroupsSameUtcInstantIntoOneDay()
    {
        var started = new DateTime(2024, 4, 10, 15, 30, 0, DateTimeKind.Utc);
        await InsertRawSessionAsync(started);
        await InsertRawSessionAsync(started);

        var localDay = DateOnly.FromDateTime(started.ToLocalTime().Date);
        IReadOnlyDictionary<DateOnly, int> map = await _sut.GetSessionCountsByLocalDayAsync(
            new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Single(map);
        Assert.Equal(2, map[localDay]);
    }

    [Fact]
    public async Task GetSessionCountsByLocalDayAsync_RespectsUtcRange()
    {
        var inside = new DateTime(2024, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        var outside = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        await InsertRawSessionAsync(inside);
        await InsertRawSessionAsync(outside);

        IReadOnlyDictionary<DateOnly, int> map = await _sut.GetSessionCountsByLocalDayAsync(
            new DateTime(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        var insideDay = DateOnly.FromDateTime(inside.ToLocalTime().Date);
        Assert.Single(map);
        Assert.Equal(1, map[insideDay]);
    }

    [Fact]
    public async Task GetSessionsForLocalDayAsync_ReturnsSessionsStartingThatLocalDay()
    {
        var localDay = new DateOnly(2024, 8, 20);
        TimeZoneInfo tz = TimeZoneInfo.Local;
        var startLocal = DateTime.SpecifyKind(localDay.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        DateTime startedUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal.AddHours(14), tz);

        await InsertRawSessionAsync(startedUtc);
        await InsertRawSessionAsync(startedUtc.AddMinutes(30));

        IReadOnlyList<WorkoutSessionLogEntity> list = await _sut.GetSessionsForLocalDayAsync(localDay);
        Assert.Equal(2, list.Count);
        Assert.All(list, s => Assert.Equal(localDay, DateOnly.FromDateTime(s.StartedAtUtc.ToLocalTime().Date)));
    }

    [Fact]
    public async Task GetSessionsForLocalDayAsync_ExcludesAdjacentLocalDays()
    {
        var localDay = new DateOnly(2024, 3, 10);
        TimeZoneInfo tz = TimeZoneInfo.Local;
        DateTime prevUtc = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(localDay.AddDays(-1).ToDateTime(new TimeOnly(12, 0)), DateTimeKind.Unspecified),
            tz);
        DateTime nextUtc = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(localDay.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified),
            tz);

        await InsertRawSessionAsync(prevUtc);
        await InsertRawSessionAsync(nextUtc);

        IReadOnlyList<WorkoutSessionLogEntity> list = await _sut.GetSessionsForLocalDayAsync(localDay);
        Assert.Empty(list);
    }

    [Fact]
    public async Task GetExerciseSessionProgressAsync_GroupsSetsPerSession_NewestFirst()
    {
        var planId = Guid.NewGuid();
        TimeZoneInfo tz = TimeZoneInfo.Local;
        var day1 = new DateOnly(2024, 2, 1);
        var day2 = new DateOnly(2024, 2, 5);
        DateTime t1 = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(day1.ToDateTime(TimeOnly.MinValue).AddHours(9), DateTimeKind.Unspecified), tz);
        DateTime t2 = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(day2.ToDateTime(TimeOnly.MinValue).AddHours(9), DateTimeKind.Unspecified), tz);

        var s1 = await InsertSessionAtUtcAsync(planId, "Old", t1);
        await _sut.LogSetAsync(s1, 0, "Bench", 0, reps: 8, weightKg: 50);
        await _sut.LogSetAsync(s1, 0, "Bench", 1, reps: 6, weightKg: 55);

        var s2 = await InsertSessionAtUtcAsync(planId, "New", t2);
        await _sut.LogSetAsync(s2, 0, "Bench", 0, reps: 5, weightKg: 60);

        IReadOnlyList<ExerciseSessionProgressEntry> rows = await _sut.GetExerciseSessionProgressAsync(planId, "Bench", maxSessions: 10);
        Assert.Equal(2, rows.Count);
        Assert.Equal(s2, rows[0].SessionId);
        Assert.Equal(60, rows[0].BestWeightKg);
        Assert.Equal(5, rows[0].TotalReps);
        Assert.Equal(1, rows[0].SetCount);
        Assert.Equal(300, rows[0].TotalVolumeKg);

        Assert.Equal(s1, rows[1].SessionId);
        Assert.Equal(55, rows[1].BestWeightKg);
        Assert.Equal(14, rows[1].TotalReps);
        Assert.Equal(2, rows[1].SetCount);
        Assert.Equal(730, rows[1].TotalVolumeKg);
    }

    [Fact]
    public async Task GetExerciseSessionProgressAsync_PartialSetMetrics_CountZeroVolume()
    {
        var planId = Guid.NewGuid();
        TimeZoneInfo tz = TimeZoneInfo.Local;
        var day1 = new DateOnly(2024, 3, 1);
        var day2 = new DateOnly(2024, 3, 2);
        var day3 = new DateOnly(2024, 3, 3);
        DateTime t1 = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(day1.ToDateTime(TimeOnly.MinValue).AddHours(9), DateTimeKind.Unspecified), tz);
        DateTime t2 = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(day2.ToDateTime(TimeOnly.MinValue).AddHours(9), DateTimeKind.Unspecified), tz);
        DateTime t3 = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(day3.ToDateTime(TimeOnly.MinValue).AddHours(9), DateTimeKind.Unspecified), tz);

        var sRepsOnly = await InsertSessionAtUtcAsync(planId, "Reps", t1);
        await _sut.LogSetAsync(sRepsOnly, 0, "Pull-Up", 0, reps: 10, weightKg: null);

        var sWeightOnly = await InsertSessionAtUtcAsync(planId, "Weight", t2);
        await _sut.LogSetAsync(sWeightOnly, 0, "Pull-Up", 0, reps: null, weightKg: 40);

        var sMixed = await InsertSessionAtUtcAsync(planId, "Mixed", t3);
        await _sut.LogSetAsync(sMixed, 0, "Pull-Up", 0, reps: 8, weightKg: 50);
        await _sut.LogSetAsync(sMixed, 0, "Pull-Up", 1, reps: 5, weightKg: null);

        IReadOnlyList<ExerciseSessionProgressEntry> rows = await _sut.GetExerciseSessionProgressAsync(planId, "Pull-Up", maxSessions: 10);
        Assert.Equal(3, rows.Count);
        Assert.Equal(sMixed, rows[0].SessionId);
        Assert.Equal(400, rows[0].TotalVolumeKg);
        Assert.Equal(sWeightOnly, rows[1].SessionId);
        Assert.Equal(0, rows[1].TotalVolumeKg);
        Assert.Equal(sRepsOnly, rows[2].SessionId);
        Assert.Equal(0, rows[2].TotalVolumeKg);
    }

    [Fact]
    public async Task GetExerciseSessionProgressAsync_ScopesByPlanAndName()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var s1 = await _sut.BeginSessionAsync(p1, "A", null);
        await _sut.LogSetAsync(s1, 0, "X", 0, reps: 1, weightKg: 10);
        var s2 = await _sut.BeginSessionAsync(p2, "B", null);
        await _sut.LogSetAsync(s2, 0, "X", 0, reps: 1, weightKg: 20);

        IReadOnlyList<ExerciseSessionProgressEntry> rows = await _sut.GetExerciseSessionProgressAsync(p1, "X", 10);
        Assert.Single(rows);
        Assert.Equal(10, rows[0].BestWeightKg);
        Assert.Equal(10, rows[0].TotalVolumeKg);
    }

    [Fact]
    public async Task GetExerciseSessionProgressAsync_WithBodyweight_ComputesAdjustedVolume()
    {
        var planId = Guid.NewGuid();
        TimeZoneInfo tz = TimeZoneInfo.Local;
        var day1 = new DateOnly(2024, 4, 1);
        DateTime t1 = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(day1.ToDateTime(TimeOnly.MinValue).AddHours(9), DateTimeKind.Unspecified), tz);

        var s1 = await InsertSessionAtUtcAsync(planId, "Bodyweight", t1);
        // Set 1: 10 reps, null weight (offset 0)
        await _sut.LogSetAsync(s1, 0, "Pull-Up", 0, reps: 10, weightKg: null);
        // Set 2: 8 reps, +5 kg offset
        await _sut.LogSetAsync(s1, 0, "Pull-Up", 1, reps: 8, weightKg: 5);
        // Set 3: 6 reps, -10 kg offset
        await _sut.LogSetAsync(s1, 0, "Pull-Up", 2, reps: 6, weightKg: -10);

        // Bodyweight is set to 80 kg
        IReadOnlyList<ExerciseSessionProgressEntry> rows = await _sut.GetExerciseSessionProgressAsync(planId, "Pull-Up", maxSessions: 10, bodyweightKg: 80, logType: ExerciseLogType.BodyweightReps);
        Assert.Single(rows);
        // Best weight should still be the max offset (5 kg)
        Assert.Equal(5, rows[0].BestWeightKg);
        Assert.Equal(24, rows[0].TotalReps);
        Assert.Equal(3, rows[0].SetCount);
        // Total Volume = 10 * (80 + 0) + 8 * (80 + 5) + 6 * (80 - 10)
        //              = 800 + 680 + 420 = 1900
        Assert.Equal(1900, rows[0].TotalVolumeKg);
    }

    [Fact]
    public async Task GetExerciseSessionProgressAsync_WithBodyweight_DoesNotAffectWeightedExercises()
    {
        var planId = Guid.NewGuid();
        TimeZoneInfo tz = TimeZoneInfo.Local;
        var day1 = new DateOnly(2024, 5, 1);
        DateTime t1 = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(day1.ToDateTime(TimeOnly.MinValue).AddHours(9), DateTimeKind.Unspecified), tz);

        var s1 = await InsertSessionAtUtcAsync(planId, "Weighted", t1);
        await _sut.LogSetAsync(s1, 0, "Bench", 0, reps: 8, weightKg: 100);

        // A profile bodyweight must never be folded into a weighted exercise's volume.
        IReadOnlyList<ExerciseSessionProgressEntry> rows = await _sut.GetExerciseSessionProgressAsync(planId, "Bench", maxSessions: 10, bodyweightKg: 80, logType: ExerciseLogType.WeightAndReps);
        Assert.Single(rows);
        Assert.Equal(800, rows[0].TotalVolumeKg);
    }

    [Fact]
    public async Task ImportBackupAsync_Idempotent_WhenReImportingSameBackup()
    {
        var sessionId = await _sut.BeginSessionAsync(Guid.NewGuid(), "X", null);
        await _sut.LogSetAsync(sessionId, 0, "Move", 0, reps: 3, weightKg: null);
        WorkoutHistoryBackup backup = await _sut.CreateBackupSnapshotAsync();

        await _sut.ImportBackupAsync(backup);
        await _sut.ImportBackupAsync(backup);

        Assert.Equal(1, await _sut.GetSessionCountAsync());
        IReadOnlyList<WorkoutSetLogEntity> sets = await _sut.GetSetsForSessionAsync(sessionId);
        Assert.Single(sets);
        Assert.Equal(3, sets[0].Reps);
    }

    [Fact]
    public async Task GetInProgressSessionForPlanAsync_ReturnsOpenSession_IgnoresEnded()
    {
        var planId = Guid.NewGuid();
        var openId = await _sut.BeginSessionAsync(planId, "Open", null);
        var endedId = await _sut.BeginSessionAsync(planId, "Ended", null);
        await _sut.EndSessionAsync(endedId);

        WorkoutSessionLogEntity? result = await _sut.GetInProgressSessionForPlanAsync(planId);

        Assert.NotNull(result);
        Assert.Equal(openId, result.Id);
    }

    [Fact]
    public async Task GetAnyInProgressSessionAsync_ReturnsNewestOpen()
    {
        var olderPlan = Guid.NewGuid();
        var newerPlan = Guid.NewGuid();
        var baseTime = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        await InsertSessionAtUtcAsync(olderPlan, "Older", baseTime);
        var newerId = await InsertSessionAtUtcAsync(newerPlan, "Newer", baseTime.AddHours(1));

        WorkoutSessionLogEntity? result = await _sut.GetAnyInProgressSessionAsync();

        Assert.NotNull(result);
        Assert.Equal(newerId, result.Id);
        Assert.Equal("Newer", result.PlanName);
    }

    private Task InsertRawSessionAsync(DateTime startedAtUtc)
    {
        return _db.Database.InsertAsync(new WorkoutSessionLogEntity
        {
            Id = Guid.NewGuid().ToString(),
            WorkoutPlanId = Guid.NewGuid().ToString(),
            PlanName = "Test",
            StartedAtUtc = startedAtUtc
        });
    }

    [Fact]
    public async Task GetRecentSessionsAsync_PaginatesWithOffset()
    {
        var baseTime = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 5; i++)
        {
            await InsertRawSessionAsync(baseTime.AddHours(i));
        }

        IReadOnlyList<WorkoutSessionLogEntity> first = await _sut.GetRecentSessionsAsync(limit: 2);
        Assert.Equal(2, first.Count);

        IReadOnlyList<WorkoutSessionLogEntity> second = await _sut.GetRecentSessionsAsync(limit: 2, offset: 2);
        Assert.Equal(2, second.Count);

        IReadOnlyList<WorkoutSessionLogEntity> all = await _sut.GetRecentSessionsAsync(limit: 10);
        Assert.Equal(all.Take(4).Select(s => s.Id), first.Select(s => s.Id).Concat(second.Select(s => s.Id)));

        IReadOnlyList<WorkoutSessionLogEntity> beyond = await _sut.GetRecentSessionsAsync(limit: 2, offset: 4);
        Assert.Single(beyond);

        Assert.Empty(await _sut.GetRecentSessionsAsync(limit: 2, offset: 10));
    }

    [Fact]
    public async Task UpdateSetLogAsync_UpdatesRepsAndWeight()
    {
        var sessionId = await _sut.BeginSessionAsync(Guid.NewGuid(), "Test", null);
        await _sut.LogSetAsync(sessionId, 0, "Squat", 2, reps: 8, weightKg: 100);

        Assert.True(await _sut.UpdateSetLogAsync(sessionId, 0, 2, reps: 10, weightKg: 102.5));

        WorkoutSetLogEntity updated = Assert.Single(await _sut.GetSetsForSessionAsync(sessionId));
        Assert.Equal(10, updated.Reps);
        Assert.Equal(102.5, updated.WeightKg);
    }

    [Fact]
    public async Task UpdateSetLogAsync_ReturnsFalse_WhenSetMissing()
    {
        var sessionId = await _sut.BeginSessionAsync(Guid.NewGuid(), "Test", null);

        Assert.False(await _sut.UpdateSetLogAsync(sessionId, 0, 0, reps: 5, weightKg: 50));
    }

    [Fact]
    public async Task GetExerciseNamesAsync_ReturnsDistinctNames_MostRecentFirst()
    {
        var sessionId = await _sut.BeginSessionAsync(Guid.NewGuid(), "Day", null);
        var baseTime = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        await InsertSetRowAsync(sessionId, "Squat", baseTime);
        await InsertSetRowAsync(sessionId, "Bench", baseTime.AddMinutes(1));
        await InsertSetRowAsync(sessionId, "Squat", baseTime.AddMinutes(2));

        IReadOnlyList<string> names = await _sut.GetExerciseNamesAsync();

        Assert.Equal(["Squat", "Bench"], names);
    }

    [Fact]
    public async Task GetExerciseNamesAsync_ReturnsEmpty_WhenNoLogs()
    {
        Assert.Empty(await _sut.GetExerciseNamesAsync());
    }

    [Fact]
    public async Task GetSessionIdsForExerciseNameAsync_MatchesCaseInsensitiveSubstring()
    {
        var s1 = await _sut.BeginSessionAsync(Guid.NewGuid(), "A", null);
        await _sut.LogSetAsync(s1, 0, "Bench Press", 0, reps: 5);
        var s2 = await _sut.BeginSessionAsync(Guid.NewGuid(), "B", null);
        await _sut.LogSetAsync(s2, 0, "Squat", 0, reps: 5);

        IReadOnlySet<string> matches = await _sut.GetSessionIdsForExerciseNameAsync("bench");

        Assert.Single(matches);
        Assert.Contains(s1, matches);
    }

    [Fact]
    public async Task GetSessionIdsForExerciseNameAsync_ReturnsEmpty_WhenNoMatch()
    {
        var s1 = await _sut.BeginSessionAsync(Guid.NewGuid(), "A", null);
        await _sut.LogSetAsync(s1, 0, "Squat", 0, reps: 5);

        Assert.Empty(await _sut.GetSessionIdsForExerciseNameAsync("Deadlift"));
    }

    [Fact]
    public async Task GetLastSessionDateByPlanAsync_ReturnsNewestStartPerPlan()
    {
        var planId = Guid.NewGuid();
        var otherPlanId = Guid.NewGuid();
        var baseTime = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);
        await InsertSessionAtUtcAsync(planId, "Old", baseTime);
        await InsertSessionAtUtcAsync(planId, "New", baseTime.AddDays(2));
        await InsertSessionAtUtcAsync(otherPlanId, "Other", baseTime.AddDays(1));

        IReadOnlyDictionary<string, DateTime> map = await _sut.GetLastSessionDateByPlanAsync();

        Assert.Equal(2, map.Count);
        Assert.Equal(baseTime.AddDays(2), map[planId.ToString()]);
        Assert.Equal(baseTime.AddDays(1), map[otherPlanId.ToString()]);
    }

    private async Task<string> InsertSessionAtUtcAsync(Guid planId, string planName, DateTime startedAtUtc)
    {
        var id = Guid.NewGuid().ToString();
        await _db.Database.InsertAsync(new WorkoutSessionLogEntity
        {
            Id = id,
            WorkoutPlanId = planId.ToString(),
            PlanName = planName,
            StartedAtUtc = startedAtUtc
        });
        return id;
    }

    private Task InsertSetRowAsync(string sessionId, string exerciseName, DateTime completedAtUtc) =>
        _db.Database.InsertAsync(new WorkoutSetLogEntity
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            ExerciseIndex = 0,
            ExerciseName = exerciseName,
            SetIndex = 0,
            CompletedAtUtc = completedAtUtc,
            Reps = 5
        });
}
