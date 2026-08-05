using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace Physiquinator;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Resize the WebView when the soft keyboard opens so inputs in dialogs
        // and steppers are never hidden behind it (Android).
        Window?.SetSoftInputMode(SoftInput.AdjustResize);
    }
}
