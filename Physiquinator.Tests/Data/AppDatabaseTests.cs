using Physiquinator.Core.Data;
using SQLite;
using Xunit;

namespace Physiquinator.Tests.Data;

public class AppDatabaseTests : IAsyncLifetime
{
    private string _pathA = null!;
    private AppDatabase _db = null!;

    static AppDatabaseTests() => SQLitePCL.Batteries_V2.Init();

    public async Task InitializeAsync()
    {
        _pathA = Path.Combine(Path.GetTempPath(), $"physiquinator_db_test_{Guid.NewGuid():N}.db");
        _db = new AppDatabase(_pathA);
        await _db.EnsureInitializedAsync();
    }

    public async Task DisposeAsync() => await _db.Database.CloseAsync();

    [Fact]
    public async Task EnsureInitializedAsync_CreatesAllTables()
    {
        List<string> tables = await _db.Database.QueryScalarsAsync<string>(
            "SELECT name FROM sqlite_master WHERE type = 'table'");

        foreach (var expected in new[]
                 {
                     "WorkoutPlans", "ExercisePlans", "WorkoutSessionLogs",
                     "WorkoutSetLogs", "BodyweightLogs", "WorkoutScheduleHistory"
                 })
        {
            Assert.Contains(expected, tables);
        }
    }

    [Fact]
    public async Task SwitchDatabaseAsync_SwapsConnection_AndTargetsNewFile()
    {
        var pathB = Path.Combine(Path.GetTempPath(), $"physiquinator_db_test_{Guid.NewGuid():N}.db");
        SQLiteAsyncConnection originalConnection = _db.Database;

        await _db.Database.InsertAsync(new WorkoutPlanEntity
        {
            Id = Guid.NewGuid().ToString(),
            Name = "PlanA",
            RestIntervalSeconds = 60,
            DefaultSetCount = 3,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SwitchDatabaseAsync(pathB);

        Assert.NotSame(originalConnection, _db.Database);
        Assert.Equal(0, await _db.Database.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM WorkoutPlans"));

        await _db.Database.InsertAsync(new WorkoutPlanEntity
        {
            Id = Guid.NewGuid().ToString(),
            Name = "PlanB",
            RestIntervalSeconds = 90,
            DefaultSetCount = 4,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SwitchDatabaseAsync(_pathA);

        Assert.Equal(1, await _db.Database.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM WorkoutPlans"));
        Assert.Equal("PlanA", (await _db.Database.Table<WorkoutPlanEntity>().ToListAsync()).Single().Name);
    }

    [Fact]
    public async Task SwitchDatabaseAsync_AppliesPragmasAndMigrations_ToLegacySchema()
    {
        var legacyPath = Path.Combine(Path.GetTempPath(), $"physiquinator_db_legacy_{Guid.NewGuid():N}.db");
        var legacy = new SQLiteAsyncConnection(legacyPath);
        await legacy.ExecuteAsync("CREATE TABLE WorkoutPlans (Id TEXT PRIMARY KEY, Name TEXT, RestIntervalSeconds INTEGER, DefaultSetCount INTEGER, CreatedAt TEXT)");
        await legacy.ExecuteAsync("CREATE TABLE ExercisePlans (Id TEXT PRIMARY KEY, WorkoutPlanId TEXT, Name TEXT, SetCount INTEGER, [Order] INTEGER, RestIntervalSeconds INTEGER)");
        await legacy.ExecuteAsync("CREATE TABLE WorkoutSessionLogs (Id TEXT PRIMARY KEY, WorkoutPlanId TEXT, PlanName TEXT, StartedAtUtc TEXT, EndedAtUtc TEXT)");
        await legacy.CloseAsync();

        await _db.SwitchDatabaseAsync(legacyPath);

        var journalMode = await _db.Database.ExecuteScalarAsync<string>("PRAGMA journal_mode;");
        Assert.Equal("wal", journalMode, ignoreCase: true);

        Assert.Equal(1, await _db.Database.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pragma_table_info('WorkoutPlans') WHERE name = 'SortOrder'"));
        Assert.Equal(1, await _db.Database.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pragma_table_info('ExercisePlans') WHERE name = 'DefaultReps'"));
        Assert.Equal(1, await _db.Database.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pragma_table_info('ExercisePlans') WHERE name = 'DefaultWeightKg'"));
        Assert.Equal(1, await _db.Database.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pragma_table_info('ExercisePlans') WHERE name = 'LogType'"));
        Assert.Equal(1, await _db.Database.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pragma_table_info('WorkoutSessionLogs') WHERE name = 'PlanSnapshotJson'"));
    }
}
