namespace Physiquinator.Core.Services;

/// <summary>Native rest-alert notifications (sound + vibration when the app is backgrounded).</summary>
public interface INotificationService
{
    Task EnsurePermissionAsync();

    void CancelAllRestNotifications();

    Task ScheduleRestEndAsync(DateTime notifyUtc, string title, string description);

    Task ShowRestCompleteNowAsync(string description);
}
