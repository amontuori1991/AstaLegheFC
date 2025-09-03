using AstaLegheFC.Data;
using AstaLegheFC.Hubs;
using AstaLegheFC.Helpers;
using AstaLegheFC.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace AstaLegheFC.Services
{
    public class LegaService
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<BazzerHub> _hubContext;
        private readonly BazzerService _bazzerService;

        public LegaService(AppDbContext context, IHubContext<BazzerHub> hubContext, BazzerService bazzerService)
        {
            _context = context;
            _hubContext = hubContext;
            _bazzerService = bazzerService;
        }

        /// <summary>
        /// 🔔 Broadcast unico: invia sia AggiornaUtente (barra crediti/puntata)
        /// sia RiepilogoAggiornato (contenuto modale rose) al gruppo della lega.
        /// </summary>
        public async Task BroadcastAggiornamentiLegaAsync(int legaId)
        {
            var legaInfo = await _context.Leghe
                .Where(l => l.Id == legaId)
                .Select(l => new { l.Id, l.Alias, l.MaxPortieri, l.MaxDifensori, l.MaxCentrocampisti, l.MaxAttaccanti })
                .FirstOrDefaultAsync();

            if (legaInfo == null) return;

            var legaLower = (legaInfo.Alias ?? "").Trim().ToLower();
            var slotTotali = legaInfo.MaxPortieri + legaInfo.MaxDifensori + legaInfo.MaxCentrocampisti + legaInfo.MaxAttaccanti;
            var mantraAttivo = _bazzerService.IsMantraAttivo(legaInfo.Alias);

            var squads = await _context.Squadre
                .Include(s => s.Giocatori)
                .Where(s => s.LegaId == legaInfo.Id)
                .Select(s => new
                {
                    squadraId = s.Id,
                    nickname = s.Nickname,
                    creditiDisponibili = s.Crediti - s.Giocatori.Sum(g => g.CreditiSpesi ?? 0),
                    puntataMassima = System.Math.Max(
                        0,
                        (s.Crediti - s.Giocatori.Sum(g => g.CreditiSpesi ?? 0))
                        - System.Math.Max(0, (slotTotali - s.Giocatori.Count()) - 1)
                    ),
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

            await _hubContext.Clients.Group(legaLower).SendAsync("AggiornaUtente");
            await _hubContext.Clients.Group(legaLower).SendAsync("RiepilogoAggiornato", new { squads, mantraAttivo });
        }

        /// <summary>
        /// Svincola un giocatore. Rimborso di default = costo attuale.
        /// Se viene passato un valore (creditiRestituiti) viene clampato a [0..costoAttuale].
        /// Regole speciali per portieri collegati (costo 0).
        /// </summary>
        public async Task SvincolaGiocatoreAsync(int giocatoreId, int creditiRestituiti)
        {
            await using var tx = await _context.Database.BeginTransactionAsync();

            var g = await _context.Giocatori
                .Include(x => x.Squadra)
                .FirstOrDefaultAsync(x => x.Id == giocatoreId);

            if (g == null)
            {
                await tx.RollbackAsync();
                return;
            }

            var squadra = g.Squadra ?? await _context.Squadre.FirstAsync(s => s.Id == g.SquadraId);

            // costo attuale (potrebbe essere stato modificato dall'admin)
            var costoAttuale = g.CreditiSpesi ?? 0;

            // Rimborso richiesto: se non valido => usa costoAttuale
            int rimborso = (creditiRestituiti >= 0)
                ? System.Math.Min(System.Math.Max(0, creditiRestituiti), costoAttuale)
                : costoAttuale;

            var isPortiere = string.Equals(g.Ruolo, "P", System.StringComparison.OrdinalIgnoreCase);
            var haSquadraReale = !string.IsNullOrWhiteSpace(g.SquadraReale);

            // Portiere collegato (costo 0) di un blocco? → rimborso 0
            var isCollegatoCostoZero =
                isPortiere && costoAttuale == 0 && haSquadraReale &&
                await _context.Giocatori.AnyAsync(x =>
                    x.SquadraId == g.SquadraId &&
                    x.Ruolo == "P" &&
                    x.SquadraReale == g.SquadraReale &&
                    (x.CreditiSpesi ?? 0) > 0);

            if (isCollegatoCostoZero)
                rimborso = 0;

            // Svincolo del principale (costo > 0) → rimuovi collegati a 0 della stessa squadra reale
            if (isPortiere && costoAttuale > 0 && haSquadraReale)
            {
                var collegatiZero = await _context.Giocatori
                    .Where(x => x.SquadraId == g.SquadraId
                                && x.Id != g.Id
                                && x.Ruolo == "P"
                                && x.SquadraReale == g.SquadraReale
                                && (x.CreditiSpesi ?? 0) == 0)
                    .ToListAsync();

                if (collegatiZero.Count > 0)
                    _context.Giocatori.RemoveRange(collegatiZero);
            }

            // Rimuovi il giocatore
            _context.Giocatori.Remove(g);

            // Delta sul budget "iniziale" della squadra.
            // Con il nostro modello, i "crediti disponibili" sono: s.Crediti - Σ(CreditiSpesi).
            // Se rimborso == costoAttuale → delta = 0 (nessuna variazione su s.Crediti).
            // Se rimborso <  costoAttuale → delta < 0 (penalizza s.Crediti per la differenza).
            var delta = rimborso - costoAttuale;
            if (delta != 0)
            {
                squadra.Crediti += delta;
                _context.Squadre.Update(squadra);
            }

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            // 🔔 broadcast
            await BroadcastAggiornamentiLegaAsync(squadra.LegaId);
        }

        public async Task AssegnaGiocatoreManualmenteAsync(int giocatoreId, int squadraId, int costo, string adminId)
        {
            var calciatoreDaListone = await _context.ListoneCalciatori
                .FirstOrDefaultAsync(c => c.Id == giocatoreId && c.AdminId == adminId);

            if (calciatoreDaListone == null) return;

            var squadra = await _context.Squadre
                .Include(s => s.Lega)
                .FirstOrDefaultAsync(s => s.Id == squadraId);

            if (squadra == null) return;

            var nuovoGiocatore = new Giocatore
            {
                IdListone = calciatoreDaListone.IdListone,
                Nome = calciatoreDaListone.Nome,
                Ruolo = calciatoreDaListone.Ruolo,
                RuoloMantra = calciatoreDaListone.RuoloMantra,
                SquadraReale = calciatoreDaListone.Squadra,
                SquadraId = squadraId,
                CreditiSpesi = costo
            };
            _context.Giocatori.Add(nuovoGiocatore);

            // Blocco portieri: aggiungi i collegati a costo 0
            if (_bazzerService.IsBloccoPortieriAttivo(squadra.Lega?.Alias ?? string.Empty)
                && calciatoreDaListone.Ruolo == "P")
            {
                var idGiocatoriAcquistatiLega = await _context.Giocatori
                    .Where(g => g.Squadra.LegaId == squadra.LegaId)
                    .Select(g => g.IdListone)
                    .ToListAsync();

                var altriPortieri = await _context.ListoneCalciatori
                    .Where(p => p.Squadra == calciatoreDaListone.Squadra &&
                                p.Ruolo == "P" &&
                                p.Id != calciatoreDaListone.Id &&
                                !idGiocatoriAcquistatiLega.Contains(p.IdListone) &&
                                p.AdminId == adminId)
                    .ToListAsync();

                foreach (var portiere in altriPortieri)
                {
                    var portiereCollegato = new Giocatore
                    {
                        IdListone = portiere.IdListone,
                        Nome = portiere.Nome,
                        Ruolo = portiere.Ruolo,
                        RuoloMantra = portiere.RuoloMantra,
                        SquadraReale = portiere.Squadra,
                        SquadraId = squadraId,
                        CreditiSpesi = 0
                    };
                    _context.Giocatori.Add(portiereCollegato);
                }
            }

            await _context.SaveChangesAsync();

            // 🔔 broadcast
            await BroadcastAggiornamentiLegaAsync(squadra.LegaId);
        }
    }
}
