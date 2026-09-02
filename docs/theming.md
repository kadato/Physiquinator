# Theming guide

This guide explains how to create a new theme for Physiquinator without drift.

## Single source of truth

Tokens live in one place:

- `Physiquinator.UI/wwwroot/css/tokens.css` — CSS variables for light and dark, plus shared globals like `--radius`, `--font-mono`, shadows.
- `Physiquinator.UI/Styles/DesignTokens.cs` — same values as constants for C# code that cannot read CSS, like `PhysiquinatorThemes.cs` for MudBlazor palettes.
- `DESIGN.md` frontmatter — the authored palette that `tokens.css` implements.

Change the palette in `tokens.css` and `DesignTokens.cs` together. `PhysiquinatorThemes.cs` reads from `DesignTokens`, so MudBlazor follows the same palette. Do not edit colors directly in `PhysiquinatorThemes.cs` or in `app-overrides.css`.

## How themes switch

`ThemeService` writes `html[data-theme="..."]` and `wwwroot/js/theme.js` persists the preference in `localStorage` under `physiquinator-theme-preference`. All components use `var(--pl-*)` variables, so switching the attribute repaints the app with no code change.

Available themes: `system` (follow OS), `light` / `dark` (Physiquinator defaults), `tokyo-night`, `tokyo-night-storm`, `tokyo-night-moon`, `tokyo-night-light` (Tokyo Night family), `dracula`, `monokai`, `one-dark-pro`, `nord`, `solarized-dark`, `solarized-light`, `github-dark`, `github-light`, `night-owl` (most popular VS Code themes). `ThemePreference` in `Physiquinator.Core` is the source of truth for ids, display names, and dark/light grouping.

## Adding a new theme

1. Duplicate the `html[data-theme="dark"]` block in `tokens.css`.
2. Rename it to `html[data-theme="forest"]` and replace the hex values. Keep the same variable names.
3. Add the variant to `DesignTokens.Forest` in `Styles/DesignTokens.cs`.
4. Extend `PhysiquinatorThemes` with a `PaletteForest` or reuse `PaletteLight` with forest values, and wire it in `ThemeService.Preference`.
5. Add the theme name to `PreferenceKeys.ThemePreference` handling and to `Settings/SettingsAppearancePanel`.

Example token override:

```css
html[data-theme="forest"] {
	--pl-paper: #0F1510;
	--pl-paper-2: #141E16;
	--pl-ink: #C8E6C9;
	--pl-chip: #1A2A1D;
	--pl-chip-2: #223524;
	--pl-yellow: #A3E635;
	--pl-cyan: #4ADE80;
	--pl-magenta: #86EFAC;
	/* keep --pl-accent-bg and --pl-accent-fg for primary actions */
}
```

Preview the new theme by setting `document.documentElement.dataset.theme = "forest"` in the browser console, then check contrast in both the AI chat and the rest timer for AA.

## Component rules for drift-free theming

- New UI must be a Razor component under `Physiquinator.UI/Components/`, not a block in `app-overrides.css`. The monolith at `wwwroot/css/app-overrides.css` is frozen and imports `tokens.css` at the top. New styles belong in the component's own `*.razor.css` and must use `var(--pl-*)` variables, not hard-coded hex.
- Shared patterns live in `Components/Shared/` and `Components/DesignSystem/`:
	- `AiChatView.razor` — single chat + clipboard bridge, used by both the `/ai` page and the `AiChatModal` dialog so the two never drift.
	- `HistorySessionCard.razor` — one card for session rows on History and Home.
	- `Services/UiFeedbackService.cs` — one place for `Snackbar.Add` wording and undo actions.
	- `Services/JsSafeInvoker.cs` — one place for `JSDisconnectedException` swallowing.
- When you copy a pattern a third time, extract it.

## Verifying a theme

- Run `dotnet build Physiquinator.UI/Physiquinator.UI.csproj` to ensure `DesignTokens` and `PhysiquinatorThemes` stay in sync.
- Check the startup splash in `Physiquinator.Web/Components/App.razor` and `wwwroot/index.html`: its inline `<style>` block duplicates the paper and ink tokens for the first paint before CSS loads. Update it when you change `tokens.css`.
- Run `dotnet test Physiquinator.Tests/Physiquinator.Tests.csproj` — no test should depend on a color value.

## Cache busting

`tokens.css` and `app-overrides.css` are referenced with `?v=87` in `wwwroot/index.html`, `Physiquinator.Web/Components/App.razor`, and `Physiquinator.Wasm/wwwroot/index.html`. Bump the number when you change either file so WebView2 and browsers fetch the new sheet.

## Past drift that is now fixed

- `AiAssistant.razor` and `AiChatModal.razor` were 769 and 781 lines of duplicated chat state and markup. They now render one `AiChatView.razor` with `IsDialog` and `OnClose` parameters.
- `WebAppPreferences` and `WasmAppPreferences` each reimplemented `Dictionary<string,string>` plus `bool` parsing. They now inherit `InMemoryAppPreferences` from `Physiquinator.Core` so the bool serialization cannot drift.
- `WorkoutHistoryRepository` had three copies of the set-normalization loop for backup restore and undo. They now call `InsertNormalizedSets`.
```

