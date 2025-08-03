// Funzione per mostrare un modale di Bootstrap personalizzato.
// Restituisce una "Promise" che si risolve con 'true' se l'utente clicca OK, 'false' altrimenti.
function mostraMessaggio(titolo, corpo, isConferma = false) {
    return new Promise(resolve => {
        const modaleElement = document.getElementById('messaggioModale');
        if (!modaleElement) {
            console.error("Elemento del modale non trovato!");
            resolve(false);
            return;
        }

        const modaleTitolo = modaleElement.querySelector('.modal-title');
        const modaleCorpo = modaleElement.querySelector('.modal-body p');
        const btnOk = modaleElement.querySelector('#messaggioModaleBtnOk');
        const btnAnnulla = modaleElement.querySelector('#messaggioModaleBtnAnnulla');

        modaleTitolo.textContent = titolo;
        modaleCorpo.textContent = corpo;
        btnAnnulla.style.display = isConferma ? 'inline-block' : 'none';

        const bootstrapModal = bootstrap.Modal.getOrCreateInstance(modaleElement);

        const onOkClick = () => {
            resolve(true);
            bootstrapModal.hide();
        };

        const onAnnullaClick = () => {
            resolve(false);
            bootstrapModal.hide();
        };

        // Rimuovi listener precedenti per evitare esecuzioni multiple
        const newBtnOk = btnOk.cloneNode(true);
        btnOk.parentNode.replaceChild(newBtnOk, btnOk);
        const newBtnAnnulla = btnAnnulla.cloneNode(true);
        btnAnnulla.parentNode.replaceChild(newBtnAnnulla, btnAnnulla);

        // Ricollega gli eventi
        document.getElementById('messaggioModaleBtnOk').addEventListener('click', onOkClick, { once: true });
        document.getElementById('messaggioModaleBtnAnnulla').addEventListener('click', onAnnullaClick, { once: true });

        modaleElement.addEventListener('hidden.bs.modal', () => {
            resolve(false);
        }, { once: true });

        bootstrapModal.show();
    });
}


// Logica di caricamento pagina (loader)
document.addEventListener("DOMContentLoaded", function () {
    const loaderOverlay = document.getElementById('loader-overlay');
    const links = document.querySelectorAll('.link-caricamento');

    links.forEach(link => {
        link.addEventListener('click', function (e) {
            if (loaderOverlay && link.href && link.target !== '_blank') {
                loaderOverlay.style.display = 'flex';
            }
        });
    });

    // NESSUN'ALTRA LOGICA QUI! Lo svincolo è gestito nella sua pagina.
});