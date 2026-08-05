using Physiquinator.Core.Services;

namespace Physiquinator.Web.Services;

/// <summary>No-op notifications for the browser debug host.</summary>
public sealed class NoopNotificationService : INotificationService
{
    public Task EnsurePermissionAsync() => Task.CompletedTask;

    public void CancelAllRestNotifications()
    {
    }

    public Task ScheduleRestEndAsync(DateTime notifyUtc, string title, string description) => Task.CompletedTask;

    public Task ShowRestCompleteNowAsync(string description) => Task.CompletedTask;
}
