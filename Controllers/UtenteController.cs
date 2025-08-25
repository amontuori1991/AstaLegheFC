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

        // In Controllers/UtenteController.cs

        // File: Controllers/UtenteController.cs

        // In Controllers/UtenteController.cs
        public async Task<IActionResult> Index(string nick, string lega)
        {
            if (string.IsNullOrEmpty(nick) || string.IsNullOrEmpty(lega))
            {
                return Content("⚠️ Parametri mancanti. Inserisci ?nick=...&lega=...");
            }

            // Ora includiamo anche le informazioni della lega nella query
            var squadra = await _context.Squadre
                .Include(s => s.Lega)
                .Include(s => s.Giocatori)
                .FirstOrDefaultAsync(s => s.Nickname == nick && s.Lega.Alias.ToLower() == lega.ToLower());

            if (squadra == null)
            {
                return Content("⚠️ Squadra non trovata per i parametri specificati.");
            }

            // ... resto del codice invariato (calcolo crediti, etc.) ...
            var giocatoreInAsta = _bazzerService.GetGiocatoreInAsta();
            var (offerente, offerta) = _bazzerService.GetOffertaAttuale();
            var mantraAttivo = _bazzerService.MantraAttivo;
            int creditiUsati = squadra.Giocatori?.Sum(g => g.CreditiSpesi) ?? 0;
            int creditiDisponibili = squadra.Crediti - creditiUsati;
            // NOTA: il calcolo di slotRimasti e puntataMassima qui usa già i dati corretti dalla lega,
            // perché `squadra.Lega` contiene le nuove regole. Dobbiamo solo aggiornare il viewModel.

            var viewModel = new UtenteViewModel
            {
                Nickname = squadra.Nickname,
                CreditiDisponibili = creditiDisponibili,
                PuntataMassima = 0, // Lo ricalcoliamo sotto per sicurezza
                MantraAttivo = mantraAttivo,
                CalciatoreInAsta = giocatoreInAsta == null ? null : new GiocatoreInAstaViewModel
                {
                    IdListone = giocatoreInAsta.IdListone,
                    Nome = giocatoreInAsta.Nome,
                    Ruolo = mantraAttivo ? giocatoreInAsta.RuoloMantra : giocatoreInAsta.Ruolo,
                    RuoloMantra = giocatoreInAsta.RuoloMantra,
                    Squadra = giocatoreInAsta.Squadra
                },
                OfferenteAttuale = offerente,
                OffertaAttuale = offerta,
                TimerAsta = _bazzerService.DurataTimer,
                PortieriAcquistati = squadra.Giocatori.Count(g => g.Ruolo == "P"),
                DifensoriAcquistati = squadra.Giocatori.Count(g => g.Ruolo == "D"),
                CentrocampistiAcquistati = squadra.Giocatori.Count(g => g.Ruolo == "C"),
                AttaccantiAcquistati = squadra.Giocatori.Count(g => g.Ruolo == "A"),
                LogoSquadra = giocatoreInAsta != null ? LogoHelper.GetLogoUrl(giocatoreInAsta.Squadra) : "",

                // 👇 Passiamo le regole specifiche della lega al ViewModel 👇
                MaxPortieri = squadra.Lega.MaxPortieri,
                MaxDifensori = squadra.Lega.MaxDifensori,
                MaxCentrocampisti = squadra.Lega.MaxCentrocampisti,
                MaxAttaccanti = squadra.Lega.MaxAttaccanti
            };

            // Ricalcoliamo puntata massima con le regole corrette
            int slotTotali = viewModel.MaxPortieri + viewModel.MaxDifensori + viewModel.MaxCentrocampisti + viewModel.MaxAttaccanti;
            int slotRimasti = slotTotali - (squadra.Giocatori?.Count ?? 0);
            viewModel.PuntataMassima = creditiDisponibili - (slotRimasti > 0 ? slotRimasti - 1 : 0);
            if (viewModel.PuntataMassima < 0) viewModel.PuntataMassima = 0;


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
        [HttpGet("/utente/riepilogo")]
        public async Task<IActionResult> Riepilogo(string lega, string nick)
        {
            if (string.IsNullOrWhiteSpace(lega))
                return BadRequest();

            var legaModel = await _context.Leghe
                .FirstOrDefaultAsync(l => l.Alias.ToLower() == lega.ToLower());

            if (legaModel == null)
                return NotFound();

            var squadre = await _context.Squadre
                .Include(s => s.Giocatori)
                .Where(s => s.LegaId == legaModel.Id)
                .OrderBy(s => s.Nickname)
                .ToListAsync();

            var payload = new
            {
                squads = squadre.Select(s => new
                {
                    squadraId = s.Id,
                    nickname = s.Nickname,
                    isMe = s.Nickname == nick,
                    portieri = s.Giocatori.Where(g => g.Ruolo == "P").Select(g => new {
                        id = g.Id,
                        nome = g.Nome,
                        crediti = g.CreditiSpesi ?? 0,
                        squadraReale = g.SquadraReale,
                        logoUrl = AstaLegheFC.Helpers.LogoHelper.GetLogoUrl(g.SquadraReale),
                        ruolo = g.Ruolo,
                        ruoloMantra = g.RuoloMantra
                    }),
                    difensori = s.Giocatori.Where(g => g.Ruolo == "D").Select(g => new {
                        id = g.Id,
                        nome = g.Nome,
                        crediti = g.CreditiSpesi ?? 0,
                        squadraReale = g.SquadraReale,
                        logoUrl = AstaLegheFC.Helpers.LogoHelper.GetLogoUrl(g.SquadraReale),
                        ruolo = g.Ruolo,
                        ruoloMantra = g.RuoloMantra
                    }),
                    centrocampisti = s.Giocatori.Where(g => g.Ruolo == "C").Select(g => new {
                        id = g.Id,
                        nome = g.Nome,
                        crediti = g.CreditiSpesi ?? 0,
                        squadraReale = g.SquadraReale,
                        logoUrl = AstaLegheFC.Helpers.LogoHelper.GetLogoUrl(g.SquadraReale),
                        ruolo = g.Ruolo,
                        ruoloMantra = g.RuoloMantra
                    }),
                    attaccanti = s.Giocatori.Where(g => g.Ruolo == "A").Select(g => new {
                        id = g.Id,
                        nome = g.Nome,
                        crediti = g.CreditiSpesi ?? 0,
                        squadraReale = g.SquadraReale,
                        logoUrl = AstaLegheFC.Helpers.LogoHelper.GetLogoUrl(g.SquadraReale),
                        ruolo = g.Ruolo,
                        ruoloMantra = g.RuoloMantra
                    })
                })
            };

            return Json(payload);
        }

    }
}
    