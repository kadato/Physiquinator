// Early theme bootstrap: sets data-theme before first paint to avoid a flash of
// the wrong appearance. Loaded synchronously in <head> (CSP-safe, no inline code).
(function () {
    var key = "physiquinator-theme-preference";
    var preference = localStorage.getItem(key) || "system";
    var isDark = window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches;
    var theme = preference === "system" ? (isDark ? "dark" : "light") : preference;
    document.documentElement.setAttribute("data-theme", theme);
})();
