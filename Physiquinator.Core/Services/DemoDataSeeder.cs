using Physiquinator.Core.Data;
using Physiquinator.Core.Models;
using Physiquinator.Core.Serialization;
using Physiquinator.Core.Services.Demo;
using System.Collections.Frozen;
using System.Text.Json;

namespace Physiquinator.Core.Services;

public sealed class DemoDataSeeder(
    WorkoutPlanService planService,
    AppDatabase database,
    WorkoutHistoryRepository historyRepository,
    WorkoutScheduleService scheduleService,
    UserProfileService userProfileService,
    IDemoSeedPreferences preferences,
    TimeProvider time)
{
    public const string InitialDemoSeedCompletedKey = PreferenceKeys.DemoDataInitialSeedCompleted;
    public const string DemoHistorySeedCompletedKey = PreferenceKeys.DemoHistorySeedCompleted;
    public const string DemoExtrasSeedCompletedKey = PreferenceKeys.DemoExtrasSeedCompleted;

    private static readonly IReadOnlyList<DayOfWeek> s_demoScheduleDays =
        [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday, DayOfWeek.Sunday];

    private readonly WorkoutPlanService _planService = planService;
    private readonly AppDatabase _database = database;
    private readonly WorkoutHistoryRepository _historyRepository = historyRepository;
    private readonly WorkoutScheduleService _scheduleService = scheduleService;
    private readonly UserProfileService _userProfileService = userProfileService;
    private readonly IDemoSeedPreferences _preferences = preferences;
    private readonly TimeProvider _time = time;

    public async Task<bool> SeedDemoDataIfNeededAsync()
    {
        if (_preferences.Get(InitialDemoSeedCompletedKey, false))
            return false;

        List<WorkoutPlan> existingPlans = await _planService.GetAllPlansAsync();
        if (existingPlans.Count > 0)
        {
            _preferences.Set(InitialDemoSeedCompletedKey, true);
            return false;
        }

        var demoPlans = new List<WorkoutPlan>
        {
            DemoPlans.CreatePushDayPlan(),
            DemoPlans.CreatePullDayPlan(),
            DemoPlans.CreateLegDayPlan(),
            DemoPlans.CreateFullBodyPlan()
        };

        for (var i = 0; i < demoPlans.Count; i++)
        {
            demoPlans[i].SortOrder = i;
            await _planService.SavePlanAsync(demoPlans[i]);
        }

        await SetDemoScheduleIfUnsetAsync();

        _preferences.Set(InitialDemoSeedCompletedKey, true);
        return true;
    }

    /// <summary>
    /// Seeds demo workout history once (empty sessions + preference gate). Requires all four demo plans.
    /// </summary>
    public async Task<bool> SeedDemoHistoryIfNeededAsync()
    {
        if (_preferences.Get(DemoHistorySeedCompletedKey, false))
            return false;

        await _database.EnsureInitializedAsync();

        if (await _historyRepository.GetSessionCountAsync() > 0)
        {
            _preferences.Set(DemoHistorySeedCompletedKey, true);
            return false;
        }

        if (!await HasAllDemoPlansAsync())
        {
            _preferences.Set(DemoHistorySeedCompletedKey, true);
            return false;
        }

        var snapshots = new Dictionary<Guid, string>
        {
            [DemoDataIds.PushPlan] = JsonSerializer.Serialize(DemoPlans.CreatePushDayPlan(), PhysiquinatorJsonContext.Default.WorkoutPlan),
            [DemoDataIds.PullPlan] = JsonSerializer.Serialize(DemoPlans.CreatePullDayPlan(), PhysiquinatorJsonContext.Default.WorkoutPlan),
            [DemoDataIds.LegPlan] = JsonSerializer.Serialize(DemoPlans.CreateLegDayPlan(), PhysiquinatorJsonContext.Default.WorkoutPlan),
            [DemoDataIds.FullBodyPlan] = JsonSerializer.Serialize(DemoPlans.CreateFullBodyPlan(), PhysiquinatorJsonContext.Default.WorkoutPlan)
        }.ToFrozenDictionary();

        DateTime todayUtc = _time.GetUtcNow().UtcDateTime.Date;
        List<DemoSessionSpec> specs = DemoScheduleGenerator.GenerateDemoSchedule(todayUtc);

        await _database.Database.RunInTransactionAsync(conn =>
        {
            for (var i = 0; i < specs.Count; i++)
            {
                DemoSessionSpec spec = specs[i];
                SeedSession(conn, i, spec, todayUtc, snapshots[spec.PlanId]);
            }
        });

        _preferences.Set(DemoHistorySeedCompletedKey, true);
        return true;
    }

    /// <summary>
    /// Seeds demo extras once: a changing bodyweight series and the profile's
    /// current bodyweight. Gated by its own preference key so existing users
    /// with history get the extras on the next launch.
    /// </summary>
    public async Task<bool> SeedDemoExtrasIfNeededAsync()
    {
        if (_preferences.Get(DemoExtrasSeedCompletedKey, false))
            return false;

        await _database.EnsureInitializedAsync();

        if (await _historyRepository.GetBodyweightLogsAsync(1) is { Count: > 0 })
        {
            _preferences.Set(DemoExtrasSeedCompletedKey, true);
            return false;
        }

        DateTime todayUtc = _time.GetUtcNow().UtcDateTime.Date;
        List<BodyweightLogEntity> entries = DemoScheduleGenerator.GenerateDemoBodyweights(todayUtc);

        await _database.Database.RunInTransactionAsync(conn =>
        {
            for (var i = 0; i < entries.Count; i++)
                conn.InsertOrReplace(entries[i]);
        });

        SetProfileBodyweight(entries[^1].BodyweightKg);

        _preferences.Set(DemoExtrasSeedCompletedKey, true);
        return true;
    }

    private static void SeedSession(
        SQLite.SQLiteConnection conn,
        int i,
        DemoSessionSpec spec,
        DateTime todayUtc,
        string planSnapshotJson)
    {
        DateTime started = todayUtc
            .AddDays(-spec.DaysAgo)
            .AddHours(spec.StartHourUtc)
            .AddMinutes(spec.StartMinuteUtc);
        DateTime? ended = spec.Ended
            ? started.AddMinutes(spec.DurationMinutes)
            : (DateTime?)null;

        var planName = GetPlanName(spec.PlanId);

        var session = new WorkoutSessionLogEntity
        {
            Id = DemoDataIds.SessionId(i),
            WorkoutPlanId = spec.PlanId.ToString(),
            PlanName = planName,
            StartedAtUtc = started,
            EndedAtUtc = ended,
            PlanSnapshotJson = planSnapshotJson
        };

        conn.InsertOrReplace(session);

        List<WorkoutSetLogEntity> sets;
        if (!spec.Ended)
        {
            var benchKg = DemoSetBuilders.BenchWeightKg(spec.PlanTypeOrdinal, deload: false);
            sets = DemoSetBuilders.BuildInProgressPushSets(i, started, benchKg);
        }
        else if (spec.PlanId == DemoDataIds.PushPlan)
            sets = DemoSetBuilders.BuildCompletedPushSets(i, started, ended!.Value, spec.PlanTypeOrdinal, spec.IsDeload);
        else if (spec.PlanId == DemoDataIds.PullPlan)
            sets = DemoSetBuilders.BuildCompletedPullSets(i, started, ended!.Value, spec.PlanTypeOrdinal, spec.IsDeload);
        else if (spec.PlanId == DemoDataIds.LegPlan)
            sets = DemoSetBuilders.BuildCompletedLegSets(i, started, ended!.Value, spec.PlanTypeOrdinal, spec.IsDeload);
        else
            sets = DemoSetBuilders.BuildCompletedFullBodySets(i, started, ended!.Value, spec.PlanTypeOrdinal, spec.IsDeload);

        foreach (WorkoutSetLogEntity set in sets)
            conn.InsertOrReplace(set);
    }

    private async Task<bool> HasAllDemoPlansAsync()
    {
        await _database.EnsureInitializedAsync();
        var count = await _database.Database.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM WorkoutPlans WHERE Id IN (?, ?, ?, ?)",
            DemoDataIds.PushPlan.ToString(),
            DemoDataIds.PullPlan.ToString(),
            DemoDataIds.LegPlan.ToString(),
            DemoDataIds.FullBodyPlan.ToString());
        return count == 4;
    }

    private static string GetPlanName(Guid planId) => planId switch
    {
        _ when planId == DemoDataIds.PushPlan => "Push Day",
        _ when planId == DemoDataIds.PullPlan => "Pull Day",
        _ when planId == DemoDataIds.LegPlan => "Leg Day",
        _ when planId == DemoDataIds.FullBodyPlan => "Full Body Workout",
        _ => "Workout"
    };

    private async Task SetDemoScheduleIfUnsetAsync()
    {
        if (_scheduleService.IsSet)
            return;

        await _scheduleService.SetDaysAsync(s_demoScheduleDays);
    }

    private void SetProfileBodyweight(double latestKg) =>
        _userProfileService.UpdateBodyweight(_userProfileService.GetActiveProfile().Id, Math.Round(latestKg, 1));
}
