using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
#if ANDROID
using Android.OS;
using Microsoft.Maui.Platform;
using AndroidX.Core.View;
using Android.Views;
#endif

namespace Physiquinator.Services;

/// <summary>
/// Aligns Android status / navigation bar colors with the app page background. iOS uses a transparent status bar over the page fill.
/// </summary>
public static class SystemBarsHelper
{
    public static void Apply(Color pageBackground, bool isDark)
    {
#if ANDROID
        ApplyAndroid(pageBackground, isDark);
#elif IOS || MACCATALYST
        ApplyApple(pageBackground, isDark);
#endif
    }

    /// <summary>
    /// Applies system chrome using the current <c>PageBackgroundColor</c> resource and perceived luminance.
    /// </summary>
    public static void ApplyFromCurrentResources()
    {
        if (Application.Current?.Resources["PageBackgroundColor"] is not Color pageBg)
        {
            return;
        }

        Apply(pageBg, IsDarkBackground(pageBg));
    }

    internal static bool IsDarkBackground(Color c)
    {
        // sRGB luminance — matches ThemeService light/dark backgrounds.
        var lum = 0.2126 * c.Red + 0.7152 * c.Green + 0.0722 * c.Blue;
        return lum < 0.45;
    }

#if ANDROID
    private static void ApplyAndroid(Color pageBackground, bool isDark)
    {
        var activity = Platform.CurrentActivity;
        var window = activity?.Window;
        if (window == null)
        {
            return;
        }

        // Enable edge-to-edge drawing
        WindowCompat.SetDecorFitsSystemWindows(window, false);

        // Allow layout to extend into the cutout (notch / camera hole) area
        if (OperatingSystem.IsAndroidVersionAtLeast(28))
        {
            var attribs = window.Attributes;
            if (attribs != null)
            {
                attribs.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.ShortEdges;
                window.Attributes = attribs;
            }
        }

#pragma warning disable CA1422 // Obsolete on Android 35+; still correct for minSdk 24–34.
        window.SetStatusBarColor(Android.Graphics.Color.Transparent);
        window.SetNavigationBarColor(Android.Graphics.Color.Transparent);
#pragma warning restore CA1422

        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            // Window.InsetsController is implemented via DecorView.getWindowInsetsController() and throws
            // if the decor view is not attached yet (e.g. MainPage.OnAppearing / theme sync during startup).
            var decorView = window.DecorView;
            if (decorView == null)
            {
                new Handler(Looper.MainLooper!).Post(() => ApplyAndroid(pageBackground, isDark));
                return;
            }

            var controller = decorView.WindowInsetsController;
            if (controller == null)
            {
                return;
            }

            const int lightBars =
                (int)Android.Views.WindowInsetsControllerAppearance.LightStatusBars
                | (int)Android.Views.WindowInsetsControllerAppearance.LightNavigationBars;

            if (isDark)
            {
                controller.SetSystemBarsAppearance(0, lightBars);
            }
            else
            {
                controller.SetSystemBarsAppearance(lightBars, lightBars);
            }
        }
        else
        {
            var decorView = window.DecorView;
            if (decorView == null)
            {
                return;
            }

            var flags = decorView.SystemUiFlags;
            flags |= Android.Views.SystemUiFlags.LayoutStable;

            if (isDark)
            {
                flags &= ~Android.Views.SystemUiFlags.LightStatusBar;
                if (OperatingSystem.IsAndroidVersionAtLeast(27))
                {
                    flags &= ~Android.Views.SystemUiFlags.LightNavigationBar;
                }
            }
            else
            {
                flags |= Android.Views.SystemUiFlags.LightStatusBar;
                if (OperatingSystem.IsAndroidVersionAtLeast(27))
                {
                    flags |= Android.Views.SystemUiFlags.LightNavigationBar;
                }
            }

            decorView.SystemUiFlags = flags;
        }
    }
#elif IOS || MACCATALYST
    private static void ApplyApple(Color pageBackground, bool isDark)
    {
        // iOS status bar is transparent; MainPage.BackgroundColor already fills the safe area.
        // (PlatformConfiguration status-bar APIs here target NavigationPage luminosity, not arbitrary page fills.)
        _ = pageBackground;
        _ = isDark;
    }
#endif
}
