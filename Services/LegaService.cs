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

        public async Task SvincolaGiocatoreAsync(int id, int creditiDaRestituire)
        {
            // transazione per consistenza crediti/rosa
            using var tx = await _context.Database.BeginTransactionAsync();

            var giocatore = await _context.Giocatori
                .Include(g => g.Squadra)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (giocatore == null)
            {
                await tx.RollbackAsync();
                return; // Nessun giocatore trovato: esci silenziosamente
            }

            // costo originale: tratta NULL come 0
            int costoOriginale = giocatore.CreditiSpesi ?? 0;

            // clamp rimborso
            if (creditiDaRestituire < 0) creditiDaRestituire = 0;
            if (creditiDaRestituire > costoOriginale) creditiDaRestituire = costoOriginale;

            // malus = quanto NON restituisci
            int malus = costoOriginale - creditiDaRestituire;

            if (giocatore.Squadra != null)
            {
                // aggiorna crediti in base al malus (se rimborso pieno, malus=0; se rimborso 0, malus=costoOriginale)
                giocatore.Squadra.Crediti -= malus;
            }

            // rimuovi dalla rosa
            _context.Giocatori.Remove(giocatore);

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            // broadcast
            await _hubContext.Clients.All.SendAsync("AggiornaUtente");
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