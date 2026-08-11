using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Activity;
using Physiquinator.Platforms.Android.Services;
using AndroidView = global::Android.Views.View;
using AndroidViewGroup = global::Android.Views.ViewGroup;
using WebView = global::Android.Webkit.WebView;

namespace Physiquinator.Platforms.Android;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    /// <summary>True while the activity is in the foreground (used by the rest-timer overlay to hide itself over the app UI).</summary>
    public static bool IsInForeground { get; private set; }

    private bool _backGuardPending;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Resize the WebView when the soft keyboard opens so inputs in dialogs
        // and steppers are never hidden behind it (Android).
        Window?.SetSoftInputMode(global::Android.Views.SoftInput.AdjustResize);

        // Asks the workout back-guard (workoutTimer.js) whether it consumed the
        // press. The guard shows the "Leave workout?" confirm and navigates on
        // its own. When the guard or the WebView is unavailable, or the press
        // is not consumed, the callback is disabled and the press is dispatched
        // to the rest of the chain (MAUI/default behavior).
        OnBackPressedDispatcher.AddCallback(this, new WorkoutBackGuardCallback(this));

        // Tracks foreground state without overriding activity lifecycle
        // methods, which can interfere with MAUI's fragment setup.
        Application?.RegisterActivityLifecycleCallbacks(new ForegroundTracker());
    }

    /// <summary>
    /// Tells the rest-timer overlay service that the app changed foreground
    /// state so it can show/hide the bubble and start/stop its ticker without
    /// polling. The service is already running as a foreground service for
    /// the whole workout; starting it again while it is not running (no
    /// workout) fails silently on Android 12+ from the background, which is
    /// the desired no-op.
    /// </summary>
    private static void NotifyOverlayVisibilityChange(string action)
    {
        try
        {
            Context context = global::Android.App.Application.Context;
            context.StartService(new Intent(context, typeof(RestOverlayService))
                .SetAction(action)
                .SetPackage(context.PackageName!));
        }
        catch (Exception)
        {
            // No workout/service running, or background-start restriction;
            // nothing to show.
        }
    }

    private sealed class WorkoutBackGuardCallback : OnBackPressedCallback
    {
        private readonly MainActivity _activity;

        public WorkoutBackGuardCallback(MainActivity activity)
            : base(true)
        {
            _activity = activity;
        }

        public override void HandleOnBackPressed()
        {
            if (_activity._backGuardPending)
                return;

            var webView = _activity.FindBlazorWebView();
            if (webView == null)
            {
                DisableAndContinue();
                return;
            }

            _activity._backGuardPending = true;
            try
            {
                webView.EvaluateJavascript(
                    "window.physiquinatorBack && typeof window.physiquinatorBack.consume === 'function' ? window.physiquinatorBack.consume() : 'false'",
                    new WorkoutBackResultCallback(handled =>
                    {
                        _activity._backGuardPending = false;
                        if (!handled)
                            DisableAndContinue();
                    }));
            }
            catch (Exception)
            {
                _activity._backGuardPending = false;
                DisableAndContinue();
            }
        }

        private void DisableAndContinue()
        {
            Enabled = false;
            _activity.OnBackPressedDispatcher.OnBackPressed();
        }
    }

    private WebView? FindBlazorWebView() => FindWebView(Window?.DecorView);

    private static WebView? FindWebView(AndroidView? root)
    {
        if (root is WebView webView)
            return webView;

        if (root is AndroidViewGroup group)
        {
            for (var i = 0; i < group.ChildCount; i++)
            {
                var found = FindWebView(group.GetChildAt(i));
                if (found != null)
                    return found;
            }
        }

        return null;
    }

    private sealed class WorkoutBackResultCallback(Action<bool> onResult) : Java.Lang.Object, global::Android.Webkit.IValueCallback
    {
        public void OnReceiveValue(Java.Lang.Object? value)
        {
            var result = (value as Java.Lang.String)?.ToString();
            onResult(result is "\"true\"" or "true");
        }
    }

    private sealed class ForegroundTracker : Java.Lang.Object, global::Android.App.Application.IActivityLifecycleCallbacks
    {
        public void OnActivityStarted(Activity? activity)
        {
            IsInForeground = true;
            NotifyOverlayVisibilityChange(RestOverlayService.ActionForegrounded);
        }

        public void OnActivityStopped(Activity? activity)
        {
            IsInForeground = false;
            NotifyOverlayVisibilityChange(RestOverlayService.ActionBackgrounded);
        }

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
