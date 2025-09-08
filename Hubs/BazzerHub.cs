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

        private static readonly object _finishLock = new();

        public BazzerHub(BazzerService bazzerService, AppDbContext context, LegaService legaService)
        {
            _bazzerService = bazzerService;
            _context = context;
            _legaService = legaService;
        }

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
        // === Throttle (mezzo secondo) ===============================================
        private static readonly ConcurrentDictionary<string, DateTime> _buzzCooldownUntil
            = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, DateTime> _bidCooldownUntil
            = new(StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentDictionary<string, object> _legaLocks
            = new(StringComparer.OrdinalIgnoreCase);

        private static readonly TimeSpan BuzzCooldown = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan BidCooldown = TimeSpan.FromMilliseconds(500);

        private static object GetLegaLock(string legaLower)
            => _legaLocks.GetOrAdd(legaLower, _ => new object());

        private static void ResetCooldowns(string legaLower)
        {
            _buzzCooldownUntil.TryRemove(legaLower, out _);
            _bidCooldownUntil.TryRemove(legaLower, out _);
        }

        private bool TryGetCallerLega(out string legaLower)
        {
            if (_connMap.TryGetValue(Context.ConnectionId, out var info))
            {
                legaLower = info.lega;
                return true;
            }
            legaLower = string.Empty;
            return false;
        }

        private List<ParticipantSnapshot> BuildPartecipantiSnapshot(string legaLower)
        {
            var now = DateTime.UtcNow;
            if (_presence.TryGetValue(legaLower, out var dict))
            {
                var pausa = _bazzerService.PausaAttiva(legaLower);
                return dict.Values
                    .OrderBy(p => p.Nick, StringComparer.OrdinalIgnoreCase)
                    .Select(p =>
                    {
                        var online = IsOnline(p, now);
                        var stato = pausa ? "waiting" : (online ? "online" : "offline");
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

        private IEnumerable<string> GetAdminConnectionIds(string legaLower)
        {
            // Trova tutte le connessioni correnti che appartengono alla lega
            // e che sono admin (in base alla tua tabella _presence) e online.
            var now = DateTime.UtcNow;

            if (!_presence.TryGetValue(legaLower, out var dict))
                yield break;

            // Mappa nick -> IsAdmin, Online
            var adminSet = dict
                .Where(kv => kv.Value.IsAdmin && IsOnline(kv.Value, now))
                .Select(kv => kv.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var kv in _connMap)
            {
                var (lega, nick) = kv.Value;
                if (string.Equals(lega, legaLower, StringComparison.OrdinalIgnoreCase) &&
                    adminSet.Contains(nick))
                {
                    yield return kv.Key; // ConnectionId
                }
            }
        }

        private async Task SendBuzzerRichiediImportoSafeAsync(string legaLower, object payload)
        {
            // 1) target principale: gruppo admin
            await Clients.Group(AdminGroup(legaLower)).SendAsync("BuzzerRichiediImporto", payload);

            // 2) fallback: invia anche diretto agli admin online (se il gruppo fosse perso al reconnect)
            var adminConnIds = GetAdminConnectionIds(legaLower).ToList();
            if (adminConnIds.Count > 0)
            {
                await Clients.Clients(adminConnIds).SendAsync("BuzzerRichiediImporto", payload);
            }
        }


        public async Task Buzz(string lega, string offerente)
        {
            var alias = NormalizeLega(lega);

            if (_bazzerService.PausaAttiva(alias)) return;
            // niente buzz se non c'è un giocatore in asta
            if (_bazzerService.GetGiocatoreInAsta(alias) == null) return;

            var now = DateTime.UtcNow;

            string winnerOfferente = null;
            string fineIso = null;

            lock (GetLegaLock(alias))
            {
                // throttle BUZZ: accetta solo il primo nei 500ms
                if (_buzzCooldownUntil.TryGetValue(alias, out var until) && now < until)
                {
                    var rem = (int)Math.Max(0, (until - now).TotalMilliseconds);
                    _ = Clients.Caller.SendAsync("BuzzRateLimited", rem);
                    return;
                }

                // ricontrollo stato in lock
                var (attualeOfferente, _) = _bazzerService.GetOffertaAttuale(alias);
                var fine = _bazzerService.GetFineOffertaUtc(alias);
                bool timerAttivo = fine.HasValue && now < fine.Value;
                bool sameOfferente =
                    !string.IsNullOrWhiteSpace(attualeOfferente) &&
                    attualeOfferente != "-" &&
                    string.Equals(attualeOfferente.Trim(), (offerente ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

                if (timerAttivo && sameOfferente) return;

                _bazzerService.RegistraBuzz(alias, offerente, now);
                _buzzCooldownUntil[alias] = now + BuzzCooldown;

                var f = _bazzerService.GetFineOffertaUtc(alias);
                winnerOfferente = offerente;
                fineIso = f?.ToUniversalTime().ToString("o");
            }

            await Clients.Group(alias).SendAsync("Buzz", winnerOfferente, fineIso, DateTime.UtcNow.ToString("o"));

        }





        public async Task RegistratiAllaLega(string legaAlias, string nickname, bool isAdmin)
        {
            var legaLower = NormalizeLega(legaAlias);
            nickname ??= "-";

            await Groups.AddToGroupAsync(Context.ConnectionId, legaLower);
            if (isAdmin) await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup(legaLower));

            var dict = _presence.GetOrAdd(legaLower, _ => new ConcurrentDictionary<string, Partecipante>(StringComparer.OrdinalIgnoreCase));
            dict.AddOrUpdate(nickname,
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
            if (!TryGetCallerLega(out var legaLower) || string.IsNullOrEmpty(legaLower)) return;
            if (_bazzerService.PausaAttiva(legaLower)) return;

            // in modalità Buzzer non si accettano offerte numeriche
            if (_bazzerService.IsBuzzerModeAttivo(legaLower)) return;

            // niente offerte se non c'è un giocatore in asta
            if (_bazzerService.GetGiocatoreInAsta(legaLower) == null) return;

            string offerenteOk = null;
            int offertaOk = 0;
            string fineIso = null;

            var now = DateTime.UtcNow;

            lock (GetLegaLock(legaLower))
            {
                // throttle OFFERTA: accetta solo la prima nei 500ms
                if (_bidCooldownUntil.TryGetValue(legaLower, out var until) && now < until)
                {
                    var rem = (int)Math.Max(0, (until - now).TotalMilliseconds);
                    _ = Clients.Caller.SendAsync("BidRateLimited", rem);
                    return;
                }

                var (_, attOfferta) = _bazzerService.GetOffertaAttuale(legaLower);
                if (baseOfferta.HasValue && baseOfferta.Value != attOfferta) return;
                if (offerta <= attOfferta) return;

                _bazzerService.AggiornaOfferta(legaLower, offerente, offerta);

                _bidCooldownUntil[legaLower] = now + BidCooldown;
                offerenteOk = offerente;
                offertaOk = offerta;

                var f = _bazzerService.GetFineOffertaUtc(legaLower);
                fineIso = f?.ToUniversalTime().ToString("o");
            }

            await Clients.Group(legaLower).SendAsync("AggiornaOfferta", offerenteOk, offertaOk, fineIso, DateTime.UtcNow.ToString("o"));

        }



        public async Task TerminaAsta(string legaAlias)
        {
            var legaLower = NormalizeLega(legaAlias);
            try
            {
                var now = DateTime.UtcNow;

                if (_bazzerService.PausaAttiva(legaLower)) return;
                var fine = _bazzerService.GetFineOffertaUtc(legaLower);
                if (fine.HasValue && now < fine.Value) return;

                lock (_finishLock)
                {
                    if (_bazzerService.AstaConclusa(legaLower)) return;
                    _bazzerService.SegnaAstaConclusa(legaLower);
                }

                var giocatoreInAsta = _bazzerService.GetGiocatoreInAsta(legaLower);
                var (offerente, offerta) = _bazzerService.GetOffertaAttuale(legaLower);

                if (_bazzerService.IsBuzzerModeAttivo(legaLower))
                {
                    if (giocatoreInAsta == null)
                    {
                        _bazzerService.AnnullaAstaCorrente(legaLower);
                        ResetCooldowns(legaLower);
                        return;
                    }

                    int? squadraId = null;
                    if (!string.IsNullOrWhiteSpace(offerente) && offerente != "-")
                    {
                        var squadraBuzzer = await _context.Squadre
                            .Include(s => s.Lega)
                            .FirstOrDefaultAsync(s => s.Nickname == offerente && s.Lega.Alias.ToLower() == legaLower);
                        squadraId = squadraBuzzer?.Id;
                    }

                    await Clients.Group(legaLower).SendAsync(
                        "AstaTerminata",
                        giocatoreInAsta.Id,
                        giocatoreInAsta.Nome,
                        string.IsNullOrWhiteSpace(offerente) ? "-" : offerente,
                        0
                    );

                    await SendBuzzerRichiediImportoSafeAsync(legaLower, new
                    {
                        giocatoreId = giocatoreInAsta.Id,
                        nomeGiocatore = giocatoreInAsta.Nome,
                        offerente = string.IsNullOrWhiteSpace(offerente) ? "-" : offerente,
                        squadraId = squadraId
                    });


                    _bazzerService.AnnullaAstaCorrente(legaLower);
                    ResetCooldowns(legaLower);
                    return;
                }

                if (giocatoreInAsta == null || offerente == "-" || offerta <= 0) return;

                var squadraVincitrice = await _context.Squadre
                    .Include(s => s.Lega)
                    .FirstOrDefaultAsync(s => s.Nickname == offerente && s.Lega.Alias.ToLower() == legaLower);
                if (squadraVincitrice == null) return;

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

                if (_bazzerService.IsBloccoPortieriAttivo(legaLower) && giocatoreInAsta.Ruolo == "P")
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

                    foreach (var portiere in altriPortieri.GroupBy(p => p.IdListone).Select(g => g.First()))
                    {
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

                await Clients.Group(legaLower).SendAsync("AstaTerminata", giocatoreInAsta.Id, giocatoreInAsta.Nome, offerente, offerta);
                await _legaService.BroadcastAggiornamentiLegaAsync(squadraVincitrice.LegaId);

                _bazzerService.AnnullaAstaCorrente(legaLower);
                ResetCooldowns(legaLower);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Eccezione in TerminaAsta: {ex.Message}");
                throw;
            }
        }

        public async Task RichiediStatoAttuale()
        {
            if (!TryGetCallerLega(out var legaLower) || string.IsNullOrEmpty(legaLower))
                return;

            var giocatoreInAsta = _bazzerService.GetGiocatoreInAsta(legaLower);
            var (offerente, offerta) = _bazzerService.GetOffertaAttuale(legaLower);

            if (giocatoreInAsta != null)
            {
                await Clients.Caller.SendAsync("MostraGiocatoreInAsta", new
                {
                    id = giocatoreInAsta.Id,
                    nome = giocatoreInAsta.Nome,
                    ruolo = _bazzerService.IsMantraAttivo(legaLower) ? giocatoreInAsta.RuoloMantra : giocatoreInAsta.Ruolo,
                    squadraReale = giocatoreInAsta.Squadra,
                    logoUrl = AstaLegheFC.Helpers.LogoHelper.GetLogoUrl(giocatoreInAsta.Squadra)
                });
            }

            var fineUtc = _bazzerService.GetFineOffertaUtc(legaLower)?.ToUniversalTime().ToString("o");
            await Clients.Caller.SendAsync("AggiornaOfferta", offerente, offerta, fineUtc, DateTime.UtcNow.ToString("o"));


            var st = _bazzerService.GetStato(legaLower);
            var buzzerAttivo = _bazzerService.IsBuzzerModeAttivo(legaLower);
            var mantraAttivo = _bazzerService.IsMantraAttivo(legaLower);
            await Clients.Caller.SendAsync("StatoAsta", new
            {
                serverNowUtc = DateTime.UtcNow.ToString("o"),
                startUtc = st.astaStartUtc?.ToUniversalTime().ToString("o"),
                pausaAccumulataSec = (int)st.pausaAccumulata.TotalSeconds,
                pausaAttiva = st.pausaAttiva,
                fineUtc = st.fineOffertaUtc?.ToUniversalTime().ToString("o"),
                mantraAttivo,
                buzzerAttivo
            });

            if (_bazzerService.PausaAttiva(legaLower))
            {
                var elapsed = (int)Math.Max(0, Math.Floor(_bazzerService.GetDurataAsta(legaLower, DateTime.UtcNow).TotalSeconds));
                var remaining = _bazzerService.GetRemainingOfferta(legaLower, DateTime.UtcNow);
                await Clients.Caller.SendAsync("AstaPausa", new { elapsedSec = elapsed, remainingSec = remaining });
            }
        }

        public async Task PausaAsta(string legaAlias)
        {
            var legaLower = NormalizeLega(legaAlias);
            _bazzerService.MettiInPausa(legaLower, DateTime.UtcNow);

            var elapsed = (int)Math.Max(0, Math.Floor(_bazzerService.GetDurataAsta(legaLower, DateTime.UtcNow).TotalSeconds));
            var remaining = _bazzerService.GetRemainingOfferta(legaLower, DateTime.UtcNow);
            var partecipanti = BuildPartecipantiSnapshot(legaLower);

            await Clients.Group(legaLower).SendAsync("AstaPausa", new { elapsedSec = elapsed, remainingSec = remaining, partecipanti });
            await Clients.Group(AdminGroup(legaLower)).SendAsync("MostraMessaggio", "⏸️ Asta in pausa", "Messaggio inviato ai partecipanti.");
            await BroadcastStatoPartecipantiAsync(legaLower);
        }

        public async Task RiprendiAsta(string legaAlias)
        {
            var legaLower = NormalizeLega(legaAlias);

            _bazzerService.Riprendi(legaLower, DateTime.UtcNow);

            var fineUtc = _bazzerService.GetFineOffertaUtc(legaLower)?.ToUniversalTime().ToString("o");
            var partecipanti = BuildPartecipantiSnapshot(legaLower);

            await Clients.Group(legaLower).SendAsync("AstaRipresa", new { fineUtc, serverNowUtc = DateTime.UtcNow.ToString("o"), partecipanti });
            await Clients.Group(AdminGroup(legaLower)).SendAsync("MostraMessaggio", "▶️ Asta ripresa", "È di nuovo possibile fare offerte.");
            await BroadcastStatoPartecipantiAsync(legaLower);
        }

        public async Task AnnullaAsta(string legaAlias = "")
        {
            var legaLower = string.IsNullOrWhiteSpace(legaAlias)
                ? (TryGetCallerLega(out var l) ? l : "")
                : NormalizeLega(legaAlias);
            if (string.IsNullOrEmpty(legaLower)) return;

            _bazzerService.AnnullaAstaCorrente(legaLower);

            // Notifica standard
            await Clients.Group(legaLower).SendAsync("AstaAnnullata");
            // Resetta immediatamente stato visuale/timer lato client
            await Clients.Group(legaLower).SendAsync("AggiornaOfferta", "-", 0, null);
            // 🔒 Hard-reset buzzer: i client ignorano qualsiasi BUZZ residuo
            await Clients.Group(legaLower).SendAsync("BuzzerHardReset");
            ResetCooldowns(legaLower);
        }



        public async Task ResetDurataAsta(string legaAlias)
        {
            var legaLower = NormalizeLega(legaAlias);
            await Clients.Group(AdminGroup(legaLower)).SendAsync("DurataResettata");
        }
    }
}