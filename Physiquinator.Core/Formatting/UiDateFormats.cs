using System.Globalization;

namespace Physiquinator.Core.Formatting;

/// <summary>Short date strings for dense mobile layouts (M/D when year is shown).</summary>
public static class UiDateFormats
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string LocalDateTimeCompact(DateTime utc)
    {
        DateTime l = utc.ToLocalTime();
        DateTime today = DateTime.Today;
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

    /// <summary>ISO 8601 compact date (yyyy-MM-dd): terminal-native, unambiguous, sorts naturally.</summary>
    public static string DateOnlyCompact(DateOnly date) =>
        date.ToString("yyyy-MM-dd", Invariant);

    /// <summary>Minimal date for chart X-axis (only month and day).</summary>
    public static string LocalDateChartAxis(DateTime utc) =>
        utc.ToLocalTime().ToString("MM/dd", Invariant);
}
