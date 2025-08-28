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

            // ✅ usa i limiti della LEGA (non le costanti globali)
            int slotTotali = (squadra.Lega?.MaxPortieri ?? 0)
                           + (squadra.Lega?.MaxDifensori ?? 0)
                           + (squadra.Lega?.MaxCentrocampisti ?? 0)
                           + (squadra.Lega?.MaxAttaccanti ?? 0);

            int slotRimasti = slotTotali - (squadra.Giocatori?.Count ?? 0);
            int puntataMassima = creditiDisponibili - (slotRimasti > 0 ? slotRimasti - 1 : 0);
            if (puntataMassima < 0) puntataMassima = 0;

            return Json(new
            {
                creditiDisponibili,
                puntataMassima,
                portieri = squadra.Giocatori.Count(g => g.Ruolo == "P"),
                difensori = squadra.Giocatori.Count(g => g.Ruolo == "D"),
                centrocampisti = squadra.Giocatori.Count(g => g.Ruolo == "C"),
                attaccanti = squadra.Giocatori.Count(g => g.Ruolo == "A")
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetListoneDisponibile(string lega)
        {
            var legaModel = await _context.Leghe
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Alias.ToLower() == lega.ToLower());
            if (legaModel == null) return NotFound();

            // IdListone già acquistati in questa lega
            var idGiocatoriAcquistati = await _context.Giocatori
                .AsNoTracking()
                .Where(g => g.Squadra.LegaId == legaModel.Id)
                .Select(g => g.IdListone)
                .ToListAsync();

            // Lista “candidata” — se hai un campo per legare il listone alla lega, filtra qui (lasciato commentato).
            var query = _context.ListoneCalciatori.AsNoTracking()
                //.Where(c => c.LegaId == legaModel.Id)
                //.Where(c => c.LegaAlias.ToLower() == lega.ToLower())
                .Where(c => !idGiocatoriAcquistati.Contains(c.IdListone));

            // Prendi solo i campi necessari (niente LogoUrl)
            var raw = await query
                .Select(c => new
                {
                    c.Id,
                    c.IdListone,
                    c.Nome,
                    c.Ruolo,
                    Squadra = c.Squadra   // usa qui il campo che hai in tabella
                })
                .ToListAsync();

            // Dedup per (Nome, Ruolo, Squadra) con normalizzazione accent/maiuscole/spazi
            string Norm(string s) =>
                (s ?? string.Empty)
                    .Normalize(System.Text.NormalizationForm.FormD)
                    .Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
                    .Aggregate(new System.Text.StringBuilder(), (sb, ch) => sb.Append(char.ToLowerInvariant(ch)))
                    .ToString()
                    .Trim();

            var dedup = raw
                .GroupBy(x => new { Nome = Norm(x.Nome), Ruolo = Norm(x.Ruolo), Squadra = Norm(x.Squadra) })
                .Select(g => g.First())
                .OrderBy(x => x.Nome)
                .ToList();

            return Json(dedup.Select(x => new
            {
                x.Id,
                x.IdListone,
                nome = x.Nome,
                ruolo = x.Ruolo,
                squadra = x.Squadra
            }));
        }


        [HttpGet]
        public async Task<IActionResult> Riepilogo(string lega, string nick)
        {
            if (string.IsNullOrWhiteSpace(lega))
                return BadRequest("lega mancante");

            var legaModel = await _context.Leghe
                .Include(l => l.Squadre)
                .FirstOrDefaultAsync(l => l.Alias.ToLower() == lega.ToLower());

            if (legaModel == null) return NotFound();

            var slotTotali = legaModel.MaxPortieri + legaModel.MaxDifensori + legaModel.MaxCentrocampisti + legaModel.MaxAttaccanti;
            var mantraAttivo = _bazzerService.MantraAttivo; // oppure leggi da impostazioni della lega

            var squads = await _context.Squadre
                .Include(s => s.Giocatori)
                .Where(s => s.LegaId == legaModel.Id)
                .Select(s => new
                {
                    squadraId = s.Id,
                    nickname = s.Nickname,
                    // crediti di budget rimasti
                    creditiDisponibili = s.Crediti - s.Giocatori.Sum(g => g.CreditiSpesi ?? 0),
                    // puntata max = crediti rimasti - (slot rimanenti - 1) (min 0)
                    puntataMassima = Math.Max(
                        0,
                        (s.Crediti - s.Giocatori.Sum(g => g.CreditiSpesi ?? 0))
                        - Math.Max(0, (slotTotali - s.Giocatori.Count()) - 1)
                    ),
                    isMe = s.Nickname == nick,

                    portieri = s.Giocatori
                        .Where(g => g.Ruolo == "P")
                        .Select(g => new {
                            nome = g.Nome,
                            ruolo = g.Ruolo,
                            ruoloMantra = g.RuoloMantra,
                            crediti = g.CreditiSpesi ?? 0,
                            logoUrl = LogoHelper.GetLogoUrl(g.SquadraReale)
                        }).ToList(),

                    difensori = s.Giocatori
                        .Where(g => g.Ruolo == "D")
                        .Select(g => new {
                            nome = g.Nome,
                            ruolo = g.Ruolo,
                            ruoloMantra = g.RuoloMantra,
                            crediti = g.CreditiSpesi ?? 0,
                            logoUrl = LogoHelper.GetLogoUrl(g.SquadraReale)
                        }).ToList(),

                    centrocampisti = s.Giocatori
                        .Where(g => g.Ruolo == "C")
                        .Select(g => new {
                            nome = g.Nome,
                            ruolo = g.Ruolo,
                            ruoloMantra = g.RuoloMantra,
                            crediti = g.CreditiSpesi ?? 0,
                            logoUrl = LogoHelper.GetLogoUrl(g.SquadraReale)
                        }).ToList(),

                    attaccanti = s.Giocatori
                        .Where(g => g.Ruolo == "A")
                        .Select(g => new {
                            nome = g.Nome,
                            ruolo = g.Ruolo,
                            ruoloMantra = g.RuoloMantra,
                            crediti = g.CreditiSpesi ?? 0,
                            logoUrl = LogoHelper.GetLogoUrl(g.SquadraReale)
                        }).ToList()
                })
                .OrderBy(s => s.nickname)
                .ToListAsync();

            return Json(new { squads, mantraAttivo });
        }


    }
}
    