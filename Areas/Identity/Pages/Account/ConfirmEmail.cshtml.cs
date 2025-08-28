// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Text;
using System.Threading.Tasks;
using AstaLegheFC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AstaLegheFC.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ConfirmEmailModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly IConfiguration _config;
        private readonly ILogger<ConfirmEmailModel> _logger;

        public ConfirmEmailModel(
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            IConfiguration config,
            ILogger<ConfirmEmailModel> logger)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _config = config;
            _logger = logger;
        }

        public bool Succeeded { get; set; }
        public string Message { get; set; }

        public async Task<IActionResult> OnGetAsync(string userId, string code, string returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(code))
            {
                _logger.LogWarning("ConfirmEmail chiamata senza userId o code");
                return RedirectToPage("/Index");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("ConfirmEmail: utente non trovato (ID: {UserId})", userId);
                return NotFound($"Impossibile caricare l'utente con ID '{userId}'.");
            }

            // era già confermata prima?
            var wasAlreadyConfirmed = user.EmailConfirmed;

            // decode token
            string decoded;
            try
            {
                decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ConfirmEmail: errore nel decode del token per {Email}", user.Email);
                Message = "Token non valido.";
                Succeeded = false;
                return Page();
            }

            // conferma email
            var result = await _userManager.ConfirmEmailAsync(user, decoded);
            Succeeded = result.Succeeded;

            if (Succeeded)
            {
                Message = "Email confermata con successo. Ora puoi accedere.";
                _logger.LogInformation("ConfirmEmail: conferma riuscita per {Email}", user.Email);

                // Notifica admin solo la prima volta
                if (!wasAlreadyConfirmed)
                {
                    await NotificaAdminRegistrazioneConfermataAsync(user);
                }
            }
            else
            {
                Message = "Non è stato possibile confermare l'email (già confermata o token non valido).";
                _logger.LogWarning("ConfirmEmail: conferma NON riuscita per {Email}.", user.Email);
            }

            return Page();
        }

        private async Task NotificaAdminRegistrazioneConfermataAsync(ApplicationUser user)
        {
            // Destinatari: Notifications:AdminEmail (fallback Gmail:EmailAddress)
            var adminEmail = _config["Notifications:AdminEmail"];
            if (string.IsNullOrWhiteSpace(adminEmail))
            {
                adminEmail = _config["Gmail:EmailAddress"];
                _logger.LogWarning("ConfirmEmail: Notifications:AdminEmail non impostato. Uso fallback {AdminEmail}", adminEmail);
            }
            if (string.IsNullOrWhiteSpace(adminEmail))
            {
                _logger.LogWarning("ConfirmEmail: nessun AdminEmail disponibile. Notifica saltata.");
                return;
            }

            var subject = "Bazzer • Nuova registrazione confermata";
            var whenUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
            var appUrl = $"{Request.Scheme}://{Request.Host}";

            var html = $@"
<!DOCTYPE html>
<html lang=""it""><body style=""font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif"">
  <div style=""background:#0e1724;padding:16px"">
    <div style=""max-width:640px;margin:0 auto;background:#223245;border-radius:12px;padding:20px;color:#e8eef6"">
      <h2 style=""margin:0 0 10px"">Nuovo utente confermato</h2>
      <p style=""margin:0 0 8px;color:#c8d3e0"">
        L'utente <strong>{System.Net.WebUtility.HtmlEncode(user.Email)}</strong> ha confermato l'email.
      </p>
      <p style=""margin:0 0 8px;color:#c8d3e0"">Conferma: {whenUtc}</p>
      <p style=""margin:14px 0 0;font-size:12px;color:#aab4c0"">
        Puoi ora procedere con l'assegnazione della licenza, se necessario.
      </p>
      <p style=""margin:10px 0 0;font-size:12px;""><a href=""{appUrl}"" style=""color:#8cc9ff"">{appUrl}</a></p>
    </div>
  </div>
</body></html>";

            try
            {
                _logger.LogInformation("ConfirmEmail: invio notifica admin a {AdminEmail} per utente {UserEmail}", adminEmail, user.Email);
                await _emailSender.SendEmailAsync(adminEmail, subject, html);
                _logger.LogInformation("ConfirmEmail: notifica admin inviata");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ConfirmEmail: invio notifica admin FALLITO a {AdminEmail} per utente {UserEmail}", adminEmail, user.Email);
            }
        }
    }
}
