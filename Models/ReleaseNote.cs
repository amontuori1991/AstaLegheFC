using System;

namespace AstaLegheFC.Models
{
    public class ReleaseNote
    {
        public string Version { get; set; } = "";
        public string Title { get; set; } = "";      // prima riga del commit
        public string Body { get; set; } = "";       // resto del messaggio (se presente)
        public string CommitSha { get; set; } = "";
        public DateTimeOffset Date { get; set; }
        public string Author { get; set; } = "";
        public string HtmlUrl { get; set; } = "";    // link al commit
    }
}
