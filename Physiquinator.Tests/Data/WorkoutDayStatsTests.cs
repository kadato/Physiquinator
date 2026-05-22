using Physiquinator.Data;
using Xunit;

namespace Physiquinator.Tests.Data;

public class WorkoutDayStatsTests
{
    [Fact]
    public void Compute_EmptyActivity_YieldsZeros()
    {
        var end = new DateOnly(2026, 5, 18);
        var start = new DateOnly(2026, 5, 1);
        var s = WorkoutDayStats.Compute(new Dictionary<DateOnly, int>(), end, start);
        Assert.Equal(0, s.CurrentStreakWorkoutDays);
        Assert.Equal(0, s.LongestStreakWorkoutDays);
        Assert.Equal(0, s.ThisWeekSessionCount);
        Assert.Equal(0, s.LastWeekSessionCount);
    }

    [Fact]
    public void Compute_CurrentStreak_EndsOnFirstMiss()
    {
        var end = new DateOnly(2026, 5, 18);
        var map = new Dictionary<DateOnly, int>
        {
            [new DateOnly(2026, 5, 18)] = 1,
            [new DateOnly(2026, 5, 17)] = 1,
            [new DateOnly(2026, 5, 16)] = 1,
            [new DateOnly(2026, 5, 14)] = 1
        };
        var s = WorkoutDayStats.Compute(map, end, new DateOnly(2026, 1, 1));
        Assert.Equal(3, s.CurrentStreakWorkoutDays);
    }

    [Fact]
    public void Compute_CurrentStreak_MaintainsStreakIfWorkedOutYesterday()
    {
        var end = new DateOnly(2026, 5, 19); // Today is 19th
        var map = new Dictionary<DateOnly, int>
        {
            [new DateOnly(2026, 5, 18)] = 1, // Worked out yesterday (18th)
            [new DateOnly(2026, 5, 17)] = 1, // Worked out 17th
            [new DateOnly(2026, 5, 16)] = 1  // Worked out 16th
        };
        var s = WorkoutDayStats.Compute(map, end, new DateOnly(2026, 1, 1));
        Assert.Equal(3, s.CurrentStreakWorkoutDays);
    }

    [Fact]
    public void Compute_CurrentStreak_LostIfNoWorkoutYesterdayOrToday()
    {
        var end = new DateOnly(2026, 5, 20); // Today is 20th
        var map = new Dictionary<DateOnly, int>
        {
            [new DateOnly(2026, 5, 18)] = 1, // Last workout was 18th (2 days ago)
            [new DateOnly(2026, 5, 17)] = 1
        };
        var s = WorkoutDayStats.Compute(map, end, new DateOnly(2026, 1, 1));
        Assert.Equal(0, s.CurrentStreakWorkoutDays);
    }

    [Fact]
    public void Compute_SameDayMultipleSessions_CountsInWeekTotals()
    {
        var mon = new DateOnly(2026, 5, 11);
        var wed = mon.AddDays(2);
        var map = new Dictionary<DateOnly, int> { [wed] = 3 };
        var s = WorkoutDayStats.Compute(map, wed, mon);
        Assert.Equal(3, s.ThisWeekSessionCount);
    }

    [Fact]
    public void Compute_LongestStreak_IgnoresGaps()
    {
        var start = new DateOnly(2026, 5, 1);
        var end = new DateOnly(2026, 5, 10);
        var map = new Dictionary<DateOnly, int>
        {
            [new DateOnly(2026, 5, 1)] = 1,
            [new DateOnly(2026, 5, 2)] = 1,
            [new DateOnly(2026, 5, 4)] = 1,
            [new DateOnly(2026, 5, 5)] = 1,
            [new DateOnly(2026, 5, 6)] = 1
        };
        var s = WorkoutDayStats.Compute(map, end, start);
        Assert.Equal(3, s.LongestStreakWorkoutDays);
    }

    [Fact]
    public void Compute_GridStartAfterEnd_SwapsRange()
    {
        var map = new Dictionary<DateOnly, int>
        {
            [new DateOnly(2026, 5, 10)] = 1,
            [new DateOnly(2026, 5, 11)] = 1
        };
        var s = WorkoutDayStats.Compute(map, new DateOnly(2026, 5, 5), new DateOnly(2026, 5, 15));
        Assert.Equal(2, s.LongestStreakWorkoutDays);
    }
}
