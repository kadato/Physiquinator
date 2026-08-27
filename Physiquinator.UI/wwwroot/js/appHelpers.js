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

    // Fix MudBlazor's internal icon buttons that lack an accessible name.
    // The autocomplete dropdown toggle (mud-icon-button-edge-end) renders as a
    // plain <button> with an SVG icon and no aria-label, which fails axe
    // button-name and Lighthouse accessibility. Add a label when missing.
    function fixMudIconButtons(root) {
        try {
            var scope = root && root.querySelectorAll ? root : document;
            scope.querySelectorAll('button.mud-icon-button-edge-end:not([aria-label])').forEach(function (btn) {
                // Mark as toggle for the autocomplete/adornment that opened it.
                // Visible text is not required when aria-label is present.
                btn.setAttribute('aria-label', 'Toggle');
            });
            // Also cover MudAutocomplete's clear button if present
            scope.querySelectorAll('button.mud-icon-button.mud-input-adornment-icon-button:not([aria-label])').forEach(function (btn) {
                if (!btn.getAttribute('aria-label')) btn.setAttribute('aria-label', 'Clear');
            });
        } catch {}
    }
    fixMudIconButtons(document);
    try {
        var observer = new MutationObserver(function (mutations) {
            for (var i=0;i<mutations.length;i++) {
                var m = mutations[i];
                if (m.type === 'childList') {
                    m.addedNodes.forEach(function (n) {
                        if (n.nodeType === 1) fixMudIconButtons(n);
                    });
                }
            }
        });
        observer.observe(document.documentElement, { childList: true, subtree: true });
    } catch {}
})();
