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
            Secondary = "#7C3AED", // electric purple
            Tertiary = "#D81B60", // hot magenta
            Success = "#4D7C0F", // acid lime darkened for light bg, distinct from error
            Info = "#1E5EFF",
            Warning = "#8F5E15",
            Error = "#BE123C", // vivid red, paired with icon
            Background = "#E5E1D2", // warm bone plate stock
            Surface = "#F0EDE2", // warm well, matches --pl-chip-2
            AppbarBackground = "#E5E1D2",
            TextPrimary = "#1A1B26", // near-black navy, 14:1 on bg
            TextSecondary = "#565349", // warm slate, ~5.9:1 on bone, passes AA
            LinesDefault = "#C6C1AB", // warm hairline 1px
            Divider = "#D8D3C0",
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
