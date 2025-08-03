function mostraMessaggio(titolo, corpo, isConferma = false) {
    return new Promise(resolve => {
        const modaleElement = document.getElementById('messaggioModale');
        const bootstrapModal = bootstrap.Modal.getOrCreateInstance(modaleElement);
        // ... (resto della funzione invariata)
    });
}


// ✅ 3. AGGIUNTA LA LOGICA PER MOSTRARE IL LOADER AL CLICK
document.addEventListener("DOMContentLoaded", function () {
    const loaderOverlay = document.getElementById('loader-overlay');
    const links = document.querySelectorAll('.link-caricamento');

    links.forEach(link => {
        link.addEventListener('click', function (e) {
            // Mostra il loader solo per i link normali, non per chiamate JavaScript
            if (loaderOverlay && link.href && link.target !== '_blank') {
                loaderOverlay.style.display = 'flex';
            }
        });
    });
});