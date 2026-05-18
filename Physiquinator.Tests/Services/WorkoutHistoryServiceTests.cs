using Physiquinator.Data;
using Physiquinator.Services;
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
        _repo = new WorkoutHistoryRepository(_db);
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

        var (sessions, sets) = await _sut.ImportFromJsonAsync(json);
        Assert.Equal(1, sessions);
        Assert.Equal(1, sets);

        var restored = await _repo.GetRecentSessionsAsync();
        Assert.Single(restored);
        var logged = await _repo.GetSetsForSessionAsync(restored[0].Id);
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
}
