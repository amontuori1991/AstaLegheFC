function mostraMessaggio(titolo, corpo, isConferma = false) {
    return new Promise(resolve => {
        const modaleElement = document.getElementById('messaggioModale');
        const modaleTitolo = document.getElementById('messaggioModaleTitolo');
        const modaleCorpo = document.getElementById('messaggioModaleCorpo');
        const btnOk = document.getElementById('messaggioModaleBtnOk');
        const btnAnnulla = document.getElementById('messaggioModaleBtnAnnulla');

        modaleTitolo.innerText = titolo;
        modaleCorpo.innerText = corpo;

        if (isConferma) {
            btnAnnulla.style.display = 'block';
        } else {
            btnAnnulla.style.display = 'none';
        }

        // ✅ RIGA MODIFICATA:
        // Invece di "new bootstrap.Modal", usiamo "getOrCreateInstance".
        // Questo evita conflitti e assicura che lo sfondo scuro venga rimosso correttamente.
        const bootstrapModal = bootstrap.Modal.getOrCreateInstance(modaleElement);

        // Funzione per ripulire gli eventi e risolvere la promise
        const cleanupAndResolve = (value) => {
            btnOk.onclick = null;
            btnAnnulla.onclick = null;
            modaleElement.removeEventListener('hidden.bs.modal', closeModalHandler);
            resolve(value);
        };

        const closeModalHandler = () => cleanupAndResolve(false);

        btnOk.onclick = () => {
            bootstrapModal.hide();
            cleanupAndResolve(true);
        };

        btnAnnulla.onclick = () => {
            bootstrapModal.hide();
            cleanupAndResolve(false);
        };

        // Aggiungiamo un listener per la chiusura (es. con ESC)
        modaleElement.addEventListener('hidden.bs.modal', closeModalHandler, { once: true });

        bootstrapModal.show();
    });
}