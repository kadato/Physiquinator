namespace Physiquinator.Core.Data;

using Physiquinator.Core.Formatting;
using Physiquinator.Core.Services;

/// <summary>Streak and week-over-week session counts derived from local-day activity.</summary>
public sealed record WorkoutDaySummary(
    int CurrentStreakWorkoutDays,
    int LongestStreakWorkoutDays,
    int ThisWeekSessionCount,
    int LastWeekSessionCount);

public static class WorkoutDayStats
{
    private static readonly IReadOnlySet<DayOfWeek> EmptySchedule = new HashSet<DayOfWeek>();

    /// <summary>
    /// Legacy overload for backward compatibility and testing.
    /// </summary>
    public static WorkoutDaySummary Compute(
        IReadOnlyDictionary<DateOnly, int> activityByDay,
        DateOnly endLocal,
        DateOnly gridStartLocal,
        IReadOnlySet<DayOfWeek>? schedule = null)
    {
        return Compute(activityByDay, endLocal, gridStartLocal,
            date => schedule ?? EmptySchedule);
    }

    /// <summary>
    /// Weeks are Monday–Sunday, matching the activity heatmap grid.
    /// Session totals sum per-day counts from <paramref name="activityByDay"/> (multiple sessions on one day count separately).
    /// When <paramref name="getSchedule"/> resolves a non-empty schedule for a date, streaks count completed scheduled days only;
    /// non-scheduled (rest) days never break a streak. When it is null or returns an empty schedule, the legacy
    /// calendar-day streak with a one-day grace period is used.
    /// </summary>
    public static WorkoutDaySummary Compute(
        IReadOnlyDictionary<DateOnly, int> activityByDay,
        DateOnly endLocal,
        DateOnly gridStartLocal,
        Func<DateOnly, IReadOnlySet<DayOfWeek>> getSchedule)
    {
        if (gridStartLocal > endLocal)
            (gridStartLocal, endLocal) = (endLocal, gridStartLocal);

        var scheduleCache = new Dictionary<DateOnly, IReadOnlySet<DayOfWeek>>();
        Func<DateOnly, IReadOnlySet<DayOfWeek>> memoizedGetSchedule = date =>
        {
            if (scheduleCache.TryGetValue(date, out var resolved))
                return resolved;
            resolved = getSchedule(date);
            scheduleCache[date] = resolved;
            return resolved;
        };

        var currentStreak = ComputeCurrentStreak(activityByDay, endLocal, memoizedGetSchedule);
        var longest = ComputeLongestStreakInRange(activityByDay, gridStartLocal, endLocal, memoizedGetSchedule);
        (var thisWeek, var lastWeek) = ComputeWeekSessionTotals(activityByDay, endLocal);

        return new WorkoutDaySummary(currentStreak, longest, thisWeek, lastWeek);
    }

    private static int ComputeCurrentStreak(
        IReadOnlyDictionary<DateOnly, int> activityByDay,
        DateOnly endLocal,
        Func<DateOnly, IReadOnlySet<DayOfWeek>>? getSchedule)
    {
        if (getSchedule is null)
        {
            return ComputeCurrentCalendarStreak(activityByDay, endLocal);
        }

        var todaySchedule = getSchedule(endLocal);
        if (todaySchedule.Count == 0)
        {
            return ComputeCurrentCalendarStreak(activityByDay, endLocal);
        }

        // Most recent scheduled day at or before today.
        DateOnly lastScheduled = endLocal;
        while (true)
        {
            var sched = getSchedule(lastScheduled);
            if (sched.Count == 0 || sched.Contains(lastScheduled.DayOfWeek))
                break;
            lastScheduled = lastScheduled.AddDays(-1);
        }

        // A scheduled day in the past without a workout breaks the streak.
        if (activityByDay.GetValueOrDefault(lastScheduled, 0) == 0 && lastScheduled != endLocal)
            return 0;

        // Today is scheduled and still in progress: the streak is at risk but not lost yet.
        var streak = 0;
        DateOnly d = lastScheduled;
        if (activityByDay.GetValueOrDefault(d, 0) == 0)
            d = d.AddDays(-1);

        while (true)
        {
            var sched = getSchedule(d);
            if (sched.Count == 0)
            {
                // Fallback to checking calendar day
                if (activityByDay.GetValueOrDefault(d, 0) == 0)
                    break;
                streak++;
                d = d.AddDays(-1);
                continue;
            }

            if (!sched.Contains(d.DayOfWeek))
            {
                d = d.AddDays(-1);
                continue;
            }

            if (activityByDay.GetValueOrDefault(d, 0) == 0)
                break;

            streak++;
            d = d.AddDays(-1);
        }

        return streak;
    }

    private static int ComputeCurrentCalendarStreak(
        IReadOnlyDictionary<DateOnly, int> activityByDay,
        DateOnly endLocal)
    {
        DateOnly startDay;
        if (activityByDay.GetValueOrDefault(endLocal, 0) > 0)
        {
            startDay = endLocal;
        }
        else if (activityByDay.GetValueOrDefault(endLocal.AddDays(-1), 0) > 0)
        {
            startDay = endLocal.AddDays(-1);
        }
        else
        {
            return 0;
        }

        var streak = 0;
        DateOnly d = startDay;
        while (activityByDay.GetValueOrDefault(d, 0) > 0)
        {
            streak++;
            d = d.AddDays(-1);
        }

        return streak;
    }

    private static int ComputeLongestStreakInRange(
        IReadOnlyDictionary<DateOnly, int> activityByDay,
        DateOnly rangeStart,
        DateOnly rangeEnd,
        Func<DateOnly, IReadOnlySet<DayOfWeek>>? getSchedule)
    {
        var best = 0;
        var run = 0;
        for (DateOnly d = rangeStart; d <= rangeEnd; d = d.AddDays(1))
        {
            var sched = getSchedule?.Invoke(d);
            if (sched is not null && sched.Count > 0 && !sched.Contains(d.DayOfWeek))
                continue;

            if (activityByDay.GetValueOrDefault(d, 0) > 0)
            {
                run++;
                if (run > best) best = run;
            }
            else
            {
                run = 0;
            }
        }

        return best;
    }

    private static (int ThisWeek, int LastWeek) ComputeWeekSessionTotals(
        IReadOnlyDictionary<DateOnly, int> activityByDay,
        DateOnly endLocal)
    {
        DateOnly thisMonday = HeatmapGrid.GetMondayOfWeek(endLocal);
        DateOnly lastMonday = thisMonday.AddDays(-7);

        var thisWeek = 0;
        for (DateOnly d = thisMonday; d <= endLocal; d = d.AddDays(1))
            thisWeek += activityByDay.GetValueOrDefault(d, 0);

        var lastWeek = 0;
        DateOnly lastSunday = thisMonday.AddDays(-1);
        for (DateOnly d = lastMonday; d <= lastSunday; d = d.AddDays(1))
            lastWeek += activityByDay.GetValueOrDefault(d, 0);

        return (thisWeek, lastWeek);
    }
}
