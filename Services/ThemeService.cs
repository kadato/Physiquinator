using Microsoft.JSInterop;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace Physiquinator.Services;

/// <summary>
/// Theme for Blazor WebView plus MAUI <see cref="AppTheme"/> / resources.
/// <see cref="IJSRuntime"/> must run on the Blazor WebView dispatcher — do not marshal JS calls through <see cref="MainThread"/>.
/// MAUI mutations (<see cref="Application.Current"/>) must run on the MAUI UI thread; use <see cref="RunOnMauiUiThread"/> when called from <see cref="JSInvokableAttribute"/> or other off-UI paths.
/// </summary>
public sealed class ThemeService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private DotNetObjectReference<ThemeService>? _dotNetRef;
    private bool _initialized;

    public ThemeService(IJSRuntime js)
    {
        _js = js;
    }

    public string Preference { get; private set; } = "system";

    public string EffectiveTheme { get; private set; } = "dark";

    public event Action? ThemeChanged;

    public async Task EnsureInitializedAsync()
    {
        await EnsureInitializedCoreAsync().ConfigureAwait(true);
    }

    private async Task EnsureInitializedCoreAsync()
    {
        if (_initialized)
        {
            return;
        }

        _dotNetRef = DotNetObjectReference.Create(this);

        var result = await _js.InvokeAsync<ThemeInitResult>(
            "physiquinatorTheme.initialize",
            _dotNetRef).ConfigureAwait(true);

        Preference = result.Preference;
        EffectiveTheme = result.Effective;
        ApplyAppThemeOverride();

        _initialized = true;
        ThemeChanged?.Invoke();
    }

    /// <summary>
    /// Persists theme preference (system/light/dark), updates WebView <c>data-theme</c>, MAUI <see cref="AppTheme"/>, and app resources.
    /// </summary>
    public async Task SetPreferenceAsync(string preference)
    {
        await EnsureInitializedCoreAsync().ConfigureAwait(true);

        var effective = await _js.InvokeAsync<string>("physiquinatorTheme.setPreference", preference).ConfigureAwait(true);

        Preference = preference;
        EffectiveTheme = effective;
        ApplyAppThemeOverride();

        ThemeChanged?.Invoke();
    }

    /// <summary>Clears the WebView theme preference so appearance matches the OS again.</summary>
    public async Task ResetStoredPreferenceToSystemAsync()
    {
        await EnsureInitializedCoreAsync().ConfigureAwait(true);

        var result = await _js.InvokeAsync<ThemeInitResult>("physiquinatorTheme.resetStoredPreferenceToSystem").ConfigureAwait(true);

        Preference = result.Preference;
        EffectiveTheme = result.Effective;
        ApplyAppThemeOverride();

        ThemeChanged?.Invoke();
    }

    [JSInvokable]
    public void OnThemePreferenceChangedFromScript(string preference, string effective)
    {
        Preference = preference;
        EffectiveTheme = effective;
        ApplyAppThemeOverride();

        ThemeChanged?.Invoke();
    }

    [JSInvokable]
    public void OnSystemThemeChanged(string effectiveTheme)
    {
        EffectiveTheme = effectiveTheme;
        RunOnMauiUiThread(SyncAppResources);
        ThemeChanged?.Invoke();
    }

    private void ApplyAppThemeOverride()
    {
        RunOnMauiUiThread(() =>
        {
            if (Application.Current == null)
            {
                return;
            }

            Application.Current.UserAppTheme = Preference switch
            {
                "light" => AppTheme.Light,
                "dark" => AppTheme.Dark,
                _ => AppTheme.Unspecified
            };

            SyncAppResources();
        });
    }

    private static void RunOnMauiUiThread(Action action)
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

    private void SyncAppResources()
    {
        if (Application.Current == null)
        {
            return;
        }

        var isDark = EffectiveTheme == "dark";

        Application.Current.Resources["PageBackgroundColor"] =
            Color.FromArgb(isDark ? "#1f2335" : "#d5d6db");
        Application.Current.Resources["PrimaryTextColor"] =
            Color.FromArgb(isDark ? "#c0caf5" : "#343b58");
        Application.Current.Resources["PrimaryButtonBackgroundColor"] =
            Color.FromArgb(isDark ? "#7aa2f7" : "#2959aa");
        Application.Current.Resources["PrimaryButtonTextColor"] =
            Color.FromArgb(isDark ? "#1a1b26" : "#f7f7fb");

        SystemBarsHelper.Apply(
            (Color)Application.Current.Resources["PageBackgroundColor"],
            isDark);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dotNetRef == null)
        {
            return;
        }

        try
        {
            await _js.InvokeVoidAsync("physiquinatorTheme.dispose").ConfigureAwait(true);
        }
        catch (JSDisconnectedException)
        {
            // WebView or scope already torn down.
        }
        finally
        {
            _dotNetRef.Dispose();
            _dotNetRef = null;
        }
    }

    private sealed record ThemeInitResult(string Preference, string Effective);
}
