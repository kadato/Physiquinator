using SQLite;

namespace Physiquinator.Core.Data;

public sealed class AppDatabase
{
    private readonly SemaphoreSlim _switchLock = new(1, 1);
    private readonly Task? _batteriesInitTask;
    private SQLiteAsyncConnection _database;
    private Task _initializationTask;

    public AppDatabase(string dbPath, Task? batteriesInitTask = null)
    {
        _batteriesInitTask = batteriesInitTask;
        _database = new SQLiteAsyncConnection(dbPath);
        _initializationTask = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        // Wait for the native SQLite library to finish loading (done on a
        // background thread in MauiProgram) before touching any SQL.
        if (_batteriesInitTask != null)
            await _batteriesInitTask.ConfigureAwait(false);

        try
        {
            // Execute PRAGMAs safely. Some pragmas return row values and must use ExecuteScalarAsync.
            await _database.ExecuteScalarAsync<string>("PRAGMA journal_mode = WAL;").ConfigureAwait(false);
            await _database.ExecuteScalarAsync<string>("PRAGMA synchronous = NORMAL;").ConfigureAwait(false);
            await _database.ExecuteScalarAsync<string>("PRAGMA temp_store = MEMORY;").ConfigureAwait(false);
            await _database.ExecuteScalarAsync<string>("PRAGMA cache_size = -2000;").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Ignore PRAGMA failures, for example for in-memory unit testing databases.
            System.Diagnostics.Debug.WriteLine(ex);
        }

        await _database.CreateTableAsync<WorkoutPlanEntity>();
        await _database.CreateTableAsync<ExercisePlanEntity>();
        await _database.CreateTableAsync<WorkoutSessionLogEntity>();
        await _database.CreateTableAsync<WorkoutSetLogEntity>();
        await _database.CreateTableAsync<BodyweightLogEntity>();
        await _database.CreateTableAsync<WorkoutScheduleHistoryEntity>();
        await MigrateAsync(_database);
    }

    /// <summary>
    /// Closes the current connection and swaps in a new database file.
    /// Serialized so concurrent switches or switch versus initialization cannot race.
    /// </summary>
    public async Task SwitchDatabaseAsync(string dbPath)
    {
        await _switchLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_database != null)
            {
                try
                {
                    await _database.CloseAsync().ConfigureAwait(false);
                }
                catch
                {
                    // Ignore connection closing errors
                }
            }
            _database = new SQLiteAsyncConnection(dbPath);
            _initializationTask = InitializeAsync();
            await _initializationTask.ConfigureAwait(false);
        }
        finally
        {
            _switchLock.Release();
        }
    }

    private const int CurrentSchemaVersion = 1;

    /// <summary>sqlite-net CreateTable does not add columns on existing installs.</summary>
    private static async Task MigrateAsync(SQLiteAsyncConnection db)
    {
        var userVersion = await db.ExecuteScalarAsync<int>("PRAGMA user_version;").ConfigureAwait(false);
        if (userVersion >= CurrentSchemaVersion)
            return;

        if (await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM pragma_table_info('WorkoutSessionLogs') WHERE name='PlanSnapshotJson'").ConfigureAwait(false) == 0)
            await db.ExecuteAsync("ALTER TABLE WorkoutSessionLogs ADD COLUMN PlanSnapshotJson TEXT").ConfigureAwait(false);

        if (await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM pragma_table_info('ExercisePlans') WHERE name='DefaultReps'").ConfigureAwait(false) == 0)
            await db.ExecuteAsync("ALTER TABLE ExercisePlans ADD COLUMN DefaultReps INTEGER").ConfigureAwait(false);

        if (await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM pragma_table_info('ExercisePlans') WHERE name='DefaultWeightKg'").ConfigureAwait(false) == 0)
            await db.ExecuteAsync("ALTER TABLE ExercisePlans ADD COLUMN DefaultWeightKg REAL").ConfigureAwait(false);

        if (await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM pragma_table_info('WorkoutPlans') WHERE name='SortOrder'").ConfigureAwait(false) == 0)
            await db.ExecuteAsync("ALTER TABLE WorkoutPlans ADD COLUMN SortOrder INTEGER NOT NULL DEFAULT 0").ConfigureAwait(false);

        if (await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM pragma_table_info('ExercisePlans') WHERE name='LogType'").ConfigureAwait(false) == 0)
            await db.ExecuteAsync("ALTER TABLE ExercisePlans ADD COLUMN LogType INTEGER NOT NULL DEFAULT 0").ConfigureAwait(false);

        if (await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM pragma_table_info('ExercisePlans') WHERE name='WarmupSetCount'").ConfigureAwait(false) == 0)
            await db.ExecuteAsync("ALTER TABLE ExercisePlans ADD COLUMN WarmupSetCount INTEGER NOT NULL DEFAULT 0").ConfigureAwait(false);

        if (await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM pragma_table_info('ExercisePlans') WHERE name='SupersetGroupId'").ConfigureAwait(false) == 0)
            await db.ExecuteAsync("ALTER TABLE ExercisePlans ADD COLUMN SupersetGroupId TEXT").ConfigureAwait(false);

        if (await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM pragma_table_info('ExercisePlans') WHERE name='BodyweightPercent'").ConfigureAwait(false) == 0)
            await db.ExecuteAsync("ALTER TABLE ExercisePlans ADD COLUMN BodyweightPercent REAL").ConfigureAwait(false);

        if (await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM pragma_table_info('WorkoutSetLogs') WHERE name='IsWarmup'").ConfigureAwait(false) == 0)
            await db.ExecuteAsync("ALTER TABLE WorkoutSetLogs ADD COLUMN IsWarmup INTEGER NOT NULL DEFAULT 0").ConfigureAwait(false);

        // sqlite-net only creates indexed-column indexes on a freshly created
        // table, so pre-existing installs get their indexes here instead.
        // Aggregate queries (progress chart, heatmap, latest-metrics) rely on them.
        await db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_WorkoutSetLogs_SessionId ON WorkoutSetLogs(SessionId)").ConfigureAwait(false);
        await db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_WorkoutSetLogs_ExerciseName ON WorkoutSetLogs(ExerciseName)").ConfigureAwait(false);
        await db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_WorkoutSetLogs_CompletedAtUtc ON WorkoutSetLogs(CompletedAtUtc)").ConfigureAwait(false);
        await db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_WorkoutSessionLogs_WorkoutPlanId ON WorkoutSessionLogs(WorkoutPlanId)").ConfigureAwait(false);
        await db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_WorkoutSessionLogs_StartedAtUtc ON WorkoutSessionLogs(StartedAtUtc)").ConfigureAwait(false);
        await db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_WorkoutSessionLogs_EndedAtUtc ON WorkoutSessionLogs(EndedAtUtc)").ConfigureAwait(false);

        await db.ExecuteAsync($"PRAGMA user_version = {CurrentSchemaVersion};").ConfigureAwait(false);
    }

    public async Task EnsureInitializedAsync() => await _initializationTask;

    /// <summary>
    /// Deletes all persisted workout plans, history, and set logs. Order respects child rows first.
    /// </summary>
    public async Task ClearAllUserDataAsync()
    {
        await EnsureInitializedAsync();
        await _database.ExecuteAsync("DELETE FROM WorkoutScheduleHistory");
        await _database.ExecuteAsync("DELETE FROM BodyweightLogs");
        await _database.ExecuteAsync("DELETE FROM WorkoutSetLogs");
        await _database.ExecuteAsync("DELETE FROM WorkoutSessionLogs");
        await _database.ExecuteAsync("DELETE FROM ExercisePlans");
        await _database.ExecuteAsync("DELETE FROM WorkoutPlans");
    }

    public SQLiteAsyncConnection Database => _database;
}
