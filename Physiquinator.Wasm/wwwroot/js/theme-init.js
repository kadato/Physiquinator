// Sets the theme before first paint so the page never flashes the wrong one.
// Runs as a blocking head script; keep it small and dependency-free.
(function () {
    const key = "physiquinator-theme-preference";
    const preference = localStorage.getItem(key) || "system";
    const isDark = window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches;
    let theme;
    if (preference === "system") {
        theme = isDark ? "dark" : "light";
    } else {
        theme = preference;
    }
    var lightThemes = { "light": 1, "tokyo-night-light": 1, "solarized-light": 1, "github-light": 1 };
    document.documentElement.dataset.theme = theme;
    document.documentElement.dataset.themeMode = lightThemes[theme] ? "light" : "dark";
    // Values mirror --pl-paper in tokens.css so the mobile status bar matches every theme.
    var papers = { "dark": "#0E0F17", "light": "#E6E2D0", "tokyo-night": "#1A1B26", "tokyo-night-storm": "#24283B", "tokyo-night-moon": "#222436", "tokyo-night-light": "#D5D6DB", "dracula": "#282A36", "monokai": "#272822", "one-dark-pro": "#282C34", "nord": "#2E3440", "solarized-dark": "#002B36", "solarized-light": "#FDF6E3", "github-dark": "#0D1117", "github-light": "#FFFFFF", "night-owl": "#011627" };
    try {
        var meta = document.querySelector('meta[name="theme-color"]');
        if (meta && papers[theme]) { meta.setAttribute("content", papers[theme]); }
        document.documentElement.style.colorScheme = lightThemes[theme] ? "light" : "dark";
    } catch (e) { /* ignore */ }
})();
