using Physiquinator.Core.Data;
using Physiquinator.Core.Models;

namespace Physiquinator.Core.Services.Demo;

internal static class DemoPlans
{
    internal const string BenchPressName = "Bench Press";
    internal const string OverheadPressName = "Overhead Press";
    internal const string PullUpsName = "Pull-Ups";
    internal const string BarbellRowsName = "Barbell Rows";
    internal const string SquatsName = "Squats";
    internal const string DeadliftName = "Deadlift";
    internal const string InclineDumbbellPressName = "Incline Dumbbell Press";
    internal const string LateralRaisesName = "Lateral Raises";
    internal const string TricepPushdownsName = "Tricep Pushdowns";
    internal const string OverheadTricepExtensionName = "Overhead Tricep Extension";
    internal const string FacePullsName = "Face Pulls";
    internal const string BicepCurlsName = "Bicep Curls";
    internal const string HammerCurlsName = "Hammer Curls";
    internal const string RomanianDeadliftName = "Romanian Deadlift";
    internal const string LegPressName = "Leg Press";
    internal const string LegCurlsName = "Leg Curls";
    internal const string CalfRaisesName = "Calf Raises";
    internal const string LegExtensionsName = "Leg Extensions";
    internal const string PlankName = "Plank";
    internal const string PushUpsName = "Push-Ups";

    private static readonly DateTime s_demoPlanCreatedAt = new(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    internal static WorkoutPlan CreatePushDayPlan()
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

    internal static WorkoutPlan CreatePullDayPlan()
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

    internal static WorkoutPlan CreateLegDayPlan()
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

    internal static WorkoutPlan CreateFullBodyPlan()
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
