// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using AstaLegheFC.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace AstaLegheFC.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "La {0} deve essere lunga almeno {2} e al massimo {1} caratteri.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Conferma password")]
            [Compare("Password", ErrorMessage = "La password e la conferma non coincidono.")]
            public string ConfirmPassword { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                var user = CreateUser();

                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);
                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("User created a new account with password.");

                    var userId = await _userManager.GetUserIdAsync(user);
                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = userId, code = code, returnUrl = returnUrl },
                        protocol: Request.Scheme);

                    // ======= EMAIL PERSONALIZZATA =======
                    var subject = "Benvenuto su Bazzer – Conferma la tua email";
                    var logoUrl = $"{Request.Scheme}://{Request.Host}/png/logo_bazzer.png";
                    var appUrl = $"{Request.Scheme}://{Request.Host}";
                    var safeLink = HtmlEncoder.Default.Encode(callbackUrl);

                    var htmlMessage = $@"
<!DOCTYPE html>
<html lang=""it"">
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>{subject}</title>
  <style>
    /* Client-safe inline CSS fallback per i client che supportano <style> */
    @media (prefers-color-scheme: dark) {{
      .card {{ background:#1f2a39 !important; }}
      .text-muted {{ color:#b7c0cc !important; }}
    }}
  </style>
</head>
<body style=""margin:0; padding:0; background:#0e1724; font-family: -apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;"">
  <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" align=""center"" width=""100%"" style=""background:#0e1724; padding:24px 12px;"">
    <tr>
      <td align=""center"">
        <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""600"" style=""max-width:600px; width:100%;"">
          <tr>
            <td align=""center"" style=""padding-bottom:18px;"">
              <img src=""{logoUrl}"" alt=""Bazzer"" width=""180"" style=""display:block; max-width:180px; height:auto;"">
            </td>
          </tr>
          <tr>
            <td class=""card"" style=""background:#223245; border-radius:14px; padding:28px; color:#e8eef6; box-shadow:0 10px 30px rgba(0,0,0,.35);"">
              <h1 style=""margin:0 0 8px; font-size:22px; line-height:1.35; font-weight:800;"">Conferma il tuo indirizzo email</h1>
              <p style=""margin:0 0 16px; color:#c8d3e0; font-size:15px;"">
                Ciao, grazie per esserti registrato su <strong>AstaLehe FC</strong>!
                Per attivare il tuo account ti basta confermare l'indirizzo email.
              </p>

              <!-- Pulsante -->
              <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""margin:22px 0 10px;"">
                <tr>
                  <td align=""center"" bgcolor=""#e91e63"" style=""border-radius:10px;"">
                    <a href=""{safeLink}"" target=""_blank""
                       style=""display:inline-block; padding:12px 22px; color:#ffffff; text-decoration:none; font-weight:700; border-radius:10px; font-size:16px;"">
                      Conferma email
                    </a>
                  </td>
                </tr>
              </table>

              <p class=""text-muted"" style=""margin:14px 0 0; font-size:12px; color:#aab4c0;"">
                Se il pulsante non funziona copia e incolla questo link nel browser:
              </p>
              <p style=""word-break:break-all; font-size:12px; margin:8px 0 0;"">
                <a href=""{safeLink}"" style=""color:#8cc9ff; text-decoration:underline;"">{safeLink}</a>
              </p>

              <hr style=""border:none; border-top:1px solid #314257; margin:22px 0;"">

              <p style=""margin:0; font-size:12px; color:#aab4c0;"">
                Questa email è stata inviata all'indirizzo fornito in fase di registrazione.
                Se non hai creato un account, ignora questo messaggio.
              </p>
            </td>
          </tr>
          <tr>
            <td align=""center"" style=""padding:14px 6px; color:#8fa0b3; font-size:12px;"">
              © {DateTime.UtcNow.Year} Bazzer • <a href=""{appUrl}"" style=""color:#8cc9ff; text-decoration:none;"">{appUrl}</a>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";

                    await _emailSender.SendEmailAsync(Input.Email, subject, htmlMessage);
                    // ======= /EMAIL PERSONALIZZATA =======

                    if (_userManager.Options.SignIn.RequireConfirmedAccount)
                    {
                        return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl = returnUrl });
                    }
                    else
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        return LocalRedirect(returnUrl);
                    }
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // Se arriviamo qui, qualcosa è andato storto
            return Page();
        }

        private ApplicationUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<ApplicationUser>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(ApplicationUser)}'. " +
                    $"Ensure that '{nameof(ApplicationUser)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
            }
        }

        private IUserEmailStore<ApplicationUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<ApplicationUser>)_userStore;
        }
    }
}
