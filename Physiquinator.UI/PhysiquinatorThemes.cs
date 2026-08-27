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
            Primary = "#FAFF00", // volt fill, identical to dark: ink text on it keeps 15:1
            PrimaryContrastText = "#10111A",
            Secondary = "#7C3AED", // electric violet, acid
            Tertiary = "#E11D48", // vivid magenta, acid
            Success = "#5A8A0B", // acid lime darkened for 4.5:1 on white tint
            Info = "#0E7490", // deeper cyan holds AA on white
            Warning = "#B45309", // acid amber 700, brutal
            Error = "#BE123C", // vivid red, paired with icon
            Background = "#E7E3D1", // acid concrete, matches --pl-paper
            Surface = "#EDE8D3", // brutal well, matches --pl-chip-2
            AppbarBackground = "#E7E3D1",
            TextPrimary = "#1A1B26", // near-black navy, 14:1 on bg
            TextSecondary = "#5A5644", // darker warm slate, 7:1 on white
            LinesDefault = "#B8B2A0", // warm hairline, brutal
            Divider = "#C6C1AB",
        },
        PaletteDark = new PaletteDark()
        {
            Primary = "#FAFF00", // volt, same role as light: one signature hue per theme
            PrimaryContrastText = "#10111A",
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
