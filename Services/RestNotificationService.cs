using Physiquinator.Core.Services;
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;
using Plugin.LocalNotification.Core.Models.AndroidOption;

namespace Physiquinator.Services;

/// <summary>
/// Schedules and shows the single rest-end alert (sound when the app is
/// backgrounded). No other notifications fire during a workout.
/// </summary>
public sealed class RestNotificationService(
    RestAlertSettingsService settings,
    TimeProvider time) : Physiquinator.Core.Services.INotificationService
{
    private readonly RestAlertSettingsService _settings = settings;
    private readonly TimeProvider _time = time;

    /// <summary>Rest-end alerts only make sense on mobile platforms; desktop hosts are stubbed.</summary>
    private bool IsSupportedPlatform => OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();

    public const int ScheduledRestNotificationId = 9001;
    public const int ImmediateRestCompleteNotificationId = 9002;
    public const int OngoingRestNotificationId = 9101;

    public const string AndroidChannelId = "physiquinator_rest";

    public async Task EnsurePermissionAsync()
    {
        if (!_settings.Enabled)
            return;

        if (!IsSupportedPlatform)
            return;

        try
        {
            if (await LocalNotificationCenter.Current.AreNotificationsEnabled() != true)
                await LocalNotificationCenter.Current.RequestNotificationPermission();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RestNotificationService permission flow failed: {ex}");
        }
    }

    // The floating rest-timer bubble is Android-only; Android registers
    // AndroidRestNotificationService, so this implementation only reports the
    // overlay as available on platforms that could host one.
    public bool SupportsNotifications => IsSupportedPlatform;

    public bool SupportsOverlay => false;

    public bool HasOverlayPermission() => IsSupportedPlatform;

    public Task RequestOverlayPermissionAsync()
    {
        if (!IsSupportedPlatform)
            return Task.CompletedTask;

        // Nothing to grant here: the Android implementation registers its own
        // overlay permission flow.
        return Task.CompletedTask;
    }

    public void CancelAllRestNotifications()
    {
        try
        {
            LocalNotificationCenter.Current.Cancel(ScheduledRestNotificationId);
            LocalNotificationCenter.Current.Cancel(ImmediateRestCompleteNotificationId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RestNotificationService cancel all failed: {ex}");
        }
    }

    public async Task ShowRestCompleteNowAsync(string description)
    {
        if (!_settings.Enabled)
            return;

        if (!IsSupportedPlatform)
            return;

        try
        {
            LocalNotificationCenter.Current.Cancel(ImmediateRestCompleteNotificationId);

            var vibration = _settings.SoundVibrationEnabled ? NotificationConstants.ImmediateRestCompleteVibrationPattern : null;

            var request = new NotificationRequest
            {
                NotificationId = ImmediateRestCompleteNotificationId,
                Title = "Rest complete",
                Description = description,
                CategoryType = NotificationCategoryType.Status,
                Android = new AndroidOptions
                {
                    ChannelId = AndroidChannelId,
                    Priority = AndroidPriority.High,
                    VibrationPattern = vibration!
                }
            };

            await LocalNotificationCenter.Current.Show(request);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RestNotificationService show complete failed: {ex}");
        }
    }

    public Task ScheduleRestEndAlarmAsync(DateTime restEndsAtUtc, string title, string description)
    {
        if (!_settings.Enabled)
            return Task.CompletedTask;

        if (!IsSupportedPlatform)
            return Task.CompletedTask;

        CancelAllRestNotifications();

        if (restEndsAtUtc <= _time.GetUtcNow().UtcDateTime.AddSeconds(1))
            return Task.CompletedTask;

        DateTime localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(restEndsAtUtc, DateTimeKind.Utc), TimeZoneInfo.Local);

        try
        {
            var vibration = _settings.SoundVibrationEnabled ? NotificationConstants.RestEndVibrationPattern : null;

            var request = new NotificationRequest
            {
                NotificationId = ScheduledRestNotificationId,
                Title = title,
                Description = description,
                CategoryType = NotificationCategoryType.Status,
                Android = new AndroidOptions
                {
                    ChannelId = AndroidChannelId,
                    Priority = AndroidPriority.High,
                    VibrationPattern = vibration!
                },
                Schedule = new NotificationRequestSchedule
                {
                    NotifyTime = localTime
                }
            };

            return LocalNotificationCenter.Current.Show(request);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RestNotificationService schedule alarm failed: {ex}");
            return Task.CompletedTask;
        }
    }

    public Task CancelRestEndAlarmAsync()
    {
        CancelAllRestNotifications();
        return Task.CompletedTask;
    }

    public Task ShowWorkoutTimerUiAsync(Physiquinator.Core.Models.WorkoutTimerState state)
    {
        // The ongoing workout status notification has been removed.
        // The floating overlay (Android only) is handled by AndroidRestNotificationService.
        // Cancel any leftover notification from previous installs.
        try
        {
            LocalNotificationCenter.Current.Cancel(OngoingRestNotificationId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RestNotificationService show timer UI failed: {ex}");
        }

        return Task.CompletedTask;
    }

    public Task HideWorkoutTimerUiAsync()
    {
        try
        {
            LocalNotificationCenter.Current.Cancel(OngoingRestNotificationId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RestNotificationService hide timer UI failed: {ex}");
        }

        return Task.CompletedTask;
    }
}
