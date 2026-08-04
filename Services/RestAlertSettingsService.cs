using Physiquinator.Models;

namespace Physiquinator.Services;

/// <summary>Persisted preference for rest-end alerts (OS notifications, sound, vibration).</summary>
public sealed class RestAlertSettingsService(
    IAppPreferences preferences,
    UserProfileService userProfileService,
    RestNotificationService restNotificationService)
{
    private string PreferenceKey
    {
        get
        {
            UserProfile activeProfile = userProfileService.GetActiveProfile();
            return activeProfile.Id == UserProfileService.DemoProfileId ? PreferenceKeys.RestAlertsEnabled : $"{PreferenceKeys.RestAlertsEnabled}_{activeProfile.Id}";
        }
    }

    public bool Enabled => preferences.Get(PreferenceKey, true);

    public event Action? Changed;

    public Task SetEnabledAsync(bool enabled)
    {
        preferences.Set(PreferenceKey, enabled);

        if (!enabled)
            restNotificationService.CancelAllRestNotifications();

        Changed?.Invoke();
        return Task.CompletedTask;
    }
}
