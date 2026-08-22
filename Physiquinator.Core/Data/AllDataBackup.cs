namespace Physiquinator.Core.Data;

/// <summary>
/// A complete snapshot of all user data: workout plans, full history (sessions, sets, bodyweight),
/// training-schedule history, and all persisted app preferences.
/// The preferences bag uses a <see cref="Dictionary{TKey,TValue}"/> so that any new preference
/// key added to <see cref="Services.PreferenceKeys"/> in the future is automatically included
/// in exports without changing this schema or the serialization code.
/// </summary>
public sealed class AllDataBackup
{
    /// <summary>Bumped only when a breaking structural change makes old files unreadable.</summary>
    public int FormatVersion { get; set; } = 1;

    /// <summary>All workout plans belonging to the active profile.</summary>
    public List<Models.WorkoutPlan> Plans { get; set; } = [];

    /// <summary>Session logs, set logs, and bodyweight entries for the active profile.</summary>
    public WorkoutHistoryBackup History { get; set; } = new();

    /// <summary>
    /// Training-schedule history entries (bitmask + effective-from pairs) for the active profile.
    /// Preserves multi-period schedule history, not just the current setting.
    /// </summary>
    public List<WorkoutScheduleHistoryEntity> Schedules { get; set; } = [];

    /// <summary>
    /// Key/value map of every app preference that belongs to the active profile.
    /// Keys match the canonical values in <see cref="Services.PreferenceKeys"/> (without any
    /// profile-ID suffix, which is stripped on export and re-applied on import).
    /// Unknown keys encountered during import are silently ignored, preserving forward compatibility.
    /// </summary>
    public Dictionary<string, string> Preferences { get; set; } = [];
}
