using Microsoft.JSInterop;
using Physiquinator.Core.Services;

namespace Physiquinator.Services;

/// <summary>
/// MAUI implementation of <see cref="ThemeService"/> that also drives the
/// native <see cref="AppTheme"/>, app resource colors, and system bars.
/// </summary>
public sealed class MauiThemeService(
    IJSRuntime js,
    UserProfileService userProfileService,
    IAppPreferences preferences) : ThemeService(js, userProfileService, preferences)
{
    protected override string GetSystemTheme()
    {
        if (Application.Current != null)
        {
            AppTheme requested = Application.Current.RequestedTheme;
            if (requested == AppTheme.Dark)
            {
                return ThemePreference.Dark;
            }
            if (requested == AppTheme.Light)
            {
                return ThemePreference.Light;
            }
        }
        return ThemePreference.Dark;
    }

    protected override void ApplyAppThemeOverride()
    {
        RunOnUiThread(() =>
        {
            if (Application.Current == null)
            {
                return;
            }

            Application.Current.UserAppTheme = Preference switch
            {
                ThemePreference.Light => AppTheme.Light,
                ThemePreference.Dark => AppTheme.Dark,
                _ => AppTheme.Unspecified
            };

            SyncAppResources();
        });
    }

    protected override void RunOnUiThread(Action action)
    {
        if (MainThread.IsMainThread)
        {
            action();
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(action);
        }
    }

    protected override void SyncAppResources()
    {
        if (Application.Current == null)
        {
            return;
        }

        var isDark = EffectiveTheme == ThemePreference.Dark;

        Application.Current.Resources["PageBackgroundColor"] =
            Color.FromArgb(isDark ? "#0E0F17" : "#E7E3D1");
        Application.Current.Resources["PrimaryTextColor"] =
            Color.FromArgb(isDark ? "#C0CAF5" : "#1A1B26");
        Application.Current.Resources["PrimaryButtonBackgroundColor"] =
            Color.FromArgb("#FAFF00");
        Application.Current.Resources["PrimaryButtonTextColor"] =
            Color.FromArgb("#10111A");

        SystemBarsHelper.Apply(
            (Color)Application.Current.Resources["PageBackgroundColor"],
            isDark);
    }
}
