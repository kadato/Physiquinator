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
            // busy_timeout makes concurrent writers (multiple circuits or tabs) wait
            // for the lock instead of failing with "database is locked" on contact.
            await _database.ExecuteScalarAsync<string>("PRAGMA busy_timeout = 5000;").ConfigureAwait(false);
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

        // Fast path for steady-state launches: user_version lives in the
        // database file itself, so a current version means this code already
        // created every table and index. Skip straight past CreateTable and
        // Migrate. Fresh files report version 0 and take the full path below.
        // When adding a table or a column, bump CurrentSchemaVersion so
        // existing installs take the full path again.
        var userVersion = await _database.ExecuteScalarAsync<int>("PRAGMA user_version;").ConfigureAwait(false);
        if (userVersion >= CurrentSchemaVersion)
            return;

        // Table creation runs sequentially: the shared connection cannot
        // overlap write transactions, and DDL counts as writes.
        await _database.CreateTableAsync<WorkoutPlanEntity>().ConfigureAwait(false);
        await _database.CreateTableAsync<ExercisePlanEntity>().ConfigureAwait(false);
        await _database.CreateTableAsync<WorkoutSessionLogEntity>().ConfigureAwait(false);
        await _database.CreateTableAsync<WorkoutSetLogEntity>().ConfigureAwait(false);
        await _database.CreateTableAsync<BodyweightLogEntity>().ConfigureAwait(false);
        await _database.CreateTableAsync<WorkoutScheduleHistoryEntity>().ConfigureAwait(false);
        await MigrateAsync(_database).ConfigureAwait(false);
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
                // Flush the WAL file into the main database so a hot-swap or a
                // subsequent process kill does not leave committed pages only in
                // the -wal sidecar. Without this a crash between the close and
                // the next open could appear as lost data on Android.
                try
                {
                    await _database.ExecuteAsync("PRAGMA wal_checkpoint(TRUNCATE);").ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"WAL checkpoint failed: {ex.Message}");
                }

                try
                {
                    await _database.CloseAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Database close failed: {ex.Message}");
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

#pragma warning disable S1144 // Row type is populated via reflection by sqlite-net.
#pragma warning disable S3459 // Unassigned members should be removed.
    private sealed class PragmaColumnRow
    {
        public string? Name { get; set; }
    }
#pragma warning restore S3459
#pragma warning restore S1144

    private static HashSet<string> ToColumnSet(List<PragmaColumnRow> rows)
    {
        return new HashSet<string>(
            rows.Where(r => !string.IsNullOrEmpty(r.Name)).Select(r => r.Name!),
            StringComparer.Ordinal);
    }

    /// <summary>sqlite-net CreateTable does not add columns on existing installs.</summary>
    private static async Task MigrateAsync(SQLiteAsyncConnection db)
    {
        var userVersion = await db.ExecuteScalarAsync<int>("PRAGMA user_version;").ConfigureAwait(false);
        if (userVersion >= CurrentSchemaVersion)
            return;

        // One column listing per table instead of one existence query per
        // column. Missing columns are decided in memory below.
        List<PragmaColumnRow> sessionColumns = await db.QueryAsync<PragmaColumnRow>(
            "SELECT name AS Name FROM pragma_table_info('WorkoutSessionLogs')").ConfigureAwait(false);
        List<PragmaColumnRow> exerciseColumns = await db.QueryAsync<PragmaColumnRow>(
            "SELECT name AS Name FROM pragma_table_info('ExercisePlans')").ConfigureAwait(false);
        List<PragmaColumnRow> planColumns = await db.QueryAsync<PragmaColumnRow>(
            "SELECT name AS Name FROM pragma_table_info('WorkoutPlans')").ConfigureAwait(false);
        List<PragmaColumnRow> setColumns = await db.QueryAsync<PragmaColumnRow>(
            "SELECT name AS Name FROM pragma_table_info('WorkoutSetLogs')").ConfigureAwait(false);

        HashSet<string> sessionColumnNames = ToColumnSet(sessionColumns);
        HashSet<string> exerciseColumnNames = ToColumnSet(exerciseColumns);
        HashSet<string> planColumnNames = ToColumnSet(planColumns);
        HashSet<string> setColumnNames = ToColumnSet(setColumns);

        if (!sessionColumnNames.Contains("PlanSnapshotJson"))
            await db.ExecuteAsync("ALTER TABLE WorkoutSessionLogs ADD COLUMN PlanSnapshotJson TEXT").ConfigureAwait(false);

        if (!exerciseColumnNames.Contains("DefaultReps"))
            await db.ExecuteAsync("ALTER TABLE ExercisePlans ADD COLUMN DefaultReps INTEGER").ConfigureAwait(false);

        if (!exerciseColumnNames.Contains("DefaultWeightKg"))
            await db.ExecuteAsync("ALTER TABLE ExercisePlans ADD COLUMN DefaultWeightKg REAL").ConfigureAwait(false);

        if (!planColumnNames.Contains("SortOrder"))
            await db.ExecuteAsync("ALTER TABLE WorkoutPlans ADD COLUMN SortOrder INTEGER NOT NULL DEFAULT 0").ConfigureAwait(false);

        if (!exerciseColumnNames.Contains("LogType"))
            await db.ExecuteAsync("ALTER TABLE ExercisePlans ADD COLUMN LogType INTEGER NOT NULL DEFAULT 0").ConfigureAwait(false);

        if (!exerciseColumnNames.Contains("WarmupSetCount"))
            await db.ExecuteAsync("ALTER TABLE ExercisePlans ADD COLUMN WarmupSetCount INTEGER NOT NULL DEFAULT 0").ConfigureAwait(false);

        if (!exerciseColumnNames.Contains("SupersetGroupId"))
            await db.ExecuteAsync("ALTER TABLE ExercisePlans ADD COLUMN SupersetGroupId TEXT").ConfigureAwait(false);

        if (!exerciseColumnNames.Contains("BodyweightPercent"))
            await db.ExecuteAsync("ALTER TABLE ExercisePlans ADD COLUMN BodyweightPercent REAL").ConfigureAwait(false);

        if (!setColumnNames.Contains("IsWarmup"))
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
