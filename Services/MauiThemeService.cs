using Microsoft.JSInterop;
using Physiquinator.Core.Services;
using Physiquinator.UI.Styles;

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

            if (Preference == ThemePreference.System)
            {
                Application.Current.UserAppTheme = AppTheme.Unspecified;
            }
            else
            {
                var isDarkPref = ThemePreference.IsDarkTheme(Preference);
                Application.Current.UserAppTheme = isDarkPref ? AppTheme.Dark : AppTheme.Light;
            }

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

        var isDark = ThemePreference.IsDarkTheme(EffectiveTheme);
        var palette = DesignTokens.Resolve(EffectiveTheme);

        Application.Current.Resources["PageBackgroundColor"] =
            Color.FromArgb(palette.Paper);
        Application.Current.Resources["PrimaryTextColor"] =
            Color.FromArgb(palette.Ink);
        Application.Current.Resources["PrimaryButtonBackgroundColor"] =
            Color.FromArgb(palette.VoltFill);
        Application.Current.Resources["PrimaryButtonTextColor"] =
            Color.FromArgb(palette.VoltFg);

        SystemBarsHelper.Apply(
            (Color)Application.Current.Resources["PageBackgroundColor"],
            isDark);
    }
}
