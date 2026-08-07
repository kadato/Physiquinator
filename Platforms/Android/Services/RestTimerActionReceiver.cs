using Android.Content;
using Android.Runtime;
using Physiquinator.Core.Services;

namespace Physiquinator.Platforms.Android.Services;

/// <summary>
/// Handles the quick-action buttons on the ongoing workout notification
/// (+add, Skip, Log set). Mutations go through
/// <see cref="WorkoutSessionService"/> / <see cref="WorkoutQuickActionService"/>,
/// which fire the session events so the coordinator re-syncs the
/// notification, overlay and alarm. Declared in AndroidManifest.xml;
/// <see cref="RegisterAttribute"/> pins the Java class name so the manifest
/// entry resolves.
/// </summary>
[Register("physiquinator.RestTimerActionReceiver")]
public sealed class RestTimerActionReceiver : BroadcastReceiver
{
    public const string RestTimerAction = "physiquinator.action.REST_TIMER";
    public const string ExtraAction = "action";
    public const string ActionAddRest = "physiquinator.action.ADD_REST";
    public const string ActionSkipRest = "physiquinator.action.SKIP_REST";
    public const string ActionLogSet = "physiquinator.action.LOG_SET";
    public const string ActionUndoSet = "physiquinator.action.UNDO_SET";

    public override void OnReceive(Context? context, Intent? intent)
    {
        try
        {
            var action = intent?.GetStringExtra(ExtraAction);
            if (action == null)
                return;

            IServiceProvider? services = IPlatformApplication.Current?.Services;
            if (services == null)
                return;

            var session = services.GetService(typeof(WorkoutSessionService)) as WorkoutSessionService;
            if (session == null)
                return;

            if (action is ActionLogSet or ActionUndoSet)
            {
                var quickAction = services.GetService(typeof(WorkoutQuickActionService)) as WorkoutQuickActionService;
                if (quickAction == null)
                    return;

                PendingResult? pendingResult = GoAsync();
                if (pendingResult == null)
                    return;

                _ = action == ActionLogSet
                    ? RunLogSetAsync(quickAction, pendingResult, services)
                    : RunUndoSetAsync(quickAction, pendingResult, services);
                return;
            }

            var settings = services.GetService(typeof(RestAlertSettingsService)) as RestAlertSettingsService;

            switch (action)
            {
                case ActionAddRest:
                    session.AddRestSeconds(settings?.AddTimeSeconds ?? 30);
                    break;
                case ActionSkipRest:
                    session.SkipRest();
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RestTimerActionReceiver failed: {ex}");
        }
    }

    private static async Task RunLogSetAsync(WorkoutQuickActionService quickAction, PendingResult pendingResult, IServiceProvider services)
    {
        try
        {
            QuickActionResult result = await quickAction.LogNextSetAsync();
            if (result.Status != QuickActionResult.NothingToLog)
            {
                var notifications = services.GetService(typeof(INotificationService)) as INotificationService;
                if (notifications != null && result.ExerciseName != null && result.LoggedSetIndex != null && result.SetTotal != null)
                    await notifications.ShowSetLoggedNotificationAsync(result.ExerciseName, result.LoggedSetIndex.Value, result.SetTotal.Value);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RestTimerActionReceiver log set failed: {ex}");
        }
        finally
        {
            pendingResult.Finish();
        }
    }

    private static async Task RunUndoSetAsync(WorkoutQuickActionService quickAction, PendingResult pendingResult, IServiceProvider services)
    {
        try
        {
            await quickAction.UndoLastSetAsync();
            var notifications = services.GetService(typeof(INotificationService)) as INotificationService;
            if (notifications != null)
                await notifications.CancelSetLoggedNotificationAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RestTimerActionReceiver undo failed: {ex}");
        }
        finally
        {
            pendingResult.Finish();
        }
    }
}
