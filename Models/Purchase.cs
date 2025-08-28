using System;

namespace AstaLegheFC.Models
{
    public class Purchase
    {
        public int Id { get; set; }
        public string UserId { get; set; } = default!;
        public string Plan { get; set; } = default!;     // "1M" | "6M" | "12M" | "LIFE"
        public int AmountCents { get; set; }
        public string Currency { get; set; } = "EUR";

        public string Provider { get; set; } = default!; // "Stripe" | "PayPal"
        public string? ProviderSessionId { get; set; }   // Stripe: Checkout Session Id
        public string? ProviderOrderId { get; set; }     // PayPal: Order Id

        public string Status { get; set; } = "Pending";  // Pending | Paid | Canceled | Failed
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? PaidAt { get; set; }
    }
}
