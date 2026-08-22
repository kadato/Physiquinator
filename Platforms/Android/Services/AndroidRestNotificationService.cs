using Android.App;
using Android.Content;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Provider;
using Physiquinator.Core.Models;
using Physiquinator.Core.Services;

namespace Physiquinator.Platforms.Android.Services;

/// <summary>
/// Android implementation of the workout timer surfaces. A foreground service
/// (with the ongoing notification as its FGS notification) runs while a
/// workout is active and hosts the floating overlay bubble. During rest the
/// notification carries quick actions (+add, Skip). Between sets it offers a
/// Log set action. A native exact alarm (AlarmManager,
/// survives Doze and process death) fires at rest end. Its alert plays the
/// deep knock sound.
/// </summary>
public sealed class AndroidRestNotificationService(
    RestAlertSettingsService settings,
    TimeProvider time) : INotificationService
{
    public const int OngoingRestNotificationId = 9100;
    public const int ImmediateRestCompleteNotificationId = 9002;
    public const string OngoingChannelId = "physiquinator_rest_ongoing";
    /// <summary>Silent low-importance channel for the mandatory FGS notification, invisible to the user.</summary>
    public const string SilentOngoingChannelId = "physiquinator_rest_ongoing_silent";
    public const string RestEndChannelId = "physiquinator_rest";
    public const string RestEndSilentChannelId = "physiquinator_rest_silent";

    private const string ExtraAction = "action";

    private const int RestEndAlarmRequestCode = 9201;
    private const int OpenAppRequestCode = 9301;

    private readonly RestAlertSettingsService _settings = settings;
    private readonly TimeProvider _time = time;
    private readonly Context _context = global::Android.App.Application.Context;

    public Task EnsurePermissionAsync()
    {
        if (!_settings.Enabled)
            return Task.CompletedTask;

        if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu)
            return Task.CompletedTask;

        if (_context.CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications) == global::Android.Content.PM.Permission.Granted)
            return Task.CompletedTask;

        Activity? activity = Platform.CurrentActivity;
        if (activity == null)
            return Task.CompletedTask;

        activity.RequestPermissions([global::Android.Manifest.Permission.PostNotifications], 1001);
        return Task.CompletedTask;
    }

    /// <summary>The floating rest-timer bubble is hosted on Android.</summary>
    public bool SupportsNotifications => true;

    public bool SupportsOverlay => true;

    /// <summary>
    /// The floating rest-timer bubble requires "Display over other apps"
    /// (SYSTEM_ALERT_WINDOW). It can never be requested with a runtime
    /// dialog. The user must toggle it from the system settings screen.
    /// </summary>
    public bool HasOverlayPermission() => Settings.CanDrawOverlays(_context);

    public Task RequestOverlayPermissionAsync()
    {
        if (HasOverlayPermission())
            return Task.CompletedTask;

        var intent = new Intent(Settings.ActionManageOverlayPermission,
            global::Android.Net.Uri.Parse("package:" + _context.PackageName));

        Activity? activity = Platform.CurrentActivity;
        if (activity != null)
        {
            activity.StartActivity(intent);
        }
        else
        {
            intent.AddFlags(ActivityFlags.NewTask);
            _context.StartActivity(intent);
        }

        return Task.CompletedTask;
    }

    public void CancelAllRestNotifications()
    {
        GetNotificationManager()?.Cancel(OngoingRestNotificationId);
        GetNotificationManager()?.Cancel(ImmediateRestCompleteNotificationId);
        CancelRestEndAlarm();
        StopOverlayService();
    }

    public Task ScheduleRestEndAlarmAsync(DateTime restEndsAtUtc, string title, string description)
    {
        if (!_settings.Enabled)
            return Task.CompletedTask;

        ScheduleRestEndAlarm(restEndsAtUtc);
        return Task.CompletedTask;
    }

    public Task CancelRestEndAlarmAsync()
    {
        CancelRestEndAlarm();
        return Task.CompletedTask;
    }

    public Task ShowWorkoutTimerUiAsync(WorkoutTimerState state)
    {
        if (!_settings.Enabled)
            return Task.CompletedTask;

        EnsureChannels();
        StartOverlayService(state);
        return Task.CompletedTask;
    }

    public Task HideWorkoutTimerUiAsync()
    {
        StopOverlayService();
        GetNotificationManager()?.Cancel(OngoingRestNotificationId);
        return Task.CompletedTask;
    }

    public Task ShowRestCompleteNowAsync(string description)
    {
        if (!_settings.Enabled)
            return Task.CompletedTask;

        // The in-app checkmark and knock sound already announce the end of
        // rest while the app is open. Posting here too would duplicate the
        // alert whenever the exact alarm wins the race against the JS tick.
        if (MainActivity.IsInForeground)
        {
            GetNotificationManager()?.Cancel(ImmediateRestCompleteNotificationId);
            return Task.CompletedTask;
        }

        EnsureChannels();

        var channelId = _settings.SoundVibrationEnabled ? RestEndChannelId : RestEndSilentChannelId;
        Notification.Builder builder = BuildBaseNotification(_context, channelId, "Rest complete", description, publicVisibility: true)
            .SetAutoCancel(true)
            .SetContentIntent(BuildOpenAppIntent(_context));

        NotificationManager? nm = GetNotificationManager();
        nm?.Cancel(ImmediateRestCompleteNotificationId);
        nm?.Notify(ImmediateRestCompleteNotificationId, builder.Build());
        return Task.CompletedTask;
    }

    private void ScheduleRestEndAlarm(DateTime restEndsAtUtc)
    {
        if (restEndsAtUtc <= _time.GetUtcNow().UtcDateTime)
            return;

        AlarmManager? alarmManager = GetAlarmManager();
        if (alarmManager == null)
            return;

        alarmManager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, ToEpochMillis(restEndsAtUtc), BuildRestEndAlarmIntent());
    }

    private void CancelRestEndAlarm()
    {
        GetAlarmManager()?.Cancel(BuildRestEndAlarmIntent());
    }

    private AlarmManager? GetAlarmManager() =>
        _context.GetSystemService(Context.AlarmService) as AlarmManager;

    private NotificationManager? GetNotificationManager() =>
        _context.GetSystemService(Context.NotificationService) as NotificationManager;

    private PendingIntent BuildRestEndAlarmIntent()
    {
        Intent intent = new Intent(_context, typeof(RestEndAlarmReceiver)).SetPackage(_context.PackageName!);
        return PendingIntent.GetBroadcast(_context, RestEndAlarmRequestCode, intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
    }

    private static PendingIntent BuildOpenAppIntent(Context context)
    {
        Intent? launch = context.PackageManager!.GetLaunchIntentForPackage(context.PackageName!);
        if (launch == null)
            return PendingIntent.GetActivity(context, OpenAppRequestCode, new Intent(context, typeof(MainActivity)),
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;

        launch.AddFlags(ActivityFlags.SingleTop);
        return PendingIntent.GetActivity(context, OpenAppRequestCode, launch,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
    }

    private static PendingIntent BuildActionIntent(Context context, string action, int requestCode)
    {
        Intent intent = new Intent(context, typeof(RestTimerActionReceiver))
            .SetAction(RestTimerActionReceiver.RestTimerAction)
            .SetPackage(context.PackageName!)
            .PutExtra(ExtraAction, action);

        return PendingIntent.GetBroadcast(context, requestCode, intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
    }

    private static Notification.Action BuildAddTimeAction(Context context, int addTimeSeconds) =>
        new Notification.Action.Builder(
            Icon.CreateWithResource(context, Resource.Drawable.ic_rest_timer),
            $"+{FormatAddLabel(addTimeSeconds)}",
            BuildActionIntent(context, RestTimerActionReceiver.ActionAddRest, 9403)).Build();

    private static Notification.Action BuildSkipRestAction(Context context) =>
        new Notification.Action.Builder(
            Icon.CreateWithResource(context, Resource.Drawable.ic_rest_timer),
            "Skip",
            BuildActionIntent(context, RestTimerActionReceiver.ActionSkipRest, 9404)).Build();

    private static Notification.Action BuildLogSetAction(Context context) =>
        new Notification.Action.Builder(
            Icon.CreateWithResource(context, Resource.Drawable.ic_rest_timer),
            "Log set",
            BuildActionIntent(context, RestTimerActionReceiver.ActionLogSet, 9405)).Build();

#pragma warning disable CA1422 // Pre-API-26 fallback is intentional (minSdk 24). Guarded by SDK checks
    internal static Notification BuildWorkoutNotification(Context context, WorkoutTimerState state, int addTimeSeconds)
    {
        var resting = state.RestEndsAtUtc != null;

        string text;
        if (resting)
        {
            // Keep a minimal, non-distracting description for the FGS notification.
            text = state.NextExerciseName is { } next ? $"Next: {next}" : "Resting";
        }
        else if (state.NextExerciseName is { } next)
        {
            text = $"Next: {next} {state.NextSetIndex}/{state.NextSetTotal}";
        }
        else
        {
            text = "Workout complete";
        }

        Notification.Builder builder = BuildBaseNotification(context, SilentOngoingChannelId, state.PlanName ?? NotificationConstants.DefaultFallbackPlanName, text, publicVisibility: false)
            .SetOngoing(true)
            .SetAutoCancel(false)
            .SetContentIntent(BuildOpenAppIntent(context));

        if (resting)
        {
            builder.AddAction(BuildAddTimeAction(context, addTimeSeconds));
            builder.AddAction(BuildSkipRestAction(context));
        }
        else if (state.NextExerciseName != null)
        {
            builder.AddAction(BuildLogSetAction(context));
        }

        return builder.Build();
    }

    private static Notification.Builder BuildBaseNotification(Context context, string channelId, string title, string text, bool publicVisibility)
    {
        Notification.Builder builder = Build.VERSION.SdkInt >= BuildVersionCodes.O
            ? new Notification.Builder(context, channelId)
            : new Notification.Builder(context);

        builder.SetContentTitle(title)
            .SetContentText(text)
            .SetSmallIcon(Resource.Drawable.ic_rest_timer)
            .SetWhen(0);

        if (publicVisibility)
            builder.SetVisibility(NotificationVisibility.Public);

        return builder;
    }
#pragma warning restore CA1422

    private void EnsureChannels()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            return;

        NotificationManager? nm = GetNotificationManager();
        if (nm == null)
            return;

        // Silent channel for the mandatory FGS notification: no sound, minimum importance.
        var silentOngoing = new NotificationChannel(SilentOngoingChannelId, "Workout timer (background)",
            NotificationImportance.Min)
        {
            Description = "Required system notification to keep the workout overlay running. No sound or alerts."
        };
        silentOngoing.SetSound(null, null);
        silentOngoing.EnableVibration(false);
        nm.CreateNotificationChannel(silentOngoing);

        // Keep the old ongoing channel for backward-compat in case it was already created.
        nm.CreateNotificationChannel(new NotificationChannel(OngoingChannelId, "Workout timer status",
            NotificationImportance.Default));

        // Alert channel with sound + vibration.
        var alertChannel = new NotificationChannel(RestEndChannelId, "Rest timer",
            NotificationImportance.High)
        {
            Description = "Alerts when rest periods end"
        };
        alertChannel.SetVibrationPattern(NotificationConstants.RestEndVibrationPattern);
        alertChannel.SetSound(global::Android.Net.Uri.Parse("android.resource://" + _context.PackageName + "/raw/rest_end_knock"), null);
        nm.CreateNotificationChannel(alertChannel);

        // Silent variant of the rest-end channel (when sound and vibration is disabled).
        var silentAlert = new NotificationChannel(RestEndSilentChannelId, "Rest timer (silent)",
            NotificationImportance.Default)
        {
            Description = "Silent rest-end notification when sound and vibration is turned off"
        };
        silentAlert.SetSound(null, null);
        silentAlert.EnableVibration(false);
        nm.CreateNotificationChannel(silentAlert);
    }

    private void StartOverlayService(WorkoutTimerState state)
    {
        Intent intent = new Intent(_context, typeof(RestOverlayService))
            .SetPackage(_context.PackageName)
            .PutExtra(RestOverlayService.ExtraEndUtcTicks, state.RestEndsAtUtc?.Ticks ?? 0)
            .PutExtra(RestOverlayService.ExtraRemainingSeconds, state.RestRemainingSeconds)
            .PutExtra(RestOverlayService.ExtraTitle, state.PlanName ?? NotificationConstants.DefaultFallbackPlanName)
            .PutExtra(RestOverlayService.ExtraNextExerciseName, state.NextExerciseName ?? string.Empty)
            .PutExtra(RestOverlayService.ExtraNextExerciseIndex, state.NextExerciseIndex ?? -1)
            .PutExtra(RestOverlayService.ExtraNextSetIndex, state.NextSetIndex ?? -1)
            .PutExtra(RestOverlayService.ExtraNextSetTotal, state.NextSetTotal ?? -1);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            _context.StartForegroundService(intent);
        else
            _context.StartService(intent);
    }

    private void StopOverlayService()
    {
        try
        {
            _context.StopService(new Intent(_context, typeof(RestOverlayService)).SetPackage(_context.PackageName));
        }
        catch (Exception)
        {
            // Ignore when the service is not running
        }
    }

    internal static string FormatRemaining(int totalSeconds)
    {
        var m = totalSeconds / 60;
        var s = totalSeconds % 60;
        return $"{m}:{s:D2}";
    }

    private static string FormatAddLabel(int addSeconds)
    {
        if (addSeconds < 60)
            return $"{addSeconds}s";

        var m = addSeconds / 60;
        var s = addSeconds % 60;
        return $"{m}:{s:D2}";
    }

    private static long ToEpochMillis(DateTime utcDateTime) =>
        new DateTimeOffset(DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
}
