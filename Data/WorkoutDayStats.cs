namespace Physiquinator.Data;

/// <summary>Streak and week-over-week session counts derived from local-day activity.</summary>
public sealed record WorkoutDaySummary(
    int CurrentStreakWorkoutDays,
    int LongestStreakWorkoutDays,
    int ThisWeekSessionCount,
    int LastWeekSessionCount);

public static class WorkoutDayStats
{
    /// <summary>
    /// Weeks are Monday–Sunday, matching the activity heatmap grid.
    /// Session totals sum per-day counts from <paramref name="activityByDay"/> (multiple sessions on one day count separately).
    /// </summary>
    public static WorkoutDaySummary Compute(
        IReadOnlyDictionary<DateOnly, int> activityByDay,
        DateOnly endLocal,
        DateOnly gridStartLocal)
    {
        if (gridStartLocal > endLocal)
            (gridStartLocal, endLocal) = (endLocal, gridStartLocal);

        var currentStreak = ComputeCurrentStreak(activityByDay, endLocal);
        var longest = ComputeLongestStreakInRange(activityByDay, gridStartLocal, endLocal);
        var (thisWeek, lastWeek) = ComputeWeekSessionTotals(activityByDay, endLocal);

        return new WorkoutDaySummary(currentStreak, longest, thisWeek, lastWeek);
    }

    private static int ComputeCurrentStreak(IReadOnlyDictionary<DateOnly, int> activityByDay, DateOnly endLocal)
    {
        var streak = 0;
        for (var d = endLocal; ; d = d.AddDays(-1))
        {
            if (activityByDay.GetValueOrDefault(d, 0) <= 0)
                break;
            streak++;
        }

        return streak;
    }

    private static int ComputeLongestStreakInRange(
        IReadOnlyDictionary<DateOnly, int> activityByDay,
        DateOnly rangeStart,
        DateOnly rangeEnd)
    {
        var best = 0;
        var run = 0;
        for (var d = rangeStart; d <= rangeEnd; d = d.AddDays(1))
        {
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
        var thisMonday = GetMondayOfWeek(endLocal);
        var lastMonday = thisMonday.AddDays(-7);

        var thisWeek = 0;
        for (var d = thisMonday; d <= endLocal; d = d.AddDays(1))
            thisWeek += activityByDay.GetValueOrDefault(d, 0);

        var lastWeek = 0;
        var lastSunday = thisMonday.AddDays(-1);
        for (var d = lastMonday; d <= lastSunday; d = d.AddDays(1))
            lastWeek += activityByDay.GetValueOrDefault(d, 0);

        return (thisWeek, lastWeek);
    }

    private static DateOnly GetMondayOfWeek(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff);
    }
}

