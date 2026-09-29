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
})();
