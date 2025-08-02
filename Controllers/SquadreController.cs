using AstaLegheFC.Data;
using AstaLegheFC.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace AstaLegheFC.Controllers
{
    public class SquadreController : Controller
    {
        private readonly AppDbContext _context;

        public SquadreController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string legaAlias)
        {
            if (string.IsNullOrWhiteSpace(legaAlias))
                return NotFound();

            var lega = await _context.Leghe
                .FirstOrDefaultAsync(l => l.Alias.ToLower() == legaAlias.ToLower());

            if (lega == null)
                return NotFound();

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
            var lega = await _context.Leghe.FindAsync(legaId);
            if (lega == null)
                return NotFound();

            ViewBag.LegaAlias = lega.Alias;

            var squadra = new Squadra
            {
                LegaId = legaId,
                Crediti = lega.CreditiIniziali // ✅ Imposta i crediti iniziali dalla lega
            };

            return View(squadra);
        }

        [HttpPost]
        public async Task<IActionResult> Crea(Squadra squadra)
        {
            if (!ModelState.IsValid)
                return View(squadra);

            var lega = await _context.Leghe.FindAsync(squadra.LegaId);
            if (lega == null)
                return NotFound();

            squadra.Crediti = lega.CreditiIniziali; // forza i crediti dal valore lega

            _context.Squadre.Add(squadra);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", new { legaAlias = lega.Alias });
        }

    }
}
