// Browser-side persistence for the web host: the SQLite database file is stored in
// IndexedDB so it survives server restarts with ephemeral filesystems.
// The server pushes the file here every ~15s (WebDbSyncService) and pulls it back
// on page load before Blazor starts (WebDbRestoreEndpoint).
(function () {
    'use strict';

    const DB_NAME = 'physiquinator-db';
    const STORE_NAME = 'files';

    function openDb() {
        return new Promise((resolve, reject) => {
            const request = indexedDB.open(DB_NAME, 1);
            request.onupgradeneeded = () => {
                const db = request.result;
                if (!db.objectStoreNames.contains(STORE_NAME)) {
                    db.createObjectStore(STORE_NAME);
                }
            };
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
    }

    function blobToBase64(blob) {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = () => {
                const result = reader.result;
                resolve(typeof result === 'string' && result.indexOf(',') >= 0 ? result.split(',')[1] : result);
            };
            reader.onerror = () => reject(reader.error);
            reader.readAsDataURL(blob);
        });
    }

    function base64ToBytes(base64) {
        const binary = atob(base64);
        const bytes = new Uint8Array(binary.length);
        for (let i = 0; i < binary.length; i++) {
            bytes[i] = binary.charCodeAt(i);
        }
        return bytes;
    }

    window.physiquinatorDb = {
        list() {
            return openDb().then((db) => new Promise((resolve, reject) => {
                const request = db.transaction(STORE_NAME, 'readonly').objectStore(STORE_NAME).getAllKeys();
                request.onsuccess = () => resolve(request.result);
                request.onerror = () => reject(request.error);
            }));
        },

        get(name) {
            return openDb().then((db) => new Promise((resolve, reject) => {
                const request = db.transaction(STORE_NAME, 'readonly').objectStore(STORE_NAME).get(name);
                request.onsuccess = () => resolve(request.result);
                request.onerror = () => reject(request.error);
            }));
        },

        put(name, blob) {
            return openDb().then((db) => new Promise((resolve, reject) => {
                const tx = db.transaction(STORE_NAME, 'readwrite');
                tx.objectStore(STORE_NAME).put(blob, name);
                tx.oncomplete = () => resolve();
                tx.onerror = () => reject(tx.error);
            }));
        },

        // Pulls the stored databases up to the server. Called before Blazor starts,
        // so the first circuit of a fresh dyno opens the visitor's data.
        restoreToServer() {
            return this.list().then(async (names) => {
                const files = [];
                for (const name of names) {
                    if (typeof name !== 'string' || !name.startsWith('physiquinator') || !name.endsWith('.db3')) {
                        continue;
                    }
                    const blob = await this.get(name);
                    if (!blob || !blob.size) {
                        continue;
                    }
                    files.push({ name, data: await blobToBase64(blob) });
                }
                if (!files.length) {
                    return;
                }
                await fetch('/api/db/restore', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ files })
                });
            });
        },

        // Stores a database pushed from the server (base64 string from Blazor interop).
        saveFromServer(name, base64) {
            return this.put(name, new Blob([base64ToBytes(base64)]));
        }
    };
})();
