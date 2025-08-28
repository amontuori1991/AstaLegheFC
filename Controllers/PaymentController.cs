using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using AstaLegheFC.Data;
using AstaLegheFC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Stripe;
using Stripe.Checkout;
using Microsoft.AspNetCore.Http;

namespace AstaLegheFC.Controllers
{

    public class PaymentController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _cfg;
        private readonly IEmailSender _emailSender;

        public PaymentController(AppDbContext db, UserManager<ApplicationUser> userManager, IConfiguration cfg, IEmailSender emailSender)
        {
            _db = db; _userManager = userManager; _cfg = cfg; _emailSender = emailSender;
        }

        private (int cents, string label) PriceFor(string plan) => plan switch
        {
            "1M" => (490, "Abbonamento 1 mese"),
            "6M" => (2490, "Abbonamento 6 mesi"),
            "12M" => (3990, "Abbonamento 12 mesi"),
            "LIFE" => (7900, "Licenza a vita"),
            _ => throw new InvalidOperationException("Piano non valido")
        };
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Begin(string plan, string provider)
        {
            plan = (plan ?? "").ToUpperInvariant();
            provider = (provider ?? "").ToLowerInvariant();

            // se NON autenticato: salva scelta in Session e manda al login con returnUrl LOCALE
            if (!(User?.Identity?.IsAuthenticated ?? false))
            {
                HttpContext.Session.SetString("pay:plan", plan);
                HttpContext.Session.SetString("pay:provider", provider);

                // returnUrl deve essere locale (niente https://...)
                var resumeLocal = Url.Action("Resume", "Payment"); // "/Payment/Resume"
                return Redirect($"/Identity/Account/Login?returnUrl={Uri.EscapeDataString(resumeLocal)}");
            }

            // se già autenticato: view che auto-posta su Start
            ViewData["Plan"] = plan;
            ViewData["Provider"] = provider;
            return View();
        }


        [Authorize]
        [HttpGet]
        public IActionResult Resume()
        {
            var plan = HttpContext.Session.GetString("pay:plan");
            var provider = HttpContext.Session.GetString("pay:provider");

            if (string.IsNullOrWhiteSpace(plan) || string.IsNullOrWhiteSpace(provider))
                return RedirectToAction("Index", "Site");

            // opzionale: non pulisco la Session per consentire retry
            return RedirectToAction(nameof(Begin), new { plan, provider });
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Start(string plan, string provider)
        {
            plan = (plan ?? "").ToUpperInvariant();
            provider = (provider ?? "").ToLowerInvariant();

            if (!(new[] { "1M", "6M", "12M", "LIFE" }.Contains(plan)))
                return BadRequest("Piano non valido.");
            if (!(new[] { "stripe", "paypal" }.Contains(provider)))
                return BadRequest("Provider non valido.");

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var (amount, label) = PriceFor(plan);
            var purchase = new Purchase
            {
                UserId = user.Id,
                Plan = plan,
                AmountCents = amount,
                Currency = "EUR",
                Provider = provider.Equals("stripe") ? "Stripe" : "PayPal",
                Status = "Pending"
            };
            _db.Purchases.Add(purchase);
            await _db.SaveChangesAsync();

            var baseUrl = _cfg["Payments:SiteBaseUrl"]?.TrimEnd('/') ?? $"{Request.Scheme}://{Request.Host}";

            if (provider == "stripe")
            {
                StripeConfiguration.ApiKey = _cfg["Payments:Stripe:SecretKey"];
                var options = new SessionCreateOptions
                {
                    Mode = "payment",
                    SuccessUrl = $"{baseUrl}/Payment/StripeSuccess?pid={purchase.Id}",
                    CancelUrl = $"{baseUrl}/Payment/Cancel?pid={purchase.Id}",
                    LineItems = new System.Collections.Generic.List<SessionLineItemOptions>
                    {
                        new SessionLineItemOptions
                        {
                            Quantity = 1,
                            PriceData = new SessionLineItemPriceDataOptions
                            {
                                Currency = "eur",
                                UnitAmount = amount,
                                ProductData = new SessionLineItemPriceDataProductDataOptions { Name = label }
                            }
                        }
                    },
                    Metadata = new System.Collections.Generic.Dictionary<string, string>
                    {
                        ["purchase_id"] = purchase.Id.ToString(),
                        ["user_id"] = user.Id,
                        ["plan"] = plan
                    }
                };
                var service = new SessionService();
                var session = await service.CreateAsync(options);
                purchase.ProviderSessionId = session.Id;
                await _db.SaveChangesAsync();
                return Redirect(session.Url);
            }
            else
            {
                // PAYPAL
                var mode = _cfg["Payments:PayPal:Mode"]?.ToLowerInvariant() == "live"
                    ? "https://api-m.paypal.com"
                    : "https://api-m.sandbox.paypal.com";

                var clientId = _cfg["Payments:PayPal:ClientId"];
                var clientSecret = _cfg["Payments:PayPal:ClientSecret"];

                using var http = new HttpClient();
                // Token
                var basic = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{clientId}:{clientSecret}"));
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", basic);
                var tokenRes = await http.PostAsync($"{mode}/v1/oauth2/token",
                    new FormUrlEncodedContent(new[] {
                        new System.Collections.Generic.KeyValuePair<string,string>("grant_type","client_credentials")
                    }));
                var tokenDoc = JsonDocument.Parse(await tokenRes.Content.ReadAsStringAsync());
                var accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString();
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                // Ordine
                var payload = new
                {
                    intent = "CAPTURE",
                    purchase_units = new[] {
                        new {
                            amount = new {
                                currency_code = "EUR",
                                value = (amount / 100.0).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                            }
                        }
                    },
                    application_context = new
                    {
                        return_url = $"{baseUrl}/Payment/PayPalSuccess?pid={purchase.Id}",
                        cancel_url = $"{baseUrl}/Payment/Cancel?pid={purchase.Id}"
                    }
                };
                var createRes = await http.PostAsync($"{mode}/v2/checkout/orders",
                    new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json"));
                var createDoc = JsonDocument.Parse(await createRes.Content.ReadAsStringAsync());
                var orderId = createDoc.RootElement.GetProperty("id").GetString();
                purchase.ProviderOrderId = orderId;
                await _db.SaveChangesAsync();

                var approve = createDoc.RootElement.GetProperty("links").EnumerateArray()
                    .First(l => l.GetProperty("rel").GetString() == "approve")
                    .GetProperty("href").GetString();

                return Redirect(approve);
            }
        }

        [AllowAnonymous]
        public IActionResult Cancel(int pid)
        {
            TempData["ContactErr"] = "Pagamento annullato.";
            return RedirectToAction("Index", "Site");
        }

        [Authorize]
        public IActionResult StripeSuccess(int pid)
        {
            TempData["ContactOk"] = "Pagamento ricevuto! Attivazione in corso…";
            return RedirectToAction("Index", "Site");
        }

        [Authorize]
        public async Task<IActionResult> PayPalSuccess(int pid)
        {
            var p = await _db.Purchases.FindAsync(pid);
            if (p == null || p.Provider != "PayPal") return RedirectToAction("Index", "Site");

            var mode = _cfg["Payments:PayPal:Mode"]?.ToLowerInvariant() == "live"
                ? "https://api-m.paypal.com"
                : "https://api-m.sandbox.paypal.com";

            var clientId = _cfg["Payments:PayPal:ClientId"];
            var clientSecret = _cfg["Payments:PayPal:ClientSecret"];

            using var http = new HttpClient();
            var basic = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{clientId}:{clientSecret}"));
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", basic);
            var tokenRes = await http.PostAsync($"{mode}/v1/oauth2/token",
                new FormUrlEncodedContent(new[] {
                    new System.Collections.Generic.KeyValuePair<string,string>("grant_type","client_credentials")
                }));
            var tokenDoc = JsonDocument.Parse(await tokenRes.Content.ReadAsStringAsync());
            var accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString();
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var capRes = await http.PostAsync($"{mode}/v2/checkout/orders/{p.ProviderOrderId}/capture",
                new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

            if (!capRes.IsSuccessStatusCode)
            {
                TempData["ContactErr"] = "Non è stato possibile confermare il pagamento PayPal.";
                return RedirectToAction("Index", "Site");
            }

            await MarkPaidAndActivateAsync(p);
            TempData["ContactOk"] = "Pagamento completato! Abbonamento attivato.";
            return RedirectToAction("Index", "Site");
        }

        // ============== STRIPE WEBHOOK ==============
        // Risponde sia a /Payment/StripeWebhook sia a /payments/webhook
        [AllowAnonymous]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Consumes("application/json")]
        [Route("Payment/StripeWebhook")]
        [Route("payments/webhook")]
        public async Task<IActionResult> StripeWebhook()
        {
            var json = await new System.IO.StreamReader(Request.Body).ReadToEndAsync();
            var secret = _cfg["Payments:Stripe:WebhookSecret"];
            Event stripeEvent;
            try
            {
                stripeEvent = EventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"], secret);
            }
            catch
            {
                return BadRequest(); // firma non valida o payload malformato
            }

            if (stripeEvent.Type == "checkout.session.completed")
            {
                var session = stripeEvent.Data.Object as Session;

                // 1) usa purchase_id messo in Metadata
                if (session?.Metadata != null &&
                    session.Metadata.TryGetValue("purchase_id", out var pidStr) &&
                    int.TryParse(pidStr, out var pid))
                {
                    var p = await _db.Purchases.FindAsync(pid);
                    if (p != null && p.Status != "Paid")
                        await MarkPaidAndActivateAsync(p);
                }
                else if (session != null && !string.IsNullOrEmpty(session.Id))
                {
                    // 2) fallback: trova per ProviderSessionId
                    var p = await _db.Purchases.FirstOrDefaultAsync(x => x.ProviderSessionId == session.Id);
                    if (p != null && p.Status != "Paid")
                        await MarkPaidAndActivateAsync(p);
                }
            }

            return Ok();
        }
        // in cima al controller (fuori dal metodo) se non l'hai già messo:
        private static readonly DateTimeOffset FAR_FUTURE =
            new DateTimeOffset(new DateTime(2099, 12, 31, 0, 0, 0, DateTimeKind.Utc));

        private async Task MarkPaidAndActivateAsync(Purchase p)
        {
            p.Status = "Paid";
            p.PaidAt = DateTimeOffset.UtcNow;

            var user = await _db.Users.FirstAsync(u => u.Id == p.UserId);

            // Imposta SEMPRE il piano per il gating
            user.LicensePlan = p.Plan; // "1M" | "6M" | "12M" | "LIFE"

            // Calcola la scadenza (per LIFE è solo informativa; il gating guarda il piano)
            DateTimeOffset? expiry = p.Plan switch
            {
                "1M" => DateTimeOffset.UtcNow.AddMonths(1),
                "6M" => DateTimeOffset.UtcNow.AddMonths(6),
                "12M" => DateTimeOffset.UtcNow.AddYears(1),
                "LIFE" => FAR_FUTURE,
                _ => DateTimeOffset.UtcNow.AddMonths(1)
            };
            user.LicenseExpiresAt = expiry;

            await _db.SaveChangesAsync();

            // Email di conferma (admin + utente)
            var admin = _cfg["Notifications:AdminEmail"];
            var when = DateTimeOffset.UtcNow.ToString("dd/MM/yyyy HH:mm") + " UTC";
            var planLabel = p.Plan switch { "1M" => "1 mese", "6M" => "6 mesi", "12M" => "12 mesi", "LIFE" => "A vita", _ => "—" };
            var scad = (p.Plan == "LIFE")
                ? "Mai (a vita)"
                : (expiry.HasValue ? expiry.Value.ToString("dd/MM/yyyy") : "Non attiva");

            var html = $@"
<div style='font-family:Segoe UI,Roboto,Arial,sans-serif'>
  <h2>Pagamento confermato</h2>
  <p>Ciao {user.Email}, grazie! Il tuo piano <b>{planLabel}</b> è stato attivato.</p>
  <p><b>Scadenza:</b> {scad}</p>
  <p style='color:#888'>Transazione {p.Provider} #{p.Id} — {when}</p>
</div>";

            try
            {
                await _emailSender.SendEmailAsync(user.Email, "AstaLeghe FC — Abbonamento attivato", html);
                if (!string.IsNullOrWhiteSpace(admin))
                    await _emailSender.SendEmailAsync(admin, "Nuovo abbonamento attivato",
                        $"Utente: {user.Email}<br/>Piano: {planLabel}<br/>Scadenza: {scad}");
            }
            catch
            {
                // Non bloccare in caso di errore email
            }
        }

    }
}
