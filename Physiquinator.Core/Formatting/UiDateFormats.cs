using System.Globalization;

namespace Physiquinator.Core.Formatting;

/// <summary>
/// Short date strings for dense mobile layouts. Month and day as M/d,
/// with a two-digit year joining in (M/d/yy) when the date is not in the
/// current year, and clock time alone for today.
/// </summary>
public static class UiDateFormats
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string LocalDateTimeCompact(DateTime utc)
    {
        DateTime l = utc.ToLocalTime();
        var time = l.ToString("HH:mm", Invariant);
        if (l.Date == DateTime.Today)
            return time;
        return $"{FormatDay(l)} {time}";
    }

    /// <summary>Clock time only (local), for tables where session date is shown elsewhere.</summary>
    public static string LocalTimeOnly(DateTime utc) =>
        utc.ToLocalTime().ToString("HH:mm", Invariant);

    public static string LocalDateCompact(DateTime utc) =>
        FormatDay(DateOnly.FromDateTime(utc.ToLocalTime()));

    /// <summary>Month and day as M/d, a two-digit year joins in for other years.</summary>
    public static string DateOnlyCompact(DateOnly date) => FormatDay(date);

    /// <summary>Minimal date for chart X-axis (month and day as M/d).</summary>
    public static string LocalDateChartAxis(DateTime utc) =>
        utc.ToLocalTime().ToString("M/d", Invariant);

    private static string FormatDay(DateTime value) =>
        value.ToString(ValueFormat(value.Year), Invariant);

    private static string FormatDay(DateOnly value) =>
        value.ToString(ValueFormat(value.Year), Invariant);

    private static string ValueFormat(int year) =>
        year == DateTime.Today.Year ? "M/d" : "M/d/yy";
}
