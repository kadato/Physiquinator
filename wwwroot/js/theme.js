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
                animation: 150,
                delay: 80,
                delayOnTouchOnly: true,
                touchStartThreshold: 6,
                forceFallback: true,
                fallbackTolerance: 8,
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
