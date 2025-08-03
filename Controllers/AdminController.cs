using AstaLegheFC.Data;
using AstaLegheFC.Helpers;
using AstaLegheFC.Hubs;
using AstaLegheFC.Models;
using AstaLegheFC.Models.ViewModels;
using AstaLegheFC.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AstaLegheFC.Controllers
{
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly BazzerService _bazzerService;
        private readonly LegaService _legaService;
        private readonly IHubContext<BazzerHub> _hubContext;

        public AdminController(AppDbContext context, BazzerService bazzerService, LegaService legaService, IHubContext<BazzerHub> hubContext)
        {
            _context = context;
            _bazzerService = bazzerService;
            _legaService = legaService;
            _hubContext = hubContext;
        }

        public async Task<IActionResult> VisualizzaListone(string lega, string nome, string squadra, string ruolo)
        {
            if (string.IsNullOrEmpty(lega)) return Content("⚠️ Parametro lega mancante. Inserisci ?lega=...");
            var legaModel = await _context.Leghe.FirstOrDefaultAsync(l => l.Alias.ToLower() == lega.ToLower());
            if (legaModel == null) return Content("⚠️ Lega non trovata.");

            var idGiocatoriAcquistati = await _context.Giocatori
                .Where(g => g.Squadra.LegaId == legaModel.Id)
                .Select(g => g.IdListone)
                .ToListAsync();

            var queryListone = _context.ListoneCalciatori.Where(c => !idGiocatoriAcquistati.Contains(c.IdListone));

            if (!string.IsNullOrEmpty(nome)) queryListone = queryListone.Where(c => c.Nome.ToLower().Contains(nome.ToLower()));
            if (!string.IsNullOrEmpty(squadra)) queryListone = queryListone.Where(c => c.Squadra.ToLower().Contains(squadra.ToLower()));
            if (!string.IsNullOrEmpty(ruolo)) queryListone = queryListone.Where(c => c.Ruolo == ruolo);

            var listoneDisponibile = await queryListone.OrderBy(g => g.Nome).ToListAsync();
            ViewBag.Nome = nome;
            ViewBag.Squadra = squadra;
            ViewBag.Ruolo = ruolo;
            ViewBag.BloccoPortieriAttivo = _bazzerService.BloccoPortieriAttivo;

            // ✅ AGGIUNTA QUESTA RIGA PER I SUONI DEL COUNTDOWN
            ViewBag.DurataTimer = _bazzerService.DurataTimer;

            #region Riepilogo Squadre e Dati Vista
            var squadre = await _context.Squadre
                .Include(s => s.Giocatori)
                .Where(s => s.LegaId == legaModel.Id)
                .OrderBy(s => s.Nickname)
                .ToListAsync();

            var riepilogo = new List<SquadraRiepilogoViewModel>();
            foreach (var s in squadre)
            {
                int creditiSpesi = s.Giocatori.Sum(g => g.CreditiSpesi ?? 0);
                int creditiDisponibili = s.Crediti - creditiSpesi;
                int giocatoriAcquistatiCount = s.Giocatori.Count;
                int slotTotali = RegoleLega.MaxPortieri + RegoleLega.MaxDifensori + RegoleLega.MaxCentrocampisti + RegoleLega.MaxAttaccanti;
                int slotRimasti = slotTotali - giocatoriAcquistatiCount;
                int puntataMassima = creditiDisponibili - (slotRimasti > 0 ? slotRimasti - 1 : 0);

                riepilogo.Add(new SquadraRiepilogoViewModel
                {
                    SquadraId = s.Id,
                    Nickname = s.Nickname,
                    CreditiDisponibili = creditiDisponibili,
                    PuntataMassima = puntataMassima > 0 ? puntataMassima : 0,
                    PortieriAssegnati = s.Giocatori.Where(g => g.Ruolo == "P").Select(g => new GiocatoreAssegnato { Id = g.Id, Nome = g.Nome, CreditiSpesi = g.CreditiSpesi ?? 0 }).ToList(),
                    DifensoriAssegnati = s.Giocatori.Where(g => g.Ruolo == "D").Select(g => new GiocatoreAssegnato { Id = g.Id, Nome = g.Nome, CreditiSpesi = g.CreditiSpesi ?? 0 }).ToList(),
                    CentrocampistiAssegnati = s.Giocatori.Where(g => g.Ruolo == "C").Select(g => new GiocatoreAssegnato { Id = g.Id, Nome = g.Nome, CreditiSpesi = g.CreditiSpesi ?? 0 }).ToList(),
                    AttaccantiAssegnati = s.Giocatori.Where(g => g.Ruolo == "A").Select(g => new GiocatoreAssegnato { Id = g.Id, Nome = g.Nome, CreditiSpesi = g.CreditiSpesi ?? 0 }).ToList()
                });
            }
            ViewBag.RiepilogoSquadre = riepilogo;
            ViewBag.LegaAlias = legaModel.Alias;
            ViewBag.RuoliDisponibili = await _context.ListoneCalciatori.Select(c => c.Ruolo).Distinct().OrderBy(r => r).ToListAsync();
            #endregion

            return View("VisualizzaListone", listoneDisponibile);
        }

        [HttpPost]
        public async Task<IActionResult> AvviaAsta(int id)
        {
            var giocatore = await _context.ListoneCalciatori.FindAsync(id);
            if (giocatore == null) return NotFound();

            _bazzerService.ImpostaGiocatoreInAsta(giocatore);

            await _hubContext.Clients.All.SendAsync("MostraGiocatoreInAsta", new
            {
                id = giocatore.Id,
                nome = giocatore.Nome,
                ruolo = giocatore.Ruolo,
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
            if (request.Id <= 0) return BadRequest("ID non valido.");
            await _legaService.SvincolaGiocatoreAsync(request.Id);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> AssegnaManualmente([FromBody] AssegnaRequest request)
        {
            if (request.Costo <= 0 || request.SquadraId <= 0 || request.GiocatoreId <= 0)
            {
                return BadRequest("Dati non validi.");
            }
            await _legaService.AssegnaGiocatoreManualmenteAsync(request.GiocatoreId, request.SquadraId, request.Costo);
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

        public class SvincolaRequest { public int Id { get; set; } }
        public class TimerRequest { public int Secondi { get; set; } }
        public class AssegnaRequest
        {
            public int GiocatoreId { get; set; }
            public int SquadraId { get; set; }
            public int Costo { get; set; }
        }
        public class BloccoPortieriRequest { public bool Attivo { get; set; } }
    }
}