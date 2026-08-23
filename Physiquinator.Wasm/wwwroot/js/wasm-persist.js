// Persists Physiquinator databases between WebAssembly sessions using Cache Storage.
// The browser is the source of truth: on boot every saved database file is restored
// into the wasm filesystem before the app opens SQLite, and a timer plus pagehide
// hook write files back so nothing is lost between saves.

const CACHE_NAME = 'physiquinator-db';
const KEY_PREFIX = '/data/';

let pageHideRef = null;

export function registerPageHide(dotNetRef) {
    if (pageHideRef != null) {
        return;
    }
    pageHideRef = dotNetRef;
    const handler = () => {
        try {
            dotNetRef.invokeMethodAsync('OnPageHide');
        } catch {
            // Best effort only. The periodic autosave bounds any loss.
        }
    };
    window.addEventListener('pagehide', handler);
    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'hidden') {
            handler();
        }
    });
}

export async function listDatabases() {
    const cache = await caches.open(CACHE_NAME);
    const keys = await cache.keys();
    return keys
        .map((req) => req.url)
        .filter((url) => url.includes(KEY_PREFIX) && url.endsWith('.db3'))
        .map((url) => decodeURIComponent(url.slice(url.lastIndexOf(KEY_PREFIX) + KEY_PREFIX.length)));
}

export async function loadDatabase(name) {
    const cache = await caches.open(CACHE_NAME);
    const resp = await cache.match(KEY_PREFIX + name);
    if (!resp) {
        return null;
    }
    return new Uint8Array(await resp.arrayBuffer());
}

export async function saveDatabase(name, bytes) {
    const cache = await caches.open(CACHE_NAME);
    await cache.put(KEY_PREFIX + name, new Response(new Blob([bytes])));
}
