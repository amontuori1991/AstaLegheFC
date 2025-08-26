using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AstaLegheFC.Models
{
    public class Lega
    {
        public int Id { get; set; }
        public string Nome { get; set; }
        public string Alias { get; set; }
        public int CreditiIniziali { get; set; }
        public string? AdminId { get; set; }
        [Display(Name = "Max Portieri")]
        public int MaxPortieri { get; set; } = 3; // Impostiamo un valore di default

        [Display(Name = "Max Difensori")]
        public int MaxDifensori { get; set; } = 8;

        [Display(Name = "Max Centrocampisti")]
        public int MaxCentrocampisti { get; set; } = 8;

        [Display(Name = "Max Attaccanti")]
        public int MaxAttaccanti { get; set; } = 6;
        public List<Squadra> Squadre { get; set; } = new List<Squadra>();

    }
}