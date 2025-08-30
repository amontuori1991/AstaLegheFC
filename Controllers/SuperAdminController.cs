using System;
using System.Linq;
using System.Threading.Tasks;
using AstaLegheFC.Data;
using AstaLegheFC.Filters;
using AstaLegheFC.Models;
using AstaLegheFC.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;

namespace AstaLegheFC.Controllers
{
    public class SuperAdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _config;
        private readonly AppDbContext _db;

        // Per ora usiamo le credenziali “richieste”.
        private const string DefaultUser = "system_admin";
        private const string DefaultPass = "Samsung1991";

        public SuperAdminController(
            UserManager<ApplicationUser> userManager,
            IConfiguration config,
            AppDbContext db)
        {
            _userManager = userManager;
            _config = config;
            _db = db;
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
                TodayUtcDate = today.Date
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

        /// <summary>
        /// Elimina definitivamente:
        /// 1) AspNetUser
        /// 2) Leghe con AdminId == userId
        /// 3) Squadre delle leghe cancellate (e relativi Giocatori)
        /// 4) ListoneCalciatori con AdminId == userId
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [SuperAdminAuthorize]
        public async Task<IActionResult> DeleteAdmin(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                TempData["err"] = "Parametro mancante (userId).";
                return RedirectToAction(nameof(Index));
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                TempData["err"] = "Utente non trovato.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await using var tx = await _db.Database.BeginTransactionAsync();

                // 2) Leghe di questo admin
                var legaIds = await _db.Leghe
                    .Where(l => l.AdminId == userId)
                    .Select(l => l.Id)
                    .ToListAsync();

                if (legaIds.Count > 0)
                {
                    // 4) Squadre delle leghe
                    var squadreIds = await _db.Squadre
                        .Where(s => legaIds.Contains(s.LegaId))
                        .Select(s => s.Id)
                        .ToListAsync();

                    if (squadreIds.Count > 0)
                    {
                        // Prima i giocatori (se non c'è cascade)
                        var squadreIdsNullable = squadreIds.Select(i => (int?)i).ToList();
                        var giocatori = _db.Giocatori
                            .Where(g => squadreIdsNullable.Contains(g.SquadraId));

                        _db.Giocatori.RemoveRange(giocatori);
                        await _db.SaveChangesAsync();

                        // Poi le squadre
                        var squadre = _db.Squadre.Where(s => squadreIds.Contains(s.Id));
                        _db.Squadre.RemoveRange(squadre);
                        await _db.SaveChangesAsync();
                    }

                    // Poi le leghe
                    var leghe = _db.Leghe.Where(l => legaIds.Contains(l.Id));
                    _db.Leghe.RemoveRange(leghe);
                    await _db.SaveChangesAsync();
                }

                // 3) Listone per admin (NB: nel codice progetto è ListoneCalciatori)
                var listone = _db.ListoneCalciatori.Where(x => x.AdminId == userId);
                _db.ListoneCalciatori.RemoveRange(listone);
                await _db.SaveChangesAsync();

                // 1) infine l'utente Identity (dopo aver rimosso le FK su Leghe)
                _db.Users.Remove(user);
                await _db.SaveChangesAsync();

                await tx.CommitAsync();
                TempData["ok"] = $"Utente '{user.Email}' e dati associati eliminati correttamente.";
            }
            catch (Exception ex)
            {
                // se il BeginTransactionAsync è stato aperto, rollback
                try { await _db.Database.RollbackTransactionAsync(); } catch { /* ignore */ }
                TempData["err"] = "Errore durante l'eliminazione: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
