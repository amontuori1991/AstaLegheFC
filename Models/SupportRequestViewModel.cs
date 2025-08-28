using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace AstaLegheFC.Models
{
    public class SupportRequestViewModel
    {
        [Required(ErrorMessage = "L'oggetto è obbligatorio")]
        [MaxLength(150)]
        public string Subject { get; set; }

        [Required(ErrorMessage = "Inserisci la tua richiesta")]
        [MaxLength(4000)]
        public string Message { get; set; }

        // Upload multiplo di immagini
        public List<IFormFile> Files { get; set; } = new();
    }
}
