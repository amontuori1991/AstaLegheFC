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
using AstaLegheFC.Filters;
using Microsoft.AspNetCore.SignalR;


namespace AstaLegheFC.Controllers
{
    [Authorize]
    //[ActiveLicenseAuthorize]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly BazzerService _bazzerService;
        private readonly LegaService _legaService;
        private readonly IHubContext<BazzerHub> _hubContext;
        private readonly UserManager<ApplicationUser> _userManager;

        private readonly IHubContext<BazzerHub> _hub;

        // Costruttore aggiornato per ricevere UserManager
        public AdminController(AppDbContext context, BazzerService bazzerService, LegaService legaService, IHubContext<BazzerHub> hubContext, UserManager<ApplicationUser> userManager)
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

        public async Task<IActionResult> VisualizzaListone(
    string lega,
    string nome,
    string squadra,
    string ruolo,
    [FromQuery(Name = "mantra")] bool mantraAttivo = false,
    [FromQuery(Name = "sorteggio")] bool sorteggioLetteraAttivo = false,
    [FromQuery(Name = "iniziale")] string? iniziale = null)
        {
            if (string.IsNullOrEmpty(lega))
                return Content("⚠️ Parametro lega mancante. Inserisci ?lega=...");

            var adminId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(adminId))
                return Unauthorized();

            var legaModel = await _context.Leghe
                .FirstOrDefaultAsync(l => l.Alias.ToLower() == lega.ToLower() && l.AdminId == adminId);
            if (legaModel == null)
                return Forbid("Lega non trovata o non appartenente a questo utente.");

            // Escludi i già acquistati nella lega
            var idGiocatoriAcquistati = await _context.Giocatori
                .Where(g => g.Squadra.LegaId == legaModel.Id)
                .Select(g => g.IdListone)
                .ToListAsync();

            // Base query: listone dell'admin, non ancora acquistati
            IQueryable<CalciatoreListone> queryListone = _context.ListoneCalciatori
                .Where(c => c.AdminId == adminId && !idGiocatoriAcquistati.Contains(c.IdListone));

            // Filtri testo (case-insensitive)
            if (!string.IsNullOrWhiteSpace(nome))
                queryListone = queryListone.Where(c => EF.Functions.ILike(c.Nome, $"%{nome}%"));
            if (!string.IsNullOrWhiteSpace(squadra))
                queryListone = queryListone.Where(c => EF.Functions.ILike(c.Squadra, $"%{squadra}%"));

            // Filtro iniziale
            if (!string.IsNullOrWhiteSpace(iniziale))
                queryListone = queryListone.Where(c => EF.Functions.ILike(c.Nome, $"{iniziale}%"));

            // Filtro ruolo (standard vs Mantra)
            if (!string.IsNullOrWhiteSpace(ruolo))
            {
                if (mantraAttivo)
                {
                    // cerca il token nei campi RuoloMantra (es: "D", "M", "W", ecc.)
                    queryListone = queryListone.Where(c =>
                        c.RuoloMantra != null &&
                        EF.Functions.ILike(c.RuoloMantra, $"%{ruolo}%"));
                }
                else
                {
                    queryListone = queryListone.Where(c => c.Ruolo == ruolo);
                }
            }

            // Applica Mantra alla sessione d’asta
            _bazzerService.ImpostaModalitaMantra(mantraAttivo);

            // Ordina e materializza
            var listoneDisponibile = await queryListone
                .OrderBy(g => g.Nome)
                .ToListAsync();

            // Flags & viewbag base
            ViewBag.Nome = nome;
            ViewBag.Squadra = squadra;
            ViewBag.Ruolo = ruolo;
            ViewBag.Iniziale = iniziale;
            ViewBag.SorteggioLetteraAttivo = sorteggioLetteraAttivo;
            ViewBag.BloccoPortieriAttivo = _bazzerService.BloccoPortieriAttivo;
            ViewBag.DurataTimer = _bazzerService.DurataTimer;
            ViewBag.MantraAttivo = mantraAttivo;
            ViewBag.AdminNick = User.Identity?.Name ?? "ADMIN";
            ViewBag.LegaAlias = legaModel.Alias;

            // Riepilogo squadre
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

            // Tendina "Ruoli"
            if (mantraAttivo)
            {
                var ruoliRaw = await _context.ListoneCalciatori
                    .Where(c => c.AdminId == adminId && c.RuoloMantra != null && c.RuoloMantra != "")
                    .Select(c => c.RuoloMantra!)
                    .ToListAsync();

                var splitter = new[] { ',', ';', '/', ' ' };
                var ruoliMantra = ruoliRaw
                    .SelectMany(s => s.Split(splitter, StringSplitOptions.RemoveEmptyEntries))
                    .Select(s => s.Trim())
                    .Where(s => s.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(s => s)
                    .ToList();

                ViewBag.RuoliDisponibili = ruoliMantra;
            }
            else
            {
                ViewBag.RuoliDisponibili = await _context.ListoneCalciatori
                    .Where(c => c.AdminId == adminId && c.Ruolo != null && c.Ruolo != "")
                    .Select(c => c.Ruolo!)
                    .Distinct()
                    .OrderBy(r => r)
                    .ToListAsync();
            }

            // ✅ ritorno certo in tutti i casi
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

            // ricavo la lega prima della modifica (dopo potrebbe non esserci più il legame)
            var gioc = await _context.Giocatori
                .Include(g => g.Squadra)
                .ThenInclude(s => s.Lega)
                .FirstOrDefaultAsync(g => g.Id == request.Id);

            var legaId = gioc?.Squadra?.LegaId;
            var legaAlias = gioc?.Squadra?.Lega?.Alias;

            // 👇 clamp difensivo (il service farà un ulteriore clamp usando il costo originale)
            if (request.CreditiRestituiti < 0) request.CreditiRestituiti = 0;

            await _legaService.SvincolaGiocatoreAsync(request.Id, request.CreditiRestituiti);

            // broadcast aggiornamento crediti/rose agli utenti della lega
            if (legaId.HasValue)
            {
                await BroadcastRiepilogoLegaAsync(legaId.Value);
                if (!string.IsNullOrEmpty(legaAlias))
                    await _hubContext.Clients.Group(legaAlias).SendAsync("AggiornaUtente");
            }

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

            // ricavo la lega dalla squadra destinataria
            var squadra = await _context.Squadre
                .Include(s => s.Lega)
                .FirstOrDefaultAsync(s => s.Id == request.SquadraId);
            var legaId = squadra?.LegaId;
            var legaAlias = squadra?.Lega?.Alias;

            await _legaService.AssegnaGiocatoreManualmenteAsync(request.GiocatoreId, request.SquadraId, request.Costo, adminId);

            // broadcast aggiornamento crediti/rose agli utenti della lega
            if (legaId.HasValue)
            {
                await BroadcastRiepilogoLegaAsync(legaId.Value);
                if (!string.IsNullOrEmpty(legaAlias))
                    await _hubContext.Clients.Group(legaAlias).SendAsync("AggiornaUtente");
            }

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

        // ========= Helper privato: broadcast riepilogo legato ad una lega =========
        private async Task BroadcastRiepilogoLegaAsync(int legaId)
        {
            var lega = await _context.Leghe.AsNoTracking().FirstOrDefaultAsync(l => l.Id == legaId);
            if (lega == null) return;

            var squadre = await _context.Squadre
                .Include(s => s.Giocatori)
                .Where(s => s.LegaId == legaId)
                .ToListAsync();

            var slotTotali = lega.MaxPortieri + lega.MaxDifensori + lega.MaxCentrocampisti + lega.MaxAttaccanti;

            var payloadSquadre = squadre.Select(s =>
            {
                int creditiSpesi = s.Giocatori.Sum(g => g.CreditiSpesi ?? 0);
                int creditiDisponibili = s.Crediti - creditiSpesi;

                int p = s.Giocatori.Count(g => g.Ruolo == "P");
                int d = s.Giocatori.Count(g => g.Ruolo == "D");
                int c = s.Giocatori.Count(g => g.Ruolo == "C");
                int a = s.Giocatori.Count(g => g.Ruolo == "A");
                int acquistati = p + d + c + a;
                int slotRimasti = slotTotali - acquistati;
                int puntataMassima = creditiDisponibili - (slotRimasti > 0 ? slotRimasti - 1 : 0);
                if (puntataMassima < 0) puntataMassima = 0;

                return new
                {
                    squadraId = s.Id,
                    nickname = s.Nickname,
                    creditiDisponibili,
                    puntataMassima,
                    portieri = p,
                    difensori = d,
                    centrocampisti = c,
                    attaccanti = a
                };
            }).ToList();

            var payload = new
            {
                squadre = payloadSquadre
            };

            await _hubContext.Clients.Group(lega.Alias).SendAsync("RiepilogoAggiornato", payload);
        }

        // ========= DTO =========
        public class SvincolaRequest { public int Id { get; set; } public int CreditiRestituiti { get; set; } }
        public class TimerRequest { public int Secondi { get; set; } }
        public class AssegnaRequest { public int GiocatoreId { get; set; } public int SquadraId { get; set; } public int Costo { get; set; } }
        public class BloccoPortieriRequest { public bool Attivo { get; set; } }
        // DTO per il bonus
        public class BonusRequest
        {
            public int SquadraId { get; set; }
            public int Delta { get; set; }       // può essere negativo
            public string? Nota { get; set; }    // opzionale, per futuro log
        }
        // Fallback HTTP: pausa
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PausaAsta()
        {
            _bazzerService.MettiInPausa(DateTime.UtcNow);

            // Fallback: broadcast semplice (tutti i client). 
            // Va benissimo perché il caso "buono" è già coperto dalla chiamata SignalR dell'admin.
            await _hub.Clients.All.SendAsync("AstaPausa");

            return Ok();
        }

        // Fallback HTTP: riprendi
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RiprendiAsta()
        {
            _bazzerService.Riprendi(DateTime.UtcNow);

            await _hub.Clients.All.SendAsync("AstaRipresa");

            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> AggiornaCreditiBonus([FromBody] BonusRequest request)
        {
            if (request == null || request.SquadraId <= 0)
                return BadRequest("Richiesta non valida.");

            var squadra = await _context.Squadre
                .Include(s => s.Lega)
                .Include(s => s.Giocatori)
                .FirstOrDefaultAsync(s => s.Id == request.SquadraId);

            if (squadra == null)
                return NotFound("Squadra non trovata.");

            // Applica il delta (positivo o negativo)
            squadra.Crediti += request.Delta;
            await _context.SaveChangesAsync();

            // Prepara payload riepilogo per TUTTA la lega della squadra
            var lega = squadra.Lega!;
            var slotTotali = lega.MaxPortieri + lega.MaxDifensori + lega.MaxCentrocampisti + lega.MaxAttaccanti;

            var squads = await _context.Squadre
                .Include(s => s.Giocatori)
                .Where(s => s.LegaId == lega.Id)
                .Select(s => new
                {
                    squadraId = s.Id,
                    nickname = s.Nickname,
                    creditiDisponibili = s.Crediti - s.Giocatori.Sum(g => g.CreditiSpesi ?? 0),
                    puntataMassima = Math.Max(
                        0,
                        (s.Crediti - s.Giocatori.Sum(g => g.CreditiSpesi ?? 0))
                        - Math.Max(0, (slotTotali - s.Giocatori.Count()) - 1)
                    )
                })
                .OrderBy(s => s.nickname)
                .ToListAsync();

            // Broadcast in tempo reale (Utente + Admin + modale rose)
            await _hubContext.Clients.All.SendAsync("RiepilogoAggiornato", new { squads, mantraAttivo = _bazzerService.MantraAttivo });
            await _hubContext.Clients.All.SendAsync("AggiornaUtente");

            return Ok(new { ok = true });
        }


    }
}
