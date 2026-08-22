using Android.Content;
using Android.Runtime;
using Physiquinator.Core.Services;

namespace Physiquinator.Platforms.Android.Services;

/// <summary>
/// Handles the quick-action buttons on the ongoing workout notification
/// (+add, Skip, Log set). Mutations go through
/// <see cref="WorkoutSessionService"/> / <see cref="WorkoutQuickActionService"/>,
/// which fire the session events so the coordinator re-syncs the
/// notification, overlay and alarm. Declared in AndroidManifest.xml.
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

            if (action == ActionLogSet)
            {
                var quickAction = services.GetService(typeof(WorkoutQuickActionService)) as WorkoutQuickActionService;
                if (quickAction == null)
                    return;

                PendingResult? pendingResult = GoAsync();
                if (pendingResult == null)
                    return;

                _ = RunLogSetAsync(quickAction, pendingResult);
                return;
            }

            var settings = services.GetService(typeof(RestAlertSettingsService)) as RestAlertSettingsService;

            switch (action)
            {
                case ActionAddRest:
                    session.AddRestSeconds(settings?.AddTimeSeconds ?? RestAlertSettingsService.DefaultAddTimeSeconds);
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

    private static async Task RunLogSetAsync(WorkoutQuickActionService quickAction, PendingResult pendingResult)
    {
        try
        {
            await quickAction.LogNextSetAsync();
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
}
