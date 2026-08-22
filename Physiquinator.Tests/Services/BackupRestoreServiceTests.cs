using Physiquinator.Core.Data;
using Physiquinator.Core.Models;
using Physiquinator.Core.Services;
using Physiquinator.Tests.TestDoubles;
using Xunit;

namespace Physiquinator.Tests.Services;

public sealed class BackupRestoreServiceTests : IAsyncLifetime
{
    private AppDatabase _db = null!;
    private InMemoryPreferences _prefs = null!;
    private UserProfileService _profileService = null!;
    private WorkoutPlanRepository _planRepo = null!;
    private WorkoutHistoryRepository _historyRepo = null!;
    private WorkoutPlanService _planService = null!;
    private WorkoutHistoryService _historyService = null!;
    private WorkoutScheduleService _scheduleService = null!;
    private BackupRestoreService _sut = null!;

    static BackupRestoreServiceTests() => SQLitePCL.Batteries_V2.Init();

    public async Task InitializeAsync()
    {
        _db = new AppDatabase(":memory:");
        await _db.EnsureInitializedAsync();

        _prefs = new InMemoryPreferences();

        _historyRepo = new WorkoutHistoryRepository(_db, TimeProvider.System);
        _planRepo = new WorkoutPlanRepository(_db);
        _planService = new WorkoutPlanService(_planRepo);
        _historyService = new WorkoutHistoryService(_historyRepo);

        // Minimal UserProfileService stub: DemoProfileId, no actual DB path needed for tests.
        var dbPathProvider = new LambdaDatabasePathProvider(_ => ":memory:");
        _profileService = new UserProfileService(_db, new WorkoutSessionService(TimeProvider.System), _prefs, dbPathProvider, TimeProvider.System);

        _scheduleService = new WorkoutScheduleService(_prefs, _profileService, _db);

        _sut = new BackupRestoreService(
            _prefs,
            _profileService,
            _planService,
            _historyService,
            _scheduleService,
            _db,
            _planRepo);
    }

    public async Task DisposeAsync() => await _db.Database.CloseAsync();

    // -------------------------------------------------------------------------
    //  Plans round-trip
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExportAllData_ThenImportAllData_RestoresPlans()
    {
        var plan = new WorkoutPlan { Id = Guid.NewGuid(), Name = "Push A", Exercises = [] };
        await _planService.SavePlanAsync(plan);

        var json = await _sut.ExportAllDataAsync();

        await _planService.DeletePlanAsync(plan.Id);
        Assert.Empty(await _planService.GetAllPlansAsync());

        var result = await _sut.ImportAllDataAsync(json);

        Assert.Equal(1, result.PlansImported);
        var restored = await _planService.GetAllPlansAsync();
        Assert.Single(restored);
        Assert.Equal("Push A", restored[0].Name);
    }

    [Fact]
    public async Task PreviewImportAsync_counts_backup_contents_without_importing()
    {
        var plan = new WorkoutPlan { Id = Guid.NewGuid(), Name = "Push A", Exercises = [] };
        await _planService.SavePlanAsync(plan);

        var sessionId = await _historyRepo.BeginSessionAsync(plan.Id, "Push A", null);
        await _historyRepo.LogSetAsync(sessionId, 0, "Press", 0, reps: 8, weightKg: 30);

        var json = await _sut.ExportAllDataAsync();
        await _planService.DeletePlanAsync(plan.Id);
        await _historyRepo.DeleteSessionAsync(sessionId);

        var preview = await BackupRestoreService.PreviewImportAsync(json);

        Assert.Equal(1, preview.PlansImported);
        Assert.Equal(1, preview.SessionsImported);
        Assert.Equal(1, preview.SetsImported);
        Assert.Empty(await _planService.GetAllPlansAsync());
    }

    [Fact]
    public Task PreviewImportAsync_throws_for_unsupported_version()
    {
        const string json = """{"formatVersion":999}""";
        return Assert.ThrowsAsync<InvalidOperationException>(
            () => BackupRestoreService.PreviewImportAsync(json));
    }

    // -------------------------------------------------------------------------
    //  Workout history round-trip
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExportAllData_ThenImportAllData_RestoresHistory()
    {
        var sessionId = await _historyRepo.BeginSessionAsync(Guid.NewGuid(), "Pull", null);
        await _historyRepo.LogSetAsync(sessionId, 0, "Row", 0, reps: 10, weightKg: 50);

        var json = await _sut.ExportAllDataAsync();
        await _historyRepo.DeleteSessionAsync(sessionId);

        var result = await _sut.ImportAllDataAsync(json);

        Assert.Equal(1, result.SessionsImported);
        Assert.Equal(1, result.SetsImported);
        var sessions = await _historyRepo.GetRecentSessionsAsync();
        Assert.Single(sessions);
    }

    // -------------------------------------------------------------------------
    //  Bodyweight round-trip (part of history backup)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExportAllData_ThenImportAllData_RestoresBodyweightEntries()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        await _historyRepo.UpsertBodyweightLogAsync(today, 80.5);

        var json = await _sut.ExportAllDataAsync();
        await _historyRepo.DeleteBodyweightLogAsync(today);

        await _sut.ImportAllDataAsync(json);

        var entries = await _historyRepo.GetBodyweightLogsAsync(10);
        Assert.Single(entries);
        Assert.Equal(80.5, entries[0].BodyweightKg);
    }

    // -------------------------------------------------------------------------
    //  Schedule history round-trip
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExportAllData_ThenImportAllData_RestoresSchedule()
    {
        await _db.Database.InsertAsync(new WorkoutScheduleHistoryEntity
        {
            DaysBitmask = 0b0010110, // Mon + Wed
            EffectiveFrom = "2024-01-01"
        });

        var json = await _sut.ExportAllDataAsync();
        await _db.Database.ExecuteAsync("DELETE FROM WorkoutScheduleHistory");

        var result = await _sut.ImportAllDataAsync(json);

        Assert.Equal(1, result.SchedulesImported);
        var rows = await _db.Database.Table<WorkoutScheduleHistoryEntity>().ToListAsync();
        Assert.Single(rows);
        Assert.Equal(0b0010110, rows[0].DaysBitmask);
    }

    // -------------------------------------------------------------------------
    //  Preferences round-trip
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExportAllData_ThenImportAllData_RestoresPreferences()
    {
        _prefs.Set(PreferenceKeys.ThemePreference, "dark");
        _prefs.Set(PreferenceKeys.RestAlertsEnabled, false);
        _prefs.Set(PreferenceKeys.AiModelName, "gpt-4o");

        var json = await _sut.ExportAllDataAsync();

        // Clear prefs
        _prefs.Set(PreferenceKeys.ThemePreference, "system");
        _prefs.Set(PreferenceKeys.RestAlertsEnabled, true);
        _prefs.Set(PreferenceKeys.AiModelName, "gpt-3.5-turbo");

        var result = await _sut.ImportAllDataAsync(json);

        Assert.True(result.PreferencesImported >= 3);
        Assert.Equal("dark", _prefs.Get(PreferenceKeys.ThemePreference, "system"));
        Assert.Equal("False", _prefs.Get(PreferenceKeys.RestAlertsEnabled, "True"));
        Assert.Equal("gpt-4o", _prefs.Get(PreferenceKeys.AiModelName, string.Empty));
    }

    // -------------------------------------------------------------------------
    //  Unknown keys in backup are silently ignored (forward compatibility)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ImportAllData_IgnoresUnknownPreferenceKeys_DoesNotThrow()
    {
        const string json = """
            {
                "formatVersion": 1,
                "plans": [],
                "history": { "formatVersion": 1, "sessions": [], "bodyweightEntries": [] },
                "schedules": [],
                "preferences": {
                    "unknown_future_key": "some_value",
                    "physiquinator_ai_model_name": "gpt-4o"
                }
            }
            """;

        var result = await _sut.ImportAllDataAsync(json);

        // Only the known key should be applied. The unknown one is silently dropped.
        Assert.Equal(1, result.PreferencesImported);
        Assert.Equal("gpt-4o", _prefs.Get(PreferenceKeys.AiModelName, string.Empty));
    }

    // -------------------------------------------------------------------------
    //  Unsupported format version throws
    // -------------------------------------------------------------------------

    [Fact]
    public Task ImportAllData_Throws_WhenFormatVersionUnsupported()
    {
        const string json = """{"formatVersion":999,"plans":[],"history":{"formatVersion":1,"sessions":[],"bodyweightEntries":[]},"schedules":[],"preferences":{}}""";
        return Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ImportAllDataAsync(json));
    }

    // -------------------------------------------------------------------------
    //  Complete round-trip with all data types present
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExportAllData_ThenImportAllData_CompleteRoundTrip()
    {
        // Seed everything
        var plan = new WorkoutPlan { Id = Guid.NewGuid(), Name = "Full Body", Exercises = [] };
        await _planService.SavePlanAsync(plan);

        var sessionId = await _historyRepo.BeginSessionAsync(plan.Id, "Full Body", null);
        await _historyRepo.LogSetAsync(sessionId, 0, "Squat", 0, reps: 5, weightKg: 100);

        await _historyRepo.UpsertBodyweightLogAsync(DateOnly.FromDateTime(DateTime.Today), 75.0);

        await _db.Database.InsertAsync(new WorkoutScheduleHistoryEntity
        {
            DaysBitmask = 0b1010100,
            EffectiveFrom = "2024-06-01"
        });

        _prefs.Set(PreferenceKeys.AiModelName, "claude-3");
        _prefs.Set(PreferenceKeys.RestAlertsEnabled, false);

        // Export
        var json = await _sut.ExportAllDataAsync();
        Assert.False(string.IsNullOrWhiteSpace(json));

        // Wipe everything
        await _db.ClearAllUserDataAsync();

        // Import
        var result = await _sut.ImportAllDataAsync(json);

        // Assert
        Assert.Equal(1, result.PlansImported);
        Assert.Equal(1, result.SessionsImported);
        Assert.Equal(1, result.SetsImported);
        Assert.Equal(1, result.SchedulesImported);
        Assert.True(result.PreferencesImported >= 2);

        var plans = await _planService.GetAllPlansAsync();
        Assert.Single(plans);
        Assert.Equal("Full Body", plans[0].Name);

        var bw = await _historyRepo.GetBodyweightLogsAsync(10);
        Assert.Single(bw);
        Assert.Equal(75.0, bw[0].BodyweightKg);
    }
}

// ---------------------------------------------------------------------------
//  Minimal stub needed by UserProfileService constructor in tests
// ---------------------------------------------------------------------------

file sealed class LambdaDatabasePathProvider(Func<Guid, string> getPath) : IDatabasePathProvider
{
    public string GetDatabasePath(Guid profileId) => getPath(profileId);
}
