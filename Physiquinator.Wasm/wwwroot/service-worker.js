const CACHE = "physiquinator-v1";
self.addEventListener("install", e=> e.waitUntil(caches.open(CACHE).then(c=>c.addAll(["./", "./index.html", "./_content/Physiquinator.UI/fonts/DepartureMono-Regular.woff2"]))));
self.addEventListener("fetch", e=> e.respondWith(caches.match(e.request).then(r=> r || fetch(e.request))));
