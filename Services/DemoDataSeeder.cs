using System.Text.Json;
using Physiquinator.Data;
using Physiquinator.Models;

namespace Physiquinator.Services;

public class DemoDataSeeder
{
    public const string InitialDemoSeedCompletedKey = "Physiquinator.DemoDataInitialSeedCompleted";
    public const string DemoHistorySeedCompletedKey = "Physiquinator.DemoHistorySeedCompleted";

    private const int DemoHistoryWeeks = 52;
    private const int SkipSessionThresholdPercent = 40;

    private static readonly DateTime s_demoPlanCreatedAt = new(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly JsonSerializerOptions s_snapshotJson = new() { WriteIndented = false };

    private readonly WorkoutPlanService _planService;
    private readonly AppDatabase _database;
    private readonly WorkoutHistoryRepository _historyRepository;
    private readonly IDemoSeedPreferences _preferences;

    public DemoDataSeeder(
        WorkoutPlanService planService,
        AppDatabase database,
        WorkoutHistoryRepository historyRepository,
        IDemoSeedPreferences preferences)
    {
        _planService = planService;
        _database = database;
        _historyRepository = historyRepository;
        _preferences = preferences;
    }

    public async Task<bool> SeedDemoDataIfNeededAsync()
    {
        if (_preferences.Get(InitialDemoSeedCompletedKey, false))
            return false;

        var existingPlans = await _planService.GetAllPlansAsync();
        if (existingPlans.Any())
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

        for (int i = 0; i < demoPlans.Count; i++)
        {
            demoPlans[i].SortOrder = i;
            await _planService.SavePlanAsync(demoPlans[i]);
        }

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
            [DemoDataIds.PushPlan] = JsonSerializer.Serialize(CreatePushDayPlan(), s_snapshotJson),
            [DemoDataIds.PullPlan] = JsonSerializer.Serialize(CreatePullDayPlan(), s_snapshotJson),
            [DemoDataIds.LegPlan] = JsonSerializer.Serialize(CreateLegDayPlan(), s_snapshotJson),
            [DemoDataIds.FullBodyPlan] = JsonSerializer.Serialize(CreateFullBodyPlan(), s_snapshotJson)
        };

        var todayUtc = DateTime.UtcNow.Date;
        var specs = GenerateDemoSchedule(todayUtc);

        await _database.Database.RunInTransactionAsync(conn =>
        {
            for (var i = 0; i < specs.Count; i++)
            {
                var spec = specs[i];
                var started = todayUtc
                    .AddDays(-spec.DaysAgo)
                    .AddHours(spec.StartHourUtc)
                    .AddMinutes(spec.StartMinuteUtc);
                var ended = spec.Ended
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
                    PlanSnapshotJson = snapshots[spec.PlanId]
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

                foreach (var set in sets)
                    conn.InsertOrReplace(set);
            }
        });

        _preferences.Set(DemoHistorySeedCompletedKey, true);
        return true;
    }

    private async Task<bool> HasAllDemoPlansAsync() =>
        await _planService.GetPlanAsync(DemoDataIds.PushPlan) is not null
        && await _planService.GetPlanAsync(DemoDataIds.PullPlan) is not null
        && await _planService.GetPlanAsync(DemoDataIds.LegPlan) is not null
        && await _planService.GetPlanAsync(DemoDataIds.FullBodyPlan) is not null;

    private static string GetPlanName(Guid planId) => planId switch
    {
        _ when planId == DemoDataIds.PushPlan => "Push Day",
        _ when planId == DemoDataIds.PullPlan => "Pull Day",
        _ when planId == DemoDataIds.LegPlan => "Leg Day",
        _ when planId == DemoDataIds.FullBodyPlan => "Full Body Workout",
        _ => "Workout"
    };

    private static IReadOnlyList<DemoSessionSpec> GenerateDemoSchedule(DateTime todayUtc)
    {
        var today = DateOnly.FromDateTime(todayUtc);
        var gridStartMonday = GetMondayOfWeek(today)
            .AddDays(-7 * (DemoHistoryWeeks - 1));

        var specs = new List<DemoSessionSpec>();
        var pushOrd = 0;
        var pullOrd = 0;
        var legOrd = 0;
        var fbOrd = 0;

        for (var week = 0; week < DemoHistoryWeeks; week++)
        {
            var weekMonday = gridStartMonday.AddDays(week * 7);

            TryAdd(specs, today, week, weekMonday, DayOfWeek.Monday, DemoDataIds.PushPlan, slotKey: 0, ref pushOrd);
            TryAdd(specs, today, week, weekMonday, DayOfWeek.Wednesday, DemoDataIds.PullPlan, slotKey: 1, ref pullOrd);
            TryAdd(specs, today, week, weekMonday, DayOfWeek.Friday, DemoDataIds.LegPlan, slotKey: 2, ref legOrd);

            if (week % 2 == 0)
                TryAdd(specs, today, week, weekMonday, DayOfWeek.Sunday, DemoDataIds.FullBodyPlan, slotKey: 3, ref fbOrd);
        }

        specs.Add(new DemoSessionSpec(
            DaysAgo: 0,
            StartHourUtc: 10,
            StartMinuteUtc: 0,
            PlanId: DemoDataIds.PushPlan,
            Ended: false,
            DurationMinutes: 0,
            PlanTypeOrdinal: pushOrd,
            IsDeload: false));

        return specs;
    }

    private static void TryAdd(
        List<DemoSessionSpec> specs,
        DateOnly today,
        int week,
        DateOnly weekMonday,
        DayOfWeek dayOfWeek,
        Guid planId,
        int slotKey,
        ref int planOrdinal)
    {
            if (ShouldSkipSession(week, slotKey))
                return;

            var sessionDate = weekMonday.AddDays(OffsetFromMonday(dayOfWeek));
            if (sessionDate > today)
                return;

            var daysAgo = today.DayNumber - sessionDate.DayNumber;
            if (daysAgo < 0)
                return;

            if (daysAgo == 0 && planId == DemoDataIds.PushPlan)
                return;

            var hash = week * 31 + slotKey * 17;
            var startHour = hash % 4 switch
            {
                0 => 7,
                1 => 9,
                2 => 17,
                _ => 18
            };
            var startMinute = (hash % 3) * 15;
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

    private static DateOnly GetMondayOfWeek(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff);
    }

    private static int OffsetFromMonday(DayOfWeek dayOfWeek) =>
        ((int)dayOfWeek - (int)DayOfWeek.Monday + 7) % 7;

    private static bool ShouldSkipSession(int weekIndex, int slotKey)
    {
        if (weekIndex >= DemoHistoryWeeks - 2)
            return false;

        return (weekIndex * 31 + slotKey * 17) % 100 < SkipSessionThresholdPercent;
    }

    private static bool IsDeloadSession(int planOrdinal) =>
        planOrdinal > 0 && (planOrdinal + 1) % 5 == 0;

    private static double ApplyDeload(double kg, bool isDeload) =>
        isDeload ? kg * 0.9 : kg;

    private static double BenchWeightKg(int ordinal, bool deload) =>
        ApplyDeload(60.0 + Math.Min(ordinal, 17) * 2.5, deload);

    private static double SquatWeightKg(int ordinal, bool deload, double baseKg = 100.0) =>
        ApplyDeload(baseKg + Math.Min(ordinal, 14) * 2.5, deload);

    private static void ClampLastSetTime(List<WorkoutSetLogEntity> sets, DateTime ended)
    {
        if (sets.Count == 0)
            return;

        var t = ended.AddMinutes(-1);
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
        var t = started.AddMinutes(3);
        var benchKg = BenchWeightKg(pushOrdinal, isDeload);
        var benchReps = new[] { 10, 9, 9, 8 };

        for (var s = 0; s < 4; s++)
        {
            sets.Add(CreateSet(sessionIndex, 0, s, "Bench Press", t, benchReps[s], benchKg));
            t = t.AddMinutes(3);
        }

        var ohpKg = ApplyDeload(42.5 + Math.Min(pushOrdinal, 16) * 1.25, isDeload);
        for (var s = 0; s < 4; s++)
        {
            sets.Add(CreateSet(sessionIndex, 1, s, "Overhead Press", t, 9 - Math.Min(s, 2), ohpKg));
            t = t.AddMinutes(2);
        }

        var inclineBase = ApplyDeload(22.5 + Math.Min(pushOrdinal, 12) * 1.25, isDeload);
        for (var s = 0; s < 3; s++)
        {
            sets.Add(CreateSet(sessionIndex, 2, s, "Incline Dumbbell Press", t, 10, inclineBase + s * 2.5));
            t = t.AddMinutes(2);
        }

        var lateralKg = ApplyDeload(8.0 + Math.Min(pushOrdinal, 10) * 0.5, isDeload);
        for (var s = 0; s < 3; s++)
        {
            sets.Add(CreateSet(sessionIndex, 3, s, "Lateral Raises", t, 12 + (s == 0 ? 2 : 0), lateralKg));
            t = t.AddMinutes(2);
        }

        var triPushKg = ApplyDeload(20.0 + Math.Min(pushOrdinal, 8) * 1.25, isDeload);
        for (var s = 0; s < 3; s++)
        {
            sets.Add(CreateSet(sessionIndex, 4, s, "Tricep Pushdowns", t, 12, triPushKg));
            t = t.AddMinutes(2);
        }

        var triOverKg = ApplyDeload(16.0 + Math.Min(pushOrdinal, 8) * 1.0, isDeload);
        for (var s = 0; s < 3; s++)
        {
            sets.Add(CreateSet(sessionIndex, 5, s, "Overhead Tricep Extension", t, 10, triOverKg));
            t = t.AddMinutes(2);
        }

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
        var t = started.AddMinutes(4);
        var dlStep = pullOrdinal / 2;
        var dlKg = ApplyDeload(100.0 + dlStep * 5.0, isDeload);

        for (var s = 0; s < 3; s++)
        {
            sets.Add(CreateSet(sessionIndex, 0, s, "Deadlift", t, 6 - s, dlKg));
            t = t.AddMinutes(4);
        }

        var pullUpReps = 6 + Math.Min(pullOrdinal, 4);
        double? pullUpWeight = null;
        if (pullOrdinal < 12)
        {
            pullUpWeight = -15.0 + pullOrdinal;
        }
        else if (pullOrdinal >= 28)
        {
            pullUpWeight = 2.5 + Math.Floor((pullOrdinal - 28) / 3.0) * 1.25;
        }

        for (var s = 0; s < 4; s++)
        {
            sets.Add(CreateSet(sessionIndex, 1, s, "Pull-Ups", t, pullUpReps - Math.Min(s, 2), pullUpWeight));
            t = t.AddMinutes(2);
        }

        var rowKg = ApplyDeload(55.0 + Math.Min(pullOrdinal, 12) * 2.5, isDeload);
        for (var s = 0; s < 4; s++)
        {
            sets.Add(CreateSet(sessionIndex, 2, s, "Barbell Rows", t, 10, rowKg));
            t = t.AddMinutes(2);
        }

        var faceKg = ApplyDeload(15.0 + Math.Min(pullOrdinal, 6) * 0.5, isDeload);
        for (var s = 0; s < 3; s++)
        {
            sets.Add(CreateSet(sessionIndex, 3, s, "Face Pulls", t, 15, faceKg));
            t = t.AddMinutes(2);
        }

        var curlKg = ApplyDeload(14.0 + Math.Min(pullOrdinal, 10) * 1.25, isDeload);
        for (var s = 0; s < 3; s++)
        {
            sets.Add(CreateSet(sessionIndex, 4, s, "Bicep Curls", t, 12, curlKg));
            t = t.AddMinutes(2);
        }

        var hammerKg = ApplyDeload(14.0 + Math.Min(pullOrdinal, 10) * 1.25, isDeload);
        for (var s = 0; s < 3; s++)
        {
            sets.Add(CreateSet(sessionIndex, 5, s, "Hammer Curls", t, 12, hammerKg));
            t = t.AddMinutes(2);
        }

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
        var t = started.AddMinutes(4);
        var squatKg = SquatWeightKg(legOrdinal, isDeload);
        var squatReps = new[] { 5, 5, 5, 5 };

        for (var s = 0; s < 4; s++)
        {
            sets.Add(CreateSet(sessionIndex, 0, s, "Squats", t, squatReps[s], squatKg));
            t = t.AddMinutes(4);
        }

        var rdlKg = ApplyDeload(80.0 + Math.Min(legOrdinal, 12) * 2.5, isDeload);
        for (var s = 0; s < 4; s++)
        {
            sets.Add(CreateSet(sessionIndex, 1, s, "Romanian Deadlift", t, 8, rdlKg));
            t = t.AddMinutes(3);
        }

        var pressKg = ApplyDeload(140.0 + Math.Min(legOrdinal, 8) * 5.0, isDeload);
        for (var s = 0; s < 3; s++)
        {
            sets.Add(CreateSet(sessionIndex, 2, s, "Leg Press", t, 12, pressKg));
            t = t.AddMinutes(2);
        }

        var curlKg = ApplyDeload(35.0 + Math.Min(legOrdinal, 8) * 1.25, isDeload);
        for (var s = 0; s < 3; s++)
        {
            sets.Add(CreateSet(sessionIndex, 3, s, "Leg Curls", t, 12, curlKg));
            t = t.AddMinutes(2);
        }

        var calfKg = ApplyDeload(50.0 + Math.Min(legOrdinal, 10) * 2.5, isDeload);
        for (var s = 0; s < 4; s++)
        {
            sets.Add(CreateSet(sessionIndex, 4, s, "Calf Raises", t, 15, calfKg));
            t = t.AddMinutes(2);
        }

        var extKg = ApplyDeload(40.0 + Math.Min(legOrdinal, 8) * 1.25, isDeload);
        for (var s = 0; s < 3; s++)
        {
            sets.Add(CreateSet(sessionIndex, 5, s, "Leg Extensions", t, 12, extKg));
            t = t.AddMinutes(2);
        }

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
        var t = started.AddMinutes(3);

        var squatKg = SquatWeightKg(fbOrdinal, isDeload, baseKg: 70.0);
        for (var s = 0; s < 3; s++)
        {
            sets.Add(CreateSet(sessionIndex, 0, s, "Squats", t, 8, squatKg));
            t = t.AddMinutes(3);
        }

        var benchKg = BenchWeightKg(fbOrdinal, isDeload);
        for (var s = 0; s < 3; s++)
        {
            sets.Add(CreateSet(sessionIndex, 1, s, "Bench Press", t, 8, benchKg));
            t = t.AddMinutes(3);
        }

        var rowKg = ApplyDeload(50.0 + Math.Min(fbOrdinal, 10) * 2.5, isDeload);
        for (var s = 0; s < 3; s++)
        {
            sets.Add(CreateSet(sessionIndex, 2, s, "Barbell Rows", t, 10, rowKg));
            t = t.AddMinutes(2);
        }

        var ohpKg = ApplyDeload(35.0 + Math.Min(fbOrdinal, 10) * 1.25, isDeload);
        for (var s = 0; s < 3; s++)
        {
            sets.Add(CreateSet(sessionIndex, 3, s, "Overhead Press", t, 8, ohpKg));
            t = t.AddMinutes(2);
        }

        var pullUpReps = 6 + Math.Min(fbOrdinal, 3);
        double? pullUpWeight = null;
        if (fbOrdinal < 8)
        {
            pullUpWeight = -10.0 + fbOrdinal;
        }
        else if (fbOrdinal >= 18)
        {
            pullUpWeight = 2.5 + (fbOrdinal - 18) * 0.5;
        }

        for (var s = 0; s < 3; s++)
        {
            sets.Add(CreateSet(sessionIndex, 4, s, "Pull-Ups", t, pullUpReps - Math.Min(s, 1), pullUpWeight));
            t = t.AddMinutes(2);
        }

        var plankSeconds = 45 + Math.Min(fbOrdinal, 6) * 5;
        for (var s = 0; s < 3; s++)
        {
            sets.Add(CreateSet(sessionIndex, 5, s, "Plank", t, plankSeconds, null));
            t = t.AddMinutes(2);
        }

        ClampLastSetTime(sets, ended);
        return sets;
    }

    private static List<WorkoutSetLogEntity> BuildInProgressPushSets(
        int sessionIndex,
        DateTime started,
        double benchKg)
    {
        var t = started.AddMinutes(2);
        return
        [
            CreateSet(sessionIndex, 0, 0, "Bench Press", t, 8, benchKg),
            CreateSet(sessionIndex, 0, 1, "Bench Press", t.AddMinutes(3), 8, benchKg)
        ];
    }

    private static WorkoutSetLogEntity CreateSet(
        int sessionIndex,
        int exerciseIndex,
        int setIndex,
        string exerciseName,
        DateTime completedAt,
        int reps,
        double? weightKg) =>
        new()
        {
            Id = DemoDataIds.SetId(sessionIndex, exerciseIndex, setIndex),
            SessionId = DemoDataIds.SessionId(sessionIndex),
            ExerciseIndex = exerciseIndex,
            ExerciseName = exerciseName,
            SetIndex = setIndex,
            CompletedAtUtc = completedAt,
            Reps = reps,
            WeightKg = weightKg
        };

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
                    Name = "Bench Press",
                    SetCount = 4,
                    Order = 0,
                    RestIntervalSeconds = 120,
                    DefaultReps = 8,
                    DefaultWeightKg = 60
                },
                new ExercisePlan
                {
                    Id = DemoDataIds.PushOhp,
                    Name = "Overhead Press",
                    SetCount = 4,
                    Order = 1,
                    RestIntervalSeconds = 90,
                    DefaultReps = 8,
                    DefaultWeightKg = 40
                },
                new ExercisePlan
                {
                    Id = DemoDataIds.PushIncline,
                    Name = "Incline Dumbbell Press",
                    SetCount = 3,
                    Order = 2,
                    RestIntervalSeconds = 90,
                    DefaultReps = 10,
                    DefaultWeightKg = 22.5
                },
                new ExercisePlan
                {
                    Id = DemoDataIds.PushLateral,
                    Name = "Lateral Raises",
                    SetCount = 3,
                    Order = 3,
                    RestIntervalSeconds = 60,
                    DefaultReps = 12,
                    DefaultWeightKg = 8
                },
                new ExercisePlan
                {
                    Id = DemoDataIds.PushTriPush,
                    Name = "Tricep Pushdowns",
                    SetCount = 3,
                    Order = 4,
                    RestIntervalSeconds = 60,
                    DefaultReps = 12,
                    DefaultWeightKg = 20
                },
                new ExercisePlan
                {
                    Id = DemoDataIds.PushTriOver,
                    Name = "Overhead Tricep Extension",
                    SetCount = 3,
                    Order = 5,
                    RestIntervalSeconds = 60,
                    DefaultReps = 10,
                    DefaultWeightKg = 16
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
                    Name = "Deadlift",
                    SetCount = 3,
                    Order = 0,
                    RestIntervalSeconds = 180,
                    DefaultReps = 5,
                    DefaultWeightKg = 100
                },
                new ExercisePlan
                {
                    Id = DemoDataIds.PullPullups,
                    Name = "Pull-Ups",
                    SetCount = 4,
                    Order = 1,
                    RestIntervalSeconds = 90,
                    DefaultReps = 8,
                    DefaultWeightKg = null,
                    LogType = ExerciseLogType.BodyweightReps
                },
                new ExercisePlan
                {
                    Id = DemoDataIds.PullRow,
                    Name = "Barbell Rows",
                    SetCount = 4,
                    Order = 2,
                    RestIntervalSeconds = 90,
                    DefaultReps = 10,
                    DefaultWeightKg = 55
                },
                new ExercisePlan
                {
                    Id = DemoDataIds.PullFace,
                    Name = "Face Pulls",
                    SetCount = 3,
                    Order = 3,
                    RestIntervalSeconds = 60,
                    DefaultReps = 15,
                    DefaultWeightKg = 15
                },
                new ExercisePlan
                {
                    Id = DemoDataIds.PullCurl,
                    Name = "Bicep Curls",
                    SetCount = 3,
                    Order = 4,
                    RestIntervalSeconds = 60,
                    DefaultReps = 12,
                    DefaultWeightKg = 14
                },
                new ExercisePlan
                {
                    Id = DemoDataIds.PullHammer,
                    Name = "Hammer Curls",
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
                new ExercisePlan { Id = DemoDataIds.LegSquat, Name = "Squats", SetCount = 4, Order = 0, RestIntervalSeconds = 180, DefaultReps = 5, DefaultWeightKg = 100 },
                new ExercisePlan { Id = DemoDataIds.LegRdl, Name = "Romanian Deadlift", SetCount = 4, Order = 1, RestIntervalSeconds = 120, DefaultReps = 8, DefaultWeightKg = 80 },
                new ExercisePlan { Id = DemoDataIds.LegPress, Name = "Leg Press", SetCount = 3, Order = 2, RestIntervalSeconds = 120, DefaultReps = 12, DefaultWeightKg = 140 },
                new ExercisePlan { Id = DemoDataIds.LegCurl, Name = "Leg Curls", SetCount = 3, Order = 3, RestIntervalSeconds = 90, DefaultReps = 12, DefaultWeightKg = 35 },
                new ExercisePlan { Id = DemoDataIds.LegCalf, Name = "Calf Raises", SetCount = 4, Order = 4, RestIntervalSeconds = 60, DefaultReps = 15, DefaultWeightKg = 50 },
                new ExercisePlan { Id = DemoDataIds.LegExt, Name = "Leg Extensions", SetCount = 3, Order = 5, RestIntervalSeconds = 90, DefaultReps = 12, DefaultWeightKg = 40 }
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
                new ExercisePlan { Id = DemoDataIds.FbSquat, Name = "Squats", SetCount = 3, Order = 0, RestIntervalSeconds = 120, DefaultReps = 8, DefaultWeightKg = 70 },
                new ExercisePlan { Id = DemoDataIds.FbBench, Name = "Bench Press", SetCount = 3, Order = 1, RestIntervalSeconds = 120, DefaultReps = 8, DefaultWeightKg = 60 },
                new ExercisePlan { Id = DemoDataIds.FbRow, Name = "Barbell Rows", SetCount = 3, Order = 2, RestIntervalSeconds = 90, DefaultReps = 10, DefaultWeightKg = 50 },
                new ExercisePlan { Id = DemoDataIds.FbOhp, Name = "Overhead Press", SetCount = 3, Order = 3, RestIntervalSeconds = 90, DefaultReps = 8, DefaultWeightKg = 35 },
                new ExercisePlan { Id = DemoDataIds.FbPullup, Name = "Pull-Ups", SetCount = 3, Order = 4, RestIntervalSeconds = 90, DefaultReps = 8, DefaultWeightKg = null, LogType = ExerciseLogType.BodyweightReps },
                new ExercisePlan { Id = DemoDataIds.FbPlank, Name = "Plank", SetCount = 3, Order = 5, RestIntervalSeconds = 45, DefaultReps = 45, DefaultWeightKg = null, LogType = ExerciseLogType.Duration }
            ]
        };
    }
}
