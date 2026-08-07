// Infinite scroll helper: observes a sentinel element and invokes a .NET
// method whenever it scrolls into view. Re-arms itself while the method
// reports that more items exist, so short lists keep loading until the
// sentinel drops out of view.
const observers = new Map();

export function observe(sentinelId, dotNetRef, methodName) {
    dispose(sentinelId);

    const sentinel = document.getElementById(sentinelId);
    if (!sentinel) return;

    const observer = new IntersectionObserver(async (entries) => {
        for (const entry of entries) {
            if (!entry.isIntersecting) continue;
            try {
                const more = await dotNetRef.invokeMethodAsync(methodName);
                if (more) rearm();
            } catch {
                // Interop teardown (e.g. navigation): stop observing.
            }
        }
    }, { rootMargin: '240px 0px' });

    function rearm() {
        observer.unobserve(sentinel);
        observer.observe(sentinel);
    }

    observer.observe(sentinel);
    observers.set(sentinelId, observer);
}

export function dispose(sentinelId) {
    const observer = observers.get(sentinelId);
    if (observer) {
        observer.disconnect();
        observers.delete(sentinelId);
    }
}
