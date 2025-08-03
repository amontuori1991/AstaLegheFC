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
            var giocatore = await _context.Giocatori
                .Include(g => g.Squadra)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (giocatore != null && giocatore.Squadra != null)
            {
                // Calcola la "penale" (o il "malus") dello svincolo
                int costoOriginale = giocatore.CreditiSpesi ?? 0;
                int malus = costoOriginale - creditiDaRestituire;

                // Applica il malus direttamente ai crediti iniziali della squadra.
                // In questo modo, quando la vista ricalcolerà i crediti disponibili,
                // il rimborso risulterà corretto.
                giocatore.Squadra.Crediti -= malus;

                // Rimuovi il giocatore dalla rosa
                _context.Giocatori.Remove(giocatore);

                await _context.SaveChangesAsync();

                // Notifica tutti gli utenti dell'aggiornamento
                await _hubContext.Clients.All.SendAsync("AggiornaUtente");
            }
        }

        public async Task AssegnaGiocatoreManualmenteAsync(int giocatoreId, int squadraId, int costo)
        {
            var calciatoreDaListone = await _context.ListoneCalciatori.FindAsync(giocatoreId);
            if (calciatoreDaListone == null) return;

            var squadra = await _context.Squadre.FindAsync(squadraId);
            if (squadra == null) return;

            var nuovoGiocatore = new Giocatore
            {
                IdListone = calciatoreDaListone.IdListone,
                Nome = calciatoreDaListone.Nome,
                Ruolo = calciatoreDaListone.Ruolo,
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
                                !idGiocatoriAcquistatiLega.Contains(p.IdListone))
                    .ToListAsync();

                foreach (var portiere in altriPortieri)
                {
                    var portiereCollegato = new Giocatore
                    {
                        IdListone = portiere.IdListone,
                        Nome = portiere.Nome,
                        Ruolo = portiere.Ruolo,
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