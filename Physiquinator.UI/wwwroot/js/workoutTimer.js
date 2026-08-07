let sharedCtx = null;
let restTimerActive = false;
let restTimerId = null;
let rafId = null;
let restStartTime = 0;
let restTotalMs = 0;
let chainGeneration = 0;

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
