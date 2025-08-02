using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace AstaLegheFC.Models
{
    public class Squadra
    {
        public int Id { get; set; }

        [Required]
        public string Nome { get; set; } = string.Empty;
        public string Nickname { get; set; }
        public int Crediti { get; set; }
        public int Portieri { get; set; }
        public int Difensori { get; set; }
        public int Centrocampisti { get; set; }
        public int Attaccanti { get; set; }
        public int LegaId { get; set; }

        public Lega? Lega { get; set; }

        // Giocatori è sufficiente
        public List<Giocatore> Giocatori { get; set; } = new();
    }
}
