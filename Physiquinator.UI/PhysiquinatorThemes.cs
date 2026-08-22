using MudBlazor;

namespace Physiquinator.UI;

/// <summary>
/// The single MudBlazor theme used by the app shell and the login page, so the
/// two share one visual identity and one set of contrast-safe palette values.
/// </summary>
public static class PhysiquinatorThemes
{
    public static readonly MudTheme Default = new()
    {
        PaletteLight = new PaletteLight()
        {
            Primary = "#24283B", // Tokyo Night ink - deep navy for primary actions, 12:1 on paper
            PrimaryContrastText = "#D5D6DB",
            Secondary = "#34548A", // Tokyo Night blue desaturated for light, distinct from success
            Tertiary = "#5A3E8C", // muted purple
            Success = "#33635C", // Tokyo Night green darkened for light bg, distinct from error
            Info = "#34548A",
            Warning = "#8F5E15",
            Error = "#8C4351", // Tokyo Night red darkened for light bg, paired with icon
            Background = "#D5D6DB", // Tokyo Night Light bg
            Surface = "#E9E9ED", // chip slightly lighter than bg
            AppbarBackground = "#D5D6DB",
            TextPrimary = "#1A1B26", // near-black navy, 14:1 on bg
            TextSecondary = "#565A6E", // 6.2:1 on bg, passes AA
            LinesDefault = "#B4B8C5", // hairline 1px
            Divider = "#C7CAD5",
        },
        PaletteDark = new PaletteDark()
        {
            Primary = "#7AA2F7", // Tokyo Night blue - primary, 6.1:1 on bg, colorblind safe (blue)
            PrimaryContrastText = "#1A1B26",
            Secondary = "#7DCFFF", // cyan
            Tertiary = "#BB9AF7", // magenta
            Success = "#9ECE6A", // green, distinct luminance from blue
            Info = "#7DCFFF",
            Warning = "#E0AF68", // yellow
            Error = "#F7768E", // red/pink, paired with icon + label
            Background = "#1A1B26", // Tokyo Night bg
            Surface = "#24283B", // surface
            AppbarBackground = "#1A1B26",
            TextPrimary = "#C0CAF5", // primary text, 9.1:1
            TextSecondary = "#8A90B8", // secondary text: 4.7:1 on surface #24283B, AA
            LinesDefault = "rgba(192, 202, 245, 0.12)",
            Divider = "rgba(192, 202, 245, 0.08)",
        },
        LayoutProperties = new LayoutProperties()
        {
            DefaultBorderRadius = "0px"
        }
    };
}
