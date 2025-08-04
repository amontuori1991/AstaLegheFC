using System.ComponentModel.DataAnnotations.Schema;

namespace AstaLegheFC.Models
{
    public class Giocatore
    {
        public int Id { get; set; }
        public int IdListone { get; set; }
        public string Nome { get; set; }
        public string Ruolo { get; set; }

        public string RuoloMantra { get; set; }
        public string SquadraReale { get; set; }

        public int? SquadraId { get; set; }  // FK verso Squadra
        [ForeignKey("SquadraId")]
        public Squadra? Squadra { get; set; }

        public int? CreditiSpesi { get; set; }
    }
}
