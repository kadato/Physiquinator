using Microsoft.Extensions.DependencyInjection;
using Physiquinator.Core.Models;

namespace Physiquinator.Core.Services;

/// <summary>Persisted preference for rest-end alerts (OS notifications, sound, vibration).</summary>
public sealed class RestAlertSettingsService(
    IAppPreferences preferences,
    UserProfileService userProfileService,
    IServiceProvider serviceProvider)
{
    /// <summary>
    /// Resolved lazily: <see cref="INotificationService"/> implementations depend on this
    /// service, so constructor injection would form a circular dependency.
    /// </summary>
    private INotificationService Notifications =>
        serviceProvider.GetRequiredService<INotificationService>();

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

    public void SetEnabled(bool enabled)
    {
        preferences.Set(PreferenceKey, enabled);

        if (!enabled)
            Notifications.CancelAllRestNotifications();

        Changed?.Invoke();
    }
}
