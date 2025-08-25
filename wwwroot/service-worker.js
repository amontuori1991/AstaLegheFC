// Esempio vanilla (senza Workbox)
self.addEventListener('fetch', (event) => {
    // Intercetta SOLO le navigazioni (document)
    if (event.request.mode === 'navigate') {
        event.respondWith((async () => {
            try {
                // prendi sempre la pagina giusta dalla rete
                return await fetch(event.request);
            } catch (err) {
                // offline fallback opzionale
                const cache = await caches.open('static-v1');
                const offline = await cache.match('/offline.html');
                return offline || new Response('Offline', { status: 503 });
            }
        })());
        return;
    }

    // per asset statici ok cache-first/smart strategy
    // (css, js, immagini…)
});
