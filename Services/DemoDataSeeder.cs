using System.Text.Json;
using Physiquinator.Data;
using Physiquinator.Models;

namespace Physiquinator.Services;

public class DemoDataSeeder
{
    public const string InitialDemoSeedCompletedKey = "Physiquinator.DemoDataInitialSeedCompleted";
    public const string DemoHistorySeedCompletedKey = "Physiquinator.DemoHistorySeedCompleted";

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

    public async Task SeedDemoDataIfNeededAsync()
    {
        if (_preferences.Get(InitialDemoSeedCompletedKey, false))
            return;

        var existingPlans = await _planService.GetAllPlansAsync();
        if (existingPlans.Any())
        {
            _preferences.Set(InitialDemoSeedCompletedKey, true);
            return;
        }

        var demoPlans = new List<WorkoutPlan>
        {
            CreatePushDayPlan(),
            CreatePullDayPlan(),
            CreateLegDayPlan(),
            CreateFullBodyPlan()
        };

        foreach (var plan in demoPlans)
        {
            await _planService.SavePlanAsync(plan);
        }

        _preferences.Set(InitialDemoSeedCompletedKey, true);
    }

    /// <summary>
    /// Seeds demo workout history once (empty sessions + preference gate). Requires demo Push/Pull plans in the database.
    /// </summary>
    public async Task SeedDemoHistoryIfNeededAsync()
    {
        if (_preferences.Get(DemoHistorySeedCompletedKey, false))
            return;

        await _database.EnsureInitializedAsync();

        if (await _historyRepository.GetSessionCountAsync() > 0)
        {
            _preferences.Set(DemoHistorySeedCompletedKey, true);
            return;
        }

        if (await _planService.GetPlanAsync(DemoDataIds.PushPlan) is null
            || await _planService.GetPlanAsync(DemoDataIds.PullPlan) is null)
        {
            // No demo plan library: user already had other plans on first launch.
            _preferences.Set(DemoHistorySeedCompletedKey, true);
            return;
        }

        var pushSnapshot = JsonSerializer.Serialize(CreatePushDayPlan(), s_snapshotJson);
        var pullSnapshot = JsonSerializer.Serialize(CreatePullDayPlan(), s_snapshotJson);

        var todayUtc = DateTime.UtcNow.Date;
        var specs = BuildSessionSpecs();
        var pushOrdinal = 0;

        for (var i = 0; i < specs.Length; i++)
        {
            var spec = specs[i];
            var isPush = spec.PlanId == DemoDataIds.PushPlan;
            var benchOrdinal = isPush ? pushOrdinal++ : -1;

            var started = todayUtc
                .AddDays(-spec.DaysAgo)
                .AddHours(spec.StartHourUtc)
                .AddMinutes(spec.StartMinuteUtc);
            var ended = spec.Ended
                ? started.AddMinutes(spec.DurationMinutes)
                : (DateTime?)null;

            var planName = isPush ? "Push Day" : "Pull Day";
            var snapshot = isPush ? pushSnapshot : pullSnapshot;

            var session = new WorkoutSessionLogEntity
            {
                Id = DemoDataIds.SessionId(i),
                WorkoutPlanId = spec.PlanId.ToString(),
                PlanName = planName,
                StartedAtUtc = started,
                EndedAtUtc = ended,
                PlanSnapshotJson = snapshot
            };

            await _database.Database.InsertOrReplaceAsync(session);

            var sets = spec.Ended
                ? isPush
                    ? BuildCompletedPushSets(i, started, ended!.Value, benchOrdinal)
                    : BuildCompletedPullSets(i, started, ended!.Value, sessionOrdinal: i)
                : BuildInProgressPushSets(i, started);

            foreach (var set in sets)
                await _database.Database.InsertOrReplaceAsync(set);
        }

        _preferences.Set(DemoHistorySeedCompletedKey, true);
    }

    private static DemoSessionSpec[] BuildSessionSpecs() =>
    [
        new(64, 17, 30, DemoDataIds.PullPlan, true, 52),
        new(60, 9, 0, DemoDataIds.PushPlan, true, 64),
        new(57, 18, 15, DemoDataIds.PullPlan, true, 50),
        new(53, 8, 45, DemoDataIds.PushPlan, true, 68),
        new(50, 19, 0, DemoDataIds.PullPlan, true, 48),
        new(46, 10, 30, DemoDataIds.PushPlan, true, 62),
        new(43, 17, 0, DemoDataIds.PullPlan, true, 55),
        new(39, 9, 15, DemoDataIds.PushPlan, true, 66),
        new(23, 18, 30, DemoDataIds.PullPlan, true, 54),
        new(21, 7, 0, DemoDataIds.PushPlan, true, 70),
        new(20, 9, 0, DemoDataIds.PullPlan, true, 56),
        new(20, 20, 30, DemoDataIds.PushPlan, true, 65),
        new(18, 17, 45, DemoDataIds.PullPlan, true, 53),
        new(16, 8, 0, DemoDataIds.PushPlan, true, 63),
        new(14, 19, 15, DemoDataIds.PullPlan, true, 51),
        new(13, 9, 30, DemoDataIds.PushPlan, true, 64),
        new(12, 18, 0, DemoDataIds.PullPlan, true, 52),
        new(11, 8, 45, DemoDataIds.PushPlan, true, 67),
        new(10, 17, 30, DemoDataIds.PullPlan, true, 50),
        new(8, 9, 0, DemoDataIds.PushPlan, true, 61),
        new(5, 18, 45, DemoDataIds.PullPlan, true, 49),
        new(3, 7, 30, DemoDataIds.PushPlan, true, 69),
        new(1, 19, 0, DemoDataIds.PullPlan, true, 57),
        new(0, 10, 0, DemoDataIds.PushPlan, false, 0)
    ];

    private readonly record struct DemoSessionSpec(
        int DaysAgo,
        int StartHourUtc,
        int StartMinuteUtc,
        Guid PlanId,
        bool Ended,
        int DurationMinutes);

    private static List<WorkoutSetLogEntity> BuildCompletedPushSets(
        int sessionIndex,
        DateTime started,
        DateTime ended,
        int benchProgressOrdinal)
    {
        var sets = new List<WorkoutSetLogEntity>();
        var t = started.AddMinutes(3);
        var benchKg = 60.0 + benchProgressOrdinal * 2.5;
        var benchReps = new[] { 10, 9, 9, 8 };

        for (var s = 0; s < 4; s++)
        {
            sets.Add(new WorkoutSetLogEntity
            {
                Id = DemoDataIds.SetId(sessionIndex, 0, s),
                SessionId = DemoDataIds.SessionId(sessionIndex),
                ExerciseIndex = 0,
                ExerciseName = "Bench Press",
                SetIndex = s,
                CompletedAtUtc = t,
                Reps = benchReps[s],
                WeightKg = benchKg
            });
            t = t.AddMinutes(3);
        }

        var ohpKg = 42.5 + benchProgressOrdinal * 1.25;
        for (var s = 0; s < 3; s++)
        {
            sets.Add(new WorkoutSetLogEntity
            {
                Id = DemoDataIds.SetId(sessionIndex, 1, s),
                SessionId = DemoDataIds.SessionId(sessionIndex),
                ExerciseIndex = 1,
                ExerciseName = "Overhead Press",
                SetIndex = s,
                CompletedAtUtc = t,
                Reps = 9 - s,
                WeightKg = ohpKg
            });
            t = t.AddMinutes(2);
        }

        for (var s = 0; s < 2; s++)
        {
            sets.Add(new WorkoutSetLogEntity
            {
                Id = DemoDataIds.SetId(sessionIndex, 2, s),
                SessionId = DemoDataIds.SessionId(sessionIndex),
                ExerciseIndex = 2,
                ExerciseName = "Incline Dumbbell Press",
                SetIndex = s,
                CompletedAtUtc = t,
                Reps = 10,
                WeightKg = 22.5 + s * 2.5
            });
            t = t.AddMinutes(2);
        }

        // Keep last completion at or before session end
        if (t > ended.AddMinutes(-1))
            t = ended.AddMinutes(-1);

        sets[^1].CompletedAtUtc = t;
        return sets;
    }

    private static List<WorkoutSetLogEntity> BuildCompletedPullSets(
        int sessionIndex,
        DateTime started,
        DateTime ended,
        int sessionOrdinal)
    {
        var sets = new List<WorkoutSetLogEntity>();
        var t = started.AddMinutes(4);
        var dlKg = 100.0 + (sessionOrdinal % 7) * 5.0;

        for (var s = 0; s < 3; s++)
        {
            sets.Add(new WorkoutSetLogEntity
            {
                Id = DemoDataIds.SetId(sessionIndex, 0, s),
                SessionId = DemoDataIds.SessionId(sessionIndex),
                ExerciseIndex = 0,
                ExerciseName = "Deadlift",
                SetIndex = s,
                CompletedAtUtc = t,
                Reps = 6 - s,
                WeightKg = dlKg
            });
            t = t.AddMinutes(4);
        }

        for (var s = 0; s < 3; s++)
        {
            sets.Add(new WorkoutSetLogEntity
            {
                Id = DemoDataIds.SetId(sessionIndex, 1, s),
                SessionId = DemoDataIds.SessionId(sessionIndex),
                ExerciseIndex = 1,
                ExerciseName = "Pull-Ups",
                SetIndex = s,
                CompletedAtUtc = t,
                Reps = 8 - s,
                WeightKg = null
            });
            t = t.AddMinutes(2);
        }

        for (var s = 0; s < 3; s++)
        {
            sets.Add(new WorkoutSetLogEntity
            {
                Id = DemoDataIds.SetId(sessionIndex, 2, s),
                SessionId = DemoDataIds.SessionId(sessionIndex),
                ExerciseIndex = 2,
                ExerciseName = "Barbell Rows",
                SetIndex = s,
                CompletedAtUtc = t,
                Reps = 10,
                WeightKg = 55 + s * 2.5
            });
            t = t.AddMinutes(2);
        }

        if (t > ended.AddMinutes(-1))
            t = ended.AddMinutes(-1);
        sets[^1].CompletedAtUtc = t;
        return sets;
    }

    private static List<WorkoutSetLogEntity> BuildInProgressPushSets(int sessionIndex, DateTime started)
    {
        var t = started.AddMinutes(2);
        return
        [
            new WorkoutSetLogEntity
            {
                Id = DemoDataIds.SetId(sessionIndex, 0, 0),
                SessionId = DemoDataIds.SessionId(sessionIndex),
                ExerciseIndex = 0,
                ExerciseName = "Bench Press",
                SetIndex = 0,
                CompletedAtUtc = t,
                Reps = 8,
                WeightKg = 82.5
            },
            new WorkoutSetLogEntity
            {
                Id = DemoDataIds.SetId(sessionIndex, 0, 1),
                SessionId = DemoDataIds.SessionId(sessionIndex),
                ExerciseIndex = 0,
                ExerciseName = "Bench Press",
                SetIndex = 1,
                CompletedAtUtc = t.AddMinutes(3),
                Reps = 8,
                WeightKg = 82.5
            }
        ];
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
                    DefaultWeightKg = null
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
                new ExercisePlan { Id = DemoDataIds.FbPullup, Name = "Pull-Ups", SetCount = 3, Order = 4, RestIntervalSeconds = 90, DefaultReps = 8, DefaultWeightKg = null },
                new ExercisePlan { Id = DemoDataIds.FbPlank, Name = "Plank", SetCount = 3, Order = 5, RestIntervalSeconds = 45, DefaultReps = 45, DefaultWeightKg = null }
            ]
        };
    }
}
