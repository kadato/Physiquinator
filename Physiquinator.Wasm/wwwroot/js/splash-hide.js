// Hides the startup splash as soon as Blazor renders real content into #app.
(function () {
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
})();
