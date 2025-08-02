using AstaLegheFC.Data;
using AstaLegheFC.Models;
using AstaLegheFC.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace AstaLegheFC.Hubs
{
    public class BazzerHub : Hub
    {
        private readonly BazzerService _bazzerService;
        private readonly AppDbContext _context;

        public BazzerHub(BazzerService bazzerService, AppDbContext context)
        {
            _bazzerService = bazzerService;
            _context = context;
        }

        public async Task InviaOfferta(string offerente, int offerta)
        {
            // ✅ CONTROLLO DI SICUREZZA SUL SERVER
            var (offerenteAttuale, offertaAttuale) = _bazzerService.GetOffertaAttuale();

            // Se l'offerta ricevuta non è valida (inferiore o uguale a quella attuale),
            // il server la ignora e non fa nulla.
            if (offerta <= offertaAttuale)
            {
                return;
            }

            _bazzerService.AggiornaOfferta(offerente, offerta);
            await Clients.All.SendAsync("AggiornaOfferta", offerente, offerta);
        }

        public async Task TerminaAsta(string legaAlias)
        {
            try
            {
                var giocatoreInAsta = _bazzerService.GetGiocatoreInAsta();
                var (offerente, offerta) = _bazzerService.GetOffertaAttuale();

                if (_bazzerService.AstaConclusa() || giocatoreInAsta == null || offerente == "-" || offerta <= 0 || string.IsNullOrEmpty(legaAlias))
                    return;

                var squadraVincitrice = await _context.Squadre
                    .Include(s => s.Lega)
                    .FirstOrDefaultAsync(s => s.Nickname == offerente && s.Lega.Alias.ToLower() == legaAlias.ToLower());

                if (squadraVincitrice != null)
                {
                    var nuovoGiocatore = new Giocatore
                    {
                        Nome = giocatoreInAsta.Nome,
                        SquadraReale = giocatoreInAsta.Squadra,
                        Ruolo = giocatoreInAsta.Ruolo,
                        SquadraId = squadraVincitrice.Id,
                        IdListone = giocatoreInAsta.IdListone,
                        CreditiSpesi = offerta
                    };
                    _context.Giocatori.Add(nuovoGiocatore);

                    if (_bazzerService.BloccoPortieriAttivo && giocatoreInAsta.Ruolo == "P")
                    {
                        var idGiocatoriAcquistatiLega = await _context.Giocatori
                            .Where(g => g.Squadra.LegaId == squadraVincitrice.LegaId)
                            .Select(g => g.IdListone)
                            .ToListAsync();

                        var altriPortieri = await _context.ListoneCalciatori
                            .Where(p => p.Squadra == giocatoreInAsta.Squadra &&
                                        p.Ruolo == "P" &&
                                        p.Id != giocatoreInAsta.Id &&
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
                                SquadraId = squadraVincitrice.Id,
                                CreditiSpesi = 0
                            };
                            _context.Giocatori.Add(portiereCollegato);
                        }
                    }

                    await _context.SaveChangesAsync();
                    _bazzerService.SegnaAstaConclusa();

                    await Clients.All.SendAsync("AstaTerminata", giocatoreInAsta.Id, giocatoreInAsta.Nome, offerente, offerta);
                    await Clients.Group(legaAlias.ToLower()).SendAsync("AggiornaUtente");
                    await Clients.Group($"admin_{legaAlias.ToLower()}").SendAsync("AggiornaAdmin");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Eccezione in TerminaAsta: {ex.Message}");
                throw;
            }
        }

        public async Task RichiediStatoAttuale()
        {
            var giocatoreInAsta = _bazzerService.GetGiocatoreInAsta();
            var (offerente, offerta) = _bazzerService.GetOffertaAttuale();
            if (giocatoreInAsta != null)
            {
                await Clients.Caller.SendAsync("MostraGiocatoreInAsta", new
                {
                    id = giocatoreInAsta.Id,
                    nome = giocatoreInAsta.Nome,
                    ruolo = giocatoreInAsta.Ruolo,
                    squadraReale = giocatoreInAsta.Squadra
                });
            }
            await Clients.Caller.SendAsync("AggiornaOfferta", offerente, offerta);
        }
    }
}