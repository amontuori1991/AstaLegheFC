using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        private readonly LegaService _legaService;

        // 🔒 lock per prevenire doppie esecuzioni concorrenti di TerminaAsta
        private static readonly object _finishLock = new();

        public BazzerHub(BazzerService bazzerService, AppDbContext context, LegaService legaService)
        {
            _bazzerService = bazzerService;
            _context = context;
            _legaService = legaService;
        }

        // ========= Presence & stato partecipanti =========

        private class Partecipante
        {
            public string Nick { get; set; } = "-";
            public bool IsAdmin { get; set; }
            public DateTime LastSeenUtc { get; set; } = DateTime.MinValue;
            public bool ReadyPreAsta { get; set; }
            public bool ReadyRipresa { get; set; }
        }

        private class ParticipantSnapshot
        {
            public string Nick { get; set; } = "-";
            public bool Online { get; set; }
            public string Stato { get; set; } = "offline";
            public bool ReadyPreAsta { get; set; }
            public bool ReadyRipresa { get; set; }
            public bool IsAdmin { get; set; }
        }

        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Partecipante>> _presence
            = new(StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentDictionary<string, (string lega, string nick)> _connMap
            = new();

        private static string NormalizeLega(string lega) => (lega ?? "").Trim().ToLowerInvariant();
        private static string AdminGroup(string legaLower) => $"admin_{legaLower}";
        private static bool IsOnline(Partecipante p, DateTime nowUtc) => (nowUtc - p.LastSeenUtc) <= TimeSpan.FromSeconds(45);

        private List<ParticipantSnapshot> BuildPartecipantiSnapshot(string legaLower)
        {
            var now = DateTime.UtcNow;

            if (_presence.TryGetValue(legaLower, out var dict))
            {
                return dict.Values
                    .OrderBy(p => p.Nick, StringComparer.OrdinalIgnoreCase)
                    .Select(p =>
                    {
                        var online = IsOnline(p, now);
                        var stato = _bazzerService.PausaAttiva ? "waiting" : (online ? "online" : "offline");
                        return new ParticipantSnapshot
                        {
                            Nick = p.Nick,
                            Online = online,
                            Stato = stato,
                            ReadyPreAsta = p.ReadyPreAsta,
                            ReadyRipresa = p.ReadyRipresa,
                            IsAdmin = p.IsAdmin
                        };
                    })
                    .ToList();
            }
            return new List<ParticipantSnapshot>();
        }

        private async Task BroadcastStatoPartecipantiAsync(string legaLower)
        {
            var snapshot = BuildPartecipantiSnapshot(legaLower);
            await Clients.Group(legaLower).SendAsync("StatoPartecipanti", new { partecipanti = snapshot });
            await Clients.Group(AdminGroup(legaLower)).SendAsync("StatoPartecipanti", new { partecipanti = snapshot });
        }

        public async Task RegistratiAllaLega(string legaAlias, string nickname, bool isAdmin)
        {
            var legaLower = NormalizeLega(legaAlias);
            nickname ??= "-";

            await Groups.AddToGroupAsync(Context.ConnectionId, legaLower);
            if (isAdmin) await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup(legaLower));

            var dict = _presence.GetOrAdd(legaLower, _ => new ConcurrentDictionary<string, Partecipante>(StringComparer.OrdinalIgnoreCase));
            var p = dict.AddOrUpdate(nickname,
                _ => new Partecipante { Nick = nickname, IsAdmin = isAdmin, LastSeenUtc = DateTime.UtcNow },
                (_, old) => { old.IsAdmin = isAdmin || old.IsAdmin; old.LastSeenUtc = DateTime.UtcNow; return old; });

            _connMap[Context.ConnectionId] = (legaLower, nickname);
            await BroadcastStatoPartecipantiAsync(legaLower);
        }

        public async Task Ping(string legaAlias, string nickname)
        {
            var legaLower = NormalizeLega(legaAlias);
            if (_presence.TryGetValue(legaLower, out var dict) && dict.TryGetValue(nickname ?? "-", out var p))
            {
                p.LastSeenUtc = DateTime.UtcNow;
                await BroadcastStatoPartecipantiAsync(legaLower);
            }
        }

        public async Task SegnalaPronto(string legaAlias, string nickname, string tipo)
        {
            var legaLower = NormalizeLega(legaAlias);
            if (_presence.TryGetValue(legaLower, out var dict) && dict.TryGetValue(nickname ?? "-", out var p))
            {
                if (string.Equals(tipo, "pre-asta", StringComparison.OrdinalIgnoreCase)) p.ReadyPreAsta = true;
                else if (string.Equals(tipo, "pre-ripresa", StringComparison.OrdinalIgnoreCase)) p.ReadyRipresa = true;

                await BroadcastStatoPartecipantiAsync(legaLower);
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (_connMap.TryRemove(Context.ConnectionId, out var info))
            {
                var (legaLower, nick) = info;
                if (_presence.TryGetValue(legaLower, out var dict) && dict.TryGetValue(nick, out var p))
                {
                    p.LastSeenUtc = DateTime.UtcNow.AddMinutes(-10);
                    await BroadcastStatoPartecipantiAsync(legaLower);
                }
            }
            await base.OnDisconnectedAsync(exception);
        }

        // ========= Funzioni di bazzer =========

        public async Task AggiungiAdminAlGruppo(string legaAlias)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup(NormalizeLega(legaAlias)));
        }

        public async Task SuggerisciGiocatore(int giocatoreId, string suggeritore, string legaAlias)
        {
            var giocatore = await _context.ListoneCalciatori.FindAsync(giocatoreId);
            if (giocatore != null)
                await Clients.Group(AdminGroup(NormalizeLega(legaAlias))).SendAsync("GiocatoreSuggerito", giocatore, suggeritore);
        }

        public async Task InviaOfferta(string offerente, int offerta, int? baseOfferta = null)
        {
            if (_bazzerService.PausaAttiva) return;

            var (_, attOfferta) = _bazzerService.GetOffertaAttuale();
            if (baseOfferta.HasValue && baseOfferta.Value != attOfferta) return;
            if (offerta <= attOfferta) return;

            _bazzerService.AggiornaOfferta(offerente, offerta);

            var fineUtc = _bazzerService.FineOffertaUtc?.ToUniversalTime().ToString("o");
            await Clients.All.SendAsync("AggiornaOfferta", offerente, offerta, fineUtc);
        }

        public async Task TerminaAsta(string legaAlias)
        {
            try
            {
                var now = DateTime.UtcNow;

                if (_bazzerService.PausaAttiva) return;
                if (_bazzerService.FineOffertaUtc.HasValue && now < _bazzerService.FineOffertaUtc.Value) return;

                // ========== SEZIONE CRITICA ==========
                lock (_finishLock)
                {
                    if (_bazzerService.AstaConclusa()) return; // già processata
                    _bazzerService.SegnaAstaConclusa();        // prenota l’esecuzione
                }
                // =====================================

                var giocatoreInAsta = _bazzerService.GetGiocatoreInAsta();
                var (offerente, offerta) = _bazzerService.GetOffertaAttuale();

                if (giocatoreInAsta == null || offerente == "-" || offerta <= 0 || string.IsNullOrEmpty(legaAlias))
                    return;

                var squadraVincitrice = await _context.Squadre
                    .Include(s => s.Lega)
                    .FirstOrDefaultAsync(s => s.Nickname == offerente && s.Lega.Alias.ToLower() == legaAlias.ToLower());

                if (squadraVincitrice == null) return;

                // Winner (idempotente)
                bool winnerGiaAssegnato = await _context.Giocatori
                    .AnyAsync(g => g.SquadraId == squadraVincitrice.Id && g.IdListone == giocatoreInAsta.IdListone);
                if (!winnerGiaAssegnato)
                {
                    _context.Giocatori.Add(new Giocatore
                    {
                        Nome = giocatoreInAsta.Nome,
                        SquadraReale = giocatoreInAsta.Squadra,
                        Ruolo = giocatoreInAsta.Ruolo,
                        RuoloMantra = giocatoreInAsta.RuoloMantra,
                        SquadraId = squadraVincitrice.Id,
                        IdListone = giocatoreInAsta.IdListone,
                        CreditiSpesi = offerta
                    });
                }

                // Blocco portieri: aggiungi SOLO i compagni di squadra (idempotente + distinct)
                if (_bazzerService.BloccoPortieriAttivo && giocatoreInAsta.Ruolo == "P")
                {
                    var idAssegnatiLega = await _context.Giocatori
                        .Where(g => g.Squadra.LegaId == squadraVincitrice.LegaId)
                        .Select(g => g.IdListone)
                        .ToListAsync();
                    var assegnatiSet = new HashSet<int>(idAssegnatiLega);

                    var altriPortieri = await _context.ListoneCalciatori
                        .AsNoTracking()
                        .Where(p => p.Ruolo == "P"
                                    && p.Squadra == giocatoreInAsta.Squadra
                                    && p.IdListone != giocatoreInAsta.IdListone
                                    && !assegnatiSet.Contains(p.IdListone))
                        .ToListAsync();

                    foreach (var portiere in altriPortieri
                        .GroupBy(p => p.IdListone).Select(g => g.First())) // distinct by IdListone
                    {
                        // ulteriore check idempotente per la SQUADRA vincitrice
                        bool giaAssegnato = await _context.Giocatori
                            .AnyAsync(g => g.SquadraId == squadraVincitrice.Id && g.IdListone == portiere.IdListone);
                        if (giaAssegnato) continue;

                        _context.Giocatori.Add(new Giocatore
                        {
                            IdListone = portiere.IdListone,
                            Nome = portiere.Nome,
                            Ruolo = portiere.Ruolo,
                            RuoloMantra = portiere.RuoloMantra,
                            SquadraReale = portiere.Squadra,
                            SquadraId = squadraVincitrice.Id,
                            CreditiSpesi = 0
                        });
                    }
                }

                await _context.SaveChangesAsync();

                await Clients.All.SendAsync("AstaTerminata", giocatoreInAsta.Id, giocatoreInAsta.Nome, offerente, offerta);
                await _legaService.BroadcastAggiornamentiLegaAsync(squadraVincitrice.LegaId);

                _bazzerService.AnnullaAstaCorrente();
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
                    ruolo = _bazzerService.MantraAttivo ? giocatoreInAsta.RuoloMantra : giocatoreInAsta.Ruolo,
                    squadraReale = giocatoreInAsta.Squadra,
                    logoUrl = AstaLegheFC.Helpers.LogoHelper.GetLogoUrl(giocatoreInAsta.Squadra)
                });
            }

            var fineUtc = _bazzerService.FineOffertaUtc?.ToUniversalTime().ToString("o");
            await Clients.Caller.SendAsync("AggiornaOfferta", offerente, offerta, fineUtc);
            var stato = _bazzerService.GetStato();
            await Clients.Caller.SendAsync("StatoAsta", new
            {
                startUtc = stato.astaStartUtc?.ToUniversalTime().ToString("o"),
                pausaAccumulataSec = (int)stato.pausaAccumulata.TotalSeconds,
                pausaAttiva = stato.pausaAttiva
            });

            if (_bazzerService.PausaAttiva)
            {
                var elapsed = (int)Math.Max(0, Math.Floor(_bazzerService.GetDurataAsta(DateTime.UtcNow).TotalSeconds));
                var remaining = _bazzerService.GetRemainingOfferta(DateTime.UtcNow);
                await Clients.Caller.SendAsync("AstaPausa", new
                {
                    elapsedSec = elapsed,
                    remainingSec = remaining
                });
            }
        }

        public async Task PausaAsta(string legaAlias)
        {
            var legaLower = NormalizeLega(legaAlias);
            _bazzerService.MettiInPausa(DateTime.UtcNow);

            var elapsed = (int)Math.Max(0, Math.Floor(_bazzerService.GetDurataAsta(DateTime.UtcNow).TotalSeconds));
            var remaining = _bazzerService.GetRemainingOfferta(DateTime.UtcNow);
            var partecipanti = BuildPartecipantiSnapshot(legaLower);

            await Clients.All.SendAsync("AstaPausa", new
            {
                elapsedSec = elapsed,
                remainingSec = remaining,
                partecipanti
            });

            await BroadcastStatoPartecipantiAsync(legaLower);
        }

        public async Task RiprendiAsta(string legaAlias)
        {
            var legaLower = NormalizeLega(legaAlias);
            _bazzerService.Riprendi(DateTime.UtcNow);

            var fineUtc = _bazzerService.FineOffertaUtc?.ToUniversalTime().ToString("o");
            var partecipanti = BuildPartecipantiSnapshot(legaLower);

            await Clients.All.SendAsync("AstaRipresa", new
            {
                fineUtc,
                partecipanti
            });

            await BroadcastStatoPartecipantiAsync(legaLower);
        }

        public async Task ResetDurataAsta(string legaAlias)
        {
            await Clients.Group($"admin_{legaAlias?.Trim().ToLower()}").SendAsync("DurataResettata");
        }
    }
}
