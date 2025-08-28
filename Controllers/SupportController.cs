using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using AstaLegheFC.Models;
using AstaLegheFC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace AstaLegheFC.Controllers
{
    [Authorize]
    public class SupportController : Controller
    {
        private readonly GmailSettings _gmail;
        private readonly string _adminEmail;

        public SupportController(IOptions<GmailSettings> gmailOptions, IConfiguration cfg)
        {
            _gmail = gmailOptions.Value;
            _adminEmail = cfg["Notifications:AdminEmail"] ?? _gmail.EmailAddress;
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new SupportRequestViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SupportRequestViewModel model)
        {
            // Validazioni lato server
            if (!ModelState.IsValid) return View(model);

            // Limiti upload
            const int maxFiles = 5;
            const long maxPerFile = 4 * 1024 * 1024; // 4 MB
            const long maxTotal = 12 * 1024 * 1024;  // 12 MB complessivi

            if (model.Files?.Count > maxFiles)
            {
                ModelState.AddModelError(string.Empty, $"Puoi allegare al massimo {maxFiles} immagini.");
                return View(model);
            }

            long totalSize = 0;
            foreach (var f in model.Files ?? Enumerable.Empty<Microsoft.AspNetCore.Http.IFormFile>())
            {
                if (f.Length == 0) continue;
                if (!f.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(string.Empty, $"File \"{f.FileName}\" non è un'immagine.");
                    return View(model);
                }
                if (f.Length > maxPerFile)
                {
                    ModelState.AddModelError(string.Empty, $"L'immagine \"{f.FileName}\" supera {maxPerFile / (1024 * 1024)} MB.");
                    return View(model);
                }
                totalSize += f.Length;
                if (totalSize > maxTotal)
                {
                    ModelState.AddModelError(string.Empty, $"Dimensione totale allegati oltre {maxTotal / (1024 * 1024)} MB.");
                    return View(model);
                }
            }

            var userEmail = User?.Identity?.Name ?? "(sconosciuto)";
            var subject = $"[Assistenza] {model.Subject}".Trim();
            var appUrl = $"{Request.Scheme}://{Request.Host}";

            var bodyHtml = $@"
<!DOCTYPE html>
<html lang=""it"">
  <body style=""font-family: -apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif; background:#0e1724; color:#e8eef6; padding:16px;"">
    <div style=""max-width:680px;margin:0 auto;background:#223245;border:1px solid #314257;border-radius:12px;padding:18px 22px;"">
      <h2 style=""margin:0 0 10px;"">Richiesta di assistenza</h2>
      <p style=""margin:0 0 6px;color:#c8d3e0""><strong>Da:</strong> {WebUtility.HtmlEncode(userEmail)}</p>
      <p style=""margin:0 0 6px;color:#c8d3e0""><strong>Oggetto:</strong> {WebUtility.HtmlEncode(model.Subject)}</p>
      <hr style=""border:none;border-top:1px solid #314257;margin:14px 0;"">
      <div style=""white-space:pre-wrap;line-height:1.6;"">{WebUtility.HtmlEncode(model.Message)}</div>
      <hr style=""border:none;border-top:1px solid #314257;margin:16px 0;"">
      <p style=""margin:0;color:#aab4c0;font-size:12px"">Inviata da <a href=""{appUrl}"" style=""color:#8cc9ff;text-decoration:none"">Bazzer</a></p>
    </div>
  </body>
</html>";

            // Invio via SMTP con allegati
            try
            {
                using var client = new SmtpClient("smtp.gmail.com", 587)
                {
                    EnableSsl = true,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(_gmail.EmailAddress, _gmail.AppPassword)
                };

                using var mail = new MailMessage
                {
                    Subject = subject,
                    Body = bodyHtml,
                    IsBodyHtml = true,
                    From = new MailAddress(_gmail.EmailAddress, _gmail.SenderName)
                };

                // To admin + CC utente
                mail.To.Add(new MailAddress(_adminEmail));
                if (!string.IsNullOrWhiteSpace(userEmail) && userEmail.Contains("@"))
                    mail.CC.Add(new MailAddress(userEmail));

                // Allegati
                foreach (var f in model.Files ?? Enumerable.Empty<Microsoft.AspNetCore.Http.IFormFile>())
                {
                    if (f?.Length > 0)
                    {
                        using var ms = new MemoryStream();
                        await f.CopyToAsync(ms);
                        ms.Position = 0;
                        var att = new Attachment(new MemoryStream(ms.ToArray()), f.FileName, f.ContentType);
                        mail.Attachments.Add(att);
                    }
                }

                await client.SendMailAsync(mail);
            }
            catch (Exception ex)
            {
                // feedback utente
                ModelState.AddModelError(string.Empty, $"Invio non riuscito: {ex.Message}");
                return View(model);
            }

            TempData["SupportOk"] = "Richiesta inviata correttamente. Ti abbiamo mandato copia via email.";
            return RedirectToAction(nameof(Create));
        }
    }
}
