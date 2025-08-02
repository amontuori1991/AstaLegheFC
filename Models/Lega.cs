namespace AstaLegheFC.Models
{
    public class Lega
    {
        public int Id { get; set; }
        public string Nome { get; set; }
        public string Alias { get; set; }
        public int CreditiIniziali { get; set; }

        public ICollection<Squadra> Squadres { get; set; } = new List<Squadra>();
    }
}
