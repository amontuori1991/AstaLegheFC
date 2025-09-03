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
using Microsoft.Extensions.Caching.Memory;
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
        private readonly UserManager<ApplicationUser> _userManager;


        public AdminController(
            AppDbContext context,
            BazzerService bazzerService,
            LegaService legaService,
            IHubContext<BazzerHub> hubContext,
            UserManager<ApplicationUser> userManager)

        {
            _context = context;
            _bazzerService = bazzerService;
            _legaService = legaService;
            _hubContext = hubContext;
            _userManager = userManager;
        }

        // ========= Helpers =========
        private static string Normalize(string? s) => (s ?? string.Empty).Trim().ToLowerInvariant();

        private int GetDurataTimer(string lega)
        {
            var t = _bazzerService.GetType();
            var m = t.GetMethod("GetDurataTimer", new[] { typeof(string) });
            if (m != null)
            {
                try
                {
                    var v = m.Invoke(_bazzerService, new object[] { lega });
                    if (v is int i) return i;
                }
                catch { }
            }
            var p = t.GetProperty("DurataTimer");
            if (p != null)
            {
                try
                {
                    var v = p.GetValue(_bazzerService);
                    if (v is int i) return i;
                }
                catch { }
            }
            return 5;
        }

        private bool IsBloccoPortieriAttivo(string lega)
        {
            var t = _bazzerService.GetType();
            var m = t.GetMethod("IsBloccoPortieriAttivo", new[] { typeof(string) })
                 ?? t.GetMethod("GetBloccoPortieriAttivo", new[] { typeof(string) });
            if (m != null)
            {
                try
                {
                    var v = m.Invoke(_bazzerService, new object[] { lega });
                    if (v is bool b) return b;
                }
                catch { }
            }
            var p = t.GetProperty("BloccoPortieriAttivo");
            if (p != null)
            {
                try
                {
                    var v = p.GetValue(_bazzerService);
                    if (v is bool b) return b;
                }
                catch { }
            }
            return false;
        }

        private bool IsMantraAttivo(string lega)
        {
            var t = _bazzerService.GetType();
            var m = t.GetMethod("IsMantraAttivo", new[] { typeof(string) })
                 ?? t.GetMethod("GetMantraAttivo", new[] { typeof(string) });
            if (m != null)
            {
                try
                {
                    var v = m.Invoke(_bazzerService, new object[] { lega });
                    if (v is bool b) return b;
                }
                catch { }
            }
            var p = t.GetProperty("MantraAttivo");
            if (p != null)
            {
                try
                {
                    var v = p.GetValue(_bazzerService);
                    if (v is bool b) return b;
                }
                catch { }
            }
            return false;
        }

        private DateTime? GetFineOffertaUtc(string lega)
        {
            var t = _bazzerService.GetType();
            var m = t.GetMethod("GetFineOffertaUtc", new[] { typeof(string) });
            if (m != null)
            {
                try
                {
                    var v = m.Invoke(_bazzerService, new object[] { lega });
                    if (v is DateTime d1) return d1;
                    var nd = v as DateTime?;
                    if (nd.HasValue) return nd.Value;
                }
                catch { }
            }
            var p = t.GetProperty("FineOffertaUtc");
            if (p != null)
            {
                try
                {
                    var v = p.GetValue(_bazzerService);
                    if (v is DateTime d2) return d2;
                    var nd2 = v as DateTime?;
                    if (nd2.HasValue) return nd2.Value;
                }
                catch { }
            }
            return null;
        }

        // ========= Import =========
        [HttpGet]
        public IActionResult ImportaListone() => View();

        [HttpPost]
        public async Task<IActionResult> ImportaListone(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                ViewBag.Errore = "Per favore, seleziona un file Excel valido.";
                return View();
            }

            var adminId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(adminId))
                return Unauthorized("Non è stato possibile identificare l'utente.");

            var vecchioListone = await _context.ListoneCalciatori.Where(c => c.AdminId == adminId).ToListAsync();
            if (vecchioListone.Any()) _context.ListoneCalciatori.RemoveRange(vecchioListone);

            var nuoviCalciatori = new List<CalciatoreListone>();
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var stream = new MemoryStream();
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
                for (int row = 2; row <= rowCount; row++)
                {
                    try
                    {
                        var idValue = worksheet.Cells[row, 1].Value;
                        if (idValue == null || !int.TryParse(idValue.ToString(), out int idListone)) continue;

                        var calciatore = new CalciatoreListone
                        {
                            IdListone = idListone,
                            Ruolo = worksheet.Cells[row, 2].Value?.ToString().Trim(),
                            RuoloMantra = worksheet.Cells[row, 3].Value?.ToString().Trim(),
                            Nome = worksheet.Cells[row, 4].Value?.ToString().Trim(),
                            Squadra = worksheet.Cells[row, 5].Value?.ToString().Trim(),
                            QtA = Convert.ToInt32(worksheet.Cells[row, 6].Value),
                            QtI = Convert.ToInt32(worksheet.Cells[row, 7].Value),
                            AdminId = adminId
                        };
                        nuoviCalciatori.Add(calciatore);
                    }
                    catch { }
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

        // ========= Vista Admin Listone (paginata) =========
        [HttpGet]
        public async Task<IActionResult> VisualizzaListone(
            string lega,
            string? nome,
            string? squadra,
            string? ruolo,
            [FromQuery(Name = "mantra")] bool mantraAttivo = false,
            [FromQuery(Name = "sorteggio")] bool sorteggioLetteraAttivo = false,
            [FromQuery(Name = "iniziale")] string? iniziale = null,
            int page = 1,
            int pageSize = 20)
        {
            if (string.IsNullOrEmpty(lega))
                return Content("⚠️ Parametro lega mancante. Inserisci ?lega=...");

            if (page < 1) page = 1;
            if (pageSize <= 0 || pageSize > 200) pageSize = 20;

            var adminId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(adminId)) return Unauthorized();

            var legaModel = await _context.Leghe
                .FirstOrDefaultAsync(l => l.Alias.ToLower() == lega.ToLower() && l.AdminId == adminId);
            if (legaModel == null) return Forbid("Lega non trovata o non appartenente a questo utente.");

            var idGiocatoriAcquistati = await _context.Giocatori
                .Where(g => g.Squadra.LegaId == legaModel.Id)
                .Select(g => g.IdListone)
                .ToListAsync();

            IQueryable<CalciatoreListone> queryListone = _context.ListoneCalciatori
                .Where(c => c.AdminId == adminId && !idGiocatoriAcquistati.Contains(c.IdListone));

            // Filtri
            if (!string.IsNullOrWhiteSpace(nome))
                queryListone = queryListone.Where(c => EF.Functions.ILike(c.Nome, $"%{nome}%"));

            if (!string.IsNullOrWhiteSpace(squadra))
                queryListone = queryListone.Where(c => EF.Functions.ILike(c.Squadra, $"%{squadra}%"));

            if (!string.IsNullOrWhiteSpace(iniziale))
                queryListone = queryListone.Where(c => EF.Functions.ILike(c.Nome, $"{iniziale}%"));

            if (!string.IsNullOrWhiteSpace(ruolo))
            {
                if (mantraAttivo)
                {
                    queryListone = queryListone.Where(c =>
                        c.RuoloMantra != null &&
                        EF.Functions.ILike(c.RuoloMantra, $"%{ruolo}%"));
                }
                else
                {
                    queryListone = queryListone.Where(c => c.Ruolo == ruolo);
                }
            }

            // per-lega
            _bazzerService.ImpostaModalitaMantra(legaModel.Alias, mantraAttivo);

            // ===== PAGINAZIONE =====
            var totalCount = await queryListone.CountAsync();

            var items = await queryListone
                .OrderBy(g => g.Nome)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // ===== ViewBag già usati dalla tua view =====
            ViewBag.Nome = nome;
            ViewBag.Squadra = squadra;
            ViewBag.Ruolo = ruolo;
            ViewBag.Iniziale = iniziale;
            ViewBag.SorteggioLetteraAttivo = sorteggioLetteraAttivo;
            ViewBag.BloccoPortieriAttivo = IsBloccoPortieriAttivo(legaModel.Alias);
            ViewBag.DurataTimer = GetDurataTimer(legaModel.Alias);
            ViewBag.MantraAttivo = IsMantraAttivo(legaModel.Alias) || mantraAttivo;
            ViewBag.BuzzerAttivo = _bazzerService.IsBuzzerModeAttivo(legaModel.Alias);
            ViewBag.AdminNick = User.Identity?.Name ?? "ADMIN";
            ViewBag.LegaAlias = legaModel.Alias;

            // ===== Riepilogo squadre (immutato) =====
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

            // ===== Ruoli disponibili (immutato) =====
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

            // ===== ViewModel paginato =====
            var vm = new ListoneViewModel
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return View("VisualizzaListone", vm);
        }


        // ========= Avvia / Annulla =========
        [HttpPost]
        public async Task<IActionResult> AvviaAsta(
            [FromForm] int id,
            [FromForm(Name = "mantra")] bool mantraAttivo,
            [FromForm] string lega)
        {
            var adminId = _userManager.GetUserId(User);
            var giocatore = await _context.ListoneCalciatori
                .FirstOrDefaultAsync(c => c.Id == id && c.AdminId == adminId);
            if (giocatore == null) return NotFound();

            _bazzerService.ImpostaGiocatoreInAsta(lega, giocatore, mantraAttivo);

            var ruoloDaMostrare = mantraAttivo ? giocatore.RuoloMantra : giocatore.Ruolo;

            await _hubContext.Clients.Group(Normalize(lega)).SendAsync("MostraGiocatoreInAsta", new
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AnnullaAsta([FromQuery] string lega)
        {
            _bazzerService.AnnullaAstaCorrente(lega);
            await _hubContext.Clients.Group(Normalize(lega)).SendAsync("AstaAnnullata");
            return Ok();
        }

        // ========= Svincola / Assegna =========
        [HttpPost]
        public async Task<IActionResult> SvincolaGiocatore([FromBody] SvincolaRequest request)
        {
            if (request.Id <= 0) return BadRequest("Dati non validi.");

            var gioc = await _context.Giocatori
                .Include(g => g.Squadra)
                .ThenInclude(s => s.Lega)
                .FirstOrDefaultAsync(g => g.Id == request.Id);

            var legaId = gioc?.Squadra?.LegaId;
            var legaAlias = gioc?.Squadra?.Lega?.Alias;

          //  if (request.CreditiRestituiti < 0) request.CreditiRestituiti = 0;

            await _legaService.SvincolaGiocatoreAsync(request.Id, request.CreditiRestituiti);

            if (legaId.HasValue)
            {
                await BroadcastRiepilogoLegaAsync(legaId.Value);
                if (!string.IsNullOrEmpty(legaAlias))
                    await _hubContext.Clients.Group(Normalize(legaAlias)).SendAsync("AggiornaUtente");
            }

            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> AssegnaManualmente([FromBody] AssegnaRequest request)
        {
            if (request.Costo < 0 || request.SquadraId <= 0 || request.GiocatoreId <= 0)
                return BadRequest("Dati non validi.");

            var adminId = _userManager.GetUserId(User);

            var squadra = await _context.Squadre
                .Include(s => s.Lega)
                .Include(s => s.Giocatori)
                .FirstOrDefaultAsync(s => s.Id == request.SquadraId);
            if (squadra == null)
                return NotFound("Squadra non trovata.");

            var lega = squadra.Lega!;
            var listoneItem = await _context.ListoneCalciatori
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.GiocatoreId && c.AdminId == adminId);
            if (listoneItem == null)
                return NotFound("Giocatore non trovato nel tuo listone.");

            var ruolo = (listoneItem.Ruolo ?? "").Trim().ToUpperInvariant();

            int countP = squadra.Giocatori.Count(g => g.Ruolo == "P");
            int countD = squadra.Giocatori.Count(g => g.Ruolo == "D");
            int countC = squadra.Giocatori.Count(g => g.Ruolo == "C");
            int countA = squadra.Giocatori.Count(g => g.Ruolo == "A");

            bool superaLimite = (ruolo == "P" && countP >= lega.MaxPortieri)
                             || (ruolo == "D" && countD >= lega.MaxDifensori)
                             || (ruolo == "C" && countC >= lega.MaxCentrocampisti)
                             || (ruolo == "A" && countA >= lega.MaxAttaccanti);

            if (superaLimite)
            {
                return Conflict(new
                {
                    ok = false,
                    message = ruolo switch
                    {
                        "P" => $"Limite portieri ({lega.MaxPortieri}) già raggiunto.",
                        "D" => $"Limite difensori ({lega.MaxDifensori}) già raggiunto.",
                        "C" => $"Limite centrocampisti ({lega.MaxCentrocampisti}) già raggiunto.",
                        "A" => $"Limite attaccanti ({lega.MaxAttaccanti}) già raggiunto.",
                        _ => "Limite di ruolo già raggiunto."
                    }
                });
            }

            await _legaService.AssegnaGiocatoreManualmenteAsync(request.GiocatoreId, request.SquadraId, request.Costo, adminId);

            await BroadcastRiepilogoLegaAsync(lega.Id);
            if (!string.IsNullOrEmpty(lega.Alias))
                await _hubContext.Clients.Group(Normalize(lega.Alias)).SendAsync("AggiornaUtente");

            return Ok();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConcludiAstaBuzzer([FromBody] ConcludiAstaBuzzerDto dto)
        {
            if (dto == null || dto.GiocatoreListoneId <= 0 || dto.SquadraId <= 0 || dto.Costo < 0)
                return BadRequest("Dati non validi.");

            var adminId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(adminId)) return Unauthorized();

            // Verifica che la squadra esista e recupera lega
            var squadra = await _context.Squadre
                .Include(s => s.Lega)
                .Include(s => s.Giocatori)
                .FirstOrDefaultAsync(s => s.Id == dto.SquadraId);

            if (squadra == null) return NotFound("Squadra non trovata.");
            var lega = squadra.Lega!;
            if (lega.AdminId != adminId) return Forbid();

            // Verifica che il giocatore sia nel TUO listone
            var listoneItem = await _context.ListoneCalciatori
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == dto.GiocatoreListoneId && c.AdminId == adminId);

            if (listoneItem == null)
                return NotFound("Giocatore non trovato nel tuo listone.");

            // Budget check (come in ModificaCostoGiocatore)
            var sumOther = squadra.Giocatori.Sum(x => x.CreditiSpesi ?? 0);
            var maxConsentito = squadra.Crediti - sumOther;
            if (dto.Costo > maxConsentito)
                return Conflict(new { message = $"Costo troppo alto. Max consentito: {maxConsentito}." });

            // Assegna usando la stessa logica di sempre (gestisce anche Blocco Portieri)
            await _legaService.AssegnaGiocatoreManualmenteAsync(dto.GiocatoreListoneId, dto.SquadraId, dto.Costo, adminId);

            // Broadcast riepilogo e refresh utenti
            await BroadcastRiepilogoLegaAsync(lega.Id);
            await _hubContext.Clients.Group(Normalize(lega.Alias)).SendAsync("AggiornaUtente");

            return Ok(new { ok = true });
        }

        // ========= Impostazioni per-lega =========
        [HttpPost]
        public async Task<IActionResult> ImpostaTimer([FromBody] TimerReq request)
        {
            _bazzerService.ImpostaDurataTimer(request.Lega, request.Secondi);
            var durata = GetDurataTimer(request.Lega);
            await _hubContext.Clients.Group(Normalize(request.Lega)).SendAsync("AggiornaDurataTimer", durata);
            return Ok();
        }

        [HttpPost]
        public IActionResult ImpostaBloccoPortieri([FromBody] BloccoPortieriRequest request)
        {
            _bazzerService.ImpostaBloccoPortieri(request.Lega, request.Attivo);
            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> EsportaRosa(int squadraId)
        {
            var squadra = await _context.Squadre
                .Include(s => s.Giocatori)
                .FirstOrDefaultAsync(s => s.Id == squadraId);

            if (squadra == null || squadra.Giocatori == null) return NotFound();

            var builder = new StringBuilder();
            foreach (var giocatore in squadra.Giocatori)
                builder.AppendLine($"{squadra.Nickname},{giocatore.IdListone},{giocatore.CreditiSpesi}");

            return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", $"{squadra.Nickname}.csv");
        }

        // ========= Broadcast riepilogo =========
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

            var payload = new { squadre = payloadSquadre };
            await _hubContext.Clients.Group(Normalize(lega.Alias)).SendAsync("RiepilogoAggiornato", payload);
        }

        // ========= Pausa / Riprendi (HTTP fallback) =========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PausaAsta([FromQuery] string lega)
        {
            _bazzerService.MettiInPausa(lega, DateTime.UtcNow);

            int remaining = 0;
            var end = GetFineOffertaUtc(lega);
            if (end.HasValue)
            {
                var sec = (int)Math.Ceiling((end.Value - DateTime.UtcNow).TotalSeconds);
                remaining = Math.Max(0, sec);
            }

            await _hubContext.Clients.Group(Normalize(lega)).SendAsync("AstaPausa", new { remainingSec = remaining });
            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RiprendiAsta([FromQuery] string lega)
        {
            _bazzerService.Riprendi(lega, DateTime.UtcNow);
            var fineUtc = GetFineOffertaUtc(lega)?.ToUniversalTime().ToString("o");
            await _hubContext.Clients.Group(Normalize(lega)).SendAsync("AstaRipresa", new { fineUtc });
            return Ok();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ModificaCostoGiocatore([FromBody] ModificaCostoDto dto)
        {
            var adminId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(adminId)) return Unauthorized();

            var giocatore = await _context.Giocatori
                .Include(g => g.Squadra)
                    .ThenInclude(s => s.Giocatori)
                .FirstOrDefaultAsync(g => g.Id == dto.GiocatoreId);

            if (giocatore == null) return NotFound(new { message = "Giocatore non trovato." });
            if (giocatore.Squadra == null) return BadRequest(new { message = "Il giocatore non è assegnato a nessuna squadra." });

            // Verifica appartenenza lega
            var lega = await _context.Leghe.FirstOrDefaultAsync(l => l.Id == giocatore.Squadra.LegaId);
            if (lega == null || lega.AdminId != adminId) return Forbid();

            var nuovo = Math.Max(0, dto.NuovoCosto);

            // Budget check
            var sumOther = giocatore.Squadra.Giocatori.Where(x => x.Id != giocatore.Id).Sum(x => x.CreditiSpesi ?? 0);
            var maxConsentito = giocatore.Squadra.Crediti - sumOther;
            if (nuovo > maxConsentito)
                return Conflict(new { message = $"Costo troppo alto. Max consentito: {maxConsentito}." });

            // Salva
            giocatore.CreditiSpesi = nuovo;
            await _context.SaveChangesAsync();

            // Recalc
            var sumAllNow = giocatore.Squadra.Giocatori.Sum(x => x.CreditiSpesi ?? 0);
            var creditiDisponibiliNow = giocatore.Squadra.Crediti - sumAllNow;

            int slotTotali = lega.MaxPortieri + lega.MaxDifensori + lega.MaxCentrocampisti + lega.MaxAttaccanti;
            int giocatoriAcquistatiCount = giocatore.Squadra.Giocatori.Count;
            int slotRimasti = slotTotali - giocatoriAcquistatiCount;
            int pmax = creditiDisponibiliNow - (slotRimasti > 0 ? slotRimasti - 1 : 0);
            if (pmax < 0) pmax = 0;

            // Broadcast
            await _hubContext.Clients.Group(Normalize(lega.Alias)).SendAsync("RiepilogoAggiornato", new
            {
                squads = new[] {
            new {
                squadraId = giocatore.SquadraId,
                creditiDisponibili = creditiDisponibiliNow,
                puntataMassima = pmax
            }
        }
            });

            return Ok(new
            {
                nuovoCosto = nuovo,
                squadraId = giocatore.SquadraId,
                creditiDisponibili = creditiDisponibiliNow,
                puntataMassima = pmax
            });
        }

        [HttpPost]
        public async Task<IActionResult> ImpostaBuzzerMode([FromBody] BuzzerModeRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Lega))
                return BadRequest("Richiesta non valida.");

            _bazzerService.ImpostaBuzzerMode(request.Lega, request.Attivo);

            var legaKey = (request.Lega ?? "").Trim().ToLowerInvariant();

            // Broadcast compatibile con client vecchi/nuovi
            await _hubContext.Clients.Group(legaKey).SendAsync("BuzzerModeChanged", new { attivo = request.Attivo });
            await _hubContext.Clients.Group(legaKey).SendAsync("BuzzerModeAggiornato", new { buzzerAttivo = request.Attivo });

            // Stato aggiornato
            var stato = _bazzerService.GetStato(request.Lega);
            await _hubContext.Clients.Group(legaKey).SendAsync("StatoAsta", new
            {
                pausaAttiva = stato.pausaAttiva,
                startUtc = stato.astaStartUtc?.ToUniversalTime().ToString("o"),
                pausaAccumulataSec = (int)stato.pausaAccumulata.TotalSeconds,
                pausaStartUtc = stato.pausaStartUtc?.ToUniversalTime().ToString("o"),
                fineOffertaUtc = stato.fineOffertaUtc?.ToUniversalTime().ToString("o"),
                mantraAttivo = stato.mantraAttivo,
                buzzerAttivo = _bazzerService.IsBuzzerModeAttivo(request.Lega)
            });

            return Ok(new { ok = true });
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

            squadra.Crediti += request.Delta;
            await _context.SaveChangesAsync();

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

            var mantraAttivo = IsMantraAttivo(lega.Alias);

            await _hubContext.Clients.Group(Normalize(lega.Alias))
                .SendAsync("RiepilogoAggiornato", new { squads, mantraAttivo });
            await _hubContext.Clients.Group(Normalize(lega.Alias))
                .SendAsync("AggiornaUtente");

            return Ok(new { ok = true });
        }

        // ========= DTO =========
        public class SvincolaRequest { public int Id { get; set; } public int CreditiRestituiti { get; set; } }
        public class TimerReq { public int Secondi { get; set; } public string Lega { get; set; } = ""; }
        public class AssegnaRequest { public int GiocatoreId { get; set; } public int SquadraId { get; set; } public int Costo { get; set; } }
        public class BloccoPortieriRequest { public string Lega { get; set; } = ""; public bool Attivo { get; set; } }
        public class BonusRequest { public int SquadraId { get; set; } public int Delta { get; set; } public string? Nota { get; set; } }

        public class ModificaCostoDto
        {
            public int GiocatoreId { get; set; }
            public int NuovoCosto { get; set; }
        }

        public class BuzzerModeRequest
        {
            public string Lega { get; set; } = "";
            public bool Attivo { get; set; }
        }


        public class ConcludiAstaBuzzerDto
        {
            public int GiocatoreListoneId { get; set; }  // <-- Id del record in ListoneCalciatori (è quello che inviamo in MostraGiocatoreInAsta)
            public int SquadraId { get; set; }           // squadra a cui assegnare
            public int Costo { get; set; }               // costo deciso a voce
            public string Lega { get; set; } = "";       // alias della lega corrente
        }


    }
}
