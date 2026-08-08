// Small shared helpers used by the Blazor UI (back fallback, chat scrolling, clipboard).
(function () {
    'use strict';

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
})();
