using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AstaLegheFC.Data;
using AstaLegheFC.Helpers;
using AstaLegheFC.Hubs;
using AstaLegheFC.Models;
using AstaLegheFC.Models.ViewModels;
using AstaLegheFC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

namespace AstaLegheFC.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly BazzerService _bazzerService;
        private readonly LegaService _legaService;
        private readonly IHubContext<BazzerHub> _hubContext;
        private readonly UserManager<IdentityUser> _userManager;

        // Costruttore aggiornato per ricevere UserManager
        public AdminController(AppDbContext context, BazzerService bazzerService, LegaService legaService, IHubContext<BazzerHub> hubContext, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _bazzerService = bazzerService;
            _legaService = legaService;
            _hubContext = hubContext;
            _userManager = userManager;
        }

        [HttpGet]
        public IActionResult ImportaListone()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ImportaListone(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                ViewBag.Errore = "Per favore, seleziona un file Excel valido.";
                return View();
            }

            // 1. Ottiene l'ID dell'admin attualmente loggato
            var adminId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(adminId))
            {
                return Unauthorized("Non è stato possibile identificare l'utente.");
            }

            // 2. Cancella SOLO il listone precedente di QUESTO admin
            var vecchioListone = await _context.ListoneCalciatori.Where(c => c.AdminId == adminId).ToListAsync();
            if (vecchioListone.Any())
            {
                _context.ListoneCalciatori.RemoveRange(vecchioListone);
            }

            var nuoviCalciatori = new List<CalciatoreListone>();
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                using (var package = new ExcelPackage(stream))
                {
                    var worksheet = package.Workbook.Worksheets.FirstOrDefault();
                    if (worksheet == null)
                    {
                        ViewBag.Errore = "Il file Excel non contiene fogli di lavoro.";
                        return View();
                    }

                    var rowCount = worksheet.Dimension.Rows;
                    for (int row = 2; row <= rowCount; row++) // Inizia da 2 per saltare l'intestazione
                    {
                        try
                        {
                            var idValue = worksheet.Cells[row, 1].Value;
                            if (idValue == null || !int.TryParse(idValue.ToString(), out int idListone))
                            {
                                continue;
                            }

                            var calciatore = new CalciatoreListone
                            {
                                IdListone = idListone,
                                Ruolo = worksheet.Cells[row, 2].Value?.ToString().Trim(),
                                RuoloMantra = worksheet.Cells[row, 3].Value?.ToString().Trim(),
                                Nome = worksheet.Cells[row, 4].Value?.ToString().Trim(),
                                Squadra = worksheet.Cells[row, 5].Value?.ToString().Trim(),
                                QtA = Convert.ToInt32(worksheet.Cells[row, 6].Value),
                                QtI = Convert.ToInt32(worksheet.Cells[row, 7].Value),
                                AdminId = adminId // 3. Etichetta ogni nuovo giocatore con l'ID dell'admin
                            };
                            nuoviCalciatori.Add(calciatore);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Errore durante la lettura della riga {row}: {ex.Message}");
                            continue;
                        }
                    }
                }
            }

            if (!nuoviCalciatori.Any())
            {
                ViewBag.Errore = "Nessun giocatore valido trovato nel file.";
                return View();
            }

            await _context.ListoneCalciatori.AddRangeAsync(nuoviCalciatori);
            await _context.SaveChangesAsync();

            ViewBag.Messaggio = $"Importazione completata! Aggiunti {nuoviCalciatori.Count} calciatori per l'utente {User.Identity.Name}.";
            return View();
        }

        public async Task<IActionResult> VisualizzaListone(string lega, string nome, string squadra, string ruolo, [FromQuery(Name = "mantra")] bool mantraAttivo = false)
        {
            if (string.IsNullOrEmpty(lega)) return Content("⚠️ Parametro lega mancante. Inserisci ?lega=...");

            var adminId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(adminId)) return Unauthorized();

            var legaModel = await _context.Leghe.FirstOrDefaultAsync(l => l.Alias.ToLower() == lega.ToLower() && l.AdminId == adminId);
            if (legaModel == null) return Forbid("Lega non trovata o non appartenente a questo utente.");

            var idGiocatoriAcquistati = await _context.Giocatori
                .Where(g => g.Squadra.LegaId == legaModel.Id)
                .Select(g => g.IdListone)
                .ToListAsync();

            var queryListone = _context.ListoneCalciatori
                                       .Where(c => c.AdminId == adminId && !idGiocatoriAcquistati.Contains(c.IdListone));

            if (!string.IsNullOrEmpty(nome)) queryListone = queryListone.Where(c => c.Nome.ToLower().Contains(nome.ToLower()));
            if (!string.IsNullOrEmpty(squadra)) queryListone = queryListone.Where(c => c.Squadra.ToLower().Contains(squadra.ToLower()));
            if (!string.IsNullOrEmpty(ruolo)) queryListone = queryListone.Where(c => c.Ruolo == ruolo);

            var listoneDisponibile = await queryListone.OrderBy(g => g.Nome).ToListAsync();

            ViewBag.Nome = nome;
            ViewBag.Squadra = squadra;
            ViewBag.Ruolo = ruolo;
            ViewBag.BloccoPortieriAttivo = _bazzerService.BloccoPortieriAttivo;
            ViewBag.DurataTimer = _bazzerService.DurataTimer;
            ViewBag.MantraAttivo = mantraAttivo;
            ViewBag.AdminNick = User.Identity?.Name ?? "ADMIN";   // <-- aggiunto
            _bazzerService.ImpostaModalitaMantra(mantraAttivo);

            // Riepilogo
            var squadreDaDb = await _context.Squadre
                .Include(s => s.Giocatori)
                .Where(s => s.LegaId == legaModel.Id)
                .OrderBy(s => s.Nickname)
                .ToListAsync();

            var riepilogo = new List<SquadraRiepilogoViewModel>();
            foreach (var s in squadreDaDb)
            {
                int creditiSpesi = s.Giocatori.Sum(g => g.CreditiSpesi ?? 0);
                int creditiDisponibili = s.Crediti - creditiSpesi;
                int giocatoriAcquistatiCount = s.Giocatori.Count;
                int slotTotali = legaModel.MaxPortieri + legaModel.MaxDifensori + legaModel.MaxCentrocampisti + legaModel.MaxAttaccanti;
                int slotRimasti = slotTotali - giocatoriAcquistatiCount;
                int puntataMassima = creditiDisponibili - (slotRimasti > 0 ? slotRimasti - 1 : 0);

                riepilogo.Add(new SquadraRiepilogoViewModel
                {
                    SquadraId = s.Id,
                    Nickname = s.Nickname,
                    CreditiDisponibili = creditiDisponibili,
                    PuntataMassima = puntataMassima > 0 ? puntataMassima : 0,
                    PortieriAssegnati = s.Giocatori.Where(g => g.Ruolo == "P").Select(g => new GiocatoreAssegnato { Id = g.Id, Nome = g.Nome, CreditiSpesi = g.CreditiSpesi ?? 0, SquadraReale = g.SquadraReale, LogoUrl = LogoHelper.GetLogoUrl(g.SquadraReale), Ruolo = g.Ruolo, RuoloMantra = g.RuoloMantra }).ToList(),
                    DifensoriAssegnati = s.Giocatori.Where(g => g.Ruolo == "D").Select(g => new GiocatoreAssegnato { Id = g.Id, Nome = g.Nome, CreditiSpesi = g.CreditiSpesi ?? 0, SquadraReale = g.SquadraReale, LogoUrl = LogoHelper.GetLogoUrl(g.SquadraReale), Ruolo = g.Ruolo, RuoloMantra = g.RuoloMantra }).ToList(),
                    CentrocampistiAssegnati = s.Giocatori.Where(g => g.Ruolo == "C").Select(g => new GiocatoreAssegnato { Id = g.Id, Nome = g.Nome, CreditiSpesi = g.CreditiSpesi ?? 0, SquadraReale = g.SquadraReale, LogoUrl = LogoHelper.GetLogoUrl(g.SquadraReale), Ruolo = g.Ruolo, RuoloMantra = g.RuoloMantra }).ToList(),
                    AttaccantiAssegnati = s.Giocatori.Where(g => g.Ruolo == "A").Select(g => new GiocatoreAssegnato { Id = g.Id, Nome = g.Nome, CreditiSpesi = g.CreditiSpesi ?? 0, SquadraReale = g.SquadraReale, LogoUrl = LogoHelper.GetLogoUrl(g.SquadraReale), Ruolo = g.Ruolo, RuoloMantra = g.RuoloMantra }).ToList()
                });
            }
            ViewBag.RiepilogoSquadre = riepilogo;
            ViewBag.LegaAlias = legaModel.Alias;

            ViewBag.RuoliDisponibili = await (mantraAttivo
                ? _context.ListoneCalciatori.Where(c => c.AdminId == adminId && c.RuoloMantra != null).Select(c => c.RuoloMantra)
                : _context.ListoneCalciatori.Where(c => c.AdminId == adminId && c.Ruolo != null).Select(c => c.Ruolo))
                .Distinct().OrderBy(r => r).ToListAsync();

            return View("VisualizzaListone", listoneDisponibile);
        }


        [HttpPost]
        public async Task<IActionResult> AvviaAsta(int id, [FromForm(Name = "mantra")] bool mantraAttivo)
        {
            var adminId = _userManager.GetUserId(User);
            // Cerca il giocatore solo tra quelli dell'admin corrente
            var giocatore = await _context.ListoneCalciatori.FirstOrDefaultAsync(c => c.Id == id && c.AdminId == adminId);
            if (giocatore == null) return NotFound();

            _bazzerService.ImpostaGiocatoreInAsta(giocatore, mantraAttivo);

            var ruoloDaMostrare = mantraAttivo ? giocatore.RuoloMantra : giocatore.Ruolo;

            await _hubContext.Clients.All.SendAsync("MostraGiocatoreInAsta", new
            {
                id = giocatore.Id,
                nome = giocatore.Nome,
                ruolo = ruoloDaMostrare,
                squadraReale = giocatore.Squadra,
                logoUrl = LogoHelper.GetLogoUrl(giocatore.Squadra)
            });

            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> AnnullaAsta()
        {
            _bazzerService.AnnullaAstaCorrente();
            await _hubContext.Clients.All.SendAsync("AstaAnnullata");
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> SvincolaGiocatore([FromBody] SvincolaRequest request)
        {
            if (request.Id <= 0) return BadRequest("Dati non validi.");

            // 👇 clamp difensivo (il service farà un ulteriore clamp usando il costo originale)
            if (request.CreditiRestituiti < 0) request.CreditiRestituiti = 0;

            await _legaService.SvincolaGiocatoreAsync(request.Id, request.CreditiRestituiti);
            return Ok();
        }


        [HttpPost]
        public async Task<IActionResult> AssegnaManualmente([FromBody] AssegnaRequest request)
        {
            if (request.Costo < 0 || request.SquadraId <= 0 || request.GiocatoreId <= 0)
            {
                return BadRequest("Dati non validi.");
            }
            var adminId = _userManager.GetUserId(User);
            await _legaService.AssegnaGiocatoreManualmenteAsync(request.GiocatoreId, request.SquadraId, request.Costo, adminId);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> ImpostaTimer([FromBody] TimerRequest request)
        {
            _bazzerService.ImpostaDurataTimer(request.Secondi);
            await _hubContext.Clients.All.SendAsync("AggiornaDurataTimer", _bazzerService.DurataTimer);
            return Ok();
        }

        [HttpPost]
        public IActionResult ImpostaBloccoPortieri([FromBody] BloccoPortieriRequest request)
        {
            _bazzerService.ImpostaBloccoPortieri(request.Attivo);
            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> EsportaRosa(int squadraId)
        {
            var squadra = await _context.Squadre
                .Include(s => s.Giocatori)
                .FirstOrDefaultAsync(s => s.Id == squadraId);

            if (squadra == null || squadra.Giocatori == null)
            {
                return NotFound();
            }

            var builder = new StringBuilder();
            foreach (var giocatore in squadra.Giocatori)
            {
                builder.AppendLine($"{squadra.Nickname},{giocatore.IdListone},{giocatore.CreditiSpesi}");
            }

            return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", $"{squadra.Nickname}.csv");
        }

        public class SvincolaRequest { public int Id { get; set; } public int CreditiRestituiti { get; set; } }
        public class TimerRequest { public int Secondi { get; set; } }
        public class AssegnaRequest { public int GiocatoreId { get; set; } public int SquadraId { get; set; } public int Costo { get; set; } }
        public class BloccoPortieriRequest { public bool Attivo { get; set; } }
    }
}