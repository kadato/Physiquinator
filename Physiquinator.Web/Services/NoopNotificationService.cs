using Physiquinator.Core.Models;
using Physiquinator.Core.Services;

namespace Physiquinator.Web.Services;

/// <summary>No-op notifications for the browser debug host.</summary>
public sealed class NoopNotificationService : INotificationService
{
    public Task EnsurePermissionAsync() => Task.CompletedTask;

    public bool SupportsNotifications => false;

    public bool SupportsOverlay => false;

    public bool HasOverlayPermission() => false;

    public Task RequestOverlayPermissionAsync() => Task.CompletedTask;

    public void CancelAllRestNotifications()
    {
    }

    public Task ShowRestCompleteNowAsync(string description) => Task.CompletedTask;

    public Task ShowWorkoutTimerUiAsync(WorkoutTimerState state) => Task.CompletedTask;

    public Task HideWorkoutTimerUiAsync() => Task.CompletedTask;

    public Task ScheduleRestEndAlarmAsync(DateTime restEndsAtUtc, string title, string description) => Task.CompletedTask;

    public Task CancelRestEndAlarmAsync() => Task.CompletedTask;
}
