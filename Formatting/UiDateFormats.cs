using System.Globalization;

namespace Physiquinator.Formatting;

/// <summary>Short date strings for dense mobile layouts (Y-M-D when year is shown).</summary>
public static class UiDateFormats
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string LocalDateTimeCompact(DateTime utc)
    {
        var l = utc.ToLocalTime();
        var today = DateTime.Today;
        var time = l.ToString("HH:mm", Invariant);
        if (l.Date == today)
            return time;
        var datePart = DateOnlyCompact(DateOnly.FromDateTime(l.Date));
        return $"{datePart} {time}";
    }

    /// <summary>Clock time only (local), for tables where session date is shown elsewhere.</summary>
    public static string LocalTimeOnly(DateTime utc) =>
        utc.ToLocalTime().ToString("HH:mm", Invariant);

    public static string LocalDateCompact(DateTime utc)
    {
        var d = DateOnly.FromDateTime(utc.ToLocalTime());
        return DateOnlyCompact(d);
    }

    public static string DateOnlyCompact(DateOnly date)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (date.Year == today.Year)
            return date.ToString("MM-dd", Invariant);
        return date.ToString("yyyy-MM-dd", Invariant);
    }

    /// <summary>Minimal date for chart X-axis (same rules as <see cref="DateOnlyCompact"/>).</summary>
    public static string LocalDateChartAxis(DateTime utc) =>
        DateOnlyCompact(DateOnly.FromDateTime(utc.ToLocalTime()));
}
