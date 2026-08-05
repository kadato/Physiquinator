let sharedCtx = null;
let restTimerActive = false;
let restTimerId = null;
let rafId = null;
let restStartTime = 0;
let restTotalMs = 0;

export function startRestTimer(dotNetRef, intervalMs, totalMs) {
    stopRestTimer();
    if (!totalMs || totalMs <= 0) return;

    restTimerActive = true;
    restStartTime = performance.now();
    restTotalMs = totalMs;

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
    restTimerId = setTimeout(async () => {
        if (!restTimerActive) return;
        try {
            const done = await dotNetRef.invokeMethodAsync('OnTimerTick');
            if (restTimerActive && !done)
                scheduleTick(dotNetRef, intervalMs);
            else
                restTimerActive = false;
        } catch {
            restTimerActive = false;
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
