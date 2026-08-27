(() => {
    window.physiquinator = window.physiquinator || {};
    /** Scroll horizontal heatmap so the most recent week is visible (mobile). */
    window.physiquinator.scrollHeatmapToEnd = (el) => {
        if (!el) return;
        try {
            requestAnimationFrame(() => {
                requestAnimationFrame(() => {
                    if (el.scrollWidth > el.clientWidth) {
                        el.scrollLeft = el.scrollWidth - el.clientWidth;
                    }
                });
            });
        } catch {
            /* ignore */
        }
    };

    /** Move keyboard focus to a heatmap cell by its roving-tabindex index. */
    window.physiquinator.focusHeatmapCell = (scrollEl, index) => {
        try {
            const root = scrollEl instanceof Element ? scrollEl : document.querySelector(".heatmap-scroll");
            const cell = root && root.querySelector(`[data-hm-index="${index}"]`);
            if (cell) {
                cell.focus({ preventScroll: true });
                cell.scrollIntoView({ block: "nearest", inline: "nearest" });
            }
        } catch {
            /* ignore */
        }
    };

    /** Skip link target: move real focus into main content. */
    window.physiquinator.focusMain = () => {
        try {
            const main = document.getElementById("main-content");
            if (main) {
                main.focus({ preventScroll: false });
            }
        } catch {
            /* ignore */
        }
    };

    /** Scroll an element or selector into view with a delay to accommodate visual viewport keyboard resizing */
    window.physiquinator.scrollSelectorIntoView = (elOrSelector) => {
        try {
            setTimeout(() => {
                const el = typeof elOrSelector === "string"
                    ? document.querySelector(elOrSelector)
                    : elOrSelector;
                if (el) {
                    el.scrollIntoView({ behavior: "smooth", block: "nearest" });
                }
            }, 300);
        } catch {
            /* ignore */
        }
    };

    /** Bring the active exercise card back under the thumb when rest ends.
        nearest avoids the page jumping to center when the card is already visible. */
    window.physiquinator.scrollExerciseCardIntoView = (exerciseIndex) => {
        try {
            const el = document.querySelector(`[data-exercise-index="${exerciseIndex}"]`);
            if (!el) return;
            const reduce = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
            el.scrollIntoView({ behavior: reduce ? "auto" : "smooth", block: "nearest" });
        } catch {
            /* ignore */
        }
    };

    /** Frame the rest timer when it takes the stage after a logged set.
        Double rAF waits one paint so the panel exists before measuring.
        block:nearest scrolls only when the timer is not already visible. */
    window.physiquinator.scrollRestTimerIntoView = () => {
        try {
            requestAnimationFrame(() => requestAnimationFrame(() => {
                const el = document.querySelector(".workout-top");
                if (!el) return;
                const reduce = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
                el.scrollIntoView({ behavior: reduce ? "auto" : "smooth", block: "nearest" });
            }));
        } catch {
            /* ignore */
        }
    };

    const storageKey = "physiquinator-theme-preference";
    let dotNetRef = null;
    let mediaQuery = null;
    let mediaListener = null;

    const getPreference = () => localStorage.getItem(storageKey) || "system";
    const getSystemTheme = () =>
        window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
    const resolveEffective = (preference) =>
        preference === "system" ? getSystemTheme() : preference;

    const applyTheme = (theme) => {
        document.documentElement.setAttribute("data-theme", theme);
    };

    const notify = (theme) => {
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync("OnSystemThemeChanged", theme);
        }
    };

    window.physiquinatorTheme = {
        initialize: (ref, suffix = "") => {
            dotNetRef = ref || null;
            const fullKey = storageKey + suffix;
            const preference = localStorage.getItem(fullKey) || "system";
            localStorage.setItem(storageKey, preference);
            const effective = resolveEffective(preference);
            applyTheme(effective);

            if (!mediaQuery) {
                mediaQuery = window.matchMedia("(prefers-color-scheme: dark)");
                mediaListener = (event) => {
                    const next = event.matches ? "dark" : "light";
                    if (getPreference() === "system") {
                        applyTheme(next);
                        notify(next);
                    }
                };

                if (mediaQuery.addEventListener) {
                    mediaQuery.addEventListener("change", mediaListener);
                } else {
                    mediaQuery.addListener(mediaListener);
                }
            }

            return { preference, effective };
        },
        setPreference: (preference, suffix = "") => {
            const fullKey = storageKey + suffix;
            localStorage.setItem(fullKey, preference);
            localStorage.setItem(storageKey, preference);
            const effective = resolveEffective(preference);
            applyTheme(effective);
            return effective;
        },
        /** Clears saved theme choice so the app follows the system appearance again. */
        resetStoredPreferenceToSystem: (suffix = "") => {
            const fullKey = storageKey + suffix;
            try {
                localStorage.removeItem(fullKey);
                localStorage.removeItem(storageKey);
            } catch {
                /* ignore */
            }
            const preference = "system";
            const effective = resolveEffective(preference);
            applyTheme(effective);
            return { preference, effective };
        },
        dispose: () => {
            if (mediaQuery && mediaListener) {
                if (mediaQuery.removeEventListener) {
                    mediaQuery.removeEventListener("change", mediaListener);
                } else {
                    mediaQuery.removeListener(mediaListener);
                }
            }
            mediaQuery = null;
            mediaListener = null;
            dotNetRef = null;
        }
    };

    function revertDomOrder(evt) {
        const parent = evt.from;
        const item = evt.item;
        const oldIndex = evt.oldIndex;
        const newIndex = evt.newIndex;
        if (oldIndex === newIndex || oldIndex == null || newIndex == null) {
            return;
        }
        if (oldIndex < newIndex) {
            parent.insertBefore(item, parent.children[oldIndex]);
        } else {
            parent.insertBefore(item, parent.children[oldIndex + 1]);
        }
    }

    window.planReorder = {
        sortable: null,
        init: function (listId, dotNetRef) {
            const el = document.getElementById(listId);
            if (!el || typeof Sortable === "undefined") {
                return false;
            }
            this.destroy();
            this.sortable = Sortable.create(el, {
                handle: ".plan-exercise-handle",
                animation: 0,
                delay: 140,
                delayOnTouchOnly: true,
                touchStartThreshold: 12,
                forceFallback: true,
                fallbackTolerance: 12,
                swapThreshold: 0.65,
                scroll: true,
                bubbleScroll: true,
                scrollSensitivity: 40,
                scrollSpeed: 12,
                fallbackClass: "plan-exercise-row--fallback",
                ghostClass: "plan-exercise-row--ghost",
                chosenClass: "plan-exercise-row--chosen",
                dragClass: "plan-exercise-row--drag",
                draggable: ".plan-exercise-row",
                onEnd: function (evt) {
                    if (evt.oldIndex === evt.newIndex) {
                        return;
                    }
                    revertDomOrder(evt);
                    dotNetRef.invokeMethodAsync("OnExerciseReordered", evt.oldIndex, evt.newIndex);
                }
            });
            return true;
        },
        destroy: function () {
            if (this.sortable) {
                this.sortable.destroy();
                this.sortable = null;
            }
        }
    };

    window.homePlansReorder = {
        sortable: null,
        init: function (listId, dotNetRef) {
            const el = document.getElementById(listId);
            if (!el || typeof Sortable === "undefined") {
                return false;
            }
            this.destroy();
            this.sortable = Sortable.create(el, {
                handle: ".plan-card-handle",
                animation: 0,
                delay: 140,
                delayOnTouchOnly: true,
                touchStartThreshold: 12,
                forceFallback: true,
                fallbackTolerance: 12,
                swapThreshold: 0.65,
                scroll: true,
                bubbleScroll: true,
                scrollSensitivity: 40,
                scrollSpeed: 12,
                fallbackClass: "plan-card--fallback",
                ghostClass: "plan-card--ghost",
                chosenClass: "plan-card--chosen",
                dragClass: "plan-card--drag",
                draggable: ".plan-card",
                onEnd: function (evt) {
                    if (evt.oldIndex === evt.newIndex) {
                        return;
                    }
                    revertDomOrder(evt);
                    dotNetRef.invokeMethodAsync("OnPlanReordered", evt.oldIndex, evt.newIndex);
                }
            });
            return true;
        },
        destroy: function () {
            if (this.sortable) {
                this.sortable.destroy();
                this.sortable = null;
            }
        }
    };

    // Global handler to submit dialogs when pressing Enter on mobile keyboards
    document.addEventListener("keydown", (e) => {
        if (e.key === "Enter") {
            const activeEl = document.activeElement;
            if (activeEl && activeEl.tagName === "INPUT" && activeEl.type !== "submit" && activeEl.type !== "button") {
                // If it is inside a form, standard form submission will handle it
                if (activeEl.closest("form")) {
                    return;
                }
                
                // If inside a MudDialog, find and trigger the primary action button
                const dialog = activeEl.closest(".mud-dialog");
                if (dialog) {
                    const primaryBtn = dialog.querySelector(".mud-dialog-actions .mud-button-filled-primary, .mud-dialog-actions button[type='submit']")
                        || dialog.querySelector(".mud-button-filled-primary")
                        || dialog.querySelector("button[type='submit']");
                    if (primaryBtn && !primaryBtn.disabled) {
                        e.preventDefault();
                        primaryBtn.click();
                    }
                }
            }
        }
    });

    // Dismiss MudBlazor snackbars on click
    document.addEventListener("click", (e) => {
        const snackbar = e.target.closest(".mud-snackbar");
        if (!snackbar) return;

        // If clicking a button, link, or interactive element inside the snackbar, let it propagate normally.
        const isInteractive = e.target.closest("button") || e.target.closest("a") || e.target.closest(".mud-button-root");
        if (isInteractive) return;

        // Otherwise, click the close button to dismiss the snackbar
        const closeBtn = snackbar.querySelector(".mud-snackbar-close-button");
        if (closeBtn) {
            closeBtn.click();
        }
    });
})();
