using Physiquinator.Core.Services;
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;
using Plugin.LocalNotification.Core.Models.AndroidOption;

namespace Physiquinator.Services;

/// <summary>
/// Schedules and shows the single rest-end alert (sound when the app is
/// backgrounded). No other notifications fire during a workout.
/// </summary>
public sealed class RestNotificationService(RestAlertSettingsService settings, TimeProvider time) : Physiquinator.Core.Services.INotificationService
{
    private readonly RestAlertSettingsService _settings = settings;
    private readonly TimeProvider _time = time;

    public const int ScheduledRestNotificationId = 9001;
    public const int ImmediateRestCompleteNotificationId = 9002;
    public const int OngoingRestNotificationId = 9101;
    public const int SetLoggedNotificationId = 9102;

    public const string AndroidChannelId = "physiquinator_rest";

    public async Task EnsurePermissionAsync()
    {
        if (!_settings.Enabled)
            return;

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

    // The floating rest-timer bubble is Android-only; Android registers
    // AndroidRestNotificationService, so this implementation always reports
    // the overlay as available (nothing to grant elsewhere).
    public bool HasOverlayPermission() => true;

    public Task RequestOverlayPermissionAsync() => Task.CompletedTask;

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

    public async Task ShowRestCompleteNowAsync(string description)
    {
        if (!_settings.Enabled)
            return;

        if (!OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS())
            return;

        try
        {
            LocalNotificationCenter.Current.Cancel(ImmediateRestCompleteNotificationId);

            long[]? vibration = _settings.SoundVibrationEnabled ? [0, 500] : null;

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
        catch
        {
            // Ignore
        }
    }

    public Task ScheduleRestEndAlarmAsync(DateTime restEndsAtUtc, string title, string description)
    {
        if (!_settings.Enabled)
            return Task.CompletedTask;

        if (!OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS())
            return Task.CompletedTask;

        CancelAllRestNotifications();

        if (restEndsAtUtc <= _time.GetUtcNow().UtcDateTime.AddSeconds(1))
            return Task.CompletedTask;

        DateTime localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(restEndsAtUtc, DateTimeKind.Utc), TimeZoneInfo.Local);

        try
        {
            long[]? vibration = _settings.SoundVibrationEnabled ? [0, 400, 200, 400] : null;

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
        catch
        {
            // Scheduling can fail on unsupported hosts
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
        try { LocalNotificationCenter.Current.Cancel(OngoingRestNotificationId); } catch { }
        return Task.CompletedTask;
    }

    public Task HideWorkoutTimerUiAsync()
    {
        try
        {
            LocalNotificationCenter.Current.Cancel(OngoingRestNotificationId);
        }
        catch
        {
            // Ignore
        }

        return Task.CompletedTask;
    }

    public async Task ShowSetLoggedNotificationAsync(string exerciseName, int setIndex, int totalSets)
    {
        if (!_settings.Enabled)
            return;

        if (!OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS())
            return;

        try
        {
            LocalNotificationCenter.Current.Cancel(SetLoggedNotificationId);

            var request = new NotificationRequest
            {
                NotificationId = SetLoggedNotificationId,
                Title = "Set logged",
                Description = $"{exerciseName} {setIndex}/{totalSets}",
                CategoryType = NotificationCategoryType.Status,
                Android = new AndroidOptions
                {
                    ChannelId = AndroidChannelId,
                    Priority = AndroidPriority.Low
                }
            };

            await LocalNotificationCenter.Current.Show(request);
        }
        catch
        {
            // Ignore
        }
    }

    public Task CancelSetLoggedNotificationAsync()
    {
        try
        {
            LocalNotificationCenter.Current.Cancel(SetLoggedNotificationId);
        }
        catch
        {
            // Ignore
        }

        return Task.CompletedTask;
    }
}
