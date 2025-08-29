using AstaLegheFC.Models;
using System.Collections.Concurrent;

namespace AstaLegheFC.Services
{
    public class BazzerService
    {
        // Stato per-lega
        private class StatoAsta
        {
            public CalciatoreListone? GiocatoreInAsta;
            public string OfferenteAttuale = "-";
            public int OffertaAttuale = 0;
            public bool AstaConclusa = false;

            public int DurataTimer = 5;
            public bool BloccoPortieriAttivo = false;
            public bool MantraAttivo = false;

            public DateTime? AstaStartUtc;
            public bool PausaAttiva = false;
            public DateTime? PausaStartUtc;
            public TimeSpan PausaAccumulata = TimeSpan.Zero;

            public DateTime? FineOffertaUtc;
            public TimeSpan? RemainingAtPause;
        }

        private static string Key(string lega) => (lega ?? "").Trim().ToLowerInvariant();
        private readonly ConcurrentDictionary<string, StatoAsta> _byLega = new(StringComparer.Ordinal);

        private StatoAsta S(string lega) => _byLega.GetOrAdd(Key(lega), _ => new StatoAsta());

        // ===== Impostazioni =====
        public void ImpostaDurataTimer(string lega, int secondi)
        {
            var s = S(lega);
            s.DurataTimer = Math.Max(2, secondi);
        }
        public int GetDurataTimer(string lega) => S(lega).DurataTimer;

        public void ImpostaBloccoPortieri(string lega, bool attivo) => S(lega).BloccoPortieriAttivo = attivo;
        public bool IsBloccoPortieriAttivo(string lega) => S(lega).BloccoPortieriAttivo;

        public void ImpostaModalitaMantra(string lega, bool attivo) => S(lega).MantraAttivo = attivo;
        public bool IsMantraAttivo(string lega) => S(lega).MantraAttivo;

        // ===== Avvio/Annullamento =====
        public void ImpostaGiocatoreInAsta(string lega, CalciatoreListone giocatore, bool mantraAttivo)
            => AvviaAsta(lega, giocatore, mantraAttivo, DateTime.UtcNow);

        public void AvviaAsta(string lega, CalciatoreListone giocatore, bool mantraAttivo, DateTime nowUtc)
        {
            var s = S(lega);
            s.GiocatoreInAsta = giocatore;
            s.OfferenteAttuale = "-";
            s.OffertaAttuale = 0;
            s.AstaConclusa = false;

            s.MantraAttivo = mantraAttivo;

            s.AstaStartUtc = nowUtc;
            s.PausaAttiva = false;
            s.PausaStartUtc = null;
            s.PausaAccumulata = TimeSpan.Zero;

            s.FineOffertaUtc = null;
            s.RemainingAtPause = null;
        }

        public void AnnullaAstaCorrente(string lega)
        {
            var s = S(lega);
            s.GiocatoreInAsta = null;
            s.OfferenteAttuale = "-";
            s.OffertaAttuale = 0;
            s.AstaConclusa = false;

            s.AstaStartUtc = null;
            s.PausaAttiva = false;
            s.PausaStartUtc = null;
            s.PausaAccumulata = TimeSpan.Zero;

            s.FineOffertaUtc = null;
            s.RemainingAtPause = null;
        }

        // ===== Pausa / Ripresa =====
        public void MettiInPausa(string lega, DateTime nowUtc)
        {
            var s = S(lega);
            if (s.PausaAttiva) return;
            s.PausaAttiva = true;
            s.PausaStartUtc = nowUtc;

            if (s.FineOffertaUtc.HasValue)
            {
                var remaining = s.FineOffertaUtc.Value - nowUtc;
                s.RemainingAtPause = remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
            else s.RemainingAtPause = null;

            s.FineOffertaUtc = null;
        }

        public void Riprendi(string lega, DateTime nowUtc)
        {
            var s = S(lega);
            if (!s.PausaAttiva) return;

            if (s.PausaStartUtc.HasValue)
                s.PausaAccumulata += (nowUtc - s.PausaStartUtc.Value);

            s.PausaAttiva = false;
            s.PausaStartUtc = null;

            if (s.RemainingAtPause.HasValue)
                s.FineOffertaUtc = nowUtc + s.RemainingAtPause.Value;

            s.RemainingAtPause = null;
        }

        // ===== Info stato =====
        public TimeSpan GetDurataAsta(string lega, DateTime nowUtc)
        {
            var s = S(lega);
            if (!s.AstaStartUtc.HasValue) return TimeSpan.Zero;

            var elapsed = nowUtc - s.AstaStartUtc.Value - s.PausaAccumulata;
            if (s.PausaAttiva && s.PausaStartUtc.HasValue)
                elapsed -= (nowUtc - s.PausaStartUtc.Value);

            return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
        }

        public int GetRemainingOfferta(string lega, DateTime nowUtc)
        {
            var s = S(lega);
            if (s.PausaAttiva)
                return (int)Math.Ceiling((s.RemainingAtPause ?? TimeSpan.Zero).TotalSeconds);

            if (s.FineOffertaUtc.HasValue)
            {
                var sec = (s.FineOffertaUtc.Value - nowUtc).TotalSeconds;
                return (int)Math.Max(0, Math.Ceiling(sec));
            }
            return 0;
        }

        public (bool pausaAttiva, DateTime? astaStartUtc, TimeSpan pausaAccumulata, DateTime? pausaStartUtc, DateTime? fineOffertaUtc, bool mantraAttivo)
            GetStato(string lega)
        {
            var s = S(lega);
            return (s.PausaAttiva, s.AstaStartUtc, s.PausaAccumulata, s.PausaStartUtc, s.FineOffertaUtc, s.MantraAttivo);
        }

        public void ReimpostaFineAstaDaOra(string lega, int durataSecondi, DateTime nowUtc)
        {
            var s = S(lega);
            if (s.PausaAttiva) return;
            s.FineOffertaUtc = nowUtc.AddSeconds(Math.Max(1, durataSecondi));
        }

        // ===== Metodi classici per-lega =====
        public CalciatoreListone? GetGiocatoreInAsta(string lega) => S(lega).GiocatoreInAsta;
        public (string offerente, int offerta) GetOffertaAttuale(string lega)
        {
            var s = S(lega);
            return (s.OfferenteAttuale, s.OffertaAttuale);
        }

        public void AggiornaOfferta(string lega, string offerente, int offerta)
        {
            var s = S(lega);
            s.OfferenteAttuale = offerente;
            s.OffertaAttuale = offerta;

            if (!s.PausaAttiva)
                ReimpostaFineAstaDaOra(lega, s.DurataTimer, DateTime.UtcNow);
        }

        public bool AstaConclusa(string lega) => S(lega).AstaConclusa;
        public void SegnaAstaConclusa(string lega) => S(lega).AstaConclusa = true;

        public DateTime? GetFineOffertaUtc(string lega) => S(lega).FineOffertaUtc;
        public bool PausaAttiva(string lega) => S(lega).PausaAttiva;
    }
}
