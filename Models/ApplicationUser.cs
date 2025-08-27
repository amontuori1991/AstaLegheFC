using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace AstaLegheFC.Models
{
    public class ApplicationUser : IdentityUser
    {
        // "monthly", "semiannual", "annual", "lifetime" (libero testo per ora)
        public string? LicensePlan { get; set; }

        // Scadenza (UTC). Per "lifetime" puoi lasciarlo NULL.
        public DateTimeOffset? LicenseExpiresAt { get; set; }

        // (opzionale) Data di attivazione
        public DateTimeOffset? LicenseActivatedAt { get; set; }

        [NotMapped]
        public bool IsLicenseActive =>
            string.Equals(LicensePlan, "lifetime", StringComparison.OrdinalIgnoreCase)
            || (LicenseExpiresAt.HasValue && LicenseExpiresAt.Value > DateTimeOffset.UtcNow);
    }
}
