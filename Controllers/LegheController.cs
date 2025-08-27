using System.Linq;
using System.Text; // ✅ Aggiungi questo using
using System.Threading.Tasks;
using AstaLegheFC.Data;
using AstaLegheFC.Hubs;
using AstaLegheFC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AstaLegheFC.Controllers
{
    [Authorize]
    public class LegheController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<BazzerHub> _hubContext;

        public LegheController(AppDbContext context,
        UserManager<ApplicationUser> userManager,        // <-- Aggiunto
        IHubContext<BazzerHub> hubContext)
        {
            _context = context;
            _userManager = userManager;                   // <-- Aggiunto
            _hubContext = hubContext;                     // <-- Questo rimane

        }

        public async Task<IActionResult> Index()
        {
            var adminId = _userManager.GetUserId(User);

            var leghe = await _context.Leghe
                                      .Where(l => l.AdminId == adminId) // <-- FILTRO FONDAMENTALE
                                      .ToListAsync();
            return View(leghe);
        }

        [HttpGet]
        public IActionResult Crea()
        {
            return View();
        }


        // In Controllers/LegheController.cs

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crea([Bind("Id,Nome,Alias,CreditiIniziali,MaxPortieri,MaxDifensori,MaxCentrocampisti,MaxAttaccanti")] Lega lega)
        {
            var adminId = _userManager.GetUserId(User);
            lega.AdminId = adminId;

            // MODIFICATO: Ora il controllo è globale e non più legato all'admin
            bool aliasEsistente = await _context.Leghe
                                              .AnyAsync(l => l.Alias.ToLower() == lega.Alias.ToLower());

            if (aliasEsistente)
            {
                ModelState.AddModelError("Alias", "Questo alias è già in uso da un altro utente. Scegline uno diverso.");
            }

            if (ModelState.IsValid)
            {
                _context.Add(lega);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(lega);
        }

        [HttpGet]
        public async Task<IActionResult> Modifica(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var lega = await _context.Leghe.FindAsync(id);
            if (lega == null)
            {
                return NotFound();
            }
            return View(lega);
        }

        // In Controllers/LegheController.cs

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Modifica(int id, Lega legaModificataDalForm) // Ho rinominato 'lega' per maggiore chiarezza
        {
            if (id != legaModificataDalForm.Id)
            {
                return NotFound();
            }

            var adminId = _userManager.GetUserId(User);

            // 1. Carica la lega originale dal database, verificando che appartenga all'admin
            var legaOriginale = await _context.Leghe.FirstOrDefaultAsync(l => l.Id == id && l.AdminId == adminId);

            // 2. Se non la troviamo (o non appartiene all'utente), neghiamo l'accesso
            if (legaOriginale == null)
            {
                return Forbid(); // Accesso negato
            }

            // Aggiungiamo un ulteriore controllo sull'alias per evitare duplicati durante la modifica
            if (legaOriginale.Alias.ToLower() != legaModificataDalForm.Alias.ToLower())
            {
                bool aliasEsistente = await _context.Leghe.AnyAsync(l => l.Alias.ToLower() == legaModificataDalForm.Alias.ToLower());
                if (aliasEsistente)
                {
                    ModelState.AddModelError("Alias", "Questo alias è già in uso. Scegline uno diverso.");
                }
            }

            if (ModelState.IsValid)
            {
                // 3. Copia SOLO i campi modificabili dal form all'oggetto originale
                legaOriginale.Nome = legaModificataDalForm.Nome;
                legaOriginale.Alias = legaModificataDalForm.Alias;
                legaOriginale.CreditiIniziali = legaModificataDalForm.CreditiIniziali;
                legaOriginale.MaxPortieri = legaModificataDalForm.MaxPortieri;
                legaOriginale.MaxDifensori = legaModificataDalForm.MaxDifensori;
                legaOriginale.MaxCentrocampisti = legaModificataDalForm.MaxCentrocampisti;
                legaOriginale.MaxAttaccanti = legaModificataDalForm.MaxAttaccanti;

                try
                {
                    await _context.SaveChangesAsync();

                    // 👇 NUOVA RIGA: Invia le nuove regole a tutti i client connessi 👇
                    await _hubContext.Clients.All.SendAsync("AggiornaRegoleLega", new
                    {
                        maxPortieri = legaOriginale.MaxPortieri,
                        maxDifensori = legaOriginale.MaxDifensori,
                        maxCentrocampisti = legaOriginale.MaxCentrocampisti,
                        maxAttaccanti = legaOriginale.MaxAttaccanti
                    });
                }
                catch (DbUpdateConcurrencyException)
                {
                    throw; // Gestire eccezione se necessario
                }
                return RedirectToAction(nameof(Index));
            }

            // Se il modello non è valido, ritorna alla vista con i dati inseriti dall'utente
            return View(legaModificataDalForm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Elimina(int id)
        {
            var lega = await _context.Leghe.FindAsync(id);
            if (lega == null)
            {
                return NotFound();
            }

            var squadre = await _context.Squadre.Where(s => s.LegaId == id).ToListAsync();
            _context.Squadre.RemoveRange(squadre);

            _context.Leghe.Remove(lega);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // ✅ AGGIUNTO IL NUOVO METODO PER L'EXPORT MASSIVO
        [HttpGet]
        public async Task<IActionResult> EsportaRoseLega(int legaId)
        {
            var lega = await _context.Leghe
                .Include(l => l.Squadre)
                .ThenInclude(s => s.Giocatori)
                .FirstOrDefaultAsync(l => l.Id == legaId);

            if (lega == null)
            {
                return NotFound();
            }

            var builder = new StringBuilder();

            // Itera su ogni squadra della lega
            foreach (var squadra in lega.Squadre.OrderBy(s => s.Nome))
            {
                // Itera su ogni giocatore di quella squadra
                foreach (var giocatore in squadra.Giocatori)
                {
                    builder.AppendLine($"{squadra.Nome},{giocatore.IdListone},{giocatore.CreditiSpesi}");
                }
            }

            return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", $"Rose_{lega.Alias}.csv");
        }
    }
}