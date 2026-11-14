// Chat behavior: tap a bubble on touch to reveal its actions,
// follow streamed tokens only while the reader stays near the bottom.
(function () {
    'use strict';
    function area() { return document.getElementById('ai-messages-scroll-area'); }
    function isNearBottom(el) {
        if (!el) return true;
        return el.scrollHeight - el.scrollTop - el.clientHeight < 120;
    }
    function hideJump(scope) {
        var root = scope || document;
        var btn = root.querySelector ? root.querySelector('.ai-jump-latest') : null;
        if (btn) btn.hidden = true;
    }
    function showJump(el) {
        var btn = el.querySelector(':scope > .ai-jump-latest');
        if (!btn) {
            btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'ai-jump-latest';
            btn.textContent = '↓ Latest';
            btn.setAttribute('aria-label', 'Jump to latest message');
            btn.addEventListener('click', function () { scrollToBottom(); });
            el.appendChild(btn);
        }
        btn.hidden = false;
    }
    function scrollToBottom() {
        try {
            var el = area();
            if (!el) return;
            hideJump(el);
            el.scrollTop = el.scrollHeight;
        } catch { /* best effort */ }
    }
    function scrollIfNear() {
        try {
            var el = area();
            if (!el) return;
            if (isNearBottom(el)) { el.scrollTop = el.scrollHeight; hideJump(el); }
            else { showJump(el); }
        } catch { /* best effort */ }
    }
    function initScrollArea() {
        var scrollArea = area();
        if (scrollArea) init(scrollArea);
        initGlobalDismiss();
    }
    function init(scrollArea) {
        if (!scrollArea || scrollArea.dataset.physiquinatorAiChat === '1') return;
        scrollArea.dataset.physiquinatorAiChat = '1';
        var interactive = 'button, a, input, textarea, select, [role="button"], [contenteditable="true"], .mud-popover, .mud-menu-item';
        var downX = 0, downY = 0;
        scrollArea.addEventListener('pointerdown', function (e) {
            try {
                if (e.pointerType !== 'touch' && e.pointerType !== 'pen') return;
                downX = e.clientX; downY = e.clientY;
            } catch { /* never break taps */ }
        }, { passive: true });
        scrollArea.addEventListener('pointerup', function (e) {
            try {
                if (e.pointerType !== 'touch' && e.pointerType !== 'pen') return;
                var dx = e.clientX - downX, dy = e.clientY - downY;
                if (dx * dx + dy * dy > 144) return;
                var t = e.target;
                if (!t || !t.closest) return;
                if (t.closest(interactive)) return;
                var message = t.closest('.ai-msg');
                if (!message || !scrollArea.contains(message)) return;
                var wasOpen = message.classList.contains('show-actions');
                var open = scrollArea.querySelectorAll('.ai-message.show-actions, .ai-msg.show-actions');
                for (var i = 0; i < open.length; i++) open[i].classList.remove('show-actions');
                if (!wasOpen) message.classList.add('show-actions');
            } catch { /* never break taps */ }
        });
        scrollArea.addEventListener('scroll', function () {
            try { if (isNearBottom(scrollArea)) hideJump(scrollArea); } catch { /* hint is best effort */ }
        }, { passive: true });
    }
    function initGlobalDismiss() {
        try {
            if (document.documentElement.dataset.physiquinatorAiDismiss === '1') return;
            document.documentElement.dataset.physiquinatorAiDismiss = '1';
            var downX = 0, downY = 0;
            document.addEventListener('pointerdown', function (e) {
                try {
                    if (e.pointerType !== 'touch' && e.pointerType !== 'pen') return;
                    downX = e.clientX; downY = e.clientY;
                } catch { /* never break taps */ }
            }, { passive: true });
            document.addEventListener('pointerup', function (e) {
                try {
                    if (e.pointerType !== 'touch' && e.pointerType !== 'pen') return;
                    var dx = e.clientX - downX, dy = e.clientY - downY;
                    if (dx * dx + dy * dy > 144) return;
                    var t = e.target;
                    if (!t || !t.closest) return;
                    if (t.closest('.ai-msg, .ai-message')) return;
                    var open = document.querySelectorAll('.ai-msg.show-actions, .ai-message.show-actions');
                    for (var i = 0; i < open.length; i++) open[i].classList.remove('show-actions');
                } catch { /* dismiss is best effort */ }
            }, { passive: true });
        } catch { /* dismiss is best effort */ }
    }
    window.physiquinatorAiChat = {
        initScrollArea: initScrollArea,
        scrollToBottom: scrollToBottom,
        scrollIfNear: scrollIfNear,
        isNearBottom: isNearBottom
    };
    if (window.physiquinatorHelpers) {
        window.physiquinatorHelpers.scrollAiToBottom = scrollToBottom;
        window.physiquinatorHelpers.scrollAiIfNear = scrollIfNear;
    }
})();
