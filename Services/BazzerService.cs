using AstaLegheFC.Models;

namespace AstaLegheFC.Services
{
    public class BazzerService
    {
        private CalciatoreListone? _giocatoreInAsta;
        private string _offerenteAttuale = "-";
        private int _offertaAttuale = 0;
        private bool _astaConclusa = false;

        // ⛓️ Lock + end-time condiviso per sincronizzazione
        private readonly object _sync = new object();
        private DateTime? _fineUtc;

        public int DurataTimer { get; private set; } = 5;
        public bool BloccoPortieriAttivo { get; private set; } = true;
        public bool MantraAttivo { get; private set; } = false;

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

        public void ImpostaGiocatoreInAsta(CalciatoreListone giocatore, bool mantraAttivo)
        {
            lock (_sync)
            {
                _giocatoreInAsta = giocatore;
                _offerenteAttuale = "-";
                _offertaAttuale = 0;
                _astaConclusa = false;
                _fineUtc = null;
                this.MantraAttivo = mantraAttivo;
            }
        }

        public void AnnullaAstaCorrente()
        {
            lock (_sync)
            {
                _giocatoreInAsta = null;
                _offerenteAttuale = "-";
                _offertaAttuale = 0;
                _astaConclusa = false;
                _fineUtc = null;
            }
        }

        public CalciatoreListone? GetGiocatoreInAsta()
        {
            lock (_sync) { return _giocatoreInAsta; }
        }

        public (string offerente, int offerta) GetOffertaAttuale()
        {
            lock (_sync) { return (_offerenteAttuale, _offertaAttuale); }
        }

        public DateTime? GetFineUtc()
        {
            lock (_sync) { return _fineUtc; }
        }

        // ✅ CAS: accetta l'offerta solo se combacia l'ultimo prezzo visto dal client
        public bool TryAggiornaOffertaCAS(string offerente, int offertaProposta, int expectedPrevOfferta, out DateTime fineUtcAccettata)
        {
            lock (_sync)
            {
                // default
                fineUtcAccettata = _fineUtc ?? DateTime.UtcNow;

                if (_astaConclusa || _giocatoreInAsta == null)
                    return false;

                // deve essere > dell'attuale...
                if (offertaProposta <= _offertaAttuale)
                    return false;

                // ...ma soprattutto deve basarsi ESATTAMENTE sul prezzo che il client ha visto
                if (_offertaAttuale != expectedPrevOfferta)
                    return false;

                _offerenteAttuale = offerente;
                _offertaAttuale = offertaProposta;

                // end-time ufficiale
                fineUtcAccettata = DateTime.UtcNow.AddSeconds(DurataTimer);
                _fineUtc = fineUtcAccettata;

                return true;
            }
        }

        public bool AstaConclusa()
        {
            lock (_sync) { return _astaConclusa; }
        }

        public void SegnaAstaConclusa()
        {
            lock (_sync) { _astaConclusa = true; }
        }
    }
}
