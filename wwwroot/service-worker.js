// --- Service Worker Fantabazzer (v7) ---
// Minimal: nessun redirect, nessuna riscrittura URL, niente cache aggressive.

const SW_VERSION = 'v7';

self.addEventListener('install', (event) => {
    // attiva subito il nuovo SW
    self.skipWaiting();
});

self.addEventListener('activate', (event) => {
    // prendi subito il controllo delle pagine
    event.waitUntil(self.clients.claim());
});

// opzionale: consenti forzare l'update da pagina
self.addEventListener('message', (event) => {
    if (event && event.data === 'SKIP_WAITING') {
        self.skipWaiting();
    }
});

// Lascia passare tutto alla rete; fallback offline minimo
self.addEventListener('fetch', (event) => {
    if (event.request.mode !== 'navigate') return;
    event.respondWith((async () => {
        try {
            return await fetch(event.request);
        } catch {
            return new Response('Offline', { status: 503, statusText: 'Offline' });
        }
    })());
});
