using Physiquinator.Data;
using Physiquinator.Models;
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
        _sut = new WorkoutHistoryRepository(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.Database.CloseAsync();
    }

    [Fact]
    public async Task GetRecentSessionsAsync_ReturnsEmpty_WhenNoSessions()
    {
        var result = await _sut.GetRecentSessionsAsync();

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

        var sessions = await _sut.GetRecentSessionsAsync();
        Assert.Single(sessions);
        Assert.Equal("Leg day", sessions[0].PlanName);
        Assert.Equal(planId.ToString(), sessions[0].WorkoutPlanId);
        Assert.Equal(snapshot, sessions[0].PlanSnapshotJson);
        Assert.NotNull(sessions[0].EndedAtUtc);

        var sets = await _sut.GetSetsForSessionAsync(sessionId);
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

        var sets = await _sut.GetSetsForSessionAsync(sessionId);

        Assert.Equal(["B", "A"], sets.Select(s => s.ExerciseName));
    }

    [Fact]
    public async Task TryDeleteLastSetLogAsync_RemovesLatestOnly()
    {
        var sessionId = await _sut.BeginSessionAsync(Guid.NewGuid(), "Test", null);
        await _sut.LogSetAsync(sessionId, 0, "A", 0);
        await _sut.LogSetAsync(sessionId, 0, "A", 1);

        Assert.True(await _sut.TryDeleteLastSetLogAsync(sessionId));
        var sets = await _sut.GetSetsForSessionAsync(sessionId);
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
    public async Task GetLatestSetMetricsForExerciseAsync_UsesMostRecentAcrossSessionsForSamePlan()
    {
        var planId = Guid.NewGuid();
        var s1 = await _sut.BeginSessionAsync(planId, "Day1", null);
        await _sut.LogSetAsync(s1, 0, "Squat", 0, reps: 10, weightKg: 60);
        await _sut.EndSessionAsync(s1);

        var m = await _sut.GetLatestSetMetricsForExerciseAsync(planId, "Squat");
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
        var parsed = WorkoutHistoryRepository.TryParsePlanSnapshot(json);
        Assert.NotNull(parsed);
        Assert.Equal("Push", parsed.Name);
        Assert.Single(parsed.Exercises);
        Assert.Equal(10, parsed.Exercises[0].DefaultReps);
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

        var backup = await _sut.CreateBackupSnapshotAsync();
        Assert.Single(backup.Sessions);
        Assert.Single(backup.Sessions[0].Sets);
        Assert.Equal(snapshot, backup.Sessions[0].Session.PlanSnapshotJson);

        await _sut.DeleteSessionAsync(sessionId);
        Assert.Equal(0, await _sut.GetSessionCountAsync());

        await _sut.ImportBackupAsync(backup);

        Assert.Equal(1, await _sut.GetSessionCountAsync());
        var sessions = await _sut.GetRecentSessionsAsync();
        Assert.Single(sessions);
        Assert.Equal(sessionId, sessions[0].Id);
        Assert.Equal("Leg day", sessions[0].PlanName);

        var sets = await _sut.GetSetsForSessionAsync(sessionId);
        Assert.Single(sets);
        Assert.Equal(5, sets[0].Reps);
        Assert.Equal(40, sets[0].WeightKg);
    }

    [Fact]
    public async Task ImportBackupAsync_Idempotent_WhenReImportingSameBackup()
    {
        var sessionId = await _sut.BeginSessionAsync(Guid.NewGuid(), "X", null);
        await _sut.LogSetAsync(sessionId, 0, "Move", 0, reps: 3, weightKg: null);
        var backup = await _sut.CreateBackupSnapshotAsync();

        await _sut.ImportBackupAsync(backup);
        await _sut.ImportBackupAsync(backup);

        Assert.Equal(1, await _sut.GetSessionCountAsync());
        var sets = await _sut.GetSetsForSessionAsync(sessionId);
        Assert.Single(sets);
        Assert.Equal(3, sets[0].Reps);
    }
}
