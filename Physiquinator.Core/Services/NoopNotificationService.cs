using Physiquinator.Core.Models;

namespace Physiquinator.Core.Services;

/// <summary>Shared no-op notifications for hosts without native alerts (web, wasm, tests).</summary>
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
