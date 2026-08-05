using Android.Content;
using Microsoft.Maui.ApplicationModel;
using Physiquinator.Core.Services;

using Android.Runtime;

namespace Physiquinator.Platforms.Android.Services;

/// <summary>
/// Fired by the exact rest-end alarm. Completes the rest and shows the
/// completion alert. Works from a cold start after process death: the alarm is
/// held by the OS and this receiver wakes the process. Declared in
/// AndroidManifest.xml; <see cref="RegisterAttribute"/> pins the Java class
/// name so the manifest entry resolves.
/// </summary>
[Register("physiquinator.RestEndAlarmReceiver")]
public sealed class RestEndAlarmReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        try
        {
            RestTimerCoordinator? coordinator =
                IPlatformApplication.Current?.Services.GetService(typeof(RestTimerCoordinator)) as RestTimerCoordinator;
            coordinator?.HandleRestEndAlarmFired();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RestEndAlarmReceiver failed: {ex}");
        }
    }
}
