namespace Physiquinator.Formatting;

/// <summary>Grid geometry for the activity heatmap (weeks run Monday–Sunday, oldest left).</summary>
public static class HeatmapGrid
{
    public static DateOnly GetMondayOfWeek(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff);
    }

    /// <summary>
    /// UTC bounds covering the whole heatmap grid: from the Monday of the oldest
    /// week through the day after <paramref name="endLocal"/> (exclusive).
    /// </summary>
    public static (DateTime UtcStart, DateTime UtcEndExclusive) GetHeatmapQueryUtcBounds(DateOnly endLocal, int weeks = 53)
    {
        weeks = Math.Clamp(weeks, 1, 104);
        var tz = TimeZoneInfo.Local;
        var mondayOfEndWeek = GetMondayOfWeek(endLocal);
        var gridStartMonday = mondayOfEndWeek.AddDays(-7 * (weeks - 1));

        var startLocalUnspecified = DateTime.SpecifyKind(
            gridStartMonday.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Unspecified);
        var endExclusiveUnspecified = DateTime.SpecifyKind(
            endLocal.AddDays(1).ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Unspecified);

        var utcStart = TimeZoneInfo.ConvertTimeToUtc(startLocalUnspecified, tz);
        var utcEndExclusive = TimeZoneInfo.ConvertTimeToUtc(endExclusiveUnspecified, tz);
        return (utcStart, utcEndExclusive);
    }
}
