using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;
using Plugin.LocalNotification.Core.Models.AndroidOption;

namespace Physiquinator.Services;

/// <summary>
/// Schedules and shows native rest alerts (Android/iOS). WebView Web Audio is unreliable on mobile;
/// local notifications provide sound when the app is backgrounded and a fallback when foregrounded.
/// </summary>
public sealed class RestNotificationService
{
    public const int ScheduledRestNotificationId = 9001;
    public const int ImmediateRestCompleteNotificationId = 9002;

    public const string AndroidChannelId = "physiquinator_rest";

    public async Task EnsurePermissionAsync()
    {
        if (!OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS())
            return;

        try
        {
            if (await LocalNotificationCenter.Current.AreNotificationsEnabled() != true)
                await LocalNotificationCenter.Current.RequestNotificationPermission();
        }
        catch
        {
            // Permission flow can fail on desktop TFMs or simulators
        }
    }

    public void CancelAllRestNotifications()
    {
        try
        {
            LocalNotificationCenter.Current.Cancel(ScheduledRestNotificationId);
            LocalNotificationCenter.Current.Cancel(ImmediateRestCompleteNotificationId);
        }
        catch
        {
            // Ignore when platform plugin is unavailable
        }
    }

    public async Task ScheduleRestEndAsync(DateTime notifyUtc, string title, string description)
    {
        if (!OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS())
            return;

        CancelAllRestNotifications();

        if (notifyUtc <= DateTime.UtcNow.AddSeconds(1))
            return;

        var localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(notifyUtc, DateTimeKind.Utc), TimeZoneInfo.Local);

        try
        {
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
                    VibrationPattern = [0, 400, 200, 400]
                },
                Schedule = new NotificationRequestSchedule
                {
                    NotifyTime = localTime
                }
            };

            await LocalNotificationCenter.Current.Show(request);
        }
        catch
        {
            // Scheduling can fail on unsupported hosts
        }
    }

    public async Task ShowRestCompleteNowAsync(string description)
    {
        if (!OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS())
            return;

        try
        {
            LocalNotificationCenter.Current.Cancel(ImmediateRestCompleteNotificationId);

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
                    VibrationPattern = [0, 500]
                }
            };

            await LocalNotificationCenter.Current.Show(request);
        }
        catch
        {
            // Ignore
        }
    }
}
