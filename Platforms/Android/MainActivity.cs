using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace Physiquinator;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    /// <summary>True while the activity is in the foreground (used by the rest-timer overlay to hide itself over the app UI).</summary>
    public static bool IsInForeground { get; private set; }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Resize the WebView when the soft keyboard opens so inputs in dialogs
        // and steppers are never hidden behind it (Android).
        Window?.SetSoftInputMode(SoftInput.AdjustResize);

        // Tracks foreground state without overriding activity lifecycle
        // methods, which can interfere with MAUI's fragment setup.
        this.Application?.RegisterActivityLifecycleCallbacks(new ForegroundTracker());
    }

    private sealed class ForegroundTracker : Java.Lang.Object, global::Android.App.Application.IActivityLifecycleCallbacks
    {
        public void OnActivityStarted(Activity? activity) => IsInForeground = true;

        public void OnActivityStopped(Activity? activity) => IsInForeground = false;

        public void OnActivityCreated(Activity? activity, Bundle? savedInstanceState)
        {
        }

        public void OnActivityResumed(Activity? activity)
        {
        }

        public void OnActivityPaused(Activity? activity)
        {
        }

        public void OnActivitySaveInstanceState(Activity? activity, Bundle? outState)
        {
        }

        public void OnActivityDestroyed(Activity? activity)
        {
        }
    }
}
