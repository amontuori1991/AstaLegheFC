// service-worker.js

// Questo service worker è intenzionalmente lasciato vuoto.
// La sua presenza è sufficiente per rendere l'app installabile (PWA).
// In futuro, potrà essere usato per gestire la cache e le funzionalità offline.

self.addEventListener('install', (event) => {
    // console.log('Service Worker: Installazione...');
});

self.addEventListener('fetch', (event) => {
    // Al momento non intercettiamo le richieste
});