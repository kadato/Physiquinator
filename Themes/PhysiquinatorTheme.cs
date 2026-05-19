using MudBlazor;

namespace Physiquinator.Themes;

public static class PhysiquinatorTheme
{
    public static MudTheme Create() => new()
    {
        PaletteDark = DarkPalette,
        PaletteLight = LightPalette,
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "12px",
            AppbarHeight = "64px",
        },
    };

    private static readonly PaletteDark DarkPalette = new()
    {
        Black = "#1a1b26",
        Background = "#1f2335",
        Surface = "#292e42",
        DrawerBackground = "#24283b",
        AppbarBackground = "#24283b",
        Primary = "#7aa2f7",
        Secondary = "#bb9af7",
        Info = "#7dcfff",
        Success = "#9ece6a",
        Warning = "#e0af68",
        Error = "#f7768e",
        TextPrimary = "#c0caf5",
        TextSecondary = "#a9b1d6",
        TextDisabled = "#565f89",
        ActionDefault = "#a9b1d6",
        ActionDisabled = "#565f89",
        ActionDisabledBackground = "#3b4261",
        Divider = "#3b4261",
        TableLines = "#3b4261",
        LinesDefault = "#3b4261",
        LinesInputs = "#3b4261",
    };

    private static readonly PaletteLight LightPalette = new()
    {
        Black = "#343b58",
        Background = "#d5d6db",
        Surface = "#e9e9ed",
        DrawerBackground = "#e1e2e7",
        AppbarBackground = "#e1e2e7",
        Primary = "#2959aa",
        Secondary = "#5a4a78",
        Info = "#007197",
        Success = "#33635c",
        Warning = "#8f5e15",
        Error = "#8c4351",
        TextPrimary = "#343b58",
        TextSecondary = "#565f89",
        TextDisabled = "#6c74a2",
        ActionDefault = "#565f89",
        ActionDisabled = "#9aa5ce",
        ActionDisabledBackground = "#c4c5c8",
        Divider = "#b4b9d6",
        TableLines = "#b4b9d6",
        LinesDefault = "#b4b9d6",
        LinesInputs = "#b4b9d6",
    };
}
