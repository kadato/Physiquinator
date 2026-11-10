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
        },

        // Named entry points for work Blazor used to do through eval().
        // Calling eval is blocked by the Content-Security-Policy, so every
        // dynamic snippet needs a real function here instead.
        readPrefEntries: function (prefix) {
            try {
                var out = {};
                for (var i = 0; i < window.localStorage.length; i++) {
                    var key = window.localStorage.key(i);
                    if (key && key.indexOf(prefix) === 0) out[key] = window.localStorage.getItem(key);
                }
                return JSON.stringify(out);
            } catch (e) {
                return '';
            }
        },

        writePrefEntry: function (key, value) {
            try {
                window.localStorage.setItem(key, value);
            } catch (e) {
                /* ignore: the in-memory copy keeps this session working */
            }
        },

        clearLocalStorage: function () {
            try {
                window.localStorage.clear();
            } catch (e) {
                /* ignore */
            }
        },

        reloadPage: function () {
            window.location.reload();
        },

        isSortableLoaded: function () {
            return typeof Sortable !== 'undefined';
        },

        loadScript: function (src) {
            return new Promise(function (resolve, reject) {
                var s = document.createElement('script');
                s.src = src;
                s.onload = function () { resolve(true); };
                s.onerror = function () { reject(new Error('script load failed: ' + src)); };
                document.head.appendChild(s);
            });
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

    // App-wide back handling for the phone gesture and hardware back.
    // The MAUI activity calls window.physiquinatorBack.consume(). It expects true
    // when the app handled the press. Dialogs close first. Then the armed
    // workout or plan guard runs. Then the registered Blazor parent navigation runs.
    // Root tabs return false so the system can exit or background the app.
    window.physiquinatorBack = window.physiquinatorBack || {};
    (function (back) {
        if (back.__appInit) return;
        back.__appInit = true;
        var appRef = null;
        back.registerAppBackHandler = function (dotNetRef) { appRef = dotNetRef; };
        back.unregisterAppBackHandler = function () { appRef = null; };

        function sendEscape() {
            try {
                var init = { key: 'Escape', code: 'Escape', keyCode: 27, bubbles: true, cancelable: true };
                document.dispatchEvent(new KeyboardEvent('keydown', init));
                document.dispatchEvent(new KeyboardEvent('keyup', init));
            } catch {}
        }

        back.closeTopLayer = function () {
            try {
                var pop = document.querySelector('.mud-popover.mud-popover-open, .mud-popover-open, .mud-menu-popover, .mud-select-popover, .mud-autocomplete-popover');
                if (pop) { sendEscape(); return true; }
                var dlg = document.querySelector('.mud-dialog-container');
                if (dlg) { sendEscape(); return true; }
            } catch {}
            return false;
        };

        back.isRootPath = function () {
            try {
                var p = (window.location.pathname || '/').replace(/^\/+|\/+$/g, '').toLowerCase();
                return p === '' || p === 'history' || p === 'settings';
            } catch (e) { return false; }
        };

        back.consume = function () {
            // A dialog, menu, or select popover always wins. Close the top layer. Stay put.
            if (back.closeTopLayer()) return true;
            try {
                if (typeof back._workoutConsume === 'function' && back._workoutConsume()) return true;
            } catch {}
            try {
                if (back.isRootPath()) return false;
            } catch {}
            if (appRef) {
                try { appRef.invokeMethodAsync('OnAppBackRequested'); } catch {}
                return true;
            }
            return false;
        };
    })(window.physiquinatorBack);

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
