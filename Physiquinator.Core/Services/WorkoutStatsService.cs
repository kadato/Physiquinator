using Physiquinator.Data;
using Physiquinator.Formatting;

namespace Physiquinator.Services;

/// <summary>Computes streak/week summaries and the per-day activity map for a heatmap grid.</summary>
public sealed class WorkoutStatsService(WorkoutHistoryRepository repository)
{
    private readonly WorkoutHistoryRepository _repository = repository;

    /// <summary>
    /// Loads session counts across the last <paramref name="weeks"/> weeks (Monday–Sunday grid,
    /// ending on <paramref name="endLocal"/>) and derives the streak/week summary.
    /// </summary>
    public async Task<(WorkoutDaySummary Summary, IReadOnlyDictionary<DateOnly, int> ActivityByDay)> GetSummaryAsync(
        DateOnly endLocal,
        int weeks)
    {
        (DateTime utcStart, DateTime utcEndExclusive) = HeatmapGrid.GetHeatmapQueryUtcBounds(endLocal, weeks);
        DateOnly gridStart = HeatmapGrid.GetMondayOfWeek(endLocal).AddDays(-7 * (weeks - 1));

        IReadOnlyDictionary<DateOnly, int> activityByDay = await _repository.GetSessionCountsByLocalDayAsync(utcStart, utcEndExclusive);
        WorkoutDaySummary summary = WorkoutDayStats.Compute(activityByDay, endLocal, gridStart);
        return (summary, activityByDay);
    }
}
