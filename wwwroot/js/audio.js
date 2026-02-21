// Audio helper module for workout notifications
export function playRestCompleteSound() {
    try {
        const audioContext = new (window.AudioContext || window.webkitAudioContext)();
        const oscillator = audioContext.createOscillator();
        const gainNode = audioContext.createGain();
        oscillator.connect(gainNode);
        gainNode.connect(audioContext.destination);
        oscillator.frequency.value = 800;
        gainNode.gain.value = 0.3;
        oscillator.start();
        setTimeout(() => oscillator.stop(), 200);
    } catch (error) {
        console.warn('Audio playback failed:', error);
    }
}

export function playWorkoutCompleteSound() {
    try {
        const audioContext = new (window.AudioContext || window.webkitAudioContext)();
        const playTone = (frequency, startTime, duration) => {
            const oscillator = audioContext.createOscillator();
            const gainNode = audioContext.createGain();
            oscillator.connect(gainNode);
            gainNode.connect(audioContext.destination);
            oscillator.frequency.value = frequency;
            gainNode.gain.value = 0.3;
            oscillator.start(startTime);
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
