// --- Service Worker Fantabazzer (v4)
const SW_VERSION = 'v4';

// Rileva se lo scope del SW è in localhost
const IS_LOCALHOST = ['localhost', '127.0.0.1', '[::1]']
    .includes(new URL(self.registration.scope).hostname);

// Pagina di bootstrap della PWA (solo produzione), in forma RELATIVA.
// La convertiamo in assoluto usando lo scope per evitare "//".
const BOOTSTRAP_REL = 'app-start.html?v=4';

self.addEventListener('install', (event) => { self.skipWaiting(); });
self.addEventListener('activate', (event) => { event.waitUntil(self.clients.claim()); });

self.addEventListener('message', (event) => {
    if (event && event.data === 'SKIP_WAITING') self.skipWaiting();
});

self.addEventListener('fetch', (event) => {
    // Intercetta SOLO le navigazioni (click link, location, ecc.)
    if (event.request.mode !== 'navigate') return;

    const url = new URL(event.request.url);
    const path = url.pathname.toLowerCase();

    // In PRODUZIONE (non localhost), se aprono la root manda alla pagina di bootstrap.
    // Evita loop se già su /app-start
    if (!IS_LOCALHOST) {
        if ((path === '/' || path === '/index.html') && !path.startsWith('/app-start')) {
            const abs = new URL(BOOTSTRAP_REL, self.registration.scope).href;
            event.respondWith(Response.redirect(abs));
            return;
        }
    }

    // Resto: vai in rete (fallback offline minimale)
    event.respondWith((async () => {
        try { return await fetch(event.request); }
        catch { return new Response('Offline', { status: 503, statusText: 'Offline' }); }
    })());
});
