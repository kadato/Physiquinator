const CACHE = "physiquinator-v3";
const OFFLINE_FALLBACK = "./index.html";

self.addEventListener("install", e => {
    self.skipWaiting();
    e.waitUntil(
        caches.open(CACHE).then(c => c.addAll([
            OFFLINE_FALLBACK,
            "./_content/Physiquinator.UI/fonts/DepartureMono-Regular.woff2"
        ]))
    );
});

self.addEventListener("activate", e => {
    e.waitUntil(
        caches.keys().then(keys =>
            Promise.all(keys.map(k => k !== CACHE ? caches.delete(k) : null))
        ).then(() => self.clients.claim())
    );
});

self.addEventListener("fetch", e => {
    const { request } = e;
    if (request.method !== "GET") return;

    if (request.mode === "navigate") {
        // SPA shell: network first so deploys reach visitors immediately,
        // cached index.html when offline. The cached copy is refreshed on
        // every successful navigation.
        e.respondWith(
            fetch(request)
                .then(response => {
                    const copy = response.clone();
                    caches.open(CACHE)
                        .then(c => c.put(OFFLINE_FALLBACK, copy))
                        .catch(() => {});
                    return response;
                })
                .catch(() => caches.match(OFFLINE_FALLBACK).then(r => r || Response.error()))
        );
        return;
    }

    e.respondWith(
        caches.match(request)
            .then(cached => cached || fetch(request))
            // A rejected fetch must never surface as an unhandled rejection:
            // report a plain network error so the page can handle it.
            .catch(() => Response.error())
    );
});
