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

    private string SoundVibrationPreferenceKey
    {
        get
        {
            UserProfile activeProfile = userProfileService.GetActiveProfile();
            return activeProfile.Id == UserProfileService.DemoProfileId ? PreferenceKeys.RestNotifSoundVibration : $"{PreferenceKeys.RestNotifSoundVibration}_{activeProfile.Id}";
        }
    }

    /// <summary>When false, no rest-end notification or alarm is posted.</summary>
    public bool Enabled => preferences.Get(PreferenceKey, true);

    /// <summary>When false, the rest-end notification plays no sound and does not vibrate.</summary>
    public bool SoundVibrationEnabled => preferences.Get(SoundVibrationPreferenceKey, true);

    public event Action? Changed;

    public void SetEnabled(bool enabled)
    {
        preferences.Set(PreferenceKey, enabled);

        if (!enabled)
            Notifications.CancelAllRestNotifications();

        Changed?.Invoke();
    }

    public void SetSoundVibrationEnabled(bool enabled)
    {
        preferences.Set(SoundVibrationPreferenceKey, enabled);
        Changed?.Invoke();
    }

    private const int DefaultAddTimeSeconds = 30;
    private const int MinAddTimeSeconds = 5;
    private const int MaxAddTimeSeconds = 300;

    private string AddTimePreferenceKey
    {
        get
        {
            UserProfile activeProfile = userProfileService.GetActiveProfile();
            return activeProfile.Id == UserProfileService.DemoProfileId ? PreferenceKeys.RestAddTimeSeconds : $"{PreferenceKeys.RestAddTimeSeconds}_{activeProfile.Id}";
        }
    }

    /// <summary>Seconds added by the + button on the rest timer (clamped to 5-300).</summary>
    public int AddTimeSeconds
    {
        get
        {
            var raw = preferences.Get(AddTimePreferenceKey, DefaultAddTimeSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                ? Math.Clamp(parsed, MinAddTimeSeconds, MaxAddTimeSeconds)
                : DefaultAddTimeSeconds;
        }
    }

    public void SetAddTimeSeconds(int seconds)
    {
        preferences.Set(
            AddTimePreferenceKey,
            Math.Clamp(seconds, MinAddTimeSeconds, MaxAddTimeSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Changed?.Invoke();
    }
}
