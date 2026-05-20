using System.Globalization;
using Physiquinator.Formatting;
using Xunit;

namespace Physiquinator.Tests.Formatting;

public class UiDateFormatsTests
{
    [Fact]
    public void DateOnlyCompact_SameYear_OmitsYearWithMonthDayOrder()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var result = UiDateFormats.DateOnlyCompact(today);
        Assert.Equal(today.ToString("MM-dd", CultureInfo.InvariantCulture), result);
    }

    [Fact]
    public void DateOnlyCompact_OtherYear_UsesYearMonthDayOrder()
    {
        var date = DateOnly.FromDateTime(DateTime.Today).AddYears(-1);
        var result = UiDateFormats.DateOnlyCompact(date);
        Assert.Equal(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), result);
    }

    [Fact]
    public void LocalDateChartAxis_AlwaysReturnsMonthAndDay()
    {
        var thisYearUtc = DateTime.Today.AddHours(12).ToUniversalTime();
        var fromChartThisYear = UiDateFormats.LocalDateChartAxis(thisYearUtc);
        Assert.Equal(thisYearUtc.ToLocalTime().ToString("MM-dd", CultureInfo.InvariantCulture), fromChartThisYear);

        var lastYearUtc = DateTime.Today.AddYears(-1).AddHours(12).ToUniversalTime();
        var fromChartLastYear = UiDateFormats.LocalDateChartAxis(lastYearUtc);
        Assert.Equal(lastYearUtc.ToLocalTime().ToString("MM-dd", CultureInfo.InvariantCulture), fromChartLastYear);
    }

    [Fact]
    public void LocalDateTimeCompact_Today_ShowsTimeOnly()
    {
        var local = DateTime.Today.AddHours(14).AddMinutes(30);
        var utc = local.ToUniversalTime();
        Assert.Equal("14:30", UiDateFormats.LocalDateTimeCompact(utc));
    }

    [Fact]
    public void LocalTimeOnly_AlwaysShowsClockTimeRegardlessOfDay()
    {
        var local = DateTime.Today.AddYears(-2).AddHours(8).AddMinutes(5);
        var utc = local.ToUniversalTime();
        Assert.Equal("08:05", UiDateFormats.LocalTimeOnly(utc));
    }

    [Fact]
    public void LocalDateTimeCompact_SameYearNotToday_ShowsMonthDayAndTime()
    {
        var local = DateTime.Today.AddDays(-3).AddHours(9).AddMinutes(15);
        var utc = local.ToUniversalTime();
        var expectedDate = DateOnly.FromDateTime(local).ToString("MM-dd", CultureInfo.InvariantCulture);
        Assert.Equal($"{expectedDate} 09:15", UiDateFormats.LocalDateTimeCompact(utc));
    }

    [Fact]
    public void LocalDateTimeCompact_OtherYear_ShowsYearMonthDayAndTime()
    {
        var local = DateTime.Today.AddYears(-2).AddHours(8);
        var utc = local.ToUniversalTime();
        var expectedDate = DateOnly.FromDateTime(local).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        Assert.Equal($"{expectedDate} 08:00", UiDateFormats.LocalDateTimeCompact(utc));
    }
}