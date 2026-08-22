using Physiquinator.Core.Formatting;
using System.Globalization;
using Xunit;

namespace Physiquinator.Tests.Formatting;

public class UiDateFormatsTests
{
    [Fact]
    public void DateOnlyCompact_AlwaysReturnsIsoYearMonthDay()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        Assert.Equal(today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            UiDateFormats.DateOnlyCompact(today));

        DateOnly lastYear = today.AddYears(-1);
        Assert.Equal(lastYear.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            UiDateFormats.DateOnlyCompact(lastYear));
    }

    [Fact]
    public void LocalDateChartAxis_AlwaysReturnsMonthAndDay()
    {
        DateTime thisYearUtc = DateTime.Today.AddHours(12).ToUniversalTime();
        var fromChartThisYear = UiDateFormats.LocalDateChartAxis(thisYearUtc);
        Assert.Equal(thisYearUtc.ToLocalTime().ToString("MM/dd", CultureInfo.InvariantCulture), fromChartThisYear);

        DateTime lastYearUtc = DateTime.Today.AddYears(-1).AddHours(12).ToUniversalTime();
        var fromChartLastYear = UiDateFormats.LocalDateChartAxis(lastYearUtc);
        Assert.Equal(lastYearUtc.ToLocalTime().ToString("MM/dd", CultureInfo.InvariantCulture), fromChartLastYear);
    }

    [Fact]
    public void LocalDateTimeCompact_Today_ShowsTimeOnly()
    {
        DateTime local = DateTime.Today.AddHours(14).AddMinutes(30);
        DateTime utc = local.ToUniversalTime();
        Assert.Equal("14:30", UiDateFormats.LocalDateTimeCompact(utc));
    }

    [Fact]
    public void LocalTimeOnly_AlwaysShowsClockTimeRegardlessOfDay()
    {
        DateTime local = DateTime.Today.AddYears(-2).AddHours(8).AddMinutes(5);
        DateTime utc = local.ToUniversalTime();
        Assert.Equal("08:05", UiDateFormats.LocalTimeOnly(utc));
    }

    [Fact]
    public void LocalDateTimeCompact_NotToday_ShowsIsoDateAndTime()
    {
        DateTime local = DateTime.Today.AddDays(-3).AddHours(9).AddMinutes(15);
        DateTime utc = local.ToUniversalTime();
        var expectedDate = DateOnly.FromDateTime(local).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        Assert.Equal($"{expectedDate} 09:15", UiDateFormats.LocalDateTimeCompact(utc));

        DateTime otherYear = DateTime.Today.AddYears(-2).AddHours(8);
        var expectedOther = DateOnly.FromDateTime(otherYear).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        Assert.Equal($"{expectedOther} 08:00", UiDateFormats.LocalDateTimeCompact(otherYear.ToUniversalTime()));
    }
}
