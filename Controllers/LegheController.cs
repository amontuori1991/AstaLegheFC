using Microsoft.AspNetCore.Mvc;
using AstaLegheFC.Data;
using AstaLegheFC.Models;
using Microsoft.EntityFrameworkCore;

namespace AstaLegheFC.Controllers
{
    public class LegheController : Controller
    {
        private readonly AppDbContext _context;

        public LegheController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var leghe = await _context.Leghe
                .OrderBy(l => l.Nome)
                .ToListAsync();

            return View(leghe);
        }

        [HttpGet]
        public IActionResult Crea()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Crea(Lega lega)
        {
            if (ModelState.IsValid)
            {
                // Check alias univoco
                if (await _context.Leghe.AnyAsync(l => l.Alias == lega.Alias))
                {
                    ModelState.AddModelError("Alias", "Alias già esistente. Scegline uno diverso.");
                    return View(lega);
                }

                _context.Leghe.Add(lega);
                await _context.SaveChangesAsync();
                return RedirectToAction("Index");
            }

            return View(lega);
        }
    }
}
