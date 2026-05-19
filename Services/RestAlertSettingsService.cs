using Microsoft.Extensions.DependencyInjection;

namespace Physiquinator.Services;

/// <summary>Persisted preference for rest-end alerts (OS notifications, sound, vibration).</summary>
public sealed class RestAlertSettingsService
{
    public const string PreferenceKey = "rest_alerts_enabled";

    private readonly IServiceProvider _serviceProvider;

    public RestAlertSettingsService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
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
