namespace Physiquinator.UI.Styles;

/// <summary>
/// Single source of primitive token values for code that cannot read CSS.
/// Keep in sync with wwwroot/css/tokens.css and DESIGN.md frontmatter.
/// Change the palette here and in tokens.css together, then rebuild.
/// </summary>
public static class DesignTokens
{
    public static class Light
    {
        public const string Paper = "#E6E2D0";
        public const string Paper2 = "#EDE8D4";
        public const string Ink = "#1A1B26";
        public const string Stone = "#5A5644";
        public const string Hairline = "#C6C1AB";
        public const string HairlineStrong = "#A39D82";
        public const string Chip = "#FAF7EB";
        public const string Chip2 = "#E8E2CB";
        public const string VoltFill = "#FAFF00";
        public const string VoltFg = "#10111A";
        public const string Yellow = "#D97706";
        public const string Purple = "#7C3AED";
        public const string Cyan = "#00A3C4";
        public const string Magenta = "#E11D48";
        public const string Success = "#16A34A";
        public const string Error = "#BE123C";
    }

    public static class Dark
    {
        public const string Paper = "#0E0F17";
        public const string Paper2 = "#141624";
        public const string Ink = "#C0CAF5";
        public const string Stone = "#8A90B8";
        public const string Hairline = "#282B42";
        public const string HairlineStrong = "#414770";
        public const string Chip = "#181A2A";
        public const string Chip2 = "#22253B";
        public const string VoltFill = "#FAFF00";
        public const string VoltFg = "#10111A";
        public const string Yellow = "#FAFF00";
        public const string Purple = "#C084FC";
        public const string Cyan = "#00E5FF";
        public const string Magenta = "#FF0055";
        public const string Success = "#A3E635";
        public const string Error = "#FF0055";
    }

    public static class Shared
    {
        public const string Radius = "0px";
        public const string FontMono = "'Departure Mono', 'JetBrains Mono', ui-monospace, monospace";
    }
}
