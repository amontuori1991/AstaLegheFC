using System;
using System.Linq;
using System.Threading.Tasks;
using AstaLegheFC.Filters;
using AstaLegheFC.Models;
using AstaLegheFC.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AstaLegheFC.Controllers
{
    public class SuperAdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _config;

        // Per ora usiamo le credenziali “richieste”.
        private const string DefaultUser = "system_admin";
        private const string DefaultPass = "Samsung1991";

        public SuperAdminController(UserManager<ApplicationUser> userManager, IConfiguration config)
        {
            _userManager = userManager;
            _config = config;
        }

        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            return View(new SuperAdminLoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(SuperAdminLoginViewModel model)
        {
            // Possibilità di spostare in appsettings: "SuperAdmin:Username", "SuperAdmin:Password"
            var expectedUser = _config["SuperAdmin:Username"] ?? DefaultUser;
            var expectedPass = _config["SuperAdmin:Password"] ?? DefaultPass;

            if (string.Equals(model.Username, expectedUser, StringComparison.Ordinal) &&
                string.Equals(model.Password, expectedPass, StringComparison.Ordinal))
            {
                HttpContext.Session.SetString(SuperAdminAuthorizeAttribute.SessionKey, "true");
                return Redirect(model.ReturnUrl ?? Url.Action("Index", "SuperAdmin"));
            }

            ModelState.AddModelError(string.Empty, "Credenziali non valide.");
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Remove(SuperAdminAuthorizeAttribute.SessionKey);
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        [SuperAdminAuthorize]
        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.AsNoTracking().ToListAsync();

            var today = DateTimeOffset.UtcNow.Date; // lavoriamo a giorno, ignorando l’orario
            var in7 = today.AddDays(7);

            var active = users
                .Where(u => !u.LicenseExpiresAt.HasValue || u.LicenseExpiresAt.Value.Date > in7)
                .OrderBy(u => u.LicenseExpiresAt ?? DateTimeOffset.MaxValue)
                .ToList();

            var expiring = users
                .Where(u => u.LicenseExpiresAt.HasValue &&
                            u.LicenseExpiresAt.Value.Date > today &&
                            u.LicenseExpiresAt.Value.Date <= in7)
                .OrderBy(u => u.LicenseExpiresAt)
                .ToList();

            var expired = users
                .Where(u => u.LicenseExpiresAt.HasValue &&
                            u.LicenseExpiresAt.Value.Date <= today)
                .OrderBy(u => u.LicenseExpiresAt)
                .ToList();

            var vm = new SuperAdminIndexViewModel
            {
                Active = active,
                Expiring = expiring,
                Expired = expired,
                TodayUtcDate = today
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [SuperAdminAuthorize]
        public async Task<IActionResult> UpdateLicense(string userId, DateTime? newDate)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return BadRequest();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            // Null = “per sempre”
            user.LicenseExpiresAt = newDate.HasValue
                ? new DateTimeOffset(newDate.Value.Date, TimeSpan.Zero)
                : (DateTimeOffset?)null;

            var res = await _userManager.UpdateAsync(user);
            if (!res.Succeeded)
            {
                TempData["err"] = string.Join("; ", res.Errors.Select(e => e.Description));
            }
            else
            {
                TempData["ok"] = "Licenza aggiornata.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
