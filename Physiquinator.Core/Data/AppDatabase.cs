using SQLite;

namespace Physiquinator.Core.Data;

public sealed class AppDatabase
{
    private readonly SemaphoreSlim _switchLock = new(1, 1);
    private SQLiteAsyncConnection _database;
    private Task _initializationTask;

    public AppDatabase(string dbPath)
    {
        _database = new SQLiteAsyncConnection(dbPath);
        _initializationTask = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            // Execute PRAGMAs safely. Some pragmas return row values and must use ExecuteScalarAsync.
            await _database.ExecuteScalarAsync<string>("PRAGMA journal_mode = WAL;").ConfigureAwait(false);
            await _database.ExecuteScalarAsync<string>("PRAGMA synchronous = NORMAL;").ConfigureAwait(false);
            await _database.ExecuteScalarAsync<string>("PRAGMA temp_store = MEMORY;").ConfigureAwait(false);
            await _database.ExecuteScalarAsync<string>("PRAGMA cache_size = -2000;").ConfigureAwait(false);
        }
        catch
        {
            // Ignore PRAGMA failures (e.g. for in-memory unit testing databases)
        }

        await _database.CreateTableAsync<WorkoutPlanEntity>();
        await _database.CreateTableAsync<ExercisePlanEntity>();
        await _database.CreateTableAsync<WorkoutSessionLogEntity>();
        await _database.CreateTableAsync<WorkoutSetLogEntity>();
        await _database.CreateTableAsync<BodyweightLogEntity>();
        await MigrateAsync(_database);
    }

    /// <summary>
    /// Closes the current connection and swaps in a new database file.
    /// Serialized so concurrent switches (or switch vs. initialization) cannot race.
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

    /// <summary>sqlite-net CreateTable does not add columns on existing installs.</summary>
    private static async Task MigrateAsync(SQLiteAsyncConnection db)
    {
        if (await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM pragma_table_info('WorkoutSessionLogs') WHERE name='PlanSnapshotJson'") == 0)
            await db.ExecuteAsync("ALTER TABLE WorkoutSessionLogs ADD COLUMN PlanSnapshotJson TEXT");

        if (await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM pragma_table_info('ExercisePlans') WHERE name='DefaultReps'") == 0)
            await db.ExecuteAsync("ALTER TABLE ExercisePlans ADD COLUMN DefaultReps INTEGER");

        if (await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM pragma_table_info('ExercisePlans') WHERE name='DefaultWeightKg'") == 0)
            await db.ExecuteAsync("ALTER TABLE ExercisePlans ADD COLUMN DefaultWeightKg REAL");

        if (await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM pragma_table_info('WorkoutPlans') WHERE name='SortOrder'") == 0)
            await db.ExecuteAsync("ALTER TABLE WorkoutPlans ADD COLUMN SortOrder INTEGER NOT NULL DEFAULT 0");

        if (await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM pragma_table_info('ExercisePlans') WHERE name='LogType'") == 0)
            await db.ExecuteAsync("ALTER TABLE ExercisePlans ADD COLUMN LogType INTEGER NOT NULL DEFAULT 0");

        // sqlite-net only creates indexed-column indexes on a freshly created
        // table, so pre-existing installs get their indexes here instead.
        // Aggregate queries (progress chart, heatmap, latest-metrics) rely on them.
        await db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_WorkoutSetLogs_SessionId ON WorkoutSetLogs(SessionId)");
        await db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_WorkoutSetLogs_ExerciseName ON WorkoutSetLogs(ExerciseName)");
        await db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_WorkoutSetLogs_CompletedAtUtc ON WorkoutSetLogs(CompletedAtUtc)");
        await db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_WorkoutSessionLogs_WorkoutPlanId ON WorkoutSessionLogs(WorkoutPlanId)");
        await db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_WorkoutSessionLogs_StartedAtUtc ON WorkoutSessionLogs(StartedAtUtc)");
        await db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_WorkoutSessionLogs_EndedAtUtc ON WorkoutSessionLogs(EndedAtUtc)");
    }

    public async Task EnsureInitializedAsync()
    {
        await _initializationTask;
    }

    /// <summary>
    /// Deletes all persisted workout plans, history, and set logs. Order respects child rows first.
    /// </summary>
    public async Task ClearAllUserDataAsync()
    {
        await EnsureInitializedAsync();
        await _database.ExecuteAsync("DELETE FROM BodyweightLogs");
        await _database.ExecuteAsync("DELETE FROM WorkoutSetLogs");
        await _database.ExecuteAsync("DELETE FROM WorkoutSessionLogs");
        await _database.ExecuteAsync("DELETE FROM ExercisePlans");
        await _database.ExecuteAsync("DELETE FROM WorkoutPlans");
    }

    public SQLiteAsyncConnection Database => _database;
}
