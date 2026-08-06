using Physiquinator.Core.Models;
using System.Globalization;

namespace Physiquinator.Core.Services;

/// <summary>
/// Weekly training schedule used to keep streaks alive on rest days.
/// An empty schedule keeps the legacy calendar-day streak behavior.
/// Persisted per profile as a day-of-week bitmask (Sunday = bit 0 .. Saturday = bit 6).
/// </summary>
public sealed class WorkoutScheduleService(
    IAppPreferences preferences,
    UserProfileService userProfileService)
{
    private string PreferenceKey
    {
        get
        {
            UserProfile activeProfile = userProfileService.GetActiveProfile();
            return activeProfile.Id == UserProfileService.DemoProfileId
                ? PreferenceKeys.WorkoutScheduleDays
                : $"{PreferenceKeys.WorkoutScheduleDays}_{activeProfile.Id}";
        }
    }

    public event Action? Changed;

    /// <summary>Scheduled training days; empty when no schedule is configured.</summary>
    public IReadOnlySet<DayOfWeek> Days
    {
        get
        {
            var raw = preferences.Get(PreferenceKey, string.Empty);
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mask) || mask == 0)
                return new HashSet<DayOfWeek>();

            var days = new HashSet<DayOfWeek>();
            for (DayOfWeek day = DayOfWeek.Sunday; day <= DayOfWeek.Saturday; day++)
            {
                if ((mask & (1 << (int)day)) != 0)
                    days.Add(day);
            }

            return days;
        }
    }

    public bool IsSet => Days.Count > 0;

    public void SetDays(IEnumerable<DayOfWeek> days)
    {
        var mask = 0;
        foreach (DayOfWeek day in days)
            mask |= 1 << (int)day;

        preferences.Set(PreferenceKey, mask.ToString(CultureInfo.InvariantCulture));
        Changed?.Invoke();
    }

    public bool IsScheduled(DateOnly date) => Days.Contains(date.DayOfWeek);

    /// <summary>Next scheduled day on or after <paramref name="from"/>; null when no schedule is configured.</summary>
    public DateOnly? NextWorkoutDay(DateOnly from)
    {
        IReadOnlySet<DayOfWeek> days = Days;
        if (days.Count == 0)
            return null;

        for (var i = 0; i < 7; i++)
        {
            DateOnly candidate = from.AddDays(i);
            if (days.Contains(candidate.DayOfWeek))
                return candidate;
        }

        return null;
    }
}
