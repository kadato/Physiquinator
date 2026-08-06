using Physiquinator.Core.Models;

namespace Physiquinator.Core.Services;

/// <summary>Native rest-alert notifications (sound + vibration when the app is backgrounded).</summary>
public interface INotificationService
{
    Task EnsurePermissionAsync();

    /// <summary>
    /// True when the platform can show the floating rest-timer overlay. On
    /// Android this is the "Display over other apps" permission, which the
    /// user must grant from system settings.
    /// </summary>
    bool HasOverlayPermission();

    /// <summary>
    /// Opens the platform screen where the user can grant the floating
    /// overlay permission (no-op on platforms without an overlay).
    /// </summary>
    Task RequestOverlayPermissionAsync();

    void CancelAllRestNotifications();

    Task ShowRestCompleteNowAsync(string description);

    /// <summary>
    /// Shows or updates the workout timer UI: an ongoing notification with
    /// quick actions (pause/resume, add time, skip, log set) and a floating
    /// overlay on platforms that support one. Active for the whole workout,
    /// not only while resting.
    /// </summary>
    Task ShowWorkoutTimerUiAsync(WorkoutTimerState state);

    /// <summary>Removes the workout timer UI (ongoing notification and floating overlay).</summary>
    Task HideWorkoutTimerUiAsync();

    /// <summary>
    /// Schedules a precise native alarm at the rest end time. Must survive
    /// Doze and process death (Android: AlarmManager exact alarm).
    /// </summary>
    Task ScheduleRestEndAlarmAsync(DateTime restEndsAtUtc, string title, string description);

    /// <summary>Cancels the scheduled rest-end alarm.</summary>
    Task CancelRestEndAlarmAsync();

    /// <summary>
    /// Shows a brief "Set logged" notification with an Undo action, mirroring
    /// the in-app undo toast after a set is logged from a background surface.
    /// </summary>
    Task ShowSetLoggedNotificationAsync(string exerciseName, int setIndex, int totalSets);

    /// <summary>Removes the set-logged notification.</summary>
    Task CancelSetLoggedNotificationAsync();
}
