using Physiquinator.Formatting;
using Xunit;

namespace Physiquinator.Tests.Formatting;

public class HeatmapGridTests
{
    [Theory]
    [InlineData(2026, 8, 3, 2026, 8, 3)]  // Monday
    [InlineData(2026, 8, 4, 2026, 8, 3)]  // Tuesday
    [InlineData(2026, 8, 9, 2026, 8, 3)]  // Sunday
    [InlineData(2026, 7, 31, 2026, 7, 27)] // Friday -> previous Monday
    public void GetMondayOfWeek_returns_monday_of_week(int y, int m, int d, int ey, int em, int ed)
    {
        var date = new DateOnly(y, m, d);
        var expected = new DateOnly(ey, em, ed);

        Assert.Equal(expected, HeatmapGrid.GetMondayOfWeek(date));
    }

    [Fact]
    public void GetHeatmapQueryUtcBounds_returns_bounds_in_utc()
    {
        var endLocal = new DateOnly(2026, 8, 9); // Sunday

        var (utcStart, utcEndExclusive) = HeatmapGrid.GetHeatmapQueryUtcBounds(endLocal, weeks: 1);

        // Grid starts on the Monday of that week; end is exclusive the day after endLocal.
        var tz = TimeZoneInfo.Local;
        var expectedStart = TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Unspecified), tz);
        var expectedEnd = TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Unspecified), tz);

        Assert.Equal(expectedStart, utcStart);
        Assert.Equal(expectedEnd, utcEndExclusive);
    }

    [Fact]
    public void GetHeatmapQueryUtcBounds_weeks_extend_grid_to_the_left()
    {
        var endLocal = new DateOnly(2026, 8, 3); // Monday

        var (utcStart, _) = HeatmapGrid.GetHeatmapQueryUtcBounds(endLocal, weeks: 53);

        var tz = TimeZoneInfo.Local;
        var expectedStart = TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(2025, 8, 4, 0, 0, 0, DateTimeKind.Unspecified), tz); // 52 weeks earlier Monday

        Assert.Equal(expectedStart, utcStart);
    }
}
