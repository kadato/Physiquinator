const CACHE = "physiquinator-v2";

self.addEventListener("install", e => {
    self.skipWaiting();
    e.waitUntil(
        caches.open(CACHE).then(c => c.addAll([
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
    if (e.request.mode === "navigate") {
        e.respondWith(
            fetch(e.request).catch(() => caches.match("./index.html"))
        );
        return;
    }
    e.respondWith(
        caches.match(e.request).then(r => r || fetch(e.request))
    );
});
