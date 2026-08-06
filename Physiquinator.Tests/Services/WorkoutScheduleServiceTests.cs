using Physiquinator.Core.Data;
using Physiquinator.Core.Services;
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
        try
        {
            var db = new AppDatabase(dbPath);
            var preferences = new InMemoryPreferences();
            var profiles = new UserProfileService(db, new WorkoutSessionService(TimeProvider.System), preferences, new TempDbPathProvider(dbPath), TimeProvider.System);

            // The default (demo) profile owns the base key.
            var schedule = new WorkoutScheduleService(preferences, profiles);
            schedule.SetDays([DayOfWeek.Monday]);

            // A new profile reads and writes its own key.
            profiles.CreateProfile("Alice");
            var alice = profiles.GetProfiles().First(p => p.Name == "Alice");
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
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
    }

    private static Fixture CreateFixture()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"physiq-sched-test-{Guid.NewGuid():N}.db3");
        var preferences = new InMemoryPreferences();
        var profiles = new UserProfileService(new AppDatabase(dbPath), new WorkoutSessionService(TimeProvider.System), preferences, new TempDbPathProvider(dbPath), TimeProvider.System);
        return new Fixture(new WorkoutScheduleService(preferences, profiles));
    }

    private sealed record Fixture(WorkoutScheduleService Schedule);

    private sealed class InMemoryPreferences : IAppPreferences
    {
        private readonly Dictionary<string, string> _values = [];

        public string Get(string key, string defaultValue) =>
            _values.TryGetValue(key, out var value) ? value : defaultValue;

        public bool Get(string key, bool defaultValue)
        {
            if (!_values.TryGetValue(key, out var value))
                return defaultValue;

            return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
        }

        public void Set(string key, string value) => _values[key] = value;

        public void Set(string key, bool value) => _values[key] = value.ToString();
    }

    private sealed class TempDbPathProvider(string path) : IDatabasePathProvider
    {
        public string GetDatabasePath(Guid profileId) => path;
    }
}
