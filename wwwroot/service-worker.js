// --- SW base con claim immediato
self.addEventListener('install', (event) => {
    self.skipWaiting();
});
self.addEventListener('activate', (event) => {
    event.waitUntil(self.clients.claim());
});

// --- NAVIGATIONS: manda "/" e "/index.html" alla bootstrap page
self.addEventListener('fetch', (event) => {
    if (event.request.mode === 'navigate') {
        const url = new URL(event.request.url);

        // Forza redirect alla bootstrap se aprono la root
        if (url.pathname === '/' || url.pathname.toLowerCase() === '/index.html') {
            event.respondWith(Response.redirect('/app-start.html?v=3'));
            return;
        }

        // altrimenti vai in rete; fallback offline opzionale
        event.respondWith((async () => {
            try {
                return await fetch(event.request);
            } catch (err) {
                // fallback minimale
                return new Response('Offline', { status: 503, statusText: 'Offline' });
            }
        })());
        return;
    }

    // per asset statici: lascia passare (o qui potresti fare cache-first)
});
