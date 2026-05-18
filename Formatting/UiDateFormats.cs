using System.Globalization;

namespace Physiquinator.Formatting;

/// <summary>Short, culture-aware date strings for dense mobile layouts.</summary>
public static class UiDateFormats
{
    public static string LocalDateTimeCompact(DateTime utc)
    {
        var l = utc.ToLocalTime();
        var ci = CultureInfo.CurrentCulture;
        var today = DateTime.Today;
        var time = l.ToString("HH:mm", ci);
        if (l.Date == today)
            return time;
        if (l.Year == today.Year)
            return $"{l.ToString("MMM d", ci)} {time}";
        return $"{l.ToString("yy-MM-dd", ci)} {time}";
    }

    public static string LocalDateCompact(DateTime utc)
    {
        var d = DateOnly.FromDateTime(utc.ToLocalTime());
        return DateOnlyCompact(d);
    }

    public static string DateOnlyCompact(DateOnly date)
    {
        var ci = CultureInfo.CurrentCulture;
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (date.Year == today.Year)
            return date.ToString("MMM d", ci);
        return date.ToString("yyyy-MM-dd", ci);
    }
}
