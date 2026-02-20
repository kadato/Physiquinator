using SQLite;

namespace Physiquinator.Data;

public class AppDatabase
{
    private readonly SQLiteAsyncConnection _database;
    private readonly Task _initializationTask;

    public AppDatabase(string dbPath)
    {
        _database = new SQLiteAsyncConnection(dbPath);
        _initializationTask = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await _database.CreateTableAsync<WorkoutPlanEntity>();
        await _database.CreateTableAsync<ExercisePlanEntity>();
    }

    public async Task EnsureInitializedAsync()
    {
        await _initializationTask;
    }

    public SQLiteAsyncConnection Database => _database;
}
