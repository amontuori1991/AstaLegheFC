using AstaLegheFC.Models;

namespace AstaLegheFC.Services
{
    public class BazzerService
    {
        private CalciatoreListone? _giocatoreInAsta;
        private string _offerenteAttuale = "-";
        private int _offertaAttuale = 0;
        private bool _astaConclusa = false;

        public int DurataTimer { get; private set; } = 5;
        public bool BloccoPortieriAttivo { get; private set; } = true;

        public void ImpostaDurataTimer(int secondi)
        {
            DurataTimer = Math.Max(2, secondi);
        }

        public void ImpostaBloccoPortieri(bool attivo)
        {
            BloccoPortieriAttivo = attivo;
        }

        public void ImpostaGiocatoreInAsta(CalciatoreListone giocatore)
        {
            _giocatoreInAsta = giocatore;
            _offerenteAttuale = "-";
            _offertaAttuale = 0;
            _astaConclusa = false;
        }

        // ✅ AGGIUNTO METODO PER ANNULLARE E AZZERARE L'ASTA
        public void AnnullaAstaCorrente()
        {
            _giocatoreInAsta = null;
            _offerenteAttuale = "-";
            _offertaAttuale = 0;
            _astaConclusa = false;
        }

        public CalciatoreListone? GetGiocatoreInAsta() => _giocatoreInAsta;
        public (string offerente, int offerta) GetOffertaAttuale() => (_offerenteAttuale, _offertaAttuale);
        public void AggiornaOfferta(string offerente, int offerta)
        {
            _offerenteAttuale = offerente;
            _offertaAttuale = offerta;
        }
        public bool AstaConclusa() => _astaConclusa;
        public void SegnaAstaConclusa() => _astaConclusa = true;
    }
}