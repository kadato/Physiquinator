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

    // Plan snapshots are identical on every seed, so serialize them once per
    // process instead of rebuilding the plans and re-serializing on each call.
    private static readonly Lazy<FrozenDictionary<Guid, string>> s_planSnapshots =
        new(BuildPlanSnapshots, LazyThreadSafetyMode.ExecutionAndPublication);

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

        // One transaction for all four plans instead of one per plan.
        await _planService.SavePlansAsync(CreateDemoPlans());

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

        DateTime todayUtc = _time.GetUtcNow().UtcDateTime.Date;
        List<DemoSessionSpec> specs = DemoScheduleGenerator.GenerateDemoSchedule(todayUtc);

        // The session-count gate above means the tables hold no demo rows, and
        // the whole insert runs in one transaction, so plain bulk inserts are
        // atomic here: a crash rolls everything back and the next launch
        // retries from an empty table. Collect first, write once.
        var sessions = new List<WorkoutSessionLogEntity>(specs.Count);
        var sets = new List<WorkoutSetLogEntity>(specs.Count * 24);
        FrozenDictionary<Guid, string> snapshots = s_planSnapshots.Value;
        for (var i = 0; i < specs.Count; i++)
        {
            DemoSessionSpec spec = specs[i];
            BuildSession(sessions, sets, i, spec, todayUtc, snapshots[spec.PlanId]);
        }

        await _database.Database.RunInTransactionAsync(conn =>
        {
            conn.InsertAll(sessions, false);
            conn.InsertAll(sets, false);
        }).ConfigureAwait(false);

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

        if (entries.Count == 0)
        {
            _preferences.Set(DemoExtrasSeedCompletedKey, true);
            return true;
        }

        // Bulk insert in its own transaction instead of one statement per row.
        await _database.Database.InsertAllAsync(entries).ConfigureAwait(false);

        SetProfileBodyweight(entries[^1].BodyweightKg);

        _preferences.Set(DemoExtrasSeedCompletedKey, true);
        return true;
    }

    private static List<WorkoutPlan> CreateDemoPlans()
    {
        var plans = new List<WorkoutPlan>
        {
            DemoPlans.CreatePushDayPlan(),
            DemoPlans.CreatePullDayPlan(),
            DemoPlans.CreateLegDayPlan(),
            DemoPlans.CreateFullBodyPlan()
        };
        for (var i = 0; i < plans.Count; i++)
            plans[i].SortOrder = i;
        return plans;
    }

    private static FrozenDictionary<Guid, string> BuildPlanSnapshots()
    {
        List<WorkoutPlan> plans = CreateDemoPlans();
        return new Dictionary<Guid, string>
        {
            [plans[0].Id] = JsonSerializer.Serialize(plans[0], PhysiquinatorJsonContext.Default.WorkoutPlan),
            [plans[1].Id] = JsonSerializer.Serialize(plans[1], PhysiquinatorJsonContext.Default.WorkoutPlan),
            [plans[2].Id] = JsonSerializer.Serialize(plans[2], PhysiquinatorJsonContext.Default.WorkoutPlan),
            [plans[3].Id] = JsonSerializer.Serialize(plans[3], PhysiquinatorJsonContext.Default.WorkoutPlan)
        }.ToFrozenDictionary();
    }

    private static void BuildSession(
        List<WorkoutSessionLogEntity> sessions,
        List<WorkoutSetLogEntity> sets,
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

        sessions.Add(new WorkoutSessionLogEntity
        {
            Id = DemoDataIds.SessionId(i),
            WorkoutPlanId = spec.PlanId.ToString(),
            PlanName = GetPlanName(spec.PlanId),
            StartedAtUtc = started,
            EndedAtUtc = ended,
            PlanSnapshotJson = planSnapshotJson
        });

        List<WorkoutSetLogEntity> sessionSets;
        if (!spec.Ended)
        {
            var benchKg = DemoSetBuilders.BenchWeightKg(spec.PlanTypeOrdinal, deload: false);
            sessionSets = DemoSetBuilders.BuildInProgressPushSets(i, started, benchKg);
        }
        else if (spec.PlanId == DemoDataIds.PushPlan)
            sessionSets = DemoSetBuilders.BuildCompletedPushSets(i, started, ended!.Value, spec.PlanTypeOrdinal, spec.IsDeload);
        else if (spec.PlanId == DemoDataIds.PullPlan)
            sessionSets = DemoSetBuilders.BuildCompletedPullSets(i, started, ended!.Value, spec.PlanTypeOrdinal, spec.IsDeload);
        else if (spec.PlanId == DemoDataIds.LegPlan)
            sessionSets = DemoSetBuilders.BuildCompletedLegSets(i, started, ended!.Value, spec.PlanTypeOrdinal, spec.IsDeload);
        else
            sessionSets = DemoSetBuilders.BuildCompletedFullBodySets(i, started, ended!.Value, spec.PlanTypeOrdinal, spec.IsDeload);

        sets.AddRange(sessionSets);
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
