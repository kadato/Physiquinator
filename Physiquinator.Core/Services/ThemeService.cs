using Microsoft.JSInterop;

namespace Physiquinator.Core.Services;

/// <summary>
/// Theme for the Blazor UI. <see cref="IJSRuntime"/> must run on the Blazor
/// dispatcher — do not marshal JS calls through a platform main thread.
/// Platform-specific shell mutations (MAUI app theme, system bars) are
/// provided by subclasses overriding the <c>Apply*/Sync*</c> hooks.
/// </summary>
public class ThemeService : IAsyncDisposable, IThemeInitialization
{
    private readonly IJSRuntime _js;
    private readonly UserProfileService _userProfileService;
    private readonly IAppPreferences _preferences;
    private DotNetObjectReference<ThemeService>? _dotNetRef;
    private string? _effectiveTheme;
    private bool _initialized;

    public ThemeService(IJSRuntime js, UserProfileService userProfileService, IAppPreferences preferences)
    {
        _js = js;
        _userProfileService = userProfileService;
        _preferences = preferences;

        Preference = ReadStoredPreference();
    }

    /// <summary>Resolves the OS-level appearance. Base returns dark; MAUI subclasses read the app theme.</summary>
    protected virtual string GetSystemTheme() => ThemePreference.Dark;

    /// <summary>Pushes the current preference to the platform shell (no-op by default).</summary>
    protected virtual void ApplyAppThemeOverride()
    {
    }

    /// <summary>Runs <paramref name="action"/> on the platform UI thread (inline by default).</summary>
    protected virtual void RunOnUiThread(Action action) => action();

    /// <summary>Refreshes platform resources (colors, system bars) after a theme change (no-op by default).</summary>
    protected virtual void SyncAppResources()
    {
    }

    private string ReadStoredPreference()
    {
        try
        {
            var suffix = GetSuffix();
            var key = PreferenceKeys.ThemePreference + suffix;
            return _preferences.Get(key, ThemePreference.System);
        }
        catch
        {
            return ThemePreference.System;
        }
    }

    private string GetSuffix()
    {
        Guid activeId = _userProfileService.GetActiveProfile().Id;
        return $"_{activeId}";
    }

    public string Preference { get; private set; }

    /// <summary>Resolved on first access; falls back to the system theme when the preference is System.</summary>
    public string EffectiveTheme
    {
        get => _effectiveTheme ??= Preference == ThemePreference.System ? GetSystemTheme() : Preference;
        private set => _effectiveTheme = value;
    }

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

        ThemeInitResult result = await _js.InvokeAsync<ThemeInitResult>(
            "physiquinatorTheme.initialize",
            _dotNetRef, GetSuffix()).ConfigureAwait(true);

        Preference = result.Preference;
        EffectiveTheme = result.Effective;
        _preferences.Set(PreferenceKeys.ThemePreference + GetSuffix(), Preference);
        ApplyAppThemeOverride();

        _initialized = true;
        ThemeChanged?.Invoke();
    }

    /// <summary>
    /// Persists theme preference (system/light/dark), updates the webview <c>data-theme</c>, and the platform shell.
    /// </summary>
    public async Task SetPreferenceAsync(string preference)
    {
        await EnsureInitializedCoreAsync().ConfigureAwait(true);

        var effective = await _js.InvokeAsync<string>("physiquinatorTheme.setPreference", preference, GetSuffix()).ConfigureAwait(true);

        Preference = preference;
        EffectiveTheme = effective;
        _preferences.Set(PreferenceKeys.ThemePreference + GetSuffix(), preference);
        ApplyAppThemeOverride();

        ThemeChanged?.Invoke();
    }

    /// <summary>Clears the stored theme preference so appearance matches the OS again.</summary>
    public async Task ResetStoredPreferenceToSystemAsync()
    {
        await EnsureInitializedCoreAsync().ConfigureAwait(true);

        ThemeInitResult result = await _js.InvokeAsync<ThemeInitResult>("physiquinatorTheme.resetStoredPreferenceToSystem", GetSuffix()).ConfigureAwait(true);

        Preference = result.Preference;
        EffectiveTheme = result.Effective;
        _preferences.Set(PreferenceKeys.ThemePreference + GetSuffix(), "system");
        ApplyAppThemeOverride();

        ThemeChanged?.Invoke();
    }

    [JSInvokable]
    public void OnSystemThemeChanged(string effectiveTheme)
    {
        EffectiveTheme = effectiveTheme;
        RunOnUiThread(SyncAppResources);
        ThemeChanged?.Invoke();
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
