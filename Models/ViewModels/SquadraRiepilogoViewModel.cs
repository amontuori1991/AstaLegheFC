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

    // File: Models/ViewModels/SquadraRiepilogoViewModel.cs

    public class GiocatoreAssegnato
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public int CreditiSpesi { get; set; }

        // 👇 ASSICURATI DI AGGIUNGERE QUESTE DUE RIGHE 👇
        public string SquadraReale { get; set; }
        public string LogoUrl { get; set; }
        public string Ruolo { get; set; }

        public string RuoloMantra { get; set; }
    }
}
