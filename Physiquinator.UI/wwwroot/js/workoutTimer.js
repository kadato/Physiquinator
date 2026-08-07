let sharedCtx = null;
let restTimerActive = false;
let restTimerId = null;
let rafId = null;
let restStartTime = 0;
let restTotalMs = 0;
let chainGeneration = 0;

// Selects the full value when a stepper edit input gains focus (moved out of
// the inline onfocus attribute for CSP hygiene). Loaded with this module.
window.physiquinator = window.physiquinator || {};
window.physiquinator.selectInput = (element) => {
    try {
        if (element && typeof element.select === 'function') element.select();
    } catch {
        /* ignore */
    }
};

// ---- Keep-screen-on (wake lock) ------------------------------------------
let wakeLockSentinel = null;
let keepScreenOnWanted = false;

export async function setKeepScreenOn(enabled) {
    keepScreenOnWanted = !!enabled;
    if (!keepScreenOnWanted) {
        releaseWakeLock();
        return;
    }
    await requestWakeLock();
}

async function requestWakeLock() {
    if (!keepScreenOnWanted || !('wakeLock' in navigator)) return;
    try {
        if (wakeLockSentinel && !wakeLockSentinel.released) return;
        wakeLockSentinel = await navigator.wakeLock.request('screen');
        wakeLockSentinel.addEventListener('release', () => {
            wakeLockSentinel = null;
        });
    } catch {
        // Unsupported or denied; the screen just may sleep.
        wakeLockSentinel = null;
    }
}

function releaseWakeLock() {
    try {
        if (wakeLockSentinel && !wakeLockSentinel.released) {
            wakeLockSentinel.release();
        }
    } catch {
        /* ignore */
    }
    wakeLockSentinel = null;
}

// Browsers drop the lock when the tab is hidden; re-request on return while
// a workout still wants the screen on.
document.addEventListener('visibilitychange', () => {
    if (keepScreenOnWanted && document.visibilityState === 'visible') {
        requestWakeLock();
    }
});

export function startRestTimer(dotNetRef, intervalMs, totalMs, activeDurationMs, continueMode) {
    if (!totalMs || totalMs <= 0) {
        stopRestTimer();
        return;
    }

    if (restTimerActive && restTimerId !== null) {
        if (continueMode) {
            // Extension (+N s), overlay action, or routine sync: continue from
            // the bar's current position and reach 100% exactly when the new
            // remaining time elapses. Using elapsed + remaining as the scale
            // keeps this correct even when the remaining time exceeds the
            // original interval (adding more than the default rest time).
            const currentProgress = Math.min(Math.max((performance.now() - restStartTime) / restTotalMs, 0), 1);
            const elapsedMs = currentProgress * restTotalMs;
            restTotalMs = elapsedMs + totalMs;
            restStartTime = performance.now() - elapsedMs;
        } else {
            // Fresh/restart/reset: re-anchor to the true fraction of the
            // active interval (0 for a fresh rest or reset).
            const activeMs = Math.max(activeDurationMs || totalMs, 1);
            const fraction = Math.min(Math.max(1 - totalMs / activeMs, 0), 1);
            restTotalMs = activeMs;
            restStartTime = performance.now() - fraction * activeMs;
        }
        return;
    }

    stopRestTimer();
    restTimerActive = true;
    chainGeneration++;
    if (continueMode) {
        // Re-arm with no live animation: start from the true fraction of the
        // active interval.
        const activeMs = Math.max(activeDurationMs || totalMs, 1);
        const fraction = Math.min(Math.max(1 - totalMs / activeMs, 0), 1);
        restTotalMs = activeMs;
        restStartTime = performance.now() - fraction * activeMs;
    } else {
        restTotalMs = Math.max(activeDurationMs || totalMs, 1);
        restStartTime = performance.now();
    }

    startProgressRaf();
    scheduleTick(dotNetRef, intervalMs);
}

function startProgressRaf() {
    function update() {
        if (!restTimerActive) return;
        const elapsed = performance.now() - restStartTime;
        const progress = Math.min(elapsed / restTotalMs, 1);

        const fill = document.querySelector('.rest-timer-edge-fill');
        if (fill) {
            fill.style.transform = `scaleX(${progress})`;
        }

        if (progress < 1) {
            rafId = requestAnimationFrame(update);
        }
    }
    rafId = requestAnimationFrame(update);
}

let undoKeyHandler = null;

export function registerUndoKeyHandler(dotNetRef) {
    unregisterUndoKeyHandler();
    undoKeyHandler = async (e) => {
        const isModifierHeld = e.ctrlKey || e.metaKey;
        if (!isModifierHeld || e.shiftKey || e.altKey) return;
        if ((e.key || '').toLowerCase() !== 'z') return;

        const target = e.target;
        if (target && (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.isContentEditable))
            return;

        e.preventDefault();
        try {
            await dotNetRef.invokeMethodAsync('OnUndoKeyDown');
        } catch {
            // transient interop failure (e.g. WebView teardown); ignore
        }
    };
    window.addEventListener('keydown', undoKeyHandler, true);
}

export function unregisterUndoKeyHandler() {
    if (!undoKeyHandler) return;
    window.removeEventListener('keydown', undoKeyHandler, true);
    undoKeyHandler = null;
}

// ---- Active-workout back guard -------------------------------------------
// A guard history entry is pushed while a workout runs. Browser back, the
// MAUI WebView back mapping, or a back gesture pops it; the guard asks .NET
// whether to leave and re-arms itself when the user stays. The Android
// activity consults window.physiquinatorBack.consume() for hardware back.
const backGuardState = { physiquinatorBackGuard: true };
let backGuardRef = null;
let backGuardActive = false;
let backGuardBusy = false;
let backGuardHandler = null;
// Set before our own history.back() in unregisterBackHandler: the browser
// delivers that pop asynchronously, after a replacement listener may already
// be registered (the workout page re-arms its guard on a second mount), and
// it must not pop up the "Leave workout?" dialog on page load.
let suppressOwnPop = false;

export function registerBackHandler(dotNetRef) {
    unregisterBackHandler();
    backGuardRef = dotNetRef;
    backGuardActive = true;
    backGuardBusy = false;
    window.history.pushState(backGuardState, '');
    backGuardHandler = () => onBackGuardPopState();
    window.addEventListener('popstate', backGuardHandler);
}

export function unregisterBackHandler() {
    backGuardActive = false;
    backGuardBusy = false;
    backGuardRef = null;
    if (backGuardHandler) {
        window.removeEventListener('popstate', backGuardHandler);
        backGuardHandler = null;
    }
    // Pop the guard entry so a later back press does not land on it.
    if (window.history.state && window.history.state.physiquinatorBackGuard) {
        suppressOwnPop = true;
        window.history.back();
    }
}

async function confirmLeaveWorkout() {
    if (!backGuardRef) return true;
    try {
        return await backGuardRef.invokeMethodAsync('OnLeaveWorkoutRequested');
    } catch {
        // Component torn down mid-prompt; do not trap the user.
        return true;
    }
}

async function onBackGuardPopState() {
    // Pop caused by our own unregisterBackHandler (delivered asynchronously,
    // possibly through a replacement listener): not a user back press.
    if (suppressOwnPop) {
        suppressOwnPop = false;
        return;
    }
    // In-app Blazor navigation never fires popstate, so any pop while the
    // guard is armed is a user-initiated back press. The guard entry sits on
    // top of the workout entry (same URL), so the first pop lands on the
    // workout entry itself — that is when the confirmation is shown.
    if (!backGuardActive) return;
    if (backGuardBusy) {
        // A prompt is already open; keep the guard entry so the next back
        // cannot silently navigate past it.
        window.history.pushState(backGuardState, '');
        return;
    }
    backGuardBusy = true;
    const leave = await confirmLeaveWorkout();
    backGuardBusy = false;
    if (!leave) {
        window.history.pushState(backGuardState, '');
    }
}

window.physiquinatorBack = {
    // Invoked from the Android activity on hardware back. Returns true when
    // the guard took over (or is busy); false lets the system fall back to
    // its default back behavior.
    consume: function () {
        if (!backGuardActive) return false;
        if (backGuardBusy) return true;
        backGuardBusy = true;
        confirmLeaveWorkout()
            .then((leave) => {
                backGuardBusy = false;
                if (!leave) window.history.pushState(backGuardState, '');
            })
            .catch(() => {
                backGuardBusy = false;
            });
        return true;
    }
};

export function stopRestTimer() {
    restTimerActive = false;
    if (rafId !== null) {
        cancelAnimationFrame(rafId);
        rafId = null;
    }
    if (restTimerId !== null) {
        clearTimeout(restTimerId);
        restTimerId = null;
    }
}

function scheduleTick(dotNetRef, intervalMs) {
    const generation = chainGeneration;
    restTimerId = setTimeout(async () => {
        if (!restTimerActive || generation !== chainGeneration) return;
        try {
            const done = await dotNetRef.invokeMethodAsync('OnTimerTick');
            if (!restTimerActive || generation !== chainGeneration) return;
            if (!done)
                scheduleTick(dotNetRef, intervalMs);
            else
                restTimerActive = false;
        } catch {
            // A transient interop failure (e.g. the WebView was suspended
            // while the app was backgrounded under the overlay) must not kill
            // the countdown permanently. Retry the next tick instead.
            if (restTimerActive && generation === chainGeneration)
                scheduleTick(dotNetRef, intervalMs);
        }
    }, intervalMs);
}

function getAudioContext() {
    if (!sharedCtx || sharedCtx.state === 'closed') {
        sharedCtx = new (window.AudioContext || window.webkitAudioContext)();
    }
    if (sharedCtx.state === 'suspended') {
        sharedCtx.resume();
    }
    return sharedCtx;
}

export function unlockAudioContext() {
    try {
        const ctx = getAudioContext();
        if (ctx.state === 'suspended') {
            ctx.resume();
        }
    } catch {
        /* ignore */
    }
}

export function playRestCompleteSound() {
    try {
        const audioContext = getAudioContext();
        const playKnock = (startTime) => {
            const duration = 0.28;
            const oscillator = audioContext.createOscillator();
            const gainNode = audioContext.createGain();
            oscillator.connect(gainNode);
            gainNode.connect(audioContext.destination);

            // Deep knock: low frequency dropping further down with a fast
            // percussive decay and a body harmonic for weight.
            oscillator.type = 'sine';
            oscillator.frequency.setValueAtTime(130, startTime);
            oscillator.frequency.exponentialRampToValueAtTime(80, startTime + 0.09);

            gainNode.gain.setValueAtTime(0.0001, startTime);
            gainNode.gain.exponentialRampToValueAtTime(0.6, startTime + 0.006);
            gainNode.gain.exponentialRampToValueAtTime(0.0001, startTime + duration);

            oscillator.start(startTime);
            oscillator.stop(startTime + duration);
            oscillator.onended = () => gainNode.disconnect();

            const harmonic = audioContext.createOscillator();
            const harmonicGain = audioContext.createGain();
            harmonic.connect(harmonicGain);
            harmonicGain.connect(audioContext.destination);
            harmonic.type = 'sine';
            harmonic.frequency.setValueAtTime(260, startTime);
            harmonic.frequency.exponentialRampToValueAtTime(160, startTime + 0.09);
            harmonicGain.gain.setValueAtTime(0.0001, startTime);
            harmonicGain.gain.exponentialRampToValueAtTime(0.22, startTime + 0.006);
            harmonicGain.gain.exponentialRampToValueAtTime(0.0001, startTime + duration);
            harmonic.start(startTime);
            harmonic.stop(startTime + duration);
            harmonic.onended = () => harmonicGain.disconnect();
        };

        // Knock knock: two deep thumps close together.
        const now = audioContext.currentTime;
        playKnock(now);
        playKnock(now + 0.32);
    } catch (error) {
        console.warn('Audio playback failed:', error);
    }
}
