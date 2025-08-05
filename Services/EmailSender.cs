using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace AstaLegheFC.Services
{
    // Classe per le impostazioni lette da appsettings.json
    public class GmailSettings
    {
        public string EmailAddress { get; set; }
        public string AppPassword { get; set; }
        public string SenderName { get; set; }
    }

    public class EmailSender : IEmailSender
    {
        private readonly GmailSettings _settings;

        public EmailSender(IOptions<GmailSettings> settings)
        {
            _settings = settings.Value;
        }

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var client = new SmtpClient("smtp.gmail.com", 587)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_settings.EmailAddress, _settings.AppPassword)
            };

            var mailMessage = new MailMessage(
                from: new MailAddress(_settings.EmailAddress, _settings.SenderName),
                to: new MailAddress(email))
            {
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true
            };

            return client.SendMailAsync(mailMessage);
        }
    }
}