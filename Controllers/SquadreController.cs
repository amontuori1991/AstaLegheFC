using AstaLegheFC.Data;
using AstaLegheFC.Models;
using Microsoft.AspNetCore.Authorization; // <-- 1. Aggiungi using
using Microsoft.AspNetCore.Identity;    // <-- 2. Aggiungi using
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace AstaLegheFC.Controllers
{
    [Authorize]
    public class SquadreController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public SquadreController(AppDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string legaAlias)
        {
            if (string.IsNullOrWhiteSpace(legaAlias))
                return NotFound();

            var adminId = _userManager.GetUserId(User);

            // Cerca la lega assicurandoti che appartenga all'admin corrente
            var lega = await _context.Leghe
                .FirstOrDefaultAsync(l => l.Alias.ToLower() == legaAlias.ToLower() && l.AdminId == adminId);

            if (lega == null)
            {
                // Se la lega non esiste O non appartiene all'utente, restituisce un errore
                return Forbid(); // "Accesso Negato" è più sicuro di "NotFound"
            }

            var squadre = await _context.Squadre
                .Where(s => s.LegaId == lega.Id)
                .ToListAsync();

            ViewBag.LegaAlias = legaAlias;
            ViewBag.LegaNome = lega.Nome;
            ViewBag.LegaId = lega.Id;
            ViewBag.BaseUrl = $"{Request.Scheme}://{Request.Host}";
            return View(squadre);
        }

        [HttpGet]
        public async Task<IActionResult> Crea(int legaId)
        {
            var adminId = _userManager.GetUserId(User);

            // Verifica che l'admin stia creando una squadra per una lega di sua proprietà
            var lega = await _context.Leghe.FirstOrDefaultAsync(l => l.Id == legaId && l.AdminId == adminId);
            if (lega == null)
            {
                return Forbid();
            }

            ViewBag.LegaAlias = lega.Alias;

            var squadra = new Squadra
            {
                LegaId = legaId,
                Crediti = lega.CreditiIniziali
            };

            return View(squadra);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crea(Squadra squadra)
        {
            var adminId = _userManager.GetUserId(User);

            // Trova la lega di appartenenza e verifica la proprietà
            var lega = await _context.Leghe.FirstOrDefaultAsync(l => l.Id == squadra.LegaId && l.AdminId == adminId);
            if (lega == null)
            {
                return Forbid();
            }

            // Controllo sul nickname duplicato
            bool nicknameEsistente = await _context.Squadre
                                                   .AnyAsync(s => s.LegaId == squadra.LegaId && s.Nickname.ToLower() == squadra.Nickname.ToLower());
            if (nicknameEsistente)
            {
                ModelState.AddModelError("Nickname", "Esiste già una squadra con questo nome nella lega.");
            }

            // Assegna i crediti per sicurezza
            squadra.Crediti = lega.CreditiIniziali;

            if (!ModelState.IsValid)
            {
                // ----- MODIFICA CHIAVE QUI -----
                // Se c'è un errore, dobbiamo ricaricare i dati del ViewBag che la pagina si aspetta
                ViewBag.LegaAlias = lega.Alias;
                ViewBag.LegaNome = lega.Nome;
                return View(squadra);
            }

            _context.Squadre.Add(squadra);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", new { legaAlias = lega.Alias });
        }

        [HttpGet]
        public async Task<IActionResult> Modifica(int id)
        {
            var adminId = _userManager.GetUserId(User);

            // Trova la squadra assicurandoti che la lega a cui appartiene sia dell'admin
            var squadra = await _context.Squadre
                .Include(s => s.Lega)
                .FirstOrDefaultAsync(s => s.Id == id && s.Lega.AdminId == adminId);

            if (squadra == null)
            {
                return Forbid();
            }

            ViewBag.LegaAlias = squadra.Lega.Alias;
            return View(squadra);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Modifica(int id, Squadra squadra)
        {
            if (id != squadra.Id)
            {
                return NotFound();
            }

            var adminId = _userManager.GetUserId(User);

            // Verifica che la squadra che si tenta di modificare appartenga a una lega dell'admin
            var squadraOriginale = await _context.Squadre.AsNoTracking()
                .Include(s => s.Lega)
                .FirstOrDefaultAsync(s => s.Id == id && s.Lega.AdminId == adminId);

            if (squadraOriginale == null)
            {
                return Forbid();
            }

            // Applica solo i campi che l'utente può modificare dal form
            squadra.LegaId = squadraOriginale.LegaId;
            squadra.Crediti = squadraOriginale.Crediti;

            if (ModelState.IsValid)
            {
                _context.Update(squadra);
                await _context.SaveChangesAsync();

                var lega = await _context.Leghe.FindAsync(squadra.LegaId);
                return RedirectToAction(nameof(Index), new { legaAlias = lega.Alias });
            }

            var legaCorrente = await _context.Leghe.FindAsync(squadra.LegaId);
            ViewBag.LegaAlias = legaCorrente.Alias;
            return View(squadra);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Elimina(int id)
        {
            var adminId = _userManager.GetUserId(User);

            // Trova la squadra da eliminare solo se appartiene a una lega dell'admin
            var squadra = await _context.Squadre
                .Include(s => s.Lega)
                .FirstOrDefaultAsync(s => s.Id == id && s.Lega.AdminId == adminId);

            if (squadra == null)
            {
                return Forbid();
            }

            var legaAlias = squadra.Lega.Alias;

            _context.Squadre.Remove(squadra);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", new { legaAlias });
        }
    }
}