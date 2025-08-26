using AstaLegheFC.Data;
using AstaLegheFC.Hubs;
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

            var costoOriginale = g.CreditiSpesi ?? 0;

            // Rimborso: libero, ma non negativo (l’admin può mettere 0, metà, più del costo, ecc.)
            var rimborso = Math.Max(0, creditiRestituiti);

            var isPortiere = string.Equals(g.Ruolo, "P", System.StringComparison.OrdinalIgnoreCase);
            var haSquadraReale = !string.IsNullOrWhiteSpace(g.SquadraReale);

            // È un portiere "collegato" (costo 0) di un blocco?
            var isCollegatoCostoZero = isPortiere && costoOriginale == 0 && haSquadraReale &&
                await _context.Giocatori.AnyAsync(x => x.SquadraId == g.SquadraId
                                                       && x.Ruolo == "P"
                                                       && x.SquadraReale == g.SquadraReale
                                                       && (x.CreditiSpesi ?? 0) > 0);

            // Se è un collegato a costo 0 → rimborso sempre 0
            if (isCollegatoCostoZero)
                rimborso = 0;

            // Se sto svincolando il portiere "principale": elimina anche i collegati a costo 0
            if (isPortiere && costoOriginale > 0 && haSquadraReale)
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

            // Rimuovo il giocatore principale
            _context.Giocatori.Remove(g);

            // Budget base: delta = rimborso - costoOriginale
            // Disponibili dopo = (S + delta) - (Σ - costo) = S - Σ + rimborso → incrementa esattamente del rimborso inserito
            var delta = rimborso - costoOriginale;
            if (delta != 0)
            {
                squadra.Crediti += delta;
                _context.Squadre.Update(squadra);
            }

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        }


        // In Services/LegaService.cs

        public async Task AssegnaGiocatoreManualmenteAsync(int giocatoreId, int squadraId, int costo, string adminId)
        {
            // La ricerca ora controlla che il giocatore appartenga al listone dell'admin corretto
            var calciatoreDaListone = await _context.ListoneCalciatori
                .FirstOrDefaultAsync(c => c.Id == giocatoreId && c.AdminId == adminId);

            if (calciatoreDaListone == null) return; // Non fa nulla se il giocatore non è dell'admin

            var squadra = await _context.Squadre.FindAsync(squadraId);
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

            if (_bazzerService.BloccoPortieriAttivo && calciatoreDaListone.Ruolo == "P")
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
                                p.AdminId == adminId) // Aggiunto controllo di sicurezza anche qui
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
            await _hubContext.Clients.All.SendAsync("AggiornaUtente");
        }
    }
}