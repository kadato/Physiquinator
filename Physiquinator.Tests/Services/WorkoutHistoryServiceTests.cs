using Physiquinator.Core.Data;
using Physiquinator.Core.Services;
using Xunit;

namespace Physiquinator.Tests.Services;

public class WorkoutHistoryServiceTests : IAsyncLifetime
{
    private AppDatabase _db = null!;
    private WorkoutHistoryRepository _repo = null!;
    private WorkoutHistoryService _sut = null!;

    static WorkoutHistoryServiceTests() => SQLitePCL.Batteries_V2.Init();

    public async Task InitializeAsync()
    {
        _db = new AppDatabase(":memory:");
        await _db.EnsureInitializedAsync();
        _repo = new WorkoutHistoryRepository(_db, TimeProvider.System);
        _sut = new WorkoutHistoryService(_repo);
    }

    public async Task DisposeAsync()
    {
        await _db.Database.CloseAsync();
    }

    [Fact]
    public async Task ExportToJsonAsync_ThenImportFromJsonAsync_RoundTrips()
    {
        var sessionId = await _repo.BeginSessionAsync(Guid.NewGuid(), "Push", null);
        await _repo.LogSetAsync(sessionId, 0, "Press", 0, reps: 8, weightKg: 30);

        var json = await _sut.ExportToJsonAsync();
        await _repo.DeleteSessionAsync(sessionId);

        (var sessions, var sets) = await _sut.ImportFromJsonAsync(json);
        Assert.Equal(1, sessions);
        Assert.Equal(1, sets);

        IReadOnlyList<WorkoutSessionLogEntity> restored = await _repo.GetRecentSessionsAsync();
        Assert.Single(restored);
        IReadOnlyList<WorkoutSetLogEntity> logged = await _repo.GetSetsForSessionAsync(restored[0].Id);
        Assert.Single(logged);
        Assert.Equal(8, logged[0].Reps);
        Assert.Equal(30, logged[0].WeightKg);
    }

    [Fact]
    public Task ImportFromJsonAsync_Throws_WhenFormatVersionUnsupported()
    {
        const string json = """{"formatVersion":999,"sessions":[]}""";
        return Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ImportFromJsonAsync(json));
    }

    [Fact]
    public async Task PreviewImportAsync_counts_without_importing()
    {
        var sessionId = await _repo.BeginSessionAsync(Guid.NewGuid(), "Push", null);
        await _repo.LogSetAsync(sessionId, 0, "Press", 0, reps: 8, weightKg: 30);
        await _repo.LogSetAsync(sessionId, 0, "Press", 1, reps: 8, weightKg: 30);
        await _repo.UpsertBodyweightLogAsync(DateOnly.FromDateTime(DateTime.Today), 80);

        var json = await _sut.ExportToJsonAsync();
        await _repo.DeleteSessionAsync(sessionId);
        await _repo.DeleteBodyweightLogAsync(DateOnly.FromDateTime(DateTime.Today));

        HistoryImportPreview preview = await WorkoutHistoryService.PreviewImportAsync(json);

        Assert.Equal(1, preview.Sessions);
        Assert.Equal(2, preview.Sets);
        Assert.Equal(1, preview.BodyweightEntries);
        Assert.Empty(await _repo.GetRecentSessionsAsync());
    }

    [Fact]
    public Task PreviewImportAsync_throws_for_unsupported_version()
    {
        const string json = """{"formatVersion":999,"sessions":[]}""";
        return Assert.ThrowsAsync<InvalidOperationException>(
            () => WorkoutHistoryService.PreviewImportAsync(json));
    }
}
