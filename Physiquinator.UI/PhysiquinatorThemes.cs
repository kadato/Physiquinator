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
            Primary = "#0F766E", // Deep Teal
            Secondary = "#2563EB", // Active Cobalt
            Tertiary = "#D946EF",
            Success = "#10B981",
            Info = "#06B6D4",
            Warning = "#F59E0B",
            Error = "#EF4444",
            Background = "#F8F9FA",
            Surface = "#FFFFFF",
            AppbarBackground = "#F8F9FA",
            TextPrimary = "#111827",
            TextSecondary = "#6B7280",
            LinesDefault = "rgba(0, 0, 0, 0.08)",
            Divider = "rgba(0, 0, 0, 0.06)",
        },
        PaletteDark = new PaletteDark()
        {
            Primary = "#10B981", // Active Volt Green (Sporty)
            // White on the volt green fails WCAG AA (2.5:1); render primary
            // fills with the near-black background tone instead.
            PrimaryContrastText = "#0B0C10",
            Secondary = "#3B82F6", // Cobalt Blue
            Tertiary = "#FF006E", // Energy Magenta
            Success = "#10B981",
            Info = "#06B6D4",
            Warning = "#F59E0B",
            Error = "#EF4444",
            Background = "#0B0C10", // Deep Midnight/Obsidian
            Surface = "#151821", // Sleek charcoal card surface
            AppbarBackground = "#0B0C10",
            TextPrimary = "#F3F4F6",
            TextSecondary = "#9CA3AF",
            LinesDefault = "rgba(255, 255, 255, 0.08)",
            Divider = "rgba(255, 255, 255, 0.06)",
        },
        LayoutProperties = new LayoutProperties()
        {
            DefaultBorderRadius = "16px"
        }
    };
}
