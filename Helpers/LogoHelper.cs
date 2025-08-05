using System.Collections.Generic;

namespace AstaLegheFC.Helpers
{
    public static class LogoHelper
    {
        // Definiamo il modello base dell'URL
        private const string BaseUrl = "https://content.fantacalcio.it/web/img/team/";

        // Manteniamo un piccolo dizionario solo per le ECCEZIONI, 
        // cioè se il nome nel listone non corrisponde a quello nell'URL.
        // Esempio: se nel listone ci fosse "H. Verona", dovremmo mapparlo a "verona".
        // Al momento sembra non servire, ma è utile averlo per il futuro.
        private static readonly Dictionary<string, string> EccezioniNomi = new Dictionary<string, string>
        {
            // Esempio: { "H. Verona", "verona" }
        };

        public static string GetLogoUrl(string nomeSquadra)
        {
            if (string.IsNullOrEmpty(nomeSquadra))
            {
                return "";
            }

            string nomePerUrl;

            // 1. Controlla se c'è un'eccezione per questa squadra
            if (EccezioniNomi.ContainsKey(nomeSquadra))
            {
                nomePerUrl = EccezioniNomi[nomeSquadra];
            }
            else
            {
                // 2. Altrimenti, usa la regola standard: nome in minuscolo
                nomePerUrl = nomeSquadra.ToLower();
            }

            // 3. Costruisce e restituisce l'URL completo
            return $"{BaseUrl}{nomePerUrl}.png";
        }
    }
}