namespace Physiquinator.Services;

/// <summary>Canonical preference keys shared across services. Values must not change (persisted user data).</summary>
public static class PreferenceKeys
{
    public const string ActiveProfileId = "Physiquinator.ActiveProfileId";
    public const string UserProfiles = "Physiquinator.UserProfiles";
    public const string ShowFirstTimeSeedModal = "Physiquinator.ShowFirstTimeSeedModal";
    public const string DemoDataInitialSeedCompleted = "Physiquinator.DemoDataInitialSeedCompleted";
    public const string DemoHistorySeedCompleted = "Physiquinator.DemoHistorySeedCompleted";

    public const string RestAlertsEnabled = "rest_alerts_enabled";
    public const string ThemePreference = "physiquinator-theme-preference";
}
