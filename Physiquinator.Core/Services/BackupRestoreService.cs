using Physiquinator.Core.Data;
using Physiquinator.Core.Serialization;
using System.Text.Json;

namespace Physiquinator.Core.Services;

/// <summary>
/// Compiles a complete snapshot of all active-profile data into a single JSON export, and
/// restores it on import.
///
/// What is included:
/// - Workout plans (WorkoutPlans + ExercisePlans tables)
/// - Workout history (WorkoutSessionLogs + WorkoutSetLogs tables)
/// - Bodyweight log (BodyweightLogs table)
/// - Training-schedule history (WorkoutScheduleHistory table)
/// - All persisted app preferences (theme, rest timer, AI settings, update settings, ...)
///
/// Preference keys are stripped of their profile-ID suffix on export and re-applied on import,
/// so a backup is always portable across devices and profiles.
///
/// Adding a new preference in the future: add its base key to <see cref="KnownPreferenceBaseKeys"/>.
/// Adding a new database table: add its export and import logic below (see "Plans" and "History" as examples).
/// </summary>
public sealed class BackupRestoreService(
    IAppPreferences preferences,
    UserProfileService userProfileService,
    WorkoutPlanService planService,
    WorkoutHistoryService historyService,
    WorkoutScheduleService scheduleService,
    AppDatabase database,
    WorkoutPlanRepository planRepository)
{
    // ---------------------------------------------------------------------------
    //  Preference key registry
    //  Add the *base* key (without any profile-ID suffix) for every preference that
    //  should be included in a full-data export.  That is the only change needed
    //  when a new setting is introduced.
    // ---------------------------------------------------------------------------

    /// <summary>
    /// All base preference keys whose values belong to the active user profile.
    /// "Base" means the canonical key without the "_&lt;profileId&gt;" suffix that some
    /// services append for multi-profile isolation.
    /// </summary>
    public static readonly IReadOnlyList<string> KnownPreferenceBaseKeys =
    [
        PreferenceKeys.ThemePreference,
        PreferenceKeys.RestAlertsEnabled,
        PreferenceKeys.RestAddTimeSeconds,
        PreferenceKeys.AutoUpdateCheckEnabled,
        PreferenceKeys.AiEnabled,
        PreferenceKeys.AiProvider,
        PreferenceKeys.AiBaseUrl,
        PreferenceKeys.AiApiKey,
        PreferenceKeys.AiModelName,
        PreferenceKeys.AiSystemPrompt,
        // WorkoutScheduleDays is the legacy single-value preference. The full
        // history lives in the DB and is captured in AllDataBackup.Schedules.
        PreferenceKeys.WorkoutScheduleDays,
    ];

    private static readonly JsonSerializerOptions s_writeOptions =
        new(PhysiquinatorJsonContext.Default.Options) { WriteIndented = true };

    // ---------------------------------------------------------------------------
    //  Export
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Exports all data for the active profile to a JSON string.
    /// </summary>
    public async Task<string> ExportAllDataAsync()
    {
        var backup = new AllDataBackup
        {
            Plans = await planService.GetAllPlansAsync(),
            History = await BuildHistoryBackupAsync(),
            Schedules = await GetScheduleHistoryAsync(),
            Preferences = ReadAllPreferences(),
        };

        return JsonSerializer.Serialize(backup, s_writeOptions);
    }

    private async Task<WorkoutHistoryBackup> BuildHistoryBackupAsync()
    {
        // Reuse the existing repository-level snapshot which includes sessions,
        // set logs, and all bodyweight entries.
        var json = await historyService.ExportToJsonAsync();
        return JsonSerializer.Deserialize(json, PhysiquinatorJsonContext.Default.WorkoutHistoryBackup)
               ?? new WorkoutHistoryBackup();
    }

    private async Task<List<WorkoutScheduleHistoryEntity>> GetScheduleHistoryAsync()
    {
        await database.EnsureInitializedAsync();
        return await database.Database
            .Table<WorkoutScheduleHistoryEntity>()
            .OrderBy(x => x.EffectiveFrom)
            .ToListAsync();
    }

    /// <summary>
    /// Reads every known preference key for the active profile, stripping the profile-ID suffix
    /// so the resulting dictionary uses bare base keys and is portable across profiles/devices.
    /// Only keys that differ from their default (that is, they have an explicit stored value) are included.
    /// Keys that were never written are omitted so import does not overwrite defaults with defaults.
    /// </summary>
    private Dictionary<string, string> ReadAllPreferences()
    {
        var suffix = GetPreferenceSuffix();
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var baseKey in KnownPreferenceBaseKeys)
        {
            // Try the profile-specific key first, then fall back to the bare key.
            var profileKey = baseKey + suffix;
            var valueWithSuffix = preferences.Get(profileKey, "\0");
            if (valueWithSuffix != "\0")
            {
                result[baseKey] = valueWithSuffix;
                continue;
            }

            // Some services (ThemeService, WeightUnitService) always write the suffixed
            // key, even for the demo profile. Check that variant too so nothing is missed.
            if (suffix.Length == 0)
            {
                var alwaysSuffixed = preferences.Get(baseKey + $"_{UserProfileService.DemoProfileId}", "\0");
                if (alwaysSuffixed != "\0")
                {
                    result[baseKey] = alwaysSuffixed;
                    continue;
                }
            }

            var valueBare = preferences.Get(baseKey, "\0");
            if (valueBare != "\0")
            {
                result[baseKey] = valueBare;
            }
        }

        return result;
    }

    // ---------------------------------------------------------------------------
    //  Import
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Deserialises a full-data backup and merges it into the active profile.
    /// Returns a summary of what was restored.
    /// </summary>
    public async Task<AllDataImportResult> ImportAllDataAsync(string json)
    {
        AllDataBackup backup = ParseBackup(json);

        var plansImported = await ImportPlansAsync(backup);
        var (sessions, sets) = await ImportHistoryAsync(backup);
        var schedulesImported = await ImportSchedulesAsync(backup);
        var prefsImported = ImportPreferences(backup);

        return new AllDataImportResult(plansImported, sessions, sets, schedulesImported, prefsImported);
    }

    /// <summary>
    /// Counts what a full-backup JSON file contains, without importing anything.
    /// </summary>
    public static Task<AllDataImportResult> PreviewImportAsync(string json)
    {
        AllDataBackup backup = ParseBackup(json);

        var plans = backup.Plans?.Count ?? 0;
        var (sessions, sets) = CountHistory(backup.History);
        var schedules = backup.Schedules?.Count ?? 0;
        var prefs = CountPreferences(backup.Preferences);

        return Task.FromResult(new AllDataImportResult(plans, sessions, sets, schedules, prefs));
    }

    private static AllDataBackup ParseBackup(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var backup = JsonSerializer.Deserialize(json, PhysiquinatorJsonContext.Default.AllDataBackup)
            ?? throw new InvalidOperationException("Failed to deserialize full-data backup.");

        if (backup.FormatVersion is < 1 or > 1)
            throw new InvalidOperationException(
                $"Unsupported full-data backup format version {backup.FormatVersion} (supported: 1).");

        return backup;
    }

    private static (int Sessions, int Sets) CountHistory(WorkoutHistoryBackup? history)
    {
        if (history is null) return (0, 0);

        var sessions = 0;
        var sets = 0;
        foreach (WorkoutHistoryBackupEntry? entry in history.Sessions ?? [])
        {
            if (entry is null || entry.Session is null || string.IsNullOrWhiteSpace(entry.Session.Id))
                continue;
            sessions++;
            sets += (entry.Sets ?? []).Count(s => s is not null);
        }

        return (sessions, sets);
    }

    private static int CountPreferences(Dictionary<string, string>? preferences)
    {
        if (preferences is not { Count: > 0 })
            return 0;

        var knownSet = new HashSet<string>(KnownPreferenceBaseKeys, StringComparer.Ordinal);
        return preferences.Count(kv => knownSet.Contains(kv.Key));
    }

    private async Task<int> ImportPlansAsync(AllDataBackup backup)
    {
        var plans = backup.Plans;
        if (plans is not { Count: > 0 })
            return 0;

        await planRepository.SavePlansAsync(plans);
        planService.InvalidatePlanCache();
        return plans.Count;
    }

    private async Task<(int Sessions, int Sets)> ImportHistoryAsync(AllDataBackup backup)
    {
        if (backup.History is null)
            return (0, 0);

        var historyJson = JsonSerializer.Serialize(
            backup.History, PhysiquinatorJsonContext.Default.WorkoutHistoryBackup);
        return await historyService.ImportFromJsonAsync(historyJson);
    }

    private async Task<int> ImportSchedulesAsync(AllDataBackup backup)
    {
        if (backup.Schedules is not { Count: > 0 })
            return 0;

        await database.EnsureInitializedAsync();
        var count = 0;

        await database.Database.RunInTransactionAsync(conn =>
        {
            foreach (var entry in backup.Schedules)
            {
                if (string.IsNullOrWhiteSpace(entry.EffectiveFrom)) continue;

                var existing = conn
                    .Table<WorkoutScheduleHistoryEntity>()
                    .FirstOrDefault(e => e.EffectiveFrom == entry.EffectiveFrom);

                if (existing is not null)
                {
                    existing.DaysBitmask = entry.DaysBitmask;
                    conn.Update(existing);
                }
                else
                {
                    entry.Id = 0; // Let SQLite assign a new auto-increment id.
                    conn.Insert(entry);
                }

                count++;
            }
        });

        await scheduleService.ResetCacheAsync().ConfigureAwait(false);
        planService.InvalidatePlanCache();
        return count;
    }

    private int ImportPreferences(AllDataBackup backup)
    {
        if (backup.Preferences is not { Count: > 0 })
            return 0;

        var suffix = GetPreferenceSuffix();
        var knownSet = new HashSet<string>(KnownPreferenceBaseKeys, StringComparer.Ordinal);
        var count = 0;

        foreach (var (baseKey, value) in backup.Preferences)
        {
            if (!knownSet.Contains(baseKey)) continue; // forward-compat: ignore unknown keys

            var targetKey = IsProfileSpecificKey(baseKey) ? baseKey + suffix : baseKey;
            preferences.Set(targetKey, value);
            count++;
        }

        return count;
    }

    // ---------------------------------------------------------------------------
    //  Helpers
    // ---------------------------------------------------------------------------

    private string GetPreferenceSuffix()
    {
        var activeId = userProfileService.GetActiveProfile().Id;
        return ProfilePreferenceKeys.GetSuffix(activeId);
    }

    /// <summary>
    /// Returns true for preference keys that are stored with a profile-ID suffix by their
    /// owning service.  Add a key here if a new service does the same.
    /// </summary>
    private static bool IsProfileSpecificKey(string baseKey) => baseKey is
        PreferenceKeys.ThemePreference or
        PreferenceKeys.RestAlertsEnabled or
        PreferenceKeys.RestAddTimeSeconds or
        PreferenceKeys.WorkoutScheduleDays;
}

/// <summary>Summary of what was restored by <see cref="BackupRestoreService.ImportAllDataAsync"/>.</summary>
public sealed record AllDataImportResult(
    int PlansImported,
    int SessionsImported,
    int SetsImported,
    int SchedulesImported,
    int PreferencesImported);
