namespace Physiquinator.Core.Services;

/// <summary>Canonical preference keys shared across services. Values must not change (persisted user data).</summary>
public static class PreferenceKeys
{
    public const string ActiveProfileId = "Physiquinator.ActiveProfileId";
    public const string UserProfiles = "Physiquinator.UserProfiles";
    public const string ShowFirstTimeSeedModal = "Physiquinator.ShowFirstTimeSeedModal";
    public const string DemoDataInitialSeedCompleted = "Physiquinator.DemoDataInitialSeedCompleted";
    public const string DemoHistorySeedCompleted = "Physiquinator.DemoHistorySeedCompleted";

    public const string RestAlertsEnabled = "rest_alerts_enabled";
    public const string RestAddTimeSeconds = "rest_add_time_seconds";
    public const string RestTimerSnapshot = "rest_timer_snapshot_v1";
    public const string ThemePreference = "physiquinator-theme-preference";
    public const string WorkoutScheduleDays = "physiquinator-workout-schedule-days";
}
