namespace AstaLegheFC.Models.ViewModels
{
    public class SquadraRiepilogoViewModel
    {
        public int SquadraId { get; set; }
        public string Nickname { get; set; } = string.Empty;
        public int CreditiDisponibili { get; set; }
        public int PuntataMassima { get; set; }

        public int Portieri { get; set; }
        public int Difensori { get; set; }
        public int Centrocampisti { get; set; }
        public int Attaccanti { get; set; }

        public List<GiocatoreAssegnato> PortieriAssegnati { get; set; } = new();
        public List<GiocatoreAssegnato> DifensoriAssegnati { get; set; } = new();
        public List<GiocatoreAssegnato> CentrocampistiAssegnati { get; set; } = new();
        public List<GiocatoreAssegnato> AttaccantiAssegnati { get; set; } = new();
    }

    public class GiocatoreAssegnato
    {
        public int Id { get; set; } // <-- questo serve per svincolarlo
        public string Nome { get; set; } = string.Empty;
        public int CreditiSpesi { get; set; }
    }
}
