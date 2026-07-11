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
            fill.style.width = `${progress * 100}%`;
        }

        if (progress < 1) {
            rafId = requestAnimationFrame(update);
        }
    }
    rafId = requestAnimationFrame(update);
}

export function pauseRestTimer() {
    if (rafId !== null) {
        cancelAnimationFrame(rafId);
        rafId = null;
    }
    if (restTimerId !== null) {
        clearTimeout(restTimerId);
        restTimerId = null;
    }
}

export function resumeRestTimer(dotNetRef, intervalMs, totalMs) {
    if (!restTimerActive || !totalMs || totalMs <= 0) return;

    restStartTime = performance.now();
    restTotalMs = totalMs;
    startProgressRaf();
    scheduleTick(dotNetRef, intervalMs);
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
        const oscillator = audioContext.createOscillator();
        const gainNode = audioContext.createGain();
        oscillator.connect(gainNode);
        gainNode.connect(audioContext.destination);
        oscillator.frequency.value = 800;
        gainNode.gain.value = 0.3;
        oscillator.start();
        oscillator.onended = () => gainNode.disconnect();
        setTimeout(() => oscillator.stop(), 200);
    } catch (error) {
        console.warn('Audio playback failed:', error);
    }
}

export function playWorkoutCompleteSound() {
    try {
        const audioContext = getAudioContext();
        const playTone = (frequency, startTime, duration) => {
            const oscillator = audioContext.createOscillator();
            const gainNode = audioContext.createGain();
            oscillator.connect(gainNode);
            gainNode.connect(audioContext.destination);
            oscillator.frequency.value = frequency;
            gainNode.gain.value = 0.3;
            oscillator.start(startTime);
            oscillator.onended = () => gainNode.disconnect();
            oscillator.stop(startTime + duration);
        };
        const now = audioContext.currentTime;
        playTone(523, now, 0.15);
        playTone(659, now + 0.15, 0.15);
        playTone(784, now + 0.3, 0.3);
    } catch (error) {
        console.warn('Audio playback failed:', error);
    }
}
