namespace Physiquinator.Core.Services;

/// <summary>Canonical theme preference values persisted in app preferences and passed to the WebView theme JS.</summary>
public static class ThemePreference
{
    public const string System = "system";
    public const string Light = "light";
    public const string Dark = "dark";

    // Tokyo Night family (enkia/tokyo-night-vscode-theme)
    public const string TokyoNight = "tokyo-night";
    public const string TokyoNightStorm = "tokyo-night-storm";
    public const string TokyoNightMoon = "tokyo-night-moon";
    public const string TokyoNightLight = "tokyo-night-light";

    // Most popular VS Code themes
    public const string Dracula = "dracula";
    public const string Monokai = "monokai";
    public const string OneDarkPro = "one-dark-pro";
    public const string Nord = "nord";
    public const string SolarizedDark = "solarized-dark";
    public const string SolarizedLight = "solarized-light";
    public const string GithubDark = "github-dark";
    public const string GithubLight = "github-light";
    public const string NightOwl = "night-owl";

    /// <summary>All theme ids that can appear as an effective data-theme value (excludes "system").</summary>
    public static readonly IReadOnlyList<string> EffectiveThemes =
    [
        Light, Dark,
        TokyoNight, TokyoNightStorm, TokyoNightMoon, TokyoNightLight,
        Dracula, Monokai, OneDarkPro, Nord,
        SolarizedDark, SolarizedLight,
        GithubDark, GithubLight,
        NightOwl
    ];

    /// <summary>All persisted preference values including "system".</summary>
    public static readonly IReadOnlyList<string> AllPreferences =
    [
        System, Light, Dark,
        TokyoNight, TokyoNightStorm, TokyoNightMoon, TokyoNightLight,
        Dracula, Monokai, OneDarkPro, Nord,
        SolarizedDark, SolarizedLight,
        GithubDark, GithubLight,
        NightOwl
    ];

    public static bool IsValidPreference(string? value) =>
        !string.IsNullOrWhiteSpace(value) && AllPreferences.Contains(value, StringComparer.Ordinal);

    /// <summary>True for themes that should render with dark chrome (Mud dark mode, system bars).</summary>
    public static bool IsDarkTheme(string? theme) => theme switch
    {
        Dark => true,
        TokyoNight => true,
        TokyoNightStorm => true,
        TokyoNightMoon => true,
        Dracula => true,
        Monokai => true,
        OneDarkPro => true,
        Nord => true,
        SolarizedDark => true,
        GithubDark => true,
        NightOwl => true,
        Light => false,
        TokyoNightLight => false,
        SolarizedLight => false,
        GithubLight => false,
        // "system" is not an effective theme; caller should resolve it first.
        _ => theme != Light && theme != TokyoNightLight && theme != SolarizedLight && theme != GithubLight
    };

    public static string GetDisplayName(string? theme) => theme switch
    {
        System => "Match system",
        Light => "Physiquinator Light",
        Dark => "Physiquinator Dark",
        TokyoNight => "Tokyo Night",
        TokyoNightStorm => "Tokyo Night Storm",
        TokyoNightMoon => "Tokyo Night Moon",
        TokyoNightLight => "Tokyo Night Light",
        Dracula => "Dracula",
        Monokai => "Monokai",
        OneDarkPro => "One Dark Pro",
        Nord => "Nord",
        SolarizedDark => "Solarized Dark",
        SolarizedLight => "Solarized Light",
        GithubDark => "GitHub Dark",
        GithubLight => "GitHub Light",
        NightOwl => "Night Owl",
        _ => theme ?? "Unknown"
    };

    public static string GetThemeGroup(string? theme) => theme switch
    {
        Light or Dark => "Physiquinator",
        TokyoNight or TokyoNightStorm or TokyoNightMoon or TokyoNightLight => "Tokyo Night",
        Dracula or Monokai or OneDarkPro or Nord or SolarizedDark or GithubDark or NightOwl => "Dark",
        SolarizedLight or GithubLight => "Light",
        System => "System",
        _ => "Other"
    };

    public static IReadOnlyList<ThemeOption> Options { get; } =
    [
        new(System, GetDisplayName(System), false, "System"),
        new(Light, GetDisplayName(Light), false, "Physiquinator"),
        new(Dark, GetDisplayName(Dark), true, "Physiquinator"),
        new(TokyoNight, GetDisplayName(TokyoNight), true, "Tokyo Night"),
        new(TokyoNightStorm, GetDisplayName(TokyoNightStorm), true, "Tokyo Night"),
        new(TokyoNightMoon, GetDisplayName(TokyoNightMoon), true, "Tokyo Night"),
        new(TokyoNightLight, GetDisplayName(TokyoNightLight), false, "Tokyo Night"),
        new(Dracula, GetDisplayName(Dracula), true, "Popular Dark"),
        new(Monokai, GetDisplayName(Monokai), true, "Popular Dark"),
        new(OneDarkPro, GetDisplayName(OneDarkPro), true, "Popular Dark"),
        new(Nord, GetDisplayName(Nord), true, "Popular Dark"),
        new(SolarizedDark, GetDisplayName(SolarizedDark), true, "Popular Dark"),
        new(GithubDark, GetDisplayName(GithubDark), true, "Popular Dark"),
        new(NightOwl, GetDisplayName(NightOwl), true, "Popular Dark"),
        new(SolarizedLight, GetDisplayName(SolarizedLight), false, "Popular Light"),
        new(GithubLight, GetDisplayName(GithubLight), false, "Popular Light"),
    ];

    public sealed record ThemeOption(string Id, string DisplayName, bool IsDark, string Group);
}
