using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace AstaLegheFC.Controllers
{
    public class SiteController : Controller
    {
        private readonly IConfiguration _config;

        public SiteController(IConfiguration config)
        {
            _config = config;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendContact(string Name, string Email, string Subject, string Message)
        {
            var adminEmail = _config["Notifications:AdminEmail"];
            var from = _config["Gmail:EmailAddress"];
            var pass = _config["Gmail:AppPassword"];

            if (string.IsNullOrEmpty(adminEmail) || string.IsNullOrEmpty(from) || string.IsNullOrEmpty(pass))
            {
                TempData["ContactErr"] = "Impossibile inviare la mail: configurazione mancante.";
                return RedirectToAction("Index", "Site", new { area = "" });
            }

            try
            {
                var body = $@"
                    Nuova richiesta dal sito AstaLeghe FC
                    -----------------------------------
                    Nome: {Name}
                    Email: {Email}
                    Oggetto: {Subject}

                    Messaggio:
                    {Message}
                ";

                using var smtp = new SmtpClient("smtp.gmail.com", 587)
                {
                    Credentials = new NetworkCredential(from, pass),
                    EnableSsl = true
                };

                var mail = new MailMessage
                {
                    From = new MailAddress(from, "AstaLeghe FC – Contatti"),
                    Subject = $"[Contatto Sito] {Subject}",
                    Body = body,
                    IsBodyHtml = false
                };

                mail.To.Add(adminEmail);
                mail.ReplyToList.Add(new MailAddress(Email));

                await smtp.SendMailAsync(mail);

                TempData["ContactOk"] = "Messaggio inviato con successo! Ti risponderemo a breve.";
            }
            catch
            {
                TempData["ContactErr"] = "Errore nell'invio. Riprova più tardi.";
            }

            return RedirectToAction("Index", "Site", new { area = "" });
        }

        public IActionResult Index() => View();
    }
}
