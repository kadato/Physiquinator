using Microsoft.JSInterop;
using Microsoft.Maui.Controls;

namespace Physiquinator.Services;

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
        if (_initialized)
        {
            return;
        }

        _dotNetRef = DotNetObjectReference.Create(this);
        var result = await _js.InvokeAsync<ThemeInitResult>(
            "physiquinatorTheme.initialize",
            _dotNetRef);

        Preference = result.Preference;
        EffectiveTheme = result.Effective;
        ApplyAppThemeOverride();
        _initialized = true;
        ThemeChanged?.Invoke();
    }

    public async Task SetPreferenceAsync(string preference)
    {
        Preference = preference;
        EffectiveTheme = await _js.InvokeAsync<string>(
            "physiquinatorTheme.setPreference",
            preference);

        ApplyAppThemeOverride();
        ThemeChanged?.Invoke();
    }

    [JSInvokable]
    public void OnSystemThemeChanged(string effectiveTheme)
    {
        EffectiveTheme = effectiveTheme;
        SyncAppResources();
        ThemeChanged?.Invoke();
    }

    private void ApplyAppThemeOverride()
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
    }

    private void SyncAppResources()
    {
        if (Application.Current == null) return;

        var isDark = EffectiveTheme == "dark";

        Application.Current.Resources["PageBackgroundColor"] =
            Color.FromArgb(isDark ? "#1f2335" : "#d5d6db");
        Application.Current.Resources["PrimaryTextColor"] =
            Color.FromArgb(isDark ? "#c0caf5" : "#343b58");
        Application.Current.Resources["PrimaryButtonBackgroundColor"] =
            Color.FromArgb(isDark ? "#7aa2f7" : "#2959aa");
        Application.Current.Resources["PrimaryButtonTextColor"] =
            Color.FromArgb(isDark ? "#1a1b26" : "#f7f7fb");
    }

    public async ValueTask DisposeAsync()
    {
        if (_dotNetRef != null)
        {
            await _js.InvokeVoidAsync("physiquinatorTheme.dispose");
            _dotNetRef.Dispose();
        }
    }

    private sealed record ThemeInitResult(string Preference, string Effective);
}
