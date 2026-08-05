using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
using Microsoft.Maui.ApplicationModel;
using Physiquinator.Core.Models;
using Physiquinator.Core.Services;
using Physiquinator;
using Physiquinator.Platforms.Android.Services;

namespace Physiquinator.Platforms.Android.Services;

/// <summary>
/// Android implementation of the workout timer surfaces. A foreground service
/// (with the ongoing notification as its FGS notification) runs while a
/// workout is active and hosts the floating overlay bubble. During rest the
/// notification carries quick actions (+add, Skip); between sets it offers a
/// Log set action. A native exact alarm (AlarmManager,
/// survives Doze and process death) fires at rest end; its alert plays the
/// deep knock sound.
/// </summary>
public sealed class AndroidRestNotificationService(
    RestAlertSettingsService settings,
    TimeProvider time) : INotificationService
{
    public const int OngoingRestNotificationId = 9100;
    public const int ImmediateRestCompleteNotificationId = 9002;
    public const int SetLoggedNotificationId = 9003;
    public const string OngoingChannelId = "physiquinator_rest_ongoing";
    public const string RestEndChannelId = "physiquinator_rest";

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

    public Task ShowSetLoggedNotificationAsync(string exerciseName, int setIndex, int totalSets)
    {
        EnsureChannels();

        var builder = BuildBaseNotification(_context, OngoingChannelId, "Set logged", $"{exerciseName} {setIndex}/{totalSets}", publicVisibility: true)
            .SetAutoCancel(true)
            .SetContentIntent(BuildOpenAppIntent(_context))
            .AddAction(0, "Undo", BuildActionIntent(_context, RestTimerActionReceiver.ActionUndoSet, 9406));

        var nm = GetNotificationManager();
        nm?.Cancel(SetLoggedNotificationId);
        nm?.Notify(SetLoggedNotificationId, builder!.Build()!);

        // Auto-dismiss after a while so it does not clutter the shade; long
        // enough to act on, unlike the in-app toast which is in your face.
        var handler = new Handler(Looper.MainLooper!);
        handler.PostDelayed(() => GetNotificationManager()?.Cancel(SetLoggedNotificationId), 15000);
        return Task.CompletedTask;
    }

    public Task CancelSetLoggedNotificationAsync()
    {
        GetNotificationManager()?.Cancel(SetLoggedNotificationId);
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

        EnsureChannels();

        var builder = BuildBaseNotification(_context, RestEndChannelId, "Rest complete", description, publicVisibility: true)
            .SetAutoCancel(true)
            .SetContentIntent(BuildOpenAppIntent(_context));

        var nm = GetNotificationManager();
        nm?.Cancel(ImmediateRestCompleteNotificationId);
        nm?.Notify(ImmediateRestCompleteNotificationId, builder.Build());
        return Task.CompletedTask;
    }

    private void ScheduleRestEndAlarm(DateTime restEndsAtUtc)
    {
        if (restEndsAtUtc <= _time.GetUtcNow().UtcDateTime)
            return;

        var alarmManager = GetAlarmManager();
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
        var intent = new Intent(_context, typeof(RestEndAlarmReceiver)).SetPackage(_context.PackageName!);
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
        var intent = new Intent(context, typeof(RestTimerActionReceiver))
            .SetAction(RestTimerActionReceiver.RestTimerAction)
            .SetPackage(context.PackageName!)
            .PutExtra(ExtraAction, action);

        return PendingIntent.GetBroadcast(context, requestCode, intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
    }

#pragma warning disable CA1422 // Pre-API-26 fallback is intentional (minSdk 24); guarded by SDK checks
    internal static Notification BuildWorkoutNotification(Context context, WorkoutTimerState state, int addTimeSeconds)
    {
        bool resting = state.RestEndsAtUtc != null;

        string text;
        if (resting)
        {
            text = $"Rest ends at {state.RestEndsAtUtc!.Value.ToLocalTime():HH:mm}";
        }
        else if (state.NextExerciseName is { } next)
        {
            text = $"Next: {next} {state.NextSetIndex}/{state.NextSetTotal}";
        }
        else
        {
            text = "Workout complete";
        }

        var builder = BuildBaseNotification(context, OngoingChannelId, state.PlanName ?? "Workout", text, publicVisibility: true)
            .SetOngoing(true)
            .SetAutoCancel(false)
            .SetContentIntent(BuildOpenAppIntent(context));

        if (resting)
        {
            builder.AddAction(0, $"+{FormatAddLabel(addTimeSeconds)}", BuildActionIntent(context, RestTimerActionReceiver.ActionAddRest, 9403));
            builder.AddAction(0, "Skip", BuildActionIntent(context, RestTimerActionReceiver.ActionSkipRest, 9404));
        }
        else if (state.NextExerciseName != null)
        {
            builder.AddAction(0, "Log set", BuildActionIntent(context, RestTimerActionReceiver.ActionLogSet, 9405));
        }

        return builder.Build();
    }

    private static Notification.Builder BuildBaseNotification(Context context, string channelId, string title, string text, bool publicVisibility)
    {
        var builder = Build.VERSION.SdkInt >= BuildVersionCodes.O
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

        var nm = GetNotificationManager();
        if (nm == null)
            return;

        nm.CreateNotificationChannel(new NotificationChannel(OngoingChannelId, "Workout timer status",
            NotificationImportance.Default));

        var alertChannel = new NotificationChannel(RestEndChannelId, "Rest timer",
            NotificationImportance.High)
        {
            Description = "Alerts when rest periods end"
        };
        alertChannel.SetVibrationPattern([0, 400, 200, 400]);
        alertChannel.SetSound(global::Android.Net.Uri.Parse("android.resource://" + _context.PackageName + "/raw/rest_end_knock"), null);
        nm.CreateNotificationChannel(alertChannel);
    }

    private void StartOverlayService(WorkoutTimerState state)
    {
        var intent = new Intent(_context, typeof(RestOverlayService))
            .SetPackage(_context.PackageName)
            .PutExtra(RestOverlayService.ExtraEndUtcTicks, state.RestEndsAtUtc?.Ticks ?? 0)
            .PutExtra(RestOverlayService.ExtraRemainingSeconds, state.RestRemainingSeconds)
            .PutExtra(RestOverlayService.ExtraTitle, state.PlanName ?? "Physiquinator")
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
