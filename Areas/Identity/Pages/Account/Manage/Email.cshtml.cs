using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using AstaLegheFC.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AstaLegheFC.Areas.Identity.Pages.Account.Manage
{
    public class EmailModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;

        public EmailModel(UserManager<ApplicationUser> userManager,
                          SignInManager<ApplicationUser> signInManager,
                          IEmailSender emailSender)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
        }

        public string Email { get; set; }
        public bool IsEmailConfirmed { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public class InputModel
        {
            [Required, EmailAddress]
            [Display(Name = "Nuova email")]
            public string NewEmail { get; set; }
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("Utente non trovato.");

            Email = await _userManager.GetEmailAsync(user);
            IsEmailConfirmed = await _userManager.IsEmailConfirmedAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("Utente non trovato.");
            if (!ModelState.IsValid) { await OnGetAsync(); return Page(); }

            var currentEmail = await _userManager.GetEmailAsync(user);
            if (Input.NewEmail == currentEmail)
            {
                StatusMessage = "La nuova email coincide con quella attuale.";
                return RedirectToPage();
            }

            // Applica subito la modifica (evita il flusso di conferma se non ti serve)
            var token = await _userManager.GenerateChangeEmailTokenAsync(user, Input.NewEmail);
            var res = await _userManager.ChangeEmailAsync(user, Input.NewEmail, token);
            if (!res.Succeeded)
            {
                foreach (var e in res.Errors) ModelState.AddModelError(string.Empty, e.Description);
                await OnGetAsync();
                return Page();
            }

            // Se vuoi che UserName segua l'email:
            await _userManager.SetUserNameAsync(user, Input.NewEmail);

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "Email aggiornata.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSendVerificationEmailAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("Utente non trovato.");

            var email = await _userManager.GetEmailAsync(user);
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);

            var url = Url.Page("/Account/ConfirmEmail", null,
                new { userId = user.Id, code }, Request.Scheme);

            await _emailSender.SendEmailAsync(email, "Conferma la tua email",
                $"Conferma cliccando <a href='{HtmlEncoder.Default.Encode(url)}'>qui</a>.");

            StatusMessage = "Email di verifica inviata.";
            return RedirectToPage();
        }
    }
}
