using Physiquinator.Core.Data;
using Physiquinator.Core.Models;
using System.Globalization;
using SQLite;

namespace Physiquinator.Core.Services;

/// <summary>
/// Weekly training schedule used to keep streaks alive on rest days.
/// An empty schedule keeps the legacy calendar-day streak behavior.
/// Persisted in the SQLite database and cached in memory.
/// Fallback migration from IAppPreferences is performed on startup.
/// </summary>
public sealed class WorkoutScheduleService(
    IAppPreferences preferences,
    UserProfileService userProfileService,
    AppDatabase database)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private SQLiteAsyncConnection? _lastConnection;
    private List<WorkoutScheduleHistoryEntity> _cache = new();

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

    /// <summary>
    /// Synchronously warm up cache. In unit tests, this is run synchronously.
    /// In the app, it is called on startup.
    /// </summary>
    public async Task EnsureLoadedAsync()
    {
        var currentConn = database.Database;
        if (_lastConnection == currentConn)
            return;

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_lastConnection == currentConn)
                return;

            await database.EnsureInitializedAsync().ConfigureAwait(false);
            var list = await database.Database.Table<WorkoutScheduleHistoryEntity>()
                .OrderBy(x => x.EffectiveFrom)
                .ToListAsync()
                .ConfigureAwait(false);

            if (list.Count == 0)
            {
                // Fallback migration from preference
                var mask = GetPreferenceMask();
                if (mask > 0)
                {
                    var entity = new WorkoutScheduleHistoryEntity
                    {
                        DaysBitmask = mask,
                        EffectiveFrom = "0001-01-01"
                    };
                    await database.Database.InsertAsync(entity).ConfigureAwait(false);
                    list.Add(entity);
                }
            }

            _cache = list;
            _lastConnection = currentConn;
        }
        finally
        {
            _lock.Release();
        }
    }

    private int GetPreferenceMask()
    {
        var raw = preferences.Get(PreferenceKey, string.Empty);
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mask))
            return mask;
        return 0;
    }

    public void ResetCache()
    {
        _lock.Wait();
        try
        {
            _cache = new List<WorkoutScheduleHistoryEntity>();
            _lastConnection = null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Scheduled training days for Today; empty when no schedule is configured.</summary>
    public IReadOnlySet<DayOfWeek> Days => GetScheduleForDate(DateOnly.FromDateTime(DateTime.Today));

    public bool IsSet => Days.Count > 0;

    public void SetDays(IEnumerable<DayOfWeek> days)
    {
        EnsureLoadedAsync().GetAwaiter().GetResult();

        var mask = 0;
        foreach (DayOfWeek day in days)
            mask |= 1 << (int)day;

        _lock.Wait();
        try
        {
            if (_cache.Count <= 1)
            {
                var existing = _cache.FirstOrDefault(x => x.EffectiveFrom == "0001-01-01");
                if (existing != null)
                {
                    existing.DaysBitmask = mask;
                    database.Database.UpdateAsync(existing).GetAwaiter().GetResult();
                }
                else
                {
                    var entity = new WorkoutScheduleHistoryEntity
                    {
                        DaysBitmask = mask,
                        EffectiveFrom = "0001-01-01"
                    };
                    database.Database.InsertAsync(entity).GetAwaiter().GetResult();
                    _cache.Add(entity);
                }

                // Sync with preferences for legacy fallback compatibility
                preferences.Set(PreferenceKey, mask.ToString(CultureInfo.InvariantCulture));
                Changed?.Invoke();
                return;
            }
        }
        finally
        {
            _lock.Release();
        }

        SetDays(days, DateOnly.FromDateTime(DateTime.Today));
    }

    public void SetDays(IEnumerable<DayOfWeek> days, DateOnly effectiveFrom)
    {
        var mask = 0;
        foreach (DayOfWeek day in days)
            mask |= 1 << (int)day;

        EnsureLoadedAsync().GetAwaiter().GetResult();

        var dateStr = effectiveFrom.ToString("yyyy-MM-dd");

        _lock.Wait();
        try
        {
            var existing = _cache.FirstOrDefault(x => x.EffectiveFrom == dateStr);
            if (existing != null)
            {
                existing.DaysBitmask = mask;
                database.Database.UpdateAsync(existing).GetAwaiter().GetResult();
            }
            else
            {
                var entity = new WorkoutScheduleHistoryEntity
                {
                    DaysBitmask = mask,
                    EffectiveFrom = dateStr
                };
                database.Database.InsertAsync(entity).GetAwaiter().GetResult();
                _cache.Add(entity);
                _cache = _cache.OrderBy(x => x.EffectiveFrom).ToList();
            }

            // Sync with preferences for legacy fallback compatibility
            preferences.Set(PreferenceKey, mask.ToString(CultureInfo.InvariantCulture));
        }
        finally
        {
            _lock.Release();
        }

        Changed?.Invoke();
    }

    public bool IsScheduled(DateOnly date) => GetScheduleForDate(date).Contains(date.DayOfWeek);

    /// <summary>
    /// Retrieves the schedule that was active on the specified date.
    /// </summary>
    public IReadOnlySet<DayOfWeek> GetScheduleForDate(DateOnly date)
    {
        EnsureLoadedAsync().GetAwaiter().GetResult();

        var dateStr = date.ToString("yyyy-MM-dd");
        WorkoutScheduleHistoryEntity? activeEntity = null;

        _lock.Wait();
        try
        {
            foreach (var item in _cache)
            {
                if (string.Compare(item.EffectiveFrom, dateStr, StringComparison.Ordinal) <= 0)
                {
                    activeEntity = item;
                }
                else
                {
                    break;
                }
            }
        }
        finally
        {
            _lock.Release();
        }

        if (activeEntity == null)
            return new HashSet<DayOfWeek>();

        return Unmask(activeEntity.DaysBitmask);
    }

    private static IReadOnlySet<DayOfWeek> Unmask(int mask)
    {
        var days = new HashSet<DayOfWeek>();
        for (DayOfWeek day = DayOfWeek.Sunday; day <= DayOfWeek.Saturday; day++)
        {
            if ((mask & (1 << (int)day)) != 0)
                days.Add(day);
        }
        return days;
    }

    /// <summary>Next scheduled day on or after <paramref name="from"/>; null when no schedule is configured.</summary>
    public DateOnly? NextWorkoutDay(DateOnly from)
    {
        for (var i = 0; i < 7; i++)
        {
            DateOnly candidate = from.AddDays(i);
            IReadOnlySet<DayOfWeek> days = GetScheduleForDate(candidate);
            if (days.Count == 0)
                continue;

            if (days.Contains(candidate.DayOfWeek))
                return candidate;
        }

        return null;
    }
}
