// Funzione per mostrare un modale di Bootstrap personalizzato.
// Restituisce una "Promise" che si risolve con 'true' se l'utente clicca OK, 'false' altrimenti.
function mostraMessaggio(titolo, corpo, isConferma = false) {
    return new Promise(resolve => {
        const modaleElement = document.getElementById('messaggioModale');
        if (!modaleElement) {
            console.error("Elemento del modale non trovato!");
            resolve(false); // Se non trova il modale, restituisce 'false' per non bloccare lo script
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

        // Funzione unica per chiudere e risolvere la promise, evitando esecuzioni multiple
        const closeHandler = (value) => {
            // Rimuovi i listener per evitare memory leak
            btnOk.removeEventListener('click', okHandler);
            btnAnnulla.removeEventListener('click', annullaHandler);
            modaleElement.removeEventListener('hidden.bs.modal', hiddenHandler);
            resolve(value);
        };

        const okHandler = () => {
            bootstrapModal.hide();
            closeHandler(true);
        };

        const annullaHandler = () => {
            bootstrapModal.hide();
            closeHandler(false);
        };

        const hiddenHandler = () => closeHandler(false);

        // Aggiungi i listener
        btnOk.addEventListener('click', okHandler, { once: true });
        btnAnnulla.addEventListener('click', annullaHandler, { once: true });
        modaleElement.addEventListener('hidden.bs.modal', hiddenHandler, { once: true });

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

    // ✅ LOGICA PER IL PULSANTE SVINCOLA SPOSTATA QUI
    // Questo metodo (event delegation) garantisce che il click funzioni
    // anche sui pulsanti che sono nascosti al caricamento della pagina.
    document.addEventListener('click', async function (event) {
        if (event.target && event.target.matches('.btn-svincola')) {
            const btn = event.target;
            const nomeGiocatore = btn.dataset.nome;

            if (await mostraMessaggio("Conferma Svincolo", `Sei sicuro di voler svincolare ${nomeGiocatore}?`, true)) {
                const id = btn.getAttribute('data-id');
                fetch('/Admin/SvincolaGiocatore', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ id: parseInt(id) })
                }).then(async response => {
                    if (response.ok) {
                        location.reload();
                    } else {
                        await mostraMessaggio("Errore", "Errore nello svincolo.");
                    }
                });
            }
        }
    });
});