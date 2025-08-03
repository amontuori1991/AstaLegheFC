namespace AstaLegheFC.Models
{
    public class UtenteViewModel
    {
        public string Nickname { get; set; }
        public int CreditiDisponibili { get; set; }
        public int PuntataMassima { get; set; }

        // CAMBIO QUI:
        public GiocatoreInAstaViewModel CalciatoreInAsta { get; set; }

        public string OfferenteAttuale { get; set; }
        public int OffertaAttuale { get; set; }
        public int TimerAsta { get; set; }
        public int PortieriAcquistati { get; set; }
        public int DifensoriAcquistati { get; set; }
        public int CentrocampistiAcquistati { get; set; }
        public int AttaccantiAcquistati { get; set; }
        public string LogoSquadra { get; set; }

    }


    public class GiocatoreViewModel
    {
        public string Nome { get; set; }
        public string Squadra { get; set; }
        public string Ruolo { get; set; }
    }
}
