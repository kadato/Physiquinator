// Offline support: serving index.html and the framework files from cache.
if ('serviceWorker' in navigator) navigator.serviceWorker.register('service-worker.js');
