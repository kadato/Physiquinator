using Microsoft.Extensions.DependencyInjection;

namespace Physiquinator.Services;

/// <summary>Persisted preference for rest-end alerts (OS notifications, sound, vibration).</summary>
public sealed class RestAlertSettingsService
{
    private readonly IServiceProvider _serviceProvider;

    public RestAlertSettingsService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    private string PreferenceKey
    {
        get
        {
            var userProfileService = _serviceProvider.GetRequiredService<UserProfileService>();
            var activeProfile = userProfileService.GetActiveProfile();
            return activeProfile.Id == Guid.Empty ? "rest_alerts_enabled" : $"rest_alerts_enabled_{activeProfile.Id}";
        }
    }

    public bool Enabled => Preferences.Default.Get(PreferenceKey, true);

    public event Action? Changed;

    public Task SetEnabledAsync(bool enabled)
    {
        Preferences.Default.Set(PreferenceKey, enabled);

        if (!enabled)
            _serviceProvider.GetRequiredService<RestNotificationService>().CancelAllRestNotifications();

        Changed?.Invoke();
        return Task.CompletedTask;
    }
}
