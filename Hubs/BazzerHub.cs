using AstaLegheFC.Data;
using AstaLegheFC.Models;
using AstaLegheFC.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

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

        public async Task AggiungiAdminAlGruppo(string legaAlias)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"admin_{legaAlias}");
        }

        public async Task SuggerisciGiocatore(int giocatoreId, string suggeritore, string legaAlias)
        {
            var giocatore = await _context.ListoneCalciatori.FindAsync(giocatoreId);
            if (giocatore != null)
            {
                await Clients.Group($"admin_{legaAlias}").SendAsync("GiocatoreSuggerito", giocatore, suggeritore);
            }
        }

        // ✅ ora riceve anche expectedPrevOfferta per la CAS
        public async Task InviaOfferta(string offerente, int offerta, int expectedPrevOfferta)
        {
            // tenta di accettare atomicamente l'offerta
            if (_bazzerService.TryAggiornaOffertaCAS(offerente, offerta, expectedPrevOfferta, out var fineUtc))
            {
                // broadcast con fineUtc per sincronizzare i timer
                await Clients.All.SendAsync("AggiornaOfferta", offerente, offerta, fineUtc.ToString("o"));
            }
            else
            {
                // rifiutata: rimanda al chiamante lo stato attuale (così vede il prezzo aggiornato)
                var (offerenteAttuale, offertaAttuale) = _bazzerService.GetOffertaAttuale();
                var fine = _bazzerService.GetFineUtc();
                await Clients.Caller.SendAsync("AggiornaOfferta", offerenteAttuale, offertaAttuale, fine?.ToString("o"));
            }
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
                        RuoloMantra = giocatoreInAsta.RuoloMantra,
                        SquadraId = squadraVincitrice.Id,
                        IdListone = giocatoreInAsta.IdListone,
                        CreditiSpesi = offerta
                    };
                    _context.Giocatori.Add(nuovoGiocatore);

                    if (_bazzerService.BloccoPortieriAttivo && giocatoreInAsta.Ruolo == "P")
                    {
                        var idGiocatoriAcquistatiLega = await _context.Giocatori
                            .Where(g => g.Squadra.LegaId == squadraVincitrice.Lega.Id)
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

                    // 👇 fondamentale per non “riproporre” l’asta al refresh/sync
                    _bazzerService.AnnullaAstaCorrente();
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
            var fine = _bazzerService.GetFineUtc();

            if (giocatoreInAsta != null)
            {
                await Clients.Caller.SendAsync("MostraGiocatoreInAsta", new
                {
                    id = giocatoreInAsta.Id,
                    nome = giocatoreInAsta.Nome,
                    ruolo = giocatoreInAsta.Ruolo,
                    squadraReale = giocatoreInAsta.Squadra,
                    logoUrl = AstaLegheFC.Helpers.LogoHelper.GetLogoUrl(giocatoreInAsta.Squadra)
                });
            }

            // include sempre fineUtc per riallineare i timer lato client
            await Clients.Caller.SendAsync("AggiornaOfferta", offerente, offerta, fine?.ToString("o"));
        }
    }
}
