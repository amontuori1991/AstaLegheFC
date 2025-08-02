namespace AstaLegheFC.Models
{
    public class CalciatoreListone
    {
        public int Id { get; set; }                  // PK EF
        public int IdListone { get; set; }           // Excel: Id
        public string Nome { get; set; }             // Excel: Nome
        public string Ruolo { get; set; }            // Excel: R
        public string RuoloMantra { get; set; }      // Excel: RM
        public string Squadra { get; set; }          // Excel: Squadra
        public int? QtA { get; set; }                // Excel: Qt.A
        public int? QtI { get; set; }                // Excel: Qt.I
        public int? Diff { get; set; }               // Excel: Diff.
        public int? QtAM { get; set; }               // Excel: Qt.A M
        public int? QtIM { get; set; }               // Excel: Qt.I M
        public int? DiffM { get; set; }              // Excel: Diff.M
        public int? FVM { get; set; }                // Excel: FVM
        public int? FVMM { get; set; }               // Excel: FVM M
    }
}
