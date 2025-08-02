namespace AstaLegheFC.Models
{
    public class Giocatore
    {
        public int Id { get; set; }
        public string Nome { get; set; }
        public string SquadraReale { get; set; }
        public string Ruolo { get; set; }
        public int? SquadraId { get; set; }
        public int? IdListone { get; set; }
        public int? CreditiSpesi { get; set; }

        public Squadra? Squadra { get; set; }
    }
}
