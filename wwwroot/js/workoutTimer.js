// Audio & timer helper module for workout notifications.
// A single AudioContext is reused across calls to avoid exhausting
// native audio resources on Android WebView (which limits concurrent contexts).
let sharedCtx = null;
let restTimerId = null;

// Starts a JS-driven rest timer that calls back into .NET on each tick.
// Using setInterval here instead of a .NET PeriodicTimer ensures every
// callback fires on the WebView thread — the same thread Blazor uses —
// which eliminates native Android crashes caused by cross-thread WebView access.
export function startRestTimer(dotNetRef, intervalMs) {
    stopRestTimer();
    restTimerId = setInterval(() => {
        dotNetRef.invokeMethodAsync('OnTimerTick').catch(() => stopRestTimer());
    }, intervalMs);
}

export function stopRestTimer() {
    if (restTimerId !== null) {
        clearInterval(restTimerId);
        restTimerId = null;
    }
}

function getAudioContext() {
    if (!sharedCtx || sharedCtx.state === 'closed') {
        sharedCtx = new (window.AudioContext || window.webkitAudioContext)();
    }
    // Resume if suspended by autoplay policy (timer callbacks are not user gestures)
    if (sharedCtx.state === 'suspended') {
        sharedCtx.resume();
    }
    return sharedCtx;
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
        playTone(523, now, 0.15);        // C5
        playTone(659, now + 0.15, 0.15); // E5
        playTone(784, now + 0.3, 0.3);   // G5
    } catch (error) {
        console.warn('Audio playback failed:', error);
    }
}
