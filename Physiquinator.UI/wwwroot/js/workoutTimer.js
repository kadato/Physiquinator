let sharedCtx = null;
let restTimerActive = false;
let restTimerId = null;
let chainGeneration = 0;

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
        // Unsupported or denied. The screen just may sleep.
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

// Browsers drop the lock when the tab is hidden. Re-request on return while
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
        chainGeneration++;
        scheduleTick(dotNetRef, intervalMs);
        return;
    }

    stopRestTimer();
    restTimerActive = true;
    chainGeneration++;
    scheduleTick(dotNetRef, intervalMs);
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
            // transient interop failure (for example, WebView teardown). Ignore
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
// MAUI WebView back mapping, or a back gesture pops it. The guard asks .NET
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
// One-shot listener and fallback timer for the pop queued by the async
// unregisterBackHandler below. Module-level so register can cancel a stale
// wait instead of letting it shield or confuse a fresh guard.
let ownPopHandler = null;
let ownPopTimer = null;

function hasGuardEntry() {
    try {
        return !!(window.history.state && window.history.state.physiquinatorBackGuard);
    } catch {
        return false;
    }
}

function disarmGuard() {
    backGuardActive = false;
    backGuardBusy = false;
    backGuardRef = null;
    if (backGuardHandler) {
        window.removeEventListener('popstate', backGuardHandler);
        backGuardHandler = null;
    }
    if (ownPopHandler) {
        window.removeEventListener('popstate', ownPopHandler);
        ownPopHandler = null;
    }
    if (ownPopTimer !== null) {
        clearTimeout(ownPopTimer);
        ownPopTimer = null;
    }
}

export function registerBackHandler(dotNetRef) {
    disarmGuard();
    // A stale guard entry (a previous page never popped it) is neutralized in
    // place. Popping it here would remove the entry this call is about to
    // push, so clear the flag synchronously instead.
    if (hasGuardEntry()) {
        try { window.history.replaceState(null, ''); } catch {}
    }
    suppressOwnPop = false;
    backGuardRef = dotNetRef;
    backGuardActive = true;
    backGuardBusy = false;
    window.history.pushState(backGuardState, '');
    backGuardHandler = () => onBackGuardPopState();
    window.addEventListener('popstate', backGuardHandler);
}

export function unregisterBackHandler() {
    disarmGuard();
    // Pop the guard entry so a later back press does not land on it.
    // history.back() is asynchronous: the pop lands after this call returns,
    // so callers MUST await the returned promise before navigating. An early
    // NavigateTo lets the queued pop fire after the navigation and drags
    // Blazor back onto the guarded page, which made discard/close look like
    // it silently did nothing.
    if (hasGuardEntry()) {
        return new Promise((resolve) => {
            let settled = false;
            const finish = () => {
                if (settled) return;
                settled = true;
                suppressOwnPop = false;
                if (ownPopHandler) {
                    window.removeEventListener('popstate', ownPopHandler);
                    ownPopHandler = null;
                }
                if (ownPopTimer !== null) {
                    clearTimeout(ownPopTimer);
                    ownPopTimer = null;
                }
                resolve();
            };
            suppressOwnPop = true;
            ownPopHandler = (e) => {
                // Shield a guard registered after us (rapid remount): the
                // queued pop is ours, not a user back press.
                try { e.stopImmediatePropagation(); } catch {}
                finish();
            };
            window.addEventListener('popstate', ownPopHandler);
            try {
                window.history.back();
            } catch {
                finish();
                return;
            }
            // If the pop never arrives (teardown, non-standard WebView),
            // do not trap the caller forever.
            ownPopTimer = setTimeout(finish, 400);
        });
    }
    suppressOwnPop = false;
    return Promise.resolve();
}

async function confirmLeaveWorkout() {
    if (!backGuardRef) return true;
    try {
        return await backGuardRef.invokeMethodAsync('OnLeaveWorkoutRequested');
    } catch {
        // Component torn down mid-prompt. Do not trap the user.
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
    // workout entry itself, and that is when the confirmation is shown.
    if (!backGuardActive) return;
    if (backGuardBusy) {
        // A prompt is already open. Keep the guard entry so the next back
        // cannot silently navigate past it.
        window.history.pushState(backGuardState, '');
        return;
    }
    // A dialog, menu, or select popover wins over a back press. Close the top layer. Re-arm the back guard.
    // Stay put. This covers browser back on the web with a sheet open.
    try {
        if (window.physiquinatorBack && typeof window.physiquinatorBack.closeTopLayer === 'function'
            && window.physiquinatorBack.closeTopLayer()) {
            window.history.pushState(backGuardState, '');
            return;
        }
    } catch {}
    backGuardBusy = true;
    const leave = await confirmLeaveWorkout();
    backGuardBusy = false;
    if (!leave) {
        window.history.pushState(backGuardState, '');
    }
}

window.physiquinatorBack = window.physiquinatorBack || {};
// Runs on Android hardware back through the shared
// consume() in appHelpers.js. Returns true when the workout or plan guard
// took over or is busy. Returns false so the shared handler tries dialogs
// and then the registered Blazor parent navigation.
window.physiquinatorBack._workoutConsume = function () {
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
};

export function stopRestTimer() {
    restTimerActive = false;
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
            // A transient interop failure (for example, the WebView was suspended
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
