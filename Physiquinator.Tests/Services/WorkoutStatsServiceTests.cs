using Physiquinator.Core.Data;
using Physiquinator.Core.Services;
using Physiquinator.Tests.TestDoubles;
using Xunit;

namespace Physiquinator.Tests.Services;

public class WorkoutStatsServiceTests : IAsyncLifetime
{
    private AppDatabase _db = null!;
    private WorkoutStatsService _sut = null!;
    static WorkoutStatsServiceTests() => SQLitePCL.Batteries_V2.Init();

    public async Task InitializeAsync()
    {
        _db = new AppDatabase(":memory:");
        await _db.EnsureInitializedAsync();
        _sut = new WorkoutStatsService(new WorkoutHistoryRepository(_db, TimeProvider.System));
    }

    public async Task DisposeAsync() => await _db.Database.CloseAsync();

    [Fact]
    public async Task GetSummaryAsync_returns_zero_summary_without_sessions()
    {
        (WorkoutDaySummary? summary, IReadOnlyDictionary<DateOnly, int>? activityByDay) = await _sut.GetSummaryAsync(DateOnly.FromDateTime(DateTime.Today), weeks: 12);

        Assert.Equal(0, summary.CurrentStreakWorkoutDays);
        Assert.Equal(0, summary.LongestStreakWorkoutDays);
        Assert.Equal(0, summary.ThisWeekSessionCount);
        Assert.Equal(0, summary.LastWeekSessionCount);
        Assert.Empty(activityByDay);
    }

    [Fact]
    public async Task GetSummaryAsync_counts_sessions_today_in_current_week_and_streak()
    {
        await _db.Database.InsertAsync(new WorkoutSessionLogEntity
        {
            Id = Guid.NewGuid().ToString(),
            WorkoutPlanId = Guid.NewGuid().ToString(),
            PlanName = "Test",
            StartedAtUtc = DateTime.UtcNow,
            EndedAtUtc = DateTime.UtcNow
        });

        (WorkoutDaySummary? summary, IReadOnlyDictionary<DateOnly, int>? activityByDay) = await _sut.GetSummaryAsync(DateOnly.FromDateTime(DateTime.Today), weeks: 12);

        Assert.True(summary.ThisWeekSessionCount >= 1);
        Assert.True(summary.CurrentStreakWorkoutDays >= 1);
        Assert.True(summary.LongestStreakWorkoutDays >= 1);
        Assert.NotEmpty(activityByDay);
    }

    [Fact]
    public async Task GetSummaryAsync_with_schedule_counts_scheduled_days_only()
    {
        var preferences = new InMemoryPreferences();
        var schedule = new WorkoutScheduleService(preferences, CreateProfileService(preferences), _db);

        // Today is scheduled and completed.
        schedule.SetDays(AllDaysOfWeek);

        var stats = new WorkoutStatsService(new WorkoutHistoryRepository(_db, TimeProvider.System), schedule);
        await _db.Database.InsertAsync(new WorkoutSessionLogEntity
        {
            Id = Guid.NewGuid().ToString(),
            WorkoutPlanId = Guid.NewGuid().ToString(),
            PlanName = "Test",
            StartedAtUtc = DateTime.UtcNow,
            EndedAtUtc = DateTime.UtcNow
        });

        (WorkoutDaySummary? allDaysSummary, _) = await stats.GetSummaryAsync(DateOnly.FromDateTime(DateTime.Today), weeks: 12);
        Assert.True(allDaysSummary.CurrentStreakWorkoutDays >= 1);

        // Today is a rest day: the most recent scheduled day is in the past and was skipped.
        var otherDay = (DayOfWeek)(((int)DateTime.Today.DayOfWeek + 1) % 7);
        schedule.SetDays([otherDay]);

        (WorkoutDaySummary? restDaySummary, _) = await stats.GetSummaryAsync(DateOnly.FromDateTime(DateTime.Today), weeks: 12);
        Assert.Equal(0, restDaySummary.CurrentStreakWorkoutDays);
    }

    private static readonly DayOfWeek[] AllDaysOfWeek =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

    private UserProfileService CreateProfileService(InMemoryPreferences preferences) =>
        new(_db, new WorkoutSessionService(TimeProvider.System), preferences, new TempDbPathProvider(":memory:"), TimeProvider.System);
}
