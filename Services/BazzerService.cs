using AstaLegheFC.Models;

public class BazzerService
{
    private CalciatoreListone? _giocatoreInAsta;
    private string _offerenteAttuale = "-";
    private int _offertaAttuale = 0;
    private bool _astaConclusa = false;

    public int DurataTimer { get; private set; } = 5;
    public bool BloccoPortieriAttivo { get; private set; } = true;
    public bool MantraAttivo { get; private set; } = false;

    // 👉 END-TIME autorevole (UTC)
    private DateTime? _astaFineUtc;

    public void ImpostaDurataTimer(int secondi) => DurataTimer = Math.Max(2, secondi);
    public void ImpostaBloccoPortieri(bool attivo) => BloccoPortieriAttivo = attivo;
    public void ImpostaModalitaMantra(bool attivo) => MantraAttivo = attivo;

    public void ImpostaGiocatoreInAsta(CalciatoreListone giocatore, bool mantraAttivo)
    {
        _giocatoreInAsta = giocatore;
        _offerenteAttuale = "-";
        _offertaAttuale = 0;
        _astaConclusa = false;
        _astaFineUtc = null; // parte alla prima offerta
        MantraAttivo = mantraAttivo;
    }

    public void AnnullaAstaCorrente()
    {
        _giocatoreInAsta = null;
        _offerenteAttuale = "-";
        _offertaAttuale = 0;
        _astaConclusa = false;
        _astaFineUtc = null;
    }

    public CalciatoreListone? GetGiocatoreInAsta() => _giocatoreInAsta;
    public (string offerente, int offerta) GetOffertaAttuale() => (_offerenteAttuale, _offertaAttuale);
    public bool AstaConclusa() => _astaConclusa;
    public void SegnaAstaConclusa() { _astaConclusa = true; _astaFineUtc = null; }

    // 👉 esponi end-time
    public DateTime? GetAstaFineUtc() => _astaFineUtc;

    // 👉 ad ogni offerta, fissa il nuovo end-time
    public DateTime AggiornaOfferta(string offerente, int offerta)
    {
        _offerenteAttuale = offerente;
        _offertaAttuale = offerta;
        _astaConclusa = false;
        _astaFineUtc = DateTime.UtcNow.AddSeconds(DurataTimer);
        return _astaFineUtc.Value;
    }
}
