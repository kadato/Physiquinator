using Physiquinator.Core.Data;
using Physiquinator.Core.Models;

namespace Physiquinator.Core.Services.Demo;

internal static class DemoSetBuilders
{
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

    internal static double BenchWeightKg(int ordinal, bool deload) =>
        ApplyDeload(60.0 + (Math.Min(ordinal, 60) * 0.75), deload);

    internal static double SquatWeightKg(int ordinal, bool deload, double baseKg = 100.0) =>
        ApplyDeload(baseKg + (Math.Min(ordinal, 60) * 0.5), deload);

    internal static void ClampLastSetTime(List<WorkoutSetLogEntity> sets, DateTime ended)
    {
        if (sets.Count == 0)
            return;

        DateTime t = ended.AddMinutes(-1);
        if (sets[^1].CompletedAtUtc > t)
            sets[^1].CompletedAtUtc = t;
    }

    private static double ApplyDeload(double kg, bool isDeload) =>
        isDeload ? kg * 0.9 : kg;

    private static double ProgressionWeight(int ordinal, bool isDeload, double baseKg, double stepKg) =>
        ApplyDeload(baseKg + (Math.Min(ordinal, 60) * stepKg), isDeload);

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

    internal static List<WorkoutSetLogEntity> BuildCompletedPushSets(
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

        sb.AddWarmups(DemoPlans.BenchPressName, 0, 2, benchKg, 8, 3);
        for (var s = 0; s < 4; s++)
        {
            var e = CreateSet(DemoPlans.BenchPressName, 0, 2 + s, benchReps[s], benchKg, sb.Time);
            AssignIds(e, sessionIndex, 0, 2 + s);
            sets.Add(e);
            sb.Time = sb.Time.AddMinutes(3);
        }

        var ohpKg = ProgressionWeight(pushOrdinal, isDeload, 42.5, 0.375);
        sb.AddWarmups(DemoPlans.OverheadPressName, 1, 1, ohpKg, 8, 2);
        for (var s = 0; s < 4; s++)
        {
            var e = CreateSet(DemoPlans.OverheadPressName, 1, 1 + s, 9 - Math.Min(s, 2), ohpKg, sb.Time);
            AssignIds(e, sessionIndex, 1, 1 + s);
            sets.Add(e);
            sb.Time = sb.Time.AddMinutes(2);
        }

        var inclineBase = ProgressionWeight(pushOrdinal, isDeload, 22.5, 0.375);
        for (var s = 0; s < 3; s++)
        {
            var e = CreateSet(DemoPlans.InclineDumbbellPressName, 2, s, 10, inclineBase + (s * 2.5), sb.Time);
            AssignIds(e, sessionIndex, 2, s);
            sets.Add(e);
            sb.Time = sb.Time.AddMinutes(2);
        }

        var lateralKg = ProgressionWeight(pushOrdinal, isDeload, 8.0, 0.15);
        for (var s = 0; s < 3; s++)
        {
            var e = CreateSet(DemoPlans.LateralRaisesName, 3, s, 12 + (s == 0 ? 2 : 0), lateralKg, sb.Time);
            AssignIds(e, sessionIndex, 3, s);
            sets.Add(e);
            sb.Time = sb.Time.AddMinutes(2);
        }

        sb.AddCompleted(DemoPlans.TricepPushdownsName, 4, 3, 12, ProgressionWeight(pushOrdinal, isDeload, 20.0, 0.375), 2);
        sb.AddCompleted(DemoPlans.OverheadTricepExtensionName, 5, 3, 10, ProgressionWeight(pushOrdinal, isDeload, 16.0, 0.3), 2);

        sb.AddCompleted(DemoPlans.PushUpsName, 6, 3, 15, null, 1);

        ClampLastSetTime(sets, ended);
        return sets;
    }

    internal static List<WorkoutSetLogEntity> BuildCompletedPullSets(
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

        sb.AddWarmups(DemoPlans.DeadliftName, 0, 1, dlKg, 6, 4);
        for (var s = 0; s < 3; s++)
        {
            var e = CreateSet(DemoPlans.DeadliftName, 0, 1 + s, 6 - s, dlKg, sb.Time);
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

        sb.AddWarmups(DemoPlans.PullUpsName, 1, 1, pullUpWeight, pullUpReps, 2);
        for (var s = 0; s < 4; s++)
        {
            var e = CreateSet(DemoPlans.PullUpsName, 1, 1 + s, pullUpReps - Math.Min(s, 2), pullUpWeight, sb.Time);
            AssignIds(e, sessionIndex, 1, 1 + s);
            sets.Add(e);
            sb.Time = sb.Time.AddMinutes(2);
        }

        sb.AddCompleted(DemoPlans.BarbellRowsName, 2, 4, 10, ProgressionWeight(pullOrdinal, isDeload, 55.0, 0.5), 2);
        sb.AddCompleted(DemoPlans.FacePullsName, 3, 3, 15, ProgressionWeight(pullOrdinal, isDeload, 15.0, 0.15), 2);
        sb.AddCompleted(DemoPlans.BicepCurlsName, 4, 3, 12, ProgressionWeight(pullOrdinal, isDeload, 14.0, 0.3), 2);
        sb.AddCompleted(DemoPlans.HammerCurlsName, 5, 3, 12, ProgressionWeight(pullOrdinal, isDeload, 14.0, 0.3), 2);

        ClampLastSetTime(sets, ended);
        return sets;
    }

    internal static List<WorkoutSetLogEntity> BuildCompletedLegSets(
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

        sb.AddWarmups(DemoPlans.SquatsName, 0, 2, squatKg, 5, 4);
        for (var s = 0; s < 4; s++)
        {
            var e = CreateSet(DemoPlans.SquatsName, 0, 2 + s, squatReps[s], squatKg, sb.Time);
            AssignIds(e, sessionIndex, 0, 2 + s);
            sets.Add(e);
            sb.Time = sb.Time.AddMinutes(4);
        }

        var rdlKg = ProgressionWeight(legOrdinal, isDeload, 80.0, 0.5);
        sb.AddWarmups(DemoPlans.RomanianDeadliftName, 1, 1, rdlKg, 8, 3);
        sb.AddCompleted(DemoPlans.RomanianDeadliftName, 1, 4, 8, rdlKg, 3, setIndexOffset: 1);
        sb.AddCompleted(DemoPlans.LegPressName, 2, 3, 12, ProgressionWeight(legOrdinal, isDeload, 140.0, 1.0), 2);
        sb.AddCompleted(DemoPlans.LegCurlsName, 3, 3, 12, ProgressionWeight(legOrdinal, isDeload, 35.0, 0.25), 2);
        sb.AddCompleted(DemoPlans.CalfRaisesName, 4, 4, 15, ProgressionWeight(legOrdinal, isDeload, 50.0, 0.5), 2);
        sb.AddCompleted(DemoPlans.LegExtensionsName, 5, 3, 12, ProgressionWeight(legOrdinal, isDeload, 40.0, 0.25), 2);

        ClampLastSetTime(sets, ended);
        return sets;
    }

    internal static List<WorkoutSetLogEntity> BuildCompletedFullBodySets(
        int sessionIndex,
        DateTime started,
        DateTime ended,
        int fbOrdinal,
        bool isDeload)
    {
        var sets = new List<WorkoutSetLogEntity>();
        var sb = new SetBuilder(sets, started.AddMinutes(3), sessionIndex);

        var squatKg = ProgressionWeight(fbOrdinal, isDeload, 70.0, 0.5);
        sb.AddWarmups(DemoPlans.SquatsName, 0, 1, squatKg, 8, 3);
        sb.AddCompleted(DemoPlans.SquatsName, 0, 3, 8, squatKg, 3, setIndexOffset: 1);

        var benchKg = ProgressionWeight(fbOrdinal, isDeload, 60.0, 0.75);
        sb.AddWarmups(DemoPlans.BenchPressName, 1, 1, benchKg, 8, 3);
        sb.AddCompleted(DemoPlans.BenchPressName, 1, 3, 8, benchKg, 3, setIndexOffset: 1);

        sb.AddCompleted(DemoPlans.BarbellRowsName, 2, 3, 10, ProgressionWeight(fbOrdinal, isDeload, 50.0, 0.5), 2);
        sb.AddCompleted(DemoPlans.OverheadPressName, 3, 3, 8, ProgressionWeight(fbOrdinal, isDeload, 35.0, 0.375), 2);

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
            var e = CreateSet(DemoPlans.PullUpsName, 4, s, pullUpReps - Math.Min(s, 1), pullUpWeight, sb.Time);
            AssignIds(e, sessionIndex, 4, s);
            sets.Add(e);
            sb.Time = sb.Time.AddMinutes(2);
        }

        var plankSeconds = 45 + Math.Min(fbOrdinal, 60);
        sb.AddCompleted(DemoPlans.PlankName, 5, 3, plankSeconds, null, 2);

        ClampLastSetTime(sets, ended);
        return sets;
    }

    internal static List<WorkoutSetLogEntity> BuildInProgressPushSets(
        int sessionIndex,
        DateTime started,
        double benchKg)
    {
        DateTime t = started.AddMinutes(2);
        var e0 = CreateSet(DemoPlans.BenchPressName, 0, 0, 8, benchKg, t);
        AssignIds(e0, sessionIndex, 0, 0);
        var e1 = CreateSet(DemoPlans.BenchPressName, 0, 1, 8, benchKg, t.AddMinutes(3));
        AssignIds(e1, sessionIndex, 0, 1);
        return [e0, e1];
    }
}
