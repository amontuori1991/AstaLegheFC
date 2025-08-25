using AstaLegheFC.Models;

namespace AstaLegheFC.Services
{
    public class BazzerService
    {
        // ===== Stato d'asta “classico” =====
        private CalciatoreListone? _giocatoreInAsta;
        private string _offerenteAttuale = "-";
        private int _offertaAttuale = 0;
        private bool _astaConclusa = false;

        public int DurataTimer { get; private set; } = 5;
        public bool BloccoPortieriAttivo { get; private set; } = true;
        public bool MantraAttivo { get; private set; } = false;

        // ===== Nuovo stato per durata complessiva e pausa =====
        private DateTime? _astaStartUtc;              // quando è partita l’asta corrente
        private bool _pausaAttiva = false;
        private DateTime? _pausaStartUtc;             // inizio della pausa corrente
        private TimeSpan _pausaAccumulata = TimeSpan.Zero; // somma di tutte le pause fatte

        // ===== End-time countdown offerta (autorevole lato server) =====
        private DateTime? _fineOffertaUtc;            // quando scade l’offerta corrente (se non in pausa)
        private TimeSpan? _remainingAtPause;          // quanto mancava quando abbiamo messo in pausa

        // Espongo in sola lettura quel che serve agli altri layer
        public DateTime? AstaStartUtc => _astaStartUtc;
        public bool PausaAttiva => _pausaAttiva;
        public DateTime? FineOffertaUtc => _fineOffertaUtc;

        // ========= Impostazioni base =========
        public void ImpostaDurataTimer(int secondi)
        {
            DurataTimer = Math.Max(2, secondi);
        }

        public void ImpostaBloccoPortieri(bool attivo)
        {
            BloccoPortieriAttivo = attivo;
        }

        public void ImpostaModalitaMantra(bool attivo)
        {
            MantraAttivo = attivo;
        }

        // ========= Avvio/Annullamento asta =========

        /// <summary>
        /// Wrapper compatibile con il codice esistente.
        /// </summary>
        public void ImpostaGiocatoreInAsta(CalciatoreListone giocatore, bool mantraAttivo)
            => AvviaAsta(giocatore, mantraAttivo, DateTime.UtcNow);

        /// <summary>
        /// Nuovo entry-point che inizializza anche i campi per durata e pause.
        /// </summary>
        public void AvviaAsta(CalciatoreListone giocatore, bool mantraAttivo, DateTime nowUtc)
        {
            _giocatoreInAsta = giocatore;
            _offerenteAttuale = "-";
            _offertaAttuale = 0;
            _astaConclusa = false;

            MantraAttivo = mantraAttivo;

            _astaStartUtc = nowUtc;
            _pausaAttiva = false;
            _pausaStartUtc = null;
            _pausaAccumulata = TimeSpan.Zero;

            _fineOffertaUtc = null;     // partirà alla prima offerta
            _remainingAtPause = null;
        }

        /// <summary>
        /// Azzera completamente lo stato dell’asta corrente.
        /// </summary>
        public void AnnullaAstaCorrente()
        {
            _giocatoreInAsta = null;
            _offerenteAttuale = "-";
            _offertaAttuale = 0;
            _astaConclusa = false;

            _astaStartUtc = null;
            _pausaAttiva = false;
            _pausaStartUtc = null;
            _pausaAccumulata = TimeSpan.Zero;

            _fineOffertaUtc = null;
            _remainingAtPause = null;
        }

        // ========= Pausa / Ripresa =========

        /// <summary>
        /// Mette in pausa l’asta: congela il countdown e accumula la pausa.
        /// </summary>
        public void MettiInPausa(DateTime nowUtc)
        {
            if (_pausaAttiva) return;
            _pausaAttiva = true;
            _pausaStartUtc = nowUtc;

            // Salva quanto manca alla scadenza e azzera l’end-time finché siamo in pausa
            if (_fineOffertaUtc.HasValue)
            {
                var remaining = _fineOffertaUtc.Value - nowUtc;
                _remainingAtPause = remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
            else
            {
                _remainingAtPause = null;
            }

            _fineOffertaUtc = null;
        }

        /// <summary>
        /// Riprende l’asta: ripristina countdown con il tempo rimanente salvato.
        /// </summary>
        public void Riprendi(DateTime nowUtc)
        {
            if (!_pausaAttiva) return;

            if (_pausaStartUtc.HasValue)
                _pausaAccumulata += (nowUtc - _pausaStartUtc.Value);

            _pausaAttiva = false;
            _pausaStartUtc = null;

            if (_remainingAtPause.HasValue)
            {
                _fineOffertaUtc = nowUtc + _remainingAtPause.Value;
            }
            _remainingAtPause = null;
        }

        // ========= Info utili al resto del sistema =========

        /// <summary>
        /// Durata dell’asta dall’avvio, al netto delle pause (se in pausa, include anche la pausa corrente).
        /// </summary>
        public TimeSpan GetDurataAsta(DateTime nowUtc)
        {
            if (!_astaStartUtc.HasValue) return TimeSpan.Zero;

            var elapsed = nowUtc - _astaStartUtc.Value - _pausaAccumulata;
            if (_pausaAttiva && _pausaStartUtc.HasValue)
                elapsed -= (nowUtc - _pausaStartUtc.Value);

            return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
        }

        /// <summary>
        /// Secondi rimanenti per l’offerta corrente, considerando la pausa.
        /// </summary>
        public int GetRemainingOfferta(DateTime nowUtc)
        {
            if (_pausaAttiva)
                return (int)Math.Ceiling((_remainingAtPause ?? TimeSpan.Zero).TotalSeconds);

            if (_fineOffertaUtc.HasValue)
            {
                var sec = (_fineOffertaUtc.Value - nowUtc).TotalSeconds;
                return (int)Math.Max(0, Math.Ceiling(sec));
            }
            return 0;
        }

        /// <summary>
        /// Stato sintetico per broadcast/diagnostica.
        /// </summary>
        public (bool pausaAttiva, DateTime? astaStartUtc, TimeSpan pausaAccumulata, DateTime? pausaStartUtc, DateTime? fineOffertaUtc) GetStato()
            => (_pausaAttiva, _astaStartUtc, _pausaAccumulata, _pausaStartUtc, _fineOffertaUtc);

        /// <summary>
        /// Permette (se non in pausa) di reimpostare l’end-time come “ora + durata” (es. dopo una nuova offerta).
        /// </summary>
        public void ReimpostaFineAstaDaOra(int durataSecondi, DateTime nowUtc)
        {
            if (_pausaAttiva) return;
            _fineOffertaUtc = nowUtc.AddSeconds(Math.Max(1, durataSecondi));
        }

        // ========= Metodi “classici” già usati dal resto dell’app =========
        public CalciatoreListone? GetGiocatoreInAsta() => _giocatoreInAsta;
        public (string offerente, int offerta) GetOffertaAttuale() => (_offerenteAttuale, _offertaAttuale);

        public void AggiornaOfferta(string offerente, int offerta)
        {
            _offerenteAttuale = offerente;
            _offertaAttuale = offerta;

            // Se non siamo in pausa, allineiamo subito l’end-time lato server.
            if (!_pausaAttiva)
            {
                ReimpostaFineAstaDaOra(DurataTimer, DateTime.UtcNow);
            }
        }

        public bool AstaConclusa() => _astaConclusa;
        public void SegnaAstaConclusa() => _astaConclusa = true;
    }
}
