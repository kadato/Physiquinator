// Early theme bootstrap: sets data-theme before first paint to avoid a flash of
// the wrong appearance. Loaded synchronously in <head> (CSP-safe, no inline code).
(function () {
    var key = "physiquinator-theme-preference";
    var preference = localStorage.getItem(key) || "system";
    var isDark = window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches;
    var theme = preference === "system" ? (isDark ? "dark" : "light") : preference;
    var lightThemes = { "light": 1, "tokyo-night-light": 1, "solarized-light": 1, "github-light": 1 };
    var mode = lightThemes[theme] ? "light" : "dark";
    document.documentElement.setAttribute("data-theme", theme);
    document.documentElement.setAttribute("data-theme-mode", mode);
})();

// Fallback for browsers without :has() - hide the HTML splash when Blazor renders.
(function () {
    function setupSplashFallback() {
        var app = document.getElementById('app');
        if (!app) return;
        function hide() {
            for (var i = 0; i < app.children.length; i++) {
                if (!app.children[i].classList.contains('app-splash')) {
                    var s = app.querySelector('.app-splash');
                    if (s) s.style.display = 'none';
                    return true;
                }
            }
            return false;
        }
        if (hide()) return;
        new MutationObserver(hide).observe(app, { childList: true });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', setupSplashFallback);
    } else {
        setupSplashFallback();
    }
})();

