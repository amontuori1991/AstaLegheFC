// --- Service Worker Fantabazzer (v6)
// Nessun redirect: la root non viene più riscritta dal SW.
// L'app-start resta gestita dal layout quando l'app è in standalone.

const SW_VERSION = 'v6';

// Attiva subito il nuovo SW
self.addEventListener('install', (event) => { self.skipWaiting(); });

// Prendi controllo subito
self.addEventListener('activate', (event) => {
    event.waitUntil(self.clients.claim());
});

// Consenti "SKIP_WAITING" da pagina
self.addEventListener('message', (event) => {
    if (event && event.data === 'SKIP_WAITING') self.skipWaiting();
});

// Navigazioni: passa in rete, con fallback minimale offline
self.addEventListener('fetch', (event) => {
    if (event.request.mode !== 'navigate') return; // lascia passare altri tipi
    event.respondWith((async () => {
        try { return await fetch(event.request); }
        catch { return new Response('Offline', { status: 503, statusText: 'Offline' }); }
    })());
});
