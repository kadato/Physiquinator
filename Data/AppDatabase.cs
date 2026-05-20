using SQLite;

namespace Physiquinator.Data;

public class AppDatabase
{
    private SQLiteAsyncConnection _database;
    private Task _initializationTask;

    public AppDatabase(string dbPath)
    {
        _database = new SQLiteAsyncConnection(dbPath);
        _initializationTask = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await _database.CreateTableAsync<WorkoutPlanEntity>();
        await _database.CreateTableAsync<ExercisePlanEntity>();
        await _database.CreateTableAsync<WorkoutSessionLogEntity>();
        await _database.CreateTableAsync<WorkoutSetLogEntity>();
        await MigrateAsync(_database);
    }

    public async Task SwitchDatabaseAsync(string dbPath)
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
        await _database.ExecuteAsync("DELETE FROM WorkoutSetLogs");
        await _database.ExecuteAsync("DELETE FROM WorkoutSessionLogs");
        await _database.ExecuteAsync("DELETE FROM ExercisePlans");
        await _database.ExecuteAsync("DELETE FROM WorkoutPlans");
    }

    public SQLiteAsyncConnection Database => _database;
}
