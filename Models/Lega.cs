using System.Collections.Generic; // Assicurati che ci sia questo using

namespace AstaLegheFC.Models
{
    public class Lega
    {
        public int Id { get; set; }
        public string Nome { get; set; }
        public string Alias { get; set; }
        public int CreditiIniziali { get; set; }

        // ✅ AGGIUNGI QUESTA RIGA
        public List<Squadra> Squadre { get; set; } = new List<Squadra>();
    }
}