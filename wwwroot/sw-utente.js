self.addEventListener('install', e => { self.skipWaiting(); });
self.addEventListener('activate', e => { e.waitUntil(clients.claim()); });

// Navigazioni: prendi sempre dalla rete (niente app-shell che “sporca” l’Utente)
self.addEventListener('fetch', (event) => {
    if (event.request.mode === 'navigate') {
        event.respondWith(fetch(event.request));
        return;
    }
    // qui opzionale: cache statici per /Utente/ (css/js/img)
});
