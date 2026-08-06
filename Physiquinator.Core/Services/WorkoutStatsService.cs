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

    /// <summary>
    /// Loads session counts across the last <paramref name="weeks"/> weeks (Monday–Sunday grid,
    /// ending on <paramref name="endLocal"/>) and derives the streak/week summary.
    /// When a workout schedule is configured, streaks count completed scheduled days only.
    /// </summary>
    public async Task<(WorkoutDaySummary Summary, IReadOnlyDictionary<DateOnly, int> ActivityByDay)> GetSummaryAsync(
        DateOnly endLocal,
        int weeks)
    {
        (DateTime utcStart, DateTime utcEndExclusive) = HeatmapGrid.GetHeatmapQueryUtcBounds(endLocal, weeks);
        DateOnly gridStart = HeatmapGrid.GetMondayOfWeek(endLocal).AddDays(-7 * (weeks - 1));

        IReadOnlySet<DayOfWeek>? schedule = _scheduleService is { IsSet: true } svc ? svc.Days : null;
        IReadOnlyDictionary<DateOnly, int> activityByDay = await _repository.GetSessionCountsByLocalDayAsync(utcStart, utcEndExclusive);
        WorkoutDaySummary summary = WorkoutDayStats.Compute(activityByDay, endLocal, gridStart, schedule);
        return (summary, activityByDay);
    }
}
