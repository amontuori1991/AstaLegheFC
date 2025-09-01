namespace AstaLegheFC.Models.ViewModels
{
    public class ListoneViewModel
    {
        public List<CalciatoreListone> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }

        public int TotalItems { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }
}
