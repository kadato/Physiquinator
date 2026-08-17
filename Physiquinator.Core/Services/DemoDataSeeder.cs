using Physiquinator.Core.Data;
using Physiquinator.Core.Formatting;
using Physiquinator.Core.Models;
using Physiquinator.Core.Serialization;
using System.Collections.Frozen;
using System.Globalization;
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

    private const int DemoHistoryWeeks = 52;
    private const int SkipSessionThresholdPercent = 40;

    private const double DemoStartBodyweightKg = 90.5;
    private const double DemoWeeklyBodyweightDeltaKg = -0.13;

    private const string BenchPressName = "Bench Press";
    private const string OverheadPressName = "Overhead Press";
    private const string PullUpsName = "Pull-Ups";
    private const string BarbellRowsName = "Barbell Rows";
    private const string SquatsName = "Squats";
    private const string DeadliftName = "Deadlift";
    private const string InclineDumbbellPressName = "Incline Dumbbell Press";
    private const string LateralRaisesName = "Lateral Raises";
    private const string TricepPushdownsName = "Tricep Pushdowns";
    private const string OverheadTricepExtensionName = "Overhead Tricep Extension";
    private const string FacePullsName = "Face Pulls";
    private const string BicepCurlsName = "Bicep Curls";
    private const string HammerCurlsName = "Hammer Curls";
    private const string RomanianDeadliftName = "Romanian Deadlift";
    private const string LegPressName = "Leg Press";
    private const string LegCurlsName = "Leg Curls";
    private const string CalfRaisesName = "Calf Raises";
    private const string LegExtensionsName = "Leg Extensions";
    private const string PlankName = "Plank";
    private const string PushUpsName = "Push-Ups";

    private static readonly DateTime s_demoPlanCreatedAt = new(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private static readonly IReadOnlyList<DayOfWeek> s_demoScheduleDays =
        [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday, DayOfWeek.Sunday];

    /// <summary>Workout start hour indexed by session hash remainder (0-3).</summary>
    private static readonly int[] s_startHoursByHashRemainder = [7, 9, 17, 18];

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
            CreatePushDayPlan(),
            CreatePullDayPlan(),
            CreateLegDayPlan(),
            CreateFullBodyPlan()
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
            [DemoDataIds.PushPlan] = JsonSerializer.Serialize(CreatePushDayPlan(), PhysiquinatorJsonContext.Default.WorkoutPlan),
            [DemoDataIds.PullPlan] = JsonSerializer.Serialize(CreatePullDayPlan(), PhysiquinatorJsonContext.Default.WorkoutPlan),
            [DemoDataIds.LegPlan] = JsonSerializer.Serialize(CreateLegDayPlan(), PhysiquinatorJsonContext.Default.WorkoutPlan),
            [DemoDataIds.FullBodyPlan] = JsonSerializer.Serialize(CreateFullBodyPlan(), PhysiquinatorJsonContext.Default.WorkoutPlan)
        }.ToFrozenDictionary();

        DateTime todayUtc = _time.GetUtcNow().UtcDateTime.Date;
        List<DemoSessionSpec> specs = GenerateDemoSchedule(todayUtc);

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
        List<BodyweightLogEntity> entries = GenerateDemoBodyweights(todayUtc);

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
            var benchKg = BenchWeightKg(spec.PlanTypeOrdinal, deload: false);
            sets = BuildInProgressPushSets(i, started, benchKg);
        }
        else if (spec.PlanId == DemoDataIds.PushPlan)
            sets = BuildCompletedPushSets(i, started, ended!.Value, spec.PlanTypeOrdinal, spec.IsDeload);
        else if (spec.PlanId == DemoDataIds.PullPlan)
            sets = BuildCompletedPullSets(i, started, ended!.Value, spec.PlanTypeOrdinal, spec.IsDeload);
        else if (spec.PlanId == DemoDataIds.LegPlan)
            sets = BuildCompletedLegSets(i, started, ended!.Value, spec.PlanTypeOrdinal, spec.IsDeload);
        else
            sets = BuildCompletedFullBodySets(i, started, ended!.Value, spec.PlanTypeOrdinal, spec.IsDeload);

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

    /// <summary>
    /// Bodyweight logged on each scheduled workout day across the demo year, trending
    /// down with a slow wave and day-to-day jitter so the chart reads as a real cut.
    /// </summary>
    private static List<BodyweightLogEntity> GenerateDemoBodyweights(DateTime todayUtc)
    {
        var today = DateOnly.FromDateTime(todayUtc);
        DateOnly gridStartMonday = HeatmapGrid.GetMondayOfWeek(today)
            .AddDays(-7 * (DemoHistoryWeeks - 1));

        var entries = new List<BodyweightLogEntity>(DemoHistoryWeeks * 4);
        for (var week = 0; week < DemoHistoryWeeks; week++)
        {
            DateOnly weekMonday = gridStartMonday.AddDays(week * 7);
            var baseKg = DemoStartBodyweightKg
                + (week * DemoWeeklyBodyweightDeltaKg)
                + (Math.Sin(week / 2.0) * 0.4);

            TryAddBodyweight(entries, today, week, weekMonday.AddDays(OffsetFromMonday(DayOfWeek.Monday)), baseKg);
            TryAddBodyweight(entries, today, week, weekMonday.AddDays(OffsetFromMonday(DayOfWeek.Wednesday)), baseKg + 0.15);
            TryAddBodyweight(entries, today, week, weekMonday.AddDays(OffsetFromMonday(DayOfWeek.Friday)), baseKg - 0.15);
            if (week % 2 == 0)
                TryAddBodyweight(entries, today, week, weekMonday.AddDays(OffsetFromMonday(DayOfWeek.Sunday)), baseKg);
        }

        return entries;
    }

    private static void TryAddBodyweight(
        List<BodyweightLogEntity> entries,
        DateOnly today,
        int week,
        DateOnly date,
        double kg)
    {
        if (date > today)
            return;

        var hash = (week * 31) + ((int)date.DayOfWeek * 17);
        var jitter = ((hash % 7) - 3) * 0.05;

        entries.Add(new BodyweightLogEntity
        {
            Date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            BodyweightKg = Math.Round(kg + jitter, 1),
            UpdatedAtUtc = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        });
    }

    private static List<DemoSessionSpec> GenerateDemoSchedule(DateTime todayUtc)
    {
        var today = DateOnly.FromDateTime(todayUtc);
        DateOnly gridStartMonday = HeatmapGrid.GetMondayOfWeek(today)
            .AddDays(-7 * (DemoHistoryWeeks - 1));

        var specs = new List<DemoSessionSpec>();
        var pushOrd = 0;
        var pullOrd = 0;
        var legOrd = 0;
        var fbOrd = 0;

        for (var week = 0; week < DemoHistoryWeeks; week++)
        {
            DateOnly weekMonday = gridStartMonday.AddDays(week * 7);

            TryAdd(specs, today, week, weekMonday.AddDays(OffsetFromMonday(DayOfWeek.Monday)), DemoDataIds.PushPlan, slotKey: 0, ref pushOrd);
            TryAdd(specs, today, week, weekMonday.AddDays(OffsetFromMonday(DayOfWeek.Wednesday)), DemoDataIds.PullPlan, slotKey: 1, ref pullOrd);
            TryAdd(specs, today, week, weekMonday.AddDays(OffsetFromMonday(DayOfWeek.Friday)), DemoDataIds.LegPlan, slotKey: 2, ref legOrd);

            if (week % 2 == 0)
                TryAdd(specs, today, week, weekMonday.AddDays(OffsetFromMonday(DayOfWeek.Sunday)), DemoDataIds.FullBodyPlan, slotKey: 3, ref fbOrd);
        }

        // A fresh demo user should not find an unfinished workout: seed today's
        // push session as a normal completed session instead.
        specs.Add(new DemoSessionSpec(
            DaysAgo: 0,
            StartHourUtc: 10,
            StartMinuteUtc: 0,
            PlanId: DemoDataIds.PushPlan,
            Ended: true,
            DurationMinutes: 45,
            PlanTypeOrdinal: pushOrd,
            IsDeload: false));

        return specs;
    }

    private static void TryAdd(
        List<DemoSessionSpec> specs,
        DateOnly today,
        int week,
        DateOnly sessionDate,
        Guid planId,
        int slotKey,
        ref int planOrdinal)
    {
        if (ShouldSkipSession(week, slotKey))
            return;

        if (sessionDate > today)
            return;

        var daysAgo = today.DayNumber - sessionDate.DayNumber;

        if (daysAgo == 0 && planId == DemoDataIds.PushPlan)
            return;

        var hash = (week * 31) + (slotKey * 17);
        var startHour = s_startHoursByHashRemainder[hash % 4];
        var startMinute = hash % 3 * 15;
        var duration = 45 + (hash % 31);
        var isDeload = IsDeloadSession(planOrdinal);

        specs.Add(new DemoSessionSpec(
            daysAgo,
            startHour,
            startMinute,
            planId,
            Ended: true,
            duration,
            planOrdinal,
            isDeload));

        planOrdinal++;
    }

    private static int OffsetFromMonday(DayOfWeek dayOfWeek) =>
        ((int)dayOfWeek - (int)DayOfWeek.Monday + 7) % 7;

    private static bool ShouldSkipSession(int weekIndex, int slotKey)
    {
        if (weekIndex >= DemoHistoryWeeks - 2)
            return false;

        return ((weekIndex * 31) + (slotKey * 17)) % 100 < SkipSessionThresholdPercent;
    }

    private static bool IsDeloadSession(int planOrdinal) =>
        planOrdinal > 0 && (planOrdinal + 1) % 5 == 0;

    private static double ApplyDeload(double kg, bool isDeload) =>
        isDeload ? kg * 0.9 : kg;

    private static double ProgressionWeight(int ordinal, bool isDeload, double baseKg, double stepKg) =>
        ApplyDeload(baseKg + (Math.Min(ordinal, 60) * stepKg), isDeload);

    // IDE0290: primary constructor not applicable - mutable Time field must remain assignable.
    private ref struct SetBuilder(List<WorkoutSetLogEntity> sets, DateTime time, int sessionIndex)
    {
        private readonly List<WorkoutSetLogEntity> _sets = sets;
        private readonly int _sessionIndex = sessionIndex;

        public DateTime Time = time;

        private readonly WorkoutSetLogEntity Create(string exerciseName, int exerciseIndex, int setIndex, int reps, double? weightKg, bool isWarmup = false)
        {
            var entity = CreateSet(exerciseName, exerciseIndex, setIndex, reps, weightKg, Time, isWarmup);
            entity.Id = DemoDataIds.SetId(_sessionIndex, exerciseIndex, setIndex);
            entity.SessionId = DemoDataIds.SessionId(_sessionIndex);
            return entity;
        }

        public void AddCompleted(string exerciseName, int exerciseIndex, int count, int reps, double? weightKg, int restMinutes, int setIndexOffset = 0)
        {
            for (var s = 0; s < count; s++)
            {
                _sets.Add(Create(exerciseName, exerciseIndex, setIndexOffset + s, reps, weightKg));
                Time = Time.AddMinutes(restMinutes);
            }
        }

        public void AddWarmups(string exerciseName, int exerciseIndex, int warmupCount, double? workingWeightKg, int workingReps, int restMinutes)
        {
            for (var w = 0; w < warmupCount; w++)
            {
                double? weight = workingWeightKg is > 0
                    ? Math.Max(2.5, Math.Round(workingWeightKg.Value * (0.45 + (w * 0.15)) / 2.5) * 2.5)
                    : null;
                var reps = Math.Max(4, Math.Min(8, workingReps)) - w;
                _sets.Add(Create(exerciseName, exerciseIndex, w, reps, weight, isWarmup: true));
                Time = Time.AddMinutes(restMinutes);
            }
        }
    }

    private static double BenchWeightKg(int ordinal, bool deload) =>
        ApplyDeload(60.0 + (Math.Min(ordinal, 60) * 0.75), deload);

    private static double SquatWeightKg(int ordinal, bool deload, double baseKg = 100.0) =>
        ApplyDeload(baseKg + (Math.Min(ordinal, 60) * 0.5), deload);

    private static void ClampLastSetTime(List<WorkoutSetLogEntity> sets, DateTime ended)
    {
        if (sets.Count == 0)
            return;

        DateTime t = ended.AddMinutes(-1);
        if (sets[^1].CompletedAtUtc > t)
            sets[^1].CompletedAtUtc = t;
    }

    private readonly record struct DemoSessionSpec(
        int DaysAgo,
        int StartHourUtc,
        int StartMinuteUtc,
        Guid PlanId,
        bool Ended,
        int DurationMinutes,
        int PlanTypeOrdinal,
        bool IsDeload);

    private static List<WorkoutSetLogEntity> BuildCompletedPushSets(
        int sessionIndex,
        DateTime started,
        DateTime ended,
        int pushOrdinal,
        bool isDeload)
    {
        var sets = new List<WorkoutSetLogEntity>();
        var sb = new SetBuilder(sets, started.AddMinutes(3), sessionIndex);
        var benchKg = BenchWeightKg(pushOrdinal, isDeload);
        var benchReps = new[] { 10, 9, 9, 8 };

        sb.AddWarmups(BenchPressName, 0, 2, benchKg, 8, 3);
        for (var s = 0; s < 4; s++)
        {
            var e = CreateSet(BenchPressName, 0, 2 + s, benchReps[s], benchKg, sb.Time);
            AssignIds(e, sessionIndex, 0, 2 + s);
            sets.Add(e);
            sb.Time = sb.Time.AddMinutes(3);
        }

        var ohpKg = ProgressionWeight(pushOrdinal, isDeload, 42.5, 0.375);
        sb.AddWarmups(OverheadPressName, 1, 1, ohpKg, 8, 2);
        for (var s = 0; s < 4; s++)
        {
            var e = CreateSet(OverheadPressName, 1, 1 + s, 9 - Math.Min(s, 2), ohpKg, sb.Time);
            AssignIds(e, sessionIndex, 1, 1 + s);
            sets.Add(e);
            sb.Time = sb.Time.AddMinutes(2);
        }

        var inclineBase = ProgressionWeight(pushOrdinal, isDeload, 22.5, 0.375);
        for (var s = 0; s < 3; s++)
        {
            var e = CreateSet(InclineDumbbellPressName, 2, s, 10, inclineBase + (s * 2.5), sb.Time);
            AssignIds(e, sessionIndex, 2, s);
            sets.Add(e);
            sb.Time = sb.Time.AddMinutes(2);
        }

        var lateralKg = ProgressionWeight(pushOrdinal, isDeload, 8.0, 0.15);
        for (var s = 0; s < 3; s++)
        {
            var e = CreateSet(LateralRaisesName, 3, s, 12 + (s == 0 ? 2 : 0), lateralKg, sb.Time);
            AssignIds(e, sessionIndex, 3, s);
            sets.Add(e);
            sb.Time = sb.Time.AddMinutes(2);
        }

        sb.AddCompleted(TricepPushdownsName, 4, 3, 12, ProgressionWeight(pushOrdinal, isDeload, 20.0, 0.375), 2);
        sb.AddCompleted(OverheadTricepExtensionName, 5, 3, 10, ProgressionWeight(pushOrdinal, isDeload, 16.0, 0.3), 2);

        sb.AddCompleted(PushUpsName, 6, 3, 15, null, 1);

        ClampLastSetTime(sets, ended);
        return sets;
    }

    private static List<WorkoutSetLogEntity> BuildCompletedPullSets(
        int sessionIndex,
        DateTime started,
        DateTime ended,
        int pullOrdinal,
        bool isDeload)
    {
        var sets = new List<WorkoutSetLogEntity>();
        var sb = new SetBuilder(sets, started.AddMinutes(4), sessionIndex);
        var dlStep = Math.Min(pullOrdinal, 40) / 2;
        var dlKg = ApplyDeload(100.0 + (dlStep * 5.0), isDeload);

        sb.AddWarmups(DeadliftName, 0, 1, dlKg, 6, 4);
        for (var s = 0; s < 3; s++)
        {
            var e = CreateSet(DeadliftName, 0, 1 + s, 6 - s, dlKg, sb.Time);
            AssignIds(e, sessionIndex, 0, 1 + s);
            sets.Add(e);
            sb.Time = sb.Time.AddMinutes(4);
        }

        var pullUpReps = 6 + Math.Min(pullOrdinal, 4);
        double? pullUpWeight = null;
        if (pullOrdinal < 12)
        {
            pullUpWeight = -15.0 + pullOrdinal;
        }
        else if (pullOrdinal >= 28)
        {
            pullUpWeight = 2.5 + (Math.Floor((pullOrdinal - 28) / 3.0) * 1.25);
        }

        sb.AddWarmups(PullUpsName, 1, 1, pullUpWeight, pullUpReps, 2);
        for (var s = 0; s < 4; s++)
        {
            var e = CreateSet(PullUpsName, 1, 1 + s, pullUpReps - Math.Min(s, 2), pullUpWeight, sb.Time);
            AssignIds(e, sessionIndex, 1, 1 + s);
            sets.Add(e);
            sb.Time = sb.Time.AddMinutes(2);
        }

        sb.AddCompleted(BarbellRowsName, 2, 4, 10, ProgressionWeight(pullOrdinal, isDeload, 55.0, 0.5), 2);
        sb.AddCompleted(FacePullsName, 3, 3, 15, ProgressionWeight(pullOrdinal, isDeload, 15.0, 0.15), 2);
        sb.AddCompleted(BicepCurlsName, 4, 3, 12, ProgressionWeight(pullOrdinal, isDeload, 14.0, 0.3), 2);
        sb.AddCompleted(HammerCurlsName, 5, 3, 12, ProgressionWeight(pullOrdinal, isDeload, 14.0, 0.3), 2);

        ClampLastSetTime(sets, ended);
        return sets;
    }

    private static List<WorkoutSetLogEntity> BuildCompletedLegSets(
        int sessionIndex,
        DateTime started,
        DateTime ended,
        int legOrdinal,
        bool isDeload)
    {
        var sets = new List<WorkoutSetLogEntity>();
        var sb = new SetBuilder(sets, started.AddMinutes(4), sessionIndex);
        var squatKg = SquatWeightKg(legOrdinal, isDeload);
        var squatReps = new[] { 5, 5, 5, 5 };

        sb.AddWarmups(SquatsName, 0, 2, squatKg, 5, 4);
        for (var s = 0; s < 4; s++)
        {
            var e = CreateSet(SquatsName, 0, 2 + s, squatReps[s], squatKg, sb.Time);
            AssignIds(e, sessionIndex, 0, 2 + s);
            sets.Add(e);
            sb.Time = sb.Time.AddMinutes(4);
        }

        var rdlKg = ProgressionWeight(legOrdinal, isDeload, 80.0, 0.5);
        sb.AddWarmups(RomanianDeadliftName, 1, 1, rdlKg, 8, 3);
        sb.AddCompleted(RomanianDeadliftName, 1, 4, 8, rdlKg, 3, setIndexOffset: 1);
        sb.AddCompleted(LegPressName, 2, 3, 12, ProgressionWeight(legOrdinal, isDeload, 140.0, 1.0), 2);
        sb.AddCompleted(LegCurlsName, 3, 3, 12, ProgressionWeight(legOrdinal, isDeload, 35.0, 0.25), 2);
        sb.AddCompleted(CalfRaisesName, 4, 4, 15, ProgressionWeight(legOrdinal, isDeload, 50.0, 0.5), 2);
        sb.AddCompleted(LegExtensionsName, 5, 3, 12, ProgressionWeight(legOrdinal, isDeload, 40.0, 0.25), 2);

        ClampLastSetTime(sets, ended);
        return sets;
    }

    private static List<WorkoutSetLogEntity> BuildCompletedFullBodySets(
        int sessionIndex,
        DateTime started,
        DateTime ended,
        int fbOrdinal,
        bool isDeload)
    {
        var sets = new List<WorkoutSetLogEntity>();
        var sb = new SetBuilder(sets, started.AddMinutes(3), sessionIndex);

        var squatKg = ProgressionWeight(fbOrdinal, isDeload, 70.0, 0.5);
        sb.AddWarmups(SquatsName, 0, 1, squatKg, 8, 3);
        sb.AddCompleted(SquatsName, 0, 3, 8, squatKg, 3, setIndexOffset: 1);

        var benchKg = ProgressionWeight(fbOrdinal, isDeload, 60.0, 0.75);
        sb.AddWarmups(BenchPressName, 1, 1, benchKg, 8, 3);
        sb.AddCompleted(BenchPressName, 1, 3, 8, benchKg, 3, setIndexOffset: 1);

        sb.AddCompleted(BarbellRowsName, 2, 3, 10, ProgressionWeight(fbOrdinal, isDeload, 50.0, 0.5), 2);
        sb.AddCompleted(OverheadPressName, 3, 3, 8, ProgressionWeight(fbOrdinal, isDeload, 35.0, 0.375), 2);

        var pullUpReps = 6 + Math.Min(fbOrdinal, 3);
        double? pullUpWeight = null;
        if (fbOrdinal < 8)
        {
            pullUpWeight = -10.0 + fbOrdinal;
        }
        else if (fbOrdinal >= 18)
        {
            pullUpWeight = 2.5 + ((fbOrdinal - 18) * 0.5);
        }

        for (var s = 0; s < 3; s++)
        {
            var e = CreateSet(PullUpsName, 4, s, pullUpReps - Math.Min(s, 1), pullUpWeight, sb.Time);
            AssignIds(e, sessionIndex, 4, s);
            sets.Add(e);
            sb.Time = sb.Time.AddMinutes(2);
        }

        var plankSeconds = 45 + Math.Min(fbOrdinal, 60);
        sb.AddCompleted(PlankName, 5, 3, plankSeconds, null, 2);

        ClampLastSetTime(sets, ended);
        return sets;
    }

    private static List<WorkoutSetLogEntity> BuildInProgressPushSets(
        int sessionIndex,
        DateTime started,
        double benchKg)
    {
        DateTime t = started.AddMinutes(2);
        var e0 = CreateSet(BenchPressName, 0, 0, 8, benchKg, t);
        AssignIds(e0, sessionIndex, 0, 0);
        var e1 = CreateSet(BenchPressName, 0, 1, 8, benchKg, t.AddMinutes(3));
        AssignIds(e1, sessionIndex, 0, 1);
        return [e0, e1];
    }

    private static WorkoutSetLogEntity CreateSet(
        string exerciseName,
        int exerciseIndex,
        int setIndex,
        int reps,
        double? weightKg,
        DateTime completedAt,
        bool isWarmup = false) =>
        new()
        {
            ExerciseIndex = exerciseIndex,
            ExerciseName = exerciseName,
            SetIndex = setIndex,
            CompletedAtUtc = completedAt,
            Reps = reps,
            WeightKg = weightKg,
            IsWarmup = isWarmup
        };

    private static void AssignIds(WorkoutSetLogEntity entity, int sessionIndex, int exerciseIndex, int setIndex)
    {
        entity.Id = DemoDataIds.SetId(sessionIndex, exerciseIndex, setIndex);
        entity.SessionId = DemoDataIds.SessionId(sessionIndex);
    }

    private static WorkoutPlan CreatePushDayPlan()
    {
        return new WorkoutPlan
        {
            Id = DemoDataIds.PushPlan,
            Name = "Push Day",
            RestIntervalSeconds = 90,
            DefaultSetCount = 4,
            CreatedAt = s_demoPlanCreatedAt,
            Exercises =
            [
                new ExercisePlan
                {
                    Id = DemoDataIds.PushBench,
                    Name = BenchPressName,
                    SetCount = 4,
                    WarmupSetCount = 2,
                    Order = 0,
                    RestIntervalSeconds = 120,
                    DefaultReps = 8,
                    DefaultWeightKg = 60
                },
                new ExercisePlan
                {
                    Id = DemoDataIds.PushOhp,
                    Name = OverheadPressName,
                    SetCount = 4,
                    WarmupSetCount = 1,
                    Order = 1,
                    RestIntervalSeconds = 90,
                    DefaultReps = 8,
                    DefaultWeightKg = 40
                },
                new ExercisePlan
                {
                    Id = DemoDataIds.PushIncline,
                    Name = InclineDumbbellPressName,
                    SetCount = 3,
                    Order = 2,
                    RestIntervalSeconds = 90,
                    DefaultReps = 10,
                    DefaultWeightKg = 22.5
                },
                new ExercisePlan
                {
                    Id = DemoDataIds.PushLateral,
                    Name = LateralRaisesName,
                    SetCount = 3,
                    Order = 3,
                    RestIntervalSeconds = 60,
                    DefaultReps = 12,
                    DefaultWeightKg = 8
                },
                new ExercisePlan
                {
                    Id = DemoDataIds.PushTriPush,
                    Name = TricepPushdownsName,
                    SetCount = 3,
                    Order = 4,
                    RestIntervalSeconds = 60,
                    DefaultReps = 12,
                    DefaultWeightKg = 20
                },
                new ExercisePlan
                {
                    Id = DemoDataIds.PushTriOver,
                    Name = OverheadTricepExtensionName,
                    SetCount = 3,
                    Order = 5,
                    RestIntervalSeconds = 60,
                    DefaultReps = 10,
                    DefaultWeightKg = 16
                },
                new ExercisePlan
                {
                    Id = DemoDataIds.PushPushups,
                    Name = PushUpsName,
                    SetCount = 3,
                    Order = 6,
                    RestIntervalSeconds = 60,
                    DefaultReps = 15,
                    DefaultWeightKg = null,
                    BodyweightPercent = 65,
                    LogType = ExerciseLogType.BodyweightReps
                }
            ]
        };
    }

    private static WorkoutPlan CreatePullDayPlan()
    {
        return new WorkoutPlan
        {
            Id = DemoDataIds.PullPlan,
            Name = "Pull Day",
            RestIntervalSeconds = 90,
            DefaultSetCount = 4,
            CreatedAt = s_demoPlanCreatedAt,
            Exercises =
            [
                new ExercisePlan
                {
                    Id = DemoDataIds.PullDeadlift,
                    Name = DeadliftName,
                    SetCount = 3,
                    WarmupSetCount = 1,
                    Order = 0,
                    RestIntervalSeconds = 180,
                    DefaultReps = 5,
                    DefaultWeightKg = 100
                },
                new ExercisePlan
                {
                    Id = DemoDataIds.PullPullups,
                    Name = PullUpsName,
                    SetCount = 4,
                    WarmupSetCount = 1,
                    Order = 1,
                    RestIntervalSeconds = 90,
                    DefaultReps = 8,
                    DefaultWeightKg = null,
                    BodyweightPercent = 100,
                    LogType = ExerciseLogType.BodyweightReps
                },
                new ExercisePlan
                {
                    Id = DemoDataIds.PullRow,
                    Name = BarbellRowsName,
                    SetCount = 4,
                    Order = 2,
                    RestIntervalSeconds = 90,
                    DefaultReps = 10,
                    DefaultWeightKg = 55
                },
                new ExercisePlan
                {
                    Id = DemoDataIds.PullFace,
                    Name = FacePullsName,
                    SetCount = 3,
                    Order = 3,
                    RestIntervalSeconds = 60,
                    DefaultReps = 15,
                    DefaultWeightKg = 15
                },
                new ExercisePlan
                {
                    Id = DemoDataIds.PullCurl,
                    Name = BicepCurlsName,
                    SetCount = 3,
                    Order = 4,
                    RestIntervalSeconds = 60,
                    DefaultReps = 12,
                    DefaultWeightKg = 14
                },
                new ExercisePlan
                {
                    Id = DemoDataIds.PullHammer,
                    Name = HammerCurlsName,
                    SetCount = 3,
                    Order = 5,
                    RestIntervalSeconds = 60,
                    DefaultReps = 12,
                    DefaultWeightKg = 14
                }
            ]
        };
    }

    private static WorkoutPlan CreateLegDayPlan()
    {
        return new WorkoutPlan
        {
            Id = DemoDataIds.LegPlan,
            Name = "Leg Day",
            RestIntervalSeconds = 120,
            DefaultSetCount = 4,
            CreatedAt = s_demoPlanCreatedAt,
            Exercises =
            [
                new ExercisePlan { Id = DemoDataIds.LegSquat, Name = SquatsName, SetCount = 4, WarmupSetCount = 2, Order = 0, RestIntervalSeconds = 180, DefaultReps = 5, DefaultWeightKg = 100 },
                new ExercisePlan { Id = DemoDataIds.LegRdl, Name = RomanianDeadliftName, SetCount = 4, WarmupSetCount = 1, Order = 1, RestIntervalSeconds = 120, DefaultReps = 8, DefaultWeightKg = 80 },
                new ExercisePlan { Id = DemoDataIds.LegPress, Name = LegPressName, SetCount = 3, Order = 2, RestIntervalSeconds = 120, DefaultReps = 12, DefaultWeightKg = 140 },
                new ExercisePlan { Id = DemoDataIds.LegCurl, Name = LegCurlsName, SetCount = 3, Order = 3, RestIntervalSeconds = 90, DefaultReps = 12, DefaultWeightKg = 35 },
                new ExercisePlan { Id = DemoDataIds.LegCalf, Name = CalfRaisesName, SetCount = 4, Order = 4, RestIntervalSeconds = 60, DefaultReps = 15, DefaultWeightKg = 50 },
                new ExercisePlan { Id = DemoDataIds.LegExt, Name = LegExtensionsName, SetCount = 3, Order = 5, RestIntervalSeconds = 90, DefaultReps = 12, DefaultWeightKg = 40 }
            ]
        };
    }

    private static WorkoutPlan CreateFullBodyPlan()
    {
        return new WorkoutPlan
        {
            Id = DemoDataIds.FullBodyPlan,
            Name = "Full Body Workout",
            RestIntervalSeconds = 90,
            DefaultSetCount = 3,
            CreatedAt = s_demoPlanCreatedAt,
            Exercises =
            [
                new ExercisePlan { Id = DemoDataIds.FbSquat, Name = SquatsName, SetCount = 3, WarmupSetCount = 1, Order = 0, RestIntervalSeconds = 120, DefaultReps = 8, DefaultWeightKg = 70 },
                new ExercisePlan { Id = DemoDataIds.FbBench, Name = BenchPressName, SetCount = 3, WarmupSetCount = 1, SupersetGroupId = "A", Order = 1, RestIntervalSeconds = 120, DefaultReps = 8, DefaultWeightKg = 60 },
                new ExercisePlan { Id = DemoDataIds.FbRow, Name = BarbellRowsName, SetCount = 3, SupersetGroupId = "A", Order = 2, RestIntervalSeconds = 90, DefaultReps = 10, DefaultWeightKg = 50 },
                new ExercisePlan { Id = DemoDataIds.FbOhp, Name = OverheadPressName, SetCount = 3, SupersetGroupId = "B", Order = 3, RestIntervalSeconds = 90, DefaultReps = 8, DefaultWeightKg = 35 },
                new ExercisePlan { Id = DemoDataIds.FbPullup, Name = PullUpsName, SetCount = 3, SupersetGroupId = "B", Order = 4, RestIntervalSeconds = 90, DefaultReps = 8, DefaultWeightKg = null, BodyweightPercent = 100, LogType = ExerciseLogType.BodyweightReps },
                new ExercisePlan { Id = DemoDataIds.FbPlank, Name = PlankName, SetCount = 3, Order = 5, RestIntervalSeconds = 45, DefaultReps = 45, DefaultWeightKg = null, LogType = ExerciseLogType.Duration }
            ]
        };
    }
}
