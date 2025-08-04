namespace AstaLegheFC.Models
{
    public class UtenteViewModel
    {
        public string Nickname { get; set; }
        public int CreditiDisponibili { get; set; }
        public int PuntataMassima { get; set; }
        public GiocatoreInAstaViewModel? CalciatoreInAsta { get; set; }
        public string OfferenteAttuale { get; set; }
        public int OffertaAttuale { get; set; }
        public int TimerAsta { get; set; }
        public int PortieriAcquistati { get; set; }
        public int DifensoriAcquistati { get; set; }
        public int CentrocampistiAcquistati { get; set; }
        public int AttaccantiAcquistati { get; set; }
        public string LogoSquadra { get; set; }
        public bool MantraAttivo { get; set; }
    }

    // ASSICURATI CHE LA CLASSE GiocatoreInAstaViewModel SIA STATA CANCELLATA DA QUESTO FILE
}