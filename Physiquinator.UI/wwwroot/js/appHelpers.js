// Small shared helpers used by the Blazor UI (back fallback, chat scrolling, clipboard).
(function () {
    'use strict';

    // Selects the full value when a stepper edit input gains focus.
    window.physiquinator = window.physiquinator || {};
    window.physiquinator.selectInput = function (element) {
        try {
            if (element && typeof element.select === 'function') element.select();
        } catch {
            /* ignore */
        }
    };

    window.physiquinatorHelpers = {
        goBackOrHome: function () {
            if (window.history.length > 1) {
                window.history.back();
            } else {
                window.location.assign('/');
            }
        },

        scrollToBottom: function (element) {
            if (element) {
                element.scrollTop = element.scrollHeight;
            }
        },

        copyText: function (text) {
            if (navigator.clipboard && navigator.clipboard.writeText) {
                return navigator.clipboard.writeText(text).then(
                    function () { return true; },
                    function () { return fallbackCopy(text); });
            }
            return Promise.resolve(fallbackCopy(text));
        }
    };

    function fallbackCopy(text) {
        try {
            var textarea = document.createElement('textarea');
            textarea.value = text;
            textarea.style.position = 'fixed';
            textarea.style.opacity = '0';
            document.body.appendChild(textarea);
            textarea.select();
            var ok = document.execCommand('copy');
            document.body.removeChild(textarea);
            return ok;
        } catch (e) {
            return false;
        }
    }

    // On-screen keyboard tracking. With Android edge-to-edge (API 35+) the
    // window is no longer resized for the IME, so the WebView keeps its full
    // height and the keyboard would cover bottom-anchored chrome (nav pill,
    // FABs, undo button) and dialogs. Expose how much of the app the keyboard
    // covers as --app-ime-inset plus the .app-ime-open root class. The CSS in
    // app-overrides.css lifts the affected surfaces above it. When the WebView
    // resizes normally (adjustResize, iOS), visualViewport.height equals
    // innerHeight, the inset is 0 and nothing moves.
    var imeRaf = 0;
    var lastInset = -1;
    function updateImeInset() {
        var vv = window.visualViewport;
        if (!vv) return;
        var inset = Math.max(0, Math.round(window.innerHeight - vv.height - vv.offsetTop));
        // Only update when the inset actually changed by more than 1px, to avoid
        // visualViewport scroll jitter from thrashing --app-ime-inset on every scroll tick.
        if (Math.abs(inset - lastInset) <= 1) return;
        lastInset = inset;
        if (imeRaf) cancelAnimationFrame(imeRaf);
        imeRaf = requestAnimationFrame(function () {
            imeRaf = 0;
            document.documentElement.style.setProperty('--app-ime-inset', inset + 'px');
            document.documentElement.classList.toggle('app-ime-open', inset > 24);
        });
    }

    var vv = window.visualViewport;
    if (vv) {
        vv.addEventListener('resize', updateImeInset);
    }
    updateImeInset();
})();
