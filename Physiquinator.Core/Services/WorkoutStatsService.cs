using Physiquinator.Core.Data;
using Physiquinator.Core.Formatting;

namespace Physiquinator.Core.Services;

/// <summary>Computes streak/week summaries and the per-day activity map for a heatmap grid.</summary>
public sealed class WorkoutStatsService(
    WorkoutHistoryRepository repository,
    WorkoutScheduleService? scheduleService = null)
{
    private readonly WorkoutHistoryRepository _repository = repository;
    private readonly WorkoutScheduleService? _scheduleService = scheduleService;

    private static readonly IReadOnlySet<DayOfWeek> EmptySchedule = new HashSet<DayOfWeek>();

    private (DateOnly EndLocal, int Weeks)? _cacheKey;
    private (WorkoutDaySummary Summary, IReadOnlyDictionary<DateOnly, int> ActivityByDay)? _cache;

    /// <summary>
    /// Loads session counts across the last <paramref name="weeks"/> weeks (Monday–Sunday grid,
    /// ending on <paramref name="endLocal"/>) and derives the streak/week summary.
    /// When a workout schedule is configured, streaks count completed scheduled days only.
    /// Results are cached for the same parameters until <see cref="InvalidateCache"/> is called.
    /// </summary>
    public async Task<(WorkoutDaySummary Summary, IReadOnlyDictionary<DateOnly, int> ActivityByDay)> GetSummaryAsync(
        DateOnly endLocal,
        int weeks)
    {
        if (_cache is { } c && _cacheKey is { } k && k.EndLocal == endLocal && k.Weeks == weeks)
            return c;

        (DateTime utcStart, DateTime utcEndExclusive) = HeatmapGrid.GetHeatmapQueryUtcBounds(endLocal, weeks);
        DateOnly gridStart = HeatmapGrid.GetMondayOfWeek(endLocal).AddDays(-7 * (weeks - 1));

        IReadOnlyDictionary<DateOnly, int> activityByDay = await _repository.GetSessionCountsByLocalDayAsync(utcStart, utcEndExclusive);
        IReadOnlySet<DayOfWeek> getSchedule(DateOnly date) => _scheduleService?.GetScheduleForDate(date) ?? EmptySchedule;
        WorkoutDaySummary summary = WorkoutDayStats.Compute(activityByDay, endLocal, gridStart, getSchedule);

        _cacheKey = (endLocal, weeks);
        _cache = (summary, activityByDay);
        return (summary, activityByDay);
    }

    /// <summary>Clears the cached heatmap data so the next call reloads from the database.</summary>
    public void InvalidateCache()
    {
        _cacheKey = null;
        _cache = null;
    }
}
