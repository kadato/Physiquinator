using System.Globalization;
using Physiquinator.Core.Data;
using Physiquinator.Core.Formatting;
using Physiquinator.Core.Models;

namespace Physiquinator.Core.Services.Demo;

internal readonly record struct DemoSessionSpec(
    int DaysAgo,
    int StartHourUtc,
    int StartMinuteUtc,
    Guid PlanId,
    bool Ended,
    int DurationMinutes,
    int PlanTypeOrdinal,
    bool IsDeload);

internal static class DemoScheduleGenerator
{
    internal const int DemoHistoryWeeks = 52;
    internal const int SkipSessionThresholdPercent = 40;

    internal const double DemoStartBodyweightKg = 90.5;
    internal const double DemoWeeklyBodyweightDeltaKg = -0.13;

    /// <summary>Workout start hour indexed by session hash remainder (0-3).</summary>
    private static readonly int[] s_startHoursByHashRemainder = [7, 9, 17, 18];

    /// <summary>
    /// Bodyweight logged on each scheduled workout day across the demo year, trending
    /// down with a slow wave and day-to-day jitter so the chart reads as a real cut.
    /// </summary>
    internal static List<BodyweightLogEntity> GenerateDemoBodyweights(DateTime todayUtc)
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

    internal static List<DemoSessionSpec> GenerateDemoSchedule(DateTime todayUtc)
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
}
