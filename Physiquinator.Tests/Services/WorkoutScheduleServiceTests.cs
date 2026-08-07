using Physiquinator.Core.Data;
using Physiquinator.Core.Models;
using Physiquinator.Core.Services;
using Physiquinator.Tests.TestDoubles;
using Xunit;

namespace Physiquinator.Tests.Services;

public class WorkoutScheduleServiceTests
{
    [Fact]
    public void Default_NoSchedule_DaysEmptyAndNextWorkoutDayNull()
    {
        Fixture fix = CreateFixture();

        Assert.Empty(fix.Schedule.Days);
        Assert.False(fix.Schedule.IsSet);
        Assert.Null(fix.Schedule.NextWorkoutDay(new DateOnly(2026, 5, 18)));
        Assert.False(fix.Schedule.IsScheduled(new DateOnly(2026, 5, 18)));
    }

    [Fact]
    public void SetDays_StoresAndReadsBack_DayOfWeekSet()
    {
        Fixture fix = CreateFixture();

        fix.Schedule.SetDays([DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday]);

        Assert.True(fix.Schedule.IsSet);
        Assert.Equal(
            new HashSet<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday },
            fix.Schedule.Days);
    }

    [Fact]
    public void SetDays_EmptySet_ClearsSchedule()
    {
        Fixture fix = CreateFixture();
        fix.Schedule.SetDays([DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday]);

        fix.Schedule.SetDays([]);

        Assert.False(fix.Schedule.IsSet);
        Assert.Empty(fix.Schedule.Days);
    }

    [Fact]
    public void IsScheduled_MatchesConfiguredWeekdays()
    {
        Fixture fix = CreateFixture();
        fix.Schedule.SetDays([DayOfWeek.Tuesday, DayOfWeek.Saturday]);

        Assert.True(fix.Schedule.IsScheduled(new DateOnly(2026, 5, 19)));  // Tuesday
        Assert.True(fix.Schedule.IsScheduled(new DateOnly(2026, 5, 23)));  // Saturday
        Assert.False(fix.Schedule.IsScheduled(new DateOnly(2026, 5, 18))); // Monday
    }

    [Fact]
    public void NextWorkoutDay_ReturnsNextOccurrence_IncludingSameDay()
    {
        Fixture fix = CreateFixture();
        fix.Schedule.SetDays([DayOfWeek.Monday, DayOfWeek.Thursday]);

        Assert.Equal(new DateOnly(2026, 5, 18), fix.Schedule.NextWorkoutDay(new DateOnly(2026, 5, 18))); // Mon
        Assert.Equal(new DateOnly(2026, 5, 21), fix.Schedule.NextWorkoutDay(new DateOnly(2026, 5, 19))); // Tue -> Thu
        Assert.Equal(new DateOnly(2026, 5, 25), fix.Schedule.NextWorkoutDay(new DateOnly(2026, 5, 22))); // Fri -> next Mon
    }

    [Fact]
    public void ChangedEvent_FiresOnSetDays()
    {
        Fixture fix = CreateFixture();
        var fired = 0;
        fix.Schedule.Changed += () => fired++;

        fix.Schedule.SetDays([DayOfWeek.Monday]);
        fix.Schedule.SetDays([]);

        Assert.Equal(2, fired);
    }

    [Fact]
    public async Task Schedule_IsIsolatedPerProfile()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"physiq-sched-test-{Guid.NewGuid():N}.db3");
        UserProfile? alice = null;
        try
        {
            var db = new AppDatabase(dbPath);
            var preferences = new InMemoryPreferences();
            var profiles = new UserProfileService(db, new WorkoutSessionService(TimeProvider.System), preferences, new TempDbPathProvider(dbPath), TimeProvider.System);

            // The default (demo) profile owns the base key.
            var schedule = new WorkoutScheduleService(preferences, profiles, db);
            schedule.SetDays([DayOfWeek.Monday]);

            // A new profile reads and writes its own key.
            profiles.CreateProfile("Alice");
            alice = profiles.GetProfiles().First(p => p.Name == "Alice");
            await profiles.SwitchProfileAsync(alice.Id);

            Assert.Empty(schedule.Days);
            schedule.SetDays([DayOfWeek.Wednesday]);
            Assert.Equal(DayOfWeek.Wednesday, Assert.Single(schedule.Days));

            // Back on the demo profile, the Monday schedule is intact.
            await profiles.SwitchProfileAsync(UserProfileService.DemoProfileId);
            Assert.Equal(DayOfWeek.Monday, Assert.Single(schedule.Days));

            await db.Database.CloseAsync();
        }
        finally
        {
            var aliceDbPath = alice != null ? Path.ChangeExtension(dbPath, $".{alice.Id:N}.db3") : null;
            for (var i = 0; i < 10; i++)
            {
                try
                {
                    if (File.Exists(dbPath))
                        File.Delete(dbPath);
                    if (aliceDbPath != null && File.Exists(aliceDbPath))
                        File.Delete(aliceDbPath);
                    break;
                }
                catch (IOException)
                {
                    await Task.Delay(50);
                }
            }
        }
    }

    [Fact]
    public void GetScheduleForDate_ReturnsHistoricalSchedulesCorrectly()
    {
        Fixture fix = CreateFixture();

        // Initially no schedule
        Assert.Empty(fix.Schedule.GetScheduleForDate(new DateOnly(2026, 5, 1)));

        // Set Mon-Wed-Fri effective on 2026-05-01
        fix.Schedule.SetDays([DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday], new DateOnly(2026, 5, 1));

        // Set Tue-Thu effective on 2026-05-10
        fix.Schedule.SetDays([DayOfWeek.Tuesday, DayOfWeek.Thursday], new DateOnly(2026, 5, 10));

        // Query historical dates:
        // Before 2026-05-01: empty
        Assert.Empty(fix.Schedule.GetScheduleForDate(new DateOnly(2026, 4, 30)));

        // Between 2026-05-01 and 2026-05-09: Mon-Wed-Fri
        var sched1 = fix.Schedule.GetScheduleForDate(new DateOnly(2026, 5, 5));
        Assert.Equal(3, sched1.Count);
        Assert.Contains(DayOfWeek.Monday, sched1);
        Assert.Contains(DayOfWeek.Wednesday, sched1);
        Assert.Contains(DayOfWeek.Friday, sched1);

        // On and after 2026-05-10: Tue-Thu
        var sched2 = fix.Schedule.GetScheduleForDate(new DateOnly(2026, 5, 10));
        Assert.Equal(2, sched2.Count);
        Assert.Contains(DayOfWeek.Tuesday, sched2);
        Assert.Contains(DayOfWeek.Thursday, sched2);

        var sched3 = fix.Schedule.GetScheduleForDate(new DateOnly(2026, 5, 20));
        Assert.Equal(2, sched3.Count);
        Assert.Contains(DayOfWeek.Tuesday, sched3);
        Assert.Contains(DayOfWeek.Thursday, sched3);
    }

    [Fact]
    public async Task Initialize_MigratesExistingPreferences()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"physiq-sched-test-{Guid.NewGuid():N}.db3");
        try
        {
            var db = new AppDatabase(dbPath);
            var preferences = new InMemoryPreferences();
            var profiles = new UserProfileService(db, new WorkoutSessionService(TimeProvider.System), preferences, new TempDbPathProvider(dbPath), TimeProvider.System);

            // Set preferences directly (Demo Profile)
            var schedule = new WorkoutScheduleService(preferences, profiles, db);
            preferences.Set("physiquinator-workout-schedule-days", "42"); // 1<<Monday(1) | 1<<Wednesday(3) | 1<<Friday(5) = 2 | 8 | 32 = 42

            // Warm the cache/run migration
            await schedule.EnsureLoadedAsync();

            // Verify it was migrated
            var days = schedule.GetScheduleForDate(new DateOnly(2026, 1, 1));
            Assert.Contains(DayOfWeek.Monday, days);
            Assert.Contains(DayOfWeek.Wednesday, days);
            Assert.Contains(DayOfWeek.Friday, days);

            await db.Database.CloseAsync();
        }
        finally
        {
            for (var i = 0; i < 10; i++)
            {
                try
                {
                    if (File.Exists(dbPath))
                        File.Delete(dbPath);
                    break;
                }
                catch (IOException)
                {
                    await Task.Delay(50);
                }
            }
        }
    }

    private static Fixture CreateFixture()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"physiq-sched-test-{Guid.NewGuid():N}.db3");
        var preferences = new InMemoryPreferences();
        var database = new AppDatabase(dbPath);
        var profiles = new UserProfileService(database, new WorkoutSessionService(TimeProvider.System), preferences, new TempDbPathProvider(dbPath), TimeProvider.System);
        return new Fixture(new WorkoutScheduleService(preferences, profiles, database));
    }

    private sealed record Fixture(WorkoutScheduleService Schedule);
}
