using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using AstaLegheFC.Filters;
using AstaLegheFC.Models;
using AstaLegheFC.Models.ViewModels;
using AstaLegheFC.Data;
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

        // ========== NUOVO: Riepilogo attività aste ==========
        // Definizioni:
        // - "Aste" = numero di leghe (per admin e totale)
        // - "Giocatori assegnati" = numero totale di record in Giocatori associati alle squadre
        // - Raggruppamento dei giocatori per RUOLO (Ruolo), NON RuoloMantra
        [HttpGet]
        [SuperAdminAuthorize]
        public async Task<IActionResult> ActivitySummary()
        {
            var leghe = await _db.Leghe
                .AsNoTracking()
                .Include(l => l.Squadre)
                    .ThenInclude(s => s.Giocatori)
                .ToListAsync();

            var adminNames = await _userManager.Users
                .AsNoTracking()
                .Select(u => new { u.Id, u.UserName })
                .ToDictionaryAsync(x => x.Id, x => x.UserName);

            var totalLeagues = leghe.Count;
            var totalAssignedPlayers = leghe.Sum(l => l.Squadre.Sum(s => s.Giocatori.Count));

            // GroupBy Admin
            var admins = leghe
                .GroupBy(l => l.AdminId)
                .Select(g =>
                {
                    var adminId = g.Key;
                    adminNames.TryGetValue(adminId, out var adminEmail);
                    adminEmail ??= "(utente sconosciuto)";

                    var leagues = g.Select(l =>
                    {
                        var legaName = !string.IsNullOrWhiteSpace(l.Nome) ? l.Nome :
                                       !string.IsNullOrWhiteSpace(l.Alias) ? l.Alias :
                                       $"Lega {l.Id}";

                        var leagueDto = new
                        {
                            legaId = l.Id,
                            nome = legaName,
                            alias = l.Alias,
                            squadsCount = l.Squadre.Count,
                            assignedCount = l.Squadre.Sum(s => s.Giocatori.Count),
                            squadre = l.Squadre
                                .OrderBy(s => s.Nome)
                                .Select(s => new
                                {
                                    squadraId = s.Id,
                                    nome = s.Nome,
                                    pCount = s.Giocatori.Count(x => x.Ruolo == "P"),
                                    dCount = s.Giocatori.Count(x => x.Ruolo == "D"),
                                    cCount = s.Giocatori.Count(x => x.Ruolo == "C"),
                                    aCount = s.Giocatori.Count(x => x.Ruolo == "A"),
                                    assignedCount = s.Giocatori.Count,
                                    players = s.Giocatori
                                        .OrderBy(x => x.Ruolo) // RUOLO, non Mantra
                                        .ThenByDescending(x => x.CreditiSpesi ?? 0)
                                        .ThenBy(x => x.Nome)
                                        .Select(x => new
                                        {
                                            nome = x.Nome,
                                            ruolo = x.Ruolo,               // <- usiamo Ruolo
                                            squadraReale = x.SquadraReale,
                                            crediti = x.CreditiSpesi ?? 0
                                        })
                                        .ToList()
                                })
                                .ToList()
                        };

                        return leagueDto;
                    })
                    .OrderBy(l => l.nome)
                    .ToList();

                    var totalAssignedAdmin = leagues.Sum(l => l.assignedCount);

                    return new
                    {
                        adminId,
                        adminEmail,
                        leaguesCount = leagues.Count,
                        assignedCount = totalAssignedAdmin,
                        leghe = leagues
                    };
                })
                .OrderBy(a => a.adminEmail)
                .ToList();

            return Json(new
            {
                totalLeagues,
                totalAssignedPlayers,
                admins
            });
        }
    }
}
