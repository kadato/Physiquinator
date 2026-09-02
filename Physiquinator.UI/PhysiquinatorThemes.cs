using MudBlazor;
using Physiquinator.UI.Styles;

namespace Physiquinator.UI;

/// <summary>
/// MudBlazor theme that mirrors tokens.css. Keep values in sync with
/// Styles/DesignTokens.cs and wwwroot/css/tokens.css, which are the source for CSS variables.
/// Change the palette in tokens.css and DesignTokens together.
/// </summary>
public static class PhysiquinatorThemes
{
    public static readonly MudTheme Default = new()
    {
        PaletteLight = new PaletteLight()
        {
            Primary = DesignTokens.Light.VoltFill,
            PrimaryContrastText = DesignTokens.Light.VoltFg,
            Secondary = DesignTokens.Light.Purple,
            Tertiary = DesignTokens.Light.Magenta,
            Success = "#5A8A0B",
            Info = "#0E7490",
            Warning = "#B45309",
            Error = DesignTokens.Light.Error,
            Background = DesignTokens.Light.Paper,
            Surface = DesignTokens.Light.Chip2,
            AppbarBackground = DesignTokens.Light.Paper,
            TextPrimary = DesignTokens.Light.Ink,
            TextSecondary = DesignTokens.Light.Stone,
            LinesDefault = "#B8B2A0",
            Divider = DesignTokens.Light.Hairline,
        },
        PaletteDark = new PaletteDark()
        {
            Primary = DesignTokens.Dark.VoltFill,
            PrimaryContrastText = DesignTokens.Dark.VoltFg,
            Secondary = "#7DCFFF",
            Tertiary = "#BB9AF7",
            Success = "#9ECE6A",
            Info = "#7DCFFF",
            Warning = "#E0AF68",
            Error = "#F7768E",
            Background = DesignTokens.Dark.Paper,
            Surface = "#24283B",
            AppbarBackground = DesignTokens.Dark.Paper,
            TextPrimary = DesignTokens.Dark.Ink,
            TextSecondary = DesignTokens.Dark.Stone,
            LinesDefault = "rgba(192, 202, 245, 0.12)",
            Divider = "rgba(192, 202, 245, 0.08)",
        },
        LayoutProperties = new LayoutProperties()
        {
            DefaultBorderRadius = "0px"
        }
    };

    /// <summary>Builds a MudTheme whose palettes follow the resolved CSS tokens for the effective theme.</summary>
    public static MudTheme ForTheme(string? effectiveTheme)
    {
        var p = DesignTokens.Resolve(effectiveTheme);

        // Build fresh theme so CSS variables and Mud palettes stay aligned per theme.
        return new MudTheme
        {
            PaletteLight = new PaletteLight
            {
                Primary = p.VoltFill,
                PrimaryContrastText = p.VoltFg,
                Secondary = p.Purple,
                Tertiary = p.Magenta,
                Success = p.Success,
                Info = p.Cyan,
                Warning = p.Yellow,
                Error = p.Error,
                Background = p.Paper,
                Surface = p.Chip2,
                AppbarBackground = p.Paper,
                TextPrimary = p.Ink,
                TextSecondary = p.Stone,
                LinesDefault = p.HairlineStrong,
                Divider = p.Hairline,
            },
            PaletteDark = new PaletteDark
            {
                Primary = p.VoltFill,
                PrimaryContrastText = p.VoltFg,
                Secondary = p.Cyan,
                Tertiary = p.Purple,
                Success = p.Success,
                Info = p.Cyan,
                Warning = p.Yellow,
                Error = p.Error,
                Background = p.Paper,
                Surface = p.Chip2,
                AppbarBackground = p.Paper,
                TextPrimary = p.Ink,
                TextSecondary = p.Stone,
                LinesDefault = p.Hairline,
                Divider = p.HairlineStrong,
            },
            LayoutProperties = new LayoutProperties { DefaultBorderRadius = "0px" }
        };
    }
}
