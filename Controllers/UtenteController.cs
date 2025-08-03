using AstaLegheFC.Data;
using AstaLegheFC.Helpers;
using AstaLegheFC.Models;
using AstaLegheFC.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace AstaLegheFC.Controllers
{
    public class UtenteController : Controller
    {
        private readonly AppDbContext _context;
        private readonly BazzerService _bazzerService;

        public UtenteController(AppDbContext context, BazzerService bazzerService)
        {
            _context = context;
            _bazzerService = bazzerService;
        }

        public async Task<IActionResult> Index(string nick, string lega)
        {
            if (string.IsNullOrEmpty(nick) || string.IsNullOrEmpty(lega))
            {
                return Content("⚠️ Parametri mancanti. Inserisci ?nick=...&lega=...");
            }

            var squadra = await _context.Squadre
                .Include(s => s.Lega)
                .Include(s => s.Giocatori)
                .FirstOrDefaultAsync(s => s.Nickname == nick && s.Lega.Alias.ToLower() == lega.ToLower());

            if (squadra == null)
            {
                return Content("⚠️ Squadra non trovata per i parametri specificati.");
            }

            var giocatoreInAsta = _bazzerService.GetGiocatoreInAsta();
            var (offerente, offerta) = _bazzerService.GetOffertaAttuale();

            int creditiUsati = squadra.Giocatori?.Sum(g => g.CreditiSpesi) ?? 0;
            int creditiDisponibili = squadra.Crediti - creditiUsati;
            int slotTotali = RegoleLega.MaxPortieri + RegoleLega.MaxDifensori + RegoleLega.MaxCentrocampisti + RegoleLega.MaxAttaccanti;
            int slotRimasti = slotTotali - (squadra.Giocatori?.Count ?? 0);
            int puntataMassima = creditiDisponibili - (slotRimasti > 0 ? slotRimasti - 1 : 0);

            var viewModel = new UtenteViewModel
            {
                Nickname = squadra.Nickname,
                CreditiDisponibili = creditiDisponibili,
                PuntataMassima = puntataMassima > 0 ? puntataMassima : 0,
                CalciatoreInAsta = giocatoreInAsta == null ? null : new GiocatoreInAstaViewModel
                {
                    IdListone = giocatoreInAsta.Id,
                    Nome = giocatoreInAsta.Nome,
                    Ruolo = giocatoreInAsta.Ruolo,
                    Squadra = giocatoreInAsta.Squadra
                },
                OfferenteAttuale = offerente,
                OffertaAttuale = offerta,
                TimerAsta = _bazzerService.DurataTimer,
                PortieriAcquistati = squadra.Giocatori.Count(g => g.Ruolo == "P"),
                DifensoriAcquistati = squadra.Giocatori.Count(g => g.Ruolo == "D"),
                CentrocampistiAcquistati = squadra.Giocatori.Count(g => g.Ruolo == "C"),
                AttaccantiAcquistati = squadra.Giocatori.Count(g => g.Ruolo == "A"),
           LogoSquadra = giocatoreInAsta != null ? LogoHelper.GetLogoUrl(giocatoreInAsta.Squadra) : ""
            };

            return View(viewModel);
        }

        [HttpGet("/utente/crediti")]
        public async Task<IActionResult> Crediti(string nick, string lega)
        {
            if (string.IsNullOrEmpty(nick) || string.IsNullOrEmpty(lega))
                return BadRequest();

            var squadra = await _context.Squadre
                .Include(s => s.Lega)
                .Include(s => s.Giocatori)
                .FirstOrDefaultAsync(s => s.Nickname == nick && s.Lega.Alias.ToLower() == lega.ToLower());

            if (squadra == null)
                return NotFound();

            int creditiUsati = squadra.Giocatori?.Sum(g => g.CreditiSpesi) ?? 0;
            int creditiDisponibili = squadra.Crediti - creditiUsati;
            int slotTotali = RegoleLega.MaxPortieri + RegoleLega.MaxDifensori + RegoleLega.MaxCentrocampisti + RegoleLega.MaxAttaccanti;
            int slotRimasti = slotTotali - (squadra.Giocatori?.Count ?? 0);
            int puntataMassima = creditiDisponibili - (slotRimasti > 0 ? slotRimasti - 1 : 0);

            // ✅ RESTITUIAMO ANCHE I CONTEGGI DEI RUOLI NEL JSON PER L'AGGIORNAMENTO IN TEMPO REALE
            return Json(new
            {
                creditiDisponibili,
                puntataMassima = puntataMassima > 0 ? puntataMassima : 0,
                portieri = squadra.Giocatori.Count(g => g.Ruolo == "P"),
                difensori = squadra.Giocatori.Count(g => g.Ruolo == "D"),
                centrocampisti = squadra.Giocatori.Count(g => g.Ruolo == "C"),
                attaccanti = squadra.Giocatori.Count(g => g.Ruolo == "A")
            });
        }
        [HttpGet]
        public async Task<IActionResult> GetListoneDisponibile(string lega)
        {
            var legaModel = await _context.Leghe.FirstOrDefaultAsync(l => l.Alias.ToLower() == lega.ToLower());
            if (legaModel == null) return NotFound();

            // Trova tutti i giocatori già acquistati in questa lega
            var idGiocatoriAcquistati = await _context.Giocatori
                .Where(g => g.Squadra.LegaId == legaModel.Id)
                .Select(g => g.IdListone)
                .ToListAsync();

            // Restituisce la lista dei giocatori non acquistati, in un formato leggero
            var listoneDisponibile = await _context.ListoneCalciatori
                .Where(c => !idGiocatoriAcquistati.Contains(c.IdListone))
                .OrderBy(c => c.Nome)
                .Select(c => new { c.Id, c.Nome, c.Ruolo, c.Squadra }) // Seleziona solo i dati necessari
                .ToListAsync();

            return Json(listoneDisponibile);
        }
    }
}
    