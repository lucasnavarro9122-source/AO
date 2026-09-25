using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

// Retos de la demo (ModRetos.bas + docs/claude/demo/decisiones.md), only with --demo:
// invitations, custody in the ledger (held on accept), rings, countdown with players frozen, best of 3
// with sides swapped every round, time limit decided by score, double death = null round, 30 s grace
// on disconnection, settlement with tax and remainder lines. The server decides all PvP damage.
sealed partial class CoopRoom
{
    sealed class DuelMember
    {
        public string Char = "", Name = "";
        public int Team, Slot, Level, Hp, MaxHp, Mana, MaxMana, X, Y;
        public int BeforeMap, BeforeX, BeforeY, BeforeHp;
        public bool Accepted, Left;
        public long OfflineSince, NextPotion;
    }
    sealed class Duel
    {
        public string Id = "", Offerer = "", OffererName = "", Phase = "invite";
        public long Bet, InviteExpires, PhaseEnds, EndsAt;
        public int MaxPotions = -1, OffererLevel, Round, Score, WinsA, WinsB, Seed, Coin;
        public bool ToldQueued;
        public Arena? Ring;
        public AOArenaLayout? Layout;
        public readonly List<DuelMember> Members = new();
        public IEnumerable<DuelMember> Playing => Members.Where(m => !m.Left);
        public bool Started => Phase is "countdown" or "fight";
    }
    sealed class Arena { public int Sala, Map, X, Y, Theme; public Duel? Busy; }

    const int RedPotion = 38;              // ModRetos: TieneObjetos(38, PocionesMaximas + 1)
    const long MaxBet = 100_000_000;       // APUESTA_MAXIMA
    readonly List<Duel> duels = new();
    readonly List<(int session, AOCoopMessage message)> outbox = new();
    List<Arena>? arenaList;

    JsonNode? Retos => catalog["retos"];
    int RetoInt(string key, int fallback) => Retos?[key] is JsonValue v ? v.GetValue<int>() : fallback;
    long Seconds(string key, int fallback) => (long)(RetoInt(key, fallback) * 1000L * Options.TimeScale);
    List<Arena> Arenas => arenaList ??= (Retos?["arenas"] as JsonArray ?? new JsonArray()).OfType<JsonObject>()
        .Where(a => templates.ContainsKey(Int(a, "map")))
        .Select(a => new Arena { Sala = Int(a, "sala"), Map = Int(a, "map"), X = Int(a, "x"), Y = Int(a, "y"), Theme = Int(a, "theme") }).ToList();
    bool DuelMap(int map) => Retos?["maps"] is JsonArray maps ? maps.Any(m => m?.GetValue<int>() == map) : Arenas.Any(a => a.Map == map);

    Session? Online(string character) => sessions.Values.FirstOrDefault(s => s.CharacterId == character);
    Duel? DuelOf(string character) => duels.FirstOrDefault(d => d.Members.Any(m => m.Char == character && !m.Left));
    static DuelMember? MemberOf(Duel d, string character) => d.Members.FirstOrDefault(m => m.Char == character && !m.Left);
    bool Fighting(Session s, out Duel duel, out DuelMember me)
    {
        duel = DuelOf(s.CharacterId)!; me = duel == null ? null! : MemberOf(duel, s.CharacterId)!;
        return duel != null && duel.Started;
    }

    // Pushes to other sessions go out after the reply (Process/Join/Leave) or with the next tick.
    public (int, AOCoopMessage)[] DrainOutbox() { var copy = outbox.ToArray(); outbox.Clear(); return copy; }
    void Push(Session? s, AOCoopMessage m) { if (s == null) return; m.serverTime = Now; outbox.Add((s.Id, m)); }
    void PushDuel(Duel d, string type, Func<DuelMember, AOCoopDuel>? payload = null, string text = "")
    { foreach (var x in d.Playing) Push(Online(x.Char), new AOCoopMessage { type = type, text = text, duel = payload?.Invoke(x) ?? Describe(d, x) }); }
    void Notice(Duel d, string text) { foreach (var x in d.Playing) Push(Online(x.Char), new AOCoopMessage { type = "duelNotice", text = text }); }
    void Announce(string text) { foreach (var s in sessions.Values) Push(s, new AOCoopMessage { type = "duelAnnounce", text = text }); }

    AOCoopDuel Describe(Duel d, DuelMember? viewer = null)
    {
        var a = d.Ring;
        string[] Names(int team) => d.Playing.Where(m => m.Team == team).Select(m => m.Name).ToArray();
        int[] Ids(int team) => d.Playing.Where(m => m.Team == team).Select(m => Online(m.Char)?.Id ?? 0).ToArray();
        return new AOCoopDuel { id = d.Id, phase = d.Phase, from = d.OffererName, level = d.OffererLevel, teamA = Names(0), teamB = Names(1),
            idsA = Ids(0), idsB = Ids(1), missing = d.Members.Where(m => !m.Accepted).Select(m => m.Name).ToArray(),
            bet = d.Bet, maxPotions = d.MaxPotions, sala = a?.Sala ?? 0, map = a?.Map ?? 0, x = a?.X ?? 0, y = a?.Y ?? 0,
            width = AOArenaGen.W, height = AOArenaGen.H, theme = a?.Theme ?? 0, seed = d.Seed, genVersion = AOArenaGen.GenVersion,
            round = d.Round, score = d.Score, winsA = d.WinsA, winsB = d.WinsB, winner = -1, serverTime = Now, startsAt = d.Phase == "countdown" ? d.PhaseEnds : 0,
            endsAt = d.EndsAt, expiresAt = d.InviteExpires, team = viewer?.Team ?? 0, hp = viewer?.Hp ?? 0, mana = viewer?.Mana ?? 0 };
    }
    AOCoopDuel RingState(Arena a) => a.Busy != null ? Describe(a.Busy)
        : new AOCoopDuel { phase = "libre", sala = a.Sala, map = a.Map, x = a.X, y = a.Y, width = AOArenaGen.W, height = AOArenaGen.H,
                           theme = a.Theme, genVersion = AOArenaGen.GenVersion, winner = -1, serverTime = Now };
    void PushRing(Arena a)
    { var state = RingState(a); foreach (var s in sessions.Values.Where(s => s.State.map == a.Map)) Push(s, new AOCoopMessage { type = "duelRingState", duel = state }); }
    void PushRingsFor(Session s)
    { foreach (var a in Arenas.Where(a => a.Map == s.State.map)) Push(s, new AOCoopMessage { type = "duelRingState", duel = RingState(a) }); }

    static string TeamNames(Duel d, int team)
    {
        var names = d.Members.Where(x => x.Team == team).Select(x => x.Name).ToList();
        return names.Count <= 1 ? string.Concat(names) : string.Join(", ", names.Take(names.Count - 1)) + " y " + names[^1];
    }

    // ModRetos.PuedeReto: not in another duel, alive, in a demo hub/arena map.
    void CanDuel(Session s, bool self, Duel? except = null)
    {
        string who = self ? "" : s.State.name + " ";
        var other = DuelOf(s.CharacterId);
        if (other != null && other != except) throw new InvalidOperationException(self ? "Ya estás en un reto." : who + "ya está en un reto.");
        if (s.State.dead) throw new InvalidOperationException(self ? "No podés retar muerto." : who + "está muerto.");
        if (!DuelMap(s.State.map)) throw new InvalidOperationException(self ? "Los retos se arman en el hub o en la zona de arenas de la demo."
                                                                            : who + "no está en el hub ni en la zona de arenas.");
    }
    void CheckPotions(Session s, int max, bool self)
    {
        if (max < 0) return;
        var inv = JsonNode.Parse(s.Record.snapshot)!["inventory"]!;
        if (CountItem(inv, RedPotion) > max)
            throw new InvalidOperationException((self ? "Tenés" : s.State.name + " tiene") + $" demasiadas pociones rojas (máximo {max}).");
    }
    void Escrow(Duel d, Session s)
    { if (d.Bet > 0) ledger.Transfer($"escrow:{d.Id}:{s.CharacterId}", Ledger.Wallet(s.CharacterId), Ledger.Escrow(d.Id), d.Bet, "apuesta", d.Id); }
    void Refund(Duel d, string reason)
    {
        foreach (var x in d.Members)
            if (ledger.Has($"escrow:{d.Id}:{x.Char}"))
                ledger.Transfer($"refund:{d.Id}:{x.Char}", Ledger.Escrow(d.Id), Ledger.Wallet(x.Char), d.Bet, reason, d.Id);
    }

    // text = "rival;compañero;rival…" (frmRetos order, without the challenger); gold = bet; item = max red potions (-1 = no limit).
    void DuelChallenge(Session s, AOCoopMessage m)
    {
        if (!Options.Demo || Arenas.Count == 0) throw new InvalidOperationException("Los retos solo existen en la demo AO BATTLESERVER.");
        CanDuel(s, true);
        int maxTeam = Math.Clamp(RetoInt("maxTeam", 5), 1, AOArenaGen.MaxTeam), minBet = RetoInt("minBet", 1000);
        string[] names = (m.text ?? "").Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (names.Length < 1 || names.Length > maxTeam * 2 - 1 || names.Length % 2 == 0)
            throw new InvalidOperationException($"Elegí rival, compañero, rival… (de 1 vs 1 a {maxTeam} vs {maxTeam}).");
        long bet = m.gold;
        if (bet != 0 && (bet < minBet || bet > MaxBet))
            throw new InvalidOperationException($"La apuesta mínima es {minBet:N0} monedas de oro (o 0 para un reto amistoso).");
        if (WalletOf(s) < bet) throw new InvalidOperationException("No tenés suficiente oro.");
        int maxPotions = Math.Max(-1, m.item);
        CheckPotions(s, maxPotions, true);
        var d = new Duel { Id = Guid.NewGuid().ToString("N"), Offerer = s.CharacterId, OffererName = s.State.name, OffererLevel = s.State.level,
                           Bet = bet, MaxPotions = maxPotions, InviteExpires = Now + Seconds("inviteSeconds", 60) };
        d.Members.Add(new DuelMember { Char = s.CharacterId, Name = s.State.name, Team = 0, Accepted = true });
        for (int i = 0; i < names.Length; i++)
        {
            var t = sessions.Values.FirstOrDefault(x => string.Equals(x.State.name, names[i], StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException(names[i] + " no está conectado.");
            if (d.Members.Any(x => x.Char == t.CharacterId)) throw new InvalidOperationException(t.State.name + " figura dos veces en el reto.");
            CanDuel(t, false);
            // Alternating list, as in /RETO: rival, teammate, rival, teammate, rival...
            d.Members.Add(new DuelMember { Char = t.CharacterId, Name = t.State.name, Team = i % 2 == 0 ? 1 : 0 });
        }
        Escrow(d, s);
        duels.Add(d);
        foreach (var x in d.Members.Skip(1)) Push(Online(x.Char), new AOCoopMessage { type = "duelInvite", duel = Describe(d, x) });
        PushDuel(d, "duelWaiting");
    }

    Duel InvitationFrom(Session s, string? offerer) =>
        duels.FirstOrDefault(x => x.Phase == "invite" && string.Equals(x.OffererName, offerer?.Trim(), StringComparison.OrdinalIgnoreCase)
                                  && x.Offerer != s.CharacterId && MemberOf(x, s.CharacterId) != null)
        ?? throw new InvalidOperationException($"No tenés una invitación de {offerer} o ya se canceló.");

    void DuelAccept(Session s, AOCoopMessage m)
    {
        var d = InvitationFrom(s, m.name); var me = MemberOf(d, s.CharacterId)!;
        if (me.Accepted) return;
        CanDuel(s, true, d);
        if (WalletOf(s) < d.Bet) throw new InvalidOperationException($"Necesitás {d.Bet:N0} monedas de oro para aceptar el reto.");
        CheckPotions(s, d.MaxPotions, true);
        Escrow(d, s); me.Accepted = true;
        Notice(d, s.State.name + " aceptó el reto.");
        if (d.Members.All(x => x.Accepted))
        {
            d.Phase = "queued";
            Notice(d, "Todos aceptaron el reto. Buscando sala...");
            StartQueued();
        }
        else PushDuel(d, "duelWaiting");
    }
    void DuelReject(Session s, AOCoopMessage m) => CancelDuel(InvitationFrom(s, m.name), s.State.name + " rechazó el reto.", "rechazado");
    void DuelCancel(Session s)
    {
        var d = DuelOf(s.CharacterId) ?? throw new InvalidOperationException("No estás en ningún reto.");
        if (d.Started) throw new InvalidOperationException("El reto ya empezó: usá /ABANDONAR para admitir la derrota.");
        CancelDuel(d, s.State.name + " canceló el reto.", "cancelado");
    }
    void CancelDuel(Duel d, string text, string reason)
    {
        Refund(d, "devolución");
        foreach (var x in d.Members)
            Push(Online(x.Char), new AOCoopMessage { type = "duelInviteClosed", text = text, duel = new AOCoopDuel { id = d.Id, from = d.OffererName, reason = reason, winner = -1 } });
        d.Phase = "done"; duels.Remove(d);
    }
    void DuelAbandon(Session s)
    {
        var d = DuelOf(s.CharacterId) ?? throw new InvalidOperationException("No estás en ningún reto.");
        if (!d.Started) throw new InvalidOperationException("El reto todavía no empezó: usá /CANCELAR.");
        Abandon(d, MemberOf(d, s.CharacterId)!, false);
    }

    // ModRetos.BuscarSala + ListaDeEspera (FIFO): a ring starts only when everybody is online.
    void StartQueued()
    {
        foreach (var d in duels.Where(x => x.Phase == "queued").ToArray())
        {
            if (d.Members.Any(x => Online(x.Char) == null)) continue;
            var free = Arenas.Where(a => a.Busy == null).ToArray();
            if (free.Length == 0)
            {
                if (!d.ToldQueued) { d.ToldQueued = true; PushDuel(d, "duelWaiting", text: "No hay salas disponibles. El reto comenzará cuando se desocupe una sala."); }
                return;
            }
            StartDuel(d, free[random.Next(free.Length)]);
        }
    }
    void StartDuel(Duel d, Arena a)
    {
        a.Busy = d; d.Ring = a;
        d.Seed = RandomNumberGenerator.GetInt32(int.MaxValue);
        d.Layout = AOArenaGen.Generate(d.Seed, (AOArenaGen.Theme)a.Theme);
        d.Coin = random.Next(2);
        foreach (int team in new[] { 0, 1 }) { int slot = 0; foreach (var x in d.Members.Where(m => m.Team == team)) x.Slot = slot++; }
        foreach (var x in d.Members)
        {
            var s = Online(x.Char)!;
            x.BeforeMap = s.State.map; x.BeforeX = s.State.x; x.BeforeY = s.State.y; x.BeforeHp = Math.Max(1, s.State.hp); x.Level = s.State.level;
        }
        d.Round = 1; d.Score = d.WinsA = d.WinsB = 0; d.EndsAt = Now + Seconds("maxSeconds", 600);
        PushDuel(d, "duelStart");
        StartRound(d);
        Announce($"Retos » {TeamNames(d, 0)} vs {TeamNames(d, 1)}. Apuesta: {d.Bet:N0} monedas de oro.");
    }
    // ModRetos.iniciarRonda: everybody revived and cleaned, back to their side, frozen during the countdown.
    void StartRound(Duel d)
    {
        d.Phase = "countdown"; d.PhaseEnds = Now + Seconds("countdownSeconds", 15);
        foreach (var x in d.Playing)
        {
            var s = Online(x.Char);
            if (s != null) { x.MaxHp = Math.Max(1, s.State.maxHp); x.MaxMana = Math.Max(0, s.State.maxMana); }
            x.Hp = x.MaxHp; x.Mana = x.MaxMana;
            (x.X, x.Y) = Spawn(d, x);
            if (s != null) { SyncVitals(s, x); Warp(s.Record, s, d.Ring!.Map, x.X, x.Y, x.Hp); }
            Push(s, new AOCoopMessage { type = "duelRoundStart", duel = Describe(d, x) });
        }
        PushRing(d.Ring!);
    }
    // Sides swap every round ((Ronda + i) Mod 2); the coin decides who starts on the left.
    static (int, int) Spawn(Duel d, DuelMember x)
    {
        int[] pairs = (x.Team + d.Coin + d.Round) % 2 == 1 ? d.Layout!.SpawnA : d.Layout!.SpawnB;
        int i = Math.Clamp(x.Slot, 0, AOArenaGen.MaxTeam - 1);
        return (d.Ring!.X + pairs[i * 2], d.Ring.Y + pairs[i * 2 + 1]);
    }
    void SyncVitals(Session s, DuelMember x)
    {
        s.State.hp = x.Hp; s.State.maxHp = x.MaxHp; s.State.mana = x.Mana; s.State.maxMana = x.MaxMana; s.State.dead = x.Hp <= 0;
        var d = DuelOf(s.CharacterId); s.State.arena = d?.Started == true ? d.Ring!.Sala : 0; s.State.team = x.Team;
    }
    // Journal event: it reaches the client even after a disconnection.
    void Warp(CharacterRecord record, Session? s, int map, int x, int y, int hp)
    {
        if (s != null) { s.State.map = map; s.State.x = x; s.State.y = y; }
        Event(record, new AOCoopEvent { type = "warp", map = map, x = x, y = y, hp = hp }, true);
    }

    void DuelTick()
    {
        long grace = Seconds("graceSeconds", 30);
        foreach (var d in duels.ToArray())
        {
            if (d.Phase == "invite" && Now >= d.InviteExpires) { CancelDuel(d, "El reto venció: no aceptaron todos a tiempo.", "vencido"); continue; }
            foreach (var x in d.Playing.Where(x => x.OfflineSince > 0 && Now - x.OfflineSince >= grace).ToList())
            {
                if (!d.Started) { CancelDuel(d, x.Name + " no volvió a tiempo; el reto se canceló.", "cancelado"); break; }
                Abandon(d, x, true);
                if (d.Phase == "done") break;
            }
            if (d.Phase == "done") continue;
            if (d.Phase == "countdown" && Now >= d.PhaseEnds) { d.Phase = "fight"; PushRing(d.Ring!); }
            if (d.Started && Now >= d.EndsAt) { Notice(d, "Se agotó el tiempo del reto."); Finish(d, true); }
        }
        StartQueued();
    }

    void CheckRound(Duel d)
    {
        bool alive0 = d.Playing.Any(x => x.Team == 0 && x.Hp > 0), alive1 = d.Playing.Any(x => x.Team == 1 && x.Hp > 0);
        if (alive0 && alive1) return;
        if (!alive0 && !alive1)
        {   // Decision 10: double death = null round (not original), replayed without a point.
            PushDuel(d, "duelRoundEnd", x => { var p = Describe(d, x); p.winner = -1; p.result = "empate"; return p; });
            Notice(d, "Doble muerte: la ronda se repite."); StartRound(d); return;
        }
        RoundWon(d, alive0 ? 0 : 1);
    }
    // ModRetos.ProcesarRondaGanada: best of 3.
    void RoundWon(Duel d, int team)
    {
        d.Score += team == 0 ? 1 : -1;
        if (team == 0) d.WinsA++; else d.WinsB++;
        PushDuel(d, "duelRoundEnd", x => { var p = Describe(d, x); p.winner = team; p.result = x.Team == team ? "victoria" : "derrota"; return p; });
        if (d.Round >= 3 || Math.Abs(d.Score) >= 2) { Finish(d, false); return; }
        d.Round++; StartRound(d);
    }
    // ModRetos.FinalizarReto: time up is decided by score (decision 10); 0 = tie.
    void Finish(Duel d, bool timeUp, int forcedWinner = -2)
    {
        int winner = forcedWinner != -2 ? forcedWinner : d.Score > 0 ? 0 : d.Score < 0 ? 1 : -1;
        var (prizes, tax) = Settle(d, winner);
        string a = TeamNames(d, 0), b = TeamNames(d, 1);
        foreach (var x in d.Playing.ToList())
            RestoreAfterDuel(d, x, winner < 0 ? (timeUp ? "tiempo" : "empate") : x.Team == winner ? "victoria" : "derrota", winner, prizes.GetValueOrDefault(x.Char), tax);
        if (winner < 0) Announce($"Retos » {a} vs {b}. Ninguno pudo vencer a su rival.");
        else Announce($"Retos » {(winner == 0 ? a : b)} venció a {(winner == 0 ? b : a)} y se quedó con un botín de {prizes.Values.Sum():N0} monedas de oro.");
        var ring = d.Ring; d.Phase = "done"; duels.Remove(d);
        if (ring != null) { ring.Busy = null; PushRing(ring); }
        StartQueued();
    }
    // RevivirYLimpiar + DevolverPosAnterior: previous life and position, never inventory, gold or experience.
    void RestoreAfterDuel(Duel d, DuelMember x, string result, int winner, long prize, long tax)
    {
        var s = Online(x.Char); var record = store.characters[x.Char];
        var payload = Describe(d, x); payload.result = result; payload.winner = winner; payload.prize = prize; payload.tax = tax; payload.hp = x.BeforeHp;
        Warp(record, s, x.BeforeMap, x.BeforeX, x.BeforeY, x.BeforeHp);
        Event(record, new AOCoopEvent { type = "duelEnd", hp = x.BeforeHp, duel = payload }, true);
        if (s != null) { s.State.hp = x.BeforeHp; s.State.dead = false; s.State.arena = s.State.team = 0; }
    }
    // ModRetos.AbandonarReto: the last one of a team loses the whole duel for the team.
    void Abandon(Duel d, DuelMember x, bool disconnected)
    {
        x.Left = true;
        RestoreAfterDuel(d, x, "abandono", -1, 0, 0);
        Notice(d, disconnected ? x.Name + " es descalificado por desconectarse." : x.Name + " ha abandonado el reto.");
        if (!d.Playing.Any(m => m.Team == x.Team)) { Finish(d, false, 1 - x.Team); return; }
        PushRing(d.Ring!);
        if (d.Phase == "fight") CheckRound(d);
    }

    // pot = bet × players; tax = 10 % of the pot; each winner gets (pot − tax) / current team size; a tie pays
    // (pot − tax) / all original players to those still in. What is not paid goes to the tax account on its
    // own line ("resto"). The plan is written first, so a restart can finish a half-written settlement.
    (Dictionary<string, long> prizes, long tax) Settle(Duel d, int winner)
    {
        var prizes = new Dictionary<string, long>();
        long pot = ledger.Balance(Ledger.Escrow(d.Id));
        if (d.Bet == 0 || pot <= 0) return (prizes, 0);
        var receivers = (winner < 0 ? d.Playing : d.Playing.Where(x => x.Team == winner)).Select(x => x.Char).ToList();
        int divisor = winner < 0 ? d.Members.Count : Math.Max(1, receivers.Count);
        long tax = pot * RetoInt("taxPercent", 10) / 100, each = (pot - tax) / divisor;
        foreach (string c in receivers) prizes[c] = each;
        long rest = pot - tax - each * receivers.Count;
        ledger.Transfer("settle:" + d.Id, Ledger.Escrow(d.Id), Ledger.Tax, 0, JsonSerializer.Serialize(new { prizes, tax, rest }, Json), d.Id);
        ApplyPlan(d.Id, prizes, tax, rest);
        return (prizes, tax + rest);
    }
    void ApplyPlan(string id, IReadOnlyDictionary<string, long> prizes, long tax, long rest)
    {
        foreach (var (c, amount) in prizes) ledger.Transfer($"payout:{id}:{c}", Ledger.Escrow(id), Ledger.Wallet(c), amount, "premio del reto", id);
        ledger.Transfer("tax:" + id, Ledger.Escrow(id), Ledger.Tax, tax, "impuesto del reto", id);
        ledger.Transfer("rest:" + id, Ledger.Escrow(id), Ledger.Tax, rest, "resto de la división", id);
    }
    // Startup: duels live in memory, so any custody still open is returned in full, unless its settlement
    // plan was already written, in which case the plan is finished.
    void RecoverDuelCustody()
    {
        foreach (var group in ledger.Entries.Where(e => e.op.StartsWith("escrow:")).GroupBy(e => e.reto).Select(g => g.ToList()).ToList())
        {
            string id = group[0].reto;
            if (ledger.Balance(Ledger.Escrow(id)) <= 0) continue;
            var settle = ledger.Entries.FirstOrDefault(e => e.op == "settle:" + id);
            if (settle != null)
            {
                var plan = JsonNode.Parse(settle.reason)!;
                ApplyPlan(id, plan["prizes"]!.AsObject().ToDictionary(p => p.Key, p => p.Value!.GetValue<long>()), Long(plan, "tax"), Long(plan, "rest"));
                Console.WriteLine($"Reto {id}: se completó un pago interrumpido.");
            }
            else
            {
                foreach (var e in group) ledger.Transfer("refund:" + id + ":" + e.op.Split(':')[2], Ledger.Escrow(id), e.from, e.amount, "devolución por reinicio", id);
                Console.WriteLine($"Reto {id}: quedó abierto al reiniciar; se devolvieron las apuestas.");
            }
        }
    }

    void DuelLeave(Session s)
    {
        var d = DuelOf(s.CharacterId); if (d == null) return;
        var me = MemberOf(d, s.CharacterId)!;
        if (d.Phase == "invite" && d.Offerer == s.CharacterId) { CancelDuel(d, s.State.name + " se desconectó; el reto se canceló.", "cancelado"); return; }
        if (d.Phase == "invite" && !me.Accepted) return;   // the invitation stays open until it expires
        me.OfflineSince = Now;
        Notice(d, $"{s.State.name} se desconectó. Tiene {Seconds("graceSeconds", 30) / 1000} s para volver.");
    }
    // Reconnection: everything needed to rebuild the screens is sent again (red.md §5).
    void DuelResume(Session s)
    {
        PushRingsFor(s);
        var d = DuelOf(s.CharacterId); if (d == null) return;
        var me = MemberOf(d, s.CharacterId)!;
        bool wasAway = me.OfflineSince > 0; me.OfflineSince = 0;
        if (d.Phase == "invite" && !me.Accepted) { Push(s, new AOCoopMessage { type = "duelInvite", duel = Describe(d, me) }); return; }
        if (!d.Started) { Push(s, new AOCoopMessage { type = "duelWaiting", duel = Describe(d, me) }); StartQueued(); return; }
        SyncVitals(s, me);
        Warp(s.Record, s, d.Ring!.Map, me.X, me.Y, me.Hp);
        Push(s, new AOCoopMessage { type = "duelStart", duel = Describe(d, me) });
        Push(s, new AOCoopMessage { type = "duelRoundStart", duel = Describe(d, me) });
        if (wasAway) Notice(d, s.State.name + " volvió al reto.");
    }

    // Returns false when a reported position must be ignored: frozen during the countdown, only walkable
    // cells of one's own ring while fighting, and nobody walks into a ring from outside.
    bool DuelPositionAllowed(Session s, int map, int x, int y)
    {
        if (Fighting(s, out var d, out var me))
        {
            if (d.Phase == "countdown" || me.Hp <= 0) return false;
            var a = d.Ring!;
            if (map != a.Map || !d.Layout!.Walkable(x - a.X, y - a.Y)) return false;
            me.X = x; me.Y = y; return true;
        }
        return !Arenas.Any(a => a.Map == map && AOArenaLayout.InBounds(x - a.X, y - a.Y));
    }

    bool DuelPair(Session a, Session b, out Duel d, out DuelMember ma, out DuelMember mb)
    {
        d = DuelOf(a.CharacterId)!; ma = mb = null!;
        if (d == null || !d.Started || DuelOf(b.CharacterId) != d) return false;
        ma = MemberOf(d, a.CharacterId)!; mb = MemberOf(d, b.CharacterId)!;
        return ma != null && mb != null;
    }
    static void RequireFight(Duel d, DuelMember me)
    {
        if (d.Phase != "fight") throw new InvalidOperationException("Esperá a que termine el conteo.");
        if (me.Hp <= 0) throw new InvalidOperationException("No podés hacer eso muerto.");
    }

    // The room's RNG for the shared original formulas (both ends inclusive).
    sealed class Dice(Random random) : AOPvpFormulas.IRandom
    { public int Range(int min, int max) => random.Next(Math.Min(min, max), Math.Max(min, max) + 1); }
    AOPvpFormulas.IRandom? dice;
    AOPvpFormulas.IRandom Roller => dice ??= new Dice(random);
    static AOPvpFormulas.MagicItem MagicOf(JsonNode? item) => new AOPvpFormulas.MagicItem
    { DamageBonusPercent = Int(item, "magicDamageBonus"), AbsoluteBonus = Int(item, "magicAbsoluteBonus"), Penetration = Int(item, "magicPenetration") };

    // attack {id: player}: AOPvpFormulas (UsuarioAtacaUsuario). Attack power, evasion, own hit and the class damage
    // modifier come from the client (same shared formula, clamped); weapon and armor from the catalog.
    void PvpAttack(Session s, AOCoopMessage m)
    {
        if (!sessions.TryGetValue(m.id, out var t) || !DuelPair(s, t, out var d, out var me, out var foe) || me.Team == foe.Team)
            throw new InvalidOperationException("Solo podés atacar a un rival de tu reto, dentro del ring.");
        RequireFight(d, me);
        if (foe.Hp <= 0) throw new InvalidOperationException("No podés atacar a un espíritu.");
        if (Now < s.NextAttack) throw new InvalidOperationException("Todavía no podés atacar.");
        JsonNode? weapon = items.GetValueOrDefault(s.State.weapon), helmet = items.GetValueOrDefault(t.State.helmet),
                  armor = items.GetValueOrDefault(t.State.armor), shield = items.GetValueOrDefault(t.State.shield);
        bool ranged = Int(weapon, "projectile") > 0;
        var a = d.Ring!;
        // Melee: next tile (+1 of lag). Bow: ±11/±9 and a clear line inside the ring (water lets arrows through).
        if (ranged ? !InBowRange(me.X, me.Y, foe.X, foe.Y) || !d.Layout!.SegmentClear(me.X - a.X, me.Y - a.Y, foe.X - a.X, foe.Y - a.Y)
                   : Distance(me.X, me.Y, foe.X, foe.Y) > 2)
            throw new InvalidOperationException("Enemigo fuera de alcance.");
        var ammo = ranged ? TakeAmmo(s, weapon) : null;
        s.NextAttack = Now + (ranged ? AOPvpFormulas.IntervalArrowMs : AOPvpFormulas.IntervalMeleeMs);
        s.NextCast = Math.Max(s.NextCast, Now + AOPvpFormulas.IntervalMeleeToSpellMs);
        if (!AOPvpFormulas.RollHit(Roller, s.State.attack, t.State.evasion)) { Fx(d, "miss", s, foe, 0, 0); return; }
        var attacker = new AOPvpFormulas.MeleeAttacker { MinHit = s.State.minHit, MaxHit = s.State.maxHit,
            WeaponMinHit = Int(weapon, "minHit"), WeaponMaxHit = Int(weapon, "maxHit"), Strength = s.State.strength,
            Projectile = ammo != null, ArrowMinHit = Int(ammo, "minHit"), ArrowMaxHit = Int(ammo, "maxHit"),
            ClassDamageModifier = s.State.damageModifier, PhysicalModifier = 1 };
        var victim = new AOPvpFormulas.MeleeVictim { HelmetMinDef = Int(helmet, "minDef"), HelmetMaxDef = Int(helmet, "maxDef"),
            ArmorMinDef = Int(armor, "minDef"), ArmorMaxDef = Int(armor, "maxDef"), HasShield = shield != null,
            ShieldMinDef = Int(shield, "minDef"), ShieldMaxDef = Int(shield, "maxDef"), PhysicalReduction = 1 };
        Damage(d, foe, (int)Math.Min(int.MaxValue, AOPvpFormulas.MeleeDamage(Roller, attacker, victim)), s, "hit", 0);
    }
    // HechizoPropUsuario: caster's staff, amulet and ring; victim's magic resistance from armor, ring, shield and helmet.
    int PvpSpellDamage(Session s, Session t, JsonNode spell)
    {
        var mine = JsonNode.Parse(s.Record.snapshot)!["inventory"]!; var theirs = JsonNode.Parse(t.Record.snapshot)!["inventory"]!;
        int Resist(int item) => Int(items.GetValueOrDefault(item), "magicResistance");
        int resistance = AOPvpFormulas.MagicResistance(Resist(t.State.armor), Resist(Int(theirs, "magicAccessory")), Resist(t.State.shield), Resist(t.State.helmet));
        long damage = AOPvpFormulas.SpellDamage(Roller, Int(spell, "minHp"), Int(spell, "maxHp"), s.State.level,
            MagicOf(items.GetValueOrDefault(s.State.weapon)), MagicOf(items.GetValueOrDefault(Int(mine, "amulet"))),
            MagicOf(items.GetValueOrDefault(Int(mine, "magicAccessory"))), Int(spell, "antiRm") != 0, resistance);
        return (int)Math.Min(int.MaxValue, damage);
    }
    // cast {id: player} between two players of the same started duel. Returns false when neither is in one.
    bool DuelSpell(Session s, Session t, JsonNode spell, AOCoopMessage m)
    {
        bool mine = DuelOf(s.CharacterId)?.Started == true, theirs = DuelOf(t.CharacterId)?.Started == true;
        if (!mine && !theirs) return false;
        if (!DuelPair(s, t, out var d, out var me, out var other)) throw new InvalidOperationException("No podés lanzar hechizos a alguien de otro reto.");
        RequireFight(d, me);
        int cost = Math.Max(0, Int(spell, "manaRequired"));
        if (me.Mana < cost) throw new InvalidOperationException("No tenés suficiente maná.");
        var a = d.Ring!;
        if (!d.Layout!.SegmentClear(me.X - a.X, me.Y - a.Y, other.X - a.X, other.Y - a.Y)) throw new InvalidOperationException("Hay un obstáculo en el camino.");
        int power = Roll(Int(spell, "minHp"), Int(spell, "maxHp"));
        s.NextAttack = Math.Max(s.NextAttack, Now + AOPvpFormulas.IntervalSpellToMeleeMs);
        if (me.Team != other.Team)
        {
            if (!Harmful(spell)) throw new InvalidOperationException("Ese hechizo no afecta a tus rivales.");
            if (other.Hp <= 0) throw new InvalidOperationException("No podés atacar a un espíritu.");
            if (Int(spell, "raiseHp") != 2) throw new InvalidOperationException("Ese efecto todavía no está disponible en los retos.");
            me.Mana -= cost; SyncVitals(s, me);
            Damage(d, other, PvpSpellDamage(s, t, spell), s, "spell", m.spell);
        }
        else
        {
            if (Harmful(spell)) throw new InvalidOperationException("No podés dañar a tu compañero.");
            if (Int(spell, "raiseHp") != 1) throw new InvalidOperationException("Ese efecto todavía no está disponible en los retos.");
            if (other.Hp <= 0) throw new InvalidOperationException("Tu compañero está muerto: en el reto se revive en la ronda siguiente.");
            me.Mana -= cost; SyncVitals(s, me);
            other.Hp = Math.Min(other.MaxHp, other.Hp + power); SyncVitals(t, other);
            Fx(d, "heal", s, other, power, m.spell);
        }
        return true;
    }
    // use {item}: potions inside the duel (outside, items are used in the local game).
    void DuelUse(Session s, AOCoopMessage m)
    {
        if (!Fighting(s, out var d, out var me)) throw new InvalidOperationException("Fuera de un reto, los objetos se usan en tu juego.");
        RequireFight(d, me);
        if (Now < me.NextPotion) throw new InvalidOperationException("Todavía no podés usar otra poción.");
        if (!items.TryGetValue(m.item, out var item) || Int(item, "objType") != 11) throw new InvalidOperationException("Ese objeto no se usa en el reto.");
        var inv = JsonNode.Parse(s.Record.snapshot)!["inventory"]!;
        int pending = s.Record.events.Where(e => e.type == "remove" && e.item == m.item).Sum(e => e.amount);
        if (CountItem(inv, m.item) - pending < 1) throw new InvalidOperationException("No tenés esa poción.");
        switch (Int(item, "potionType"))
        {
            case 3: me.Hp = Math.Min(me.MaxHp, me.Hp + Roll(Int(item, "minModifier"), Int(item, "maxModifier"))); break;
            // PROVISIONAL (UseInvItem, to confirm with Contenido): 4 % of max mana + level / 2 + 40 / level.
            case 4: me.Mana = Math.Min(me.MaxMana, me.Mana + me.MaxMana * 4 / 100 + me.Level / 2 + 40 / Math.Max(1, me.Level)); break;
            default: throw new InvalidOperationException("Esa poción todavía no está disponible en los retos.");
        }
        RoomForEvent(s);
        me.NextPotion = Now + AOPvpFormulas.IntervalPotionKeyMs;
        Event(s, new AOCoopEvent { type = "remove", item = m.item, amount = 1 });
        SyncVitals(s, me);
    }

    void Damage(Duel d, DuelMember target, int amount, Session attacker, string kind, int spell)
    {
        target.Hp = Math.Max(0, target.Hp - amount);
        var victim = Online(target.Char);
        // "duelHurt", not "hurt": duel life belongs to the server and must never be subtracted from the character's own life.
        // Journal events reach the victim with the next state, i.e. after the immediate duelRoundEnd/duelRoundStart: the
        // round lets the client drop a hurt from a round that is already over (QA R-08: the loser started the next round down).
        if (victim != null)
        {
            SyncVitals(victim, target);
            Event(victim, new AOCoopEvent { type = "duelHurt", damage = amount, hp = target.Hp, text = attacker.State.name,
                                            duel = new AOCoopDuel { id = d.Id, round = d.Round, winner = -1 } }, true);
        }
        Fx(d, kind, attacker, target, amount, spell);
        if (target.Hp > 0) return;
        Fx(d, "death", attacker, target, 0, 0);
        CheckRound(d);
    }
    // Ephemeral map-wide effect so spectators see hits, spells and deaths (text = hit, miss, spell, heal, death).
    void Fx(Duel d, string kind, Session from, DuelMember to, int damage, int spell)
    {
        int target = Online(to.Char)?.Id ?? 0;
        foreach (var s in sessions.Values.Where(v => v.State.map == d.Ring!.Map))
            Push(s, new AOCoopMessage { type = "fx", text = kind, id = from.Id, target = target, spell = spell, damage = damage, x = to.X, y = to.Y });
    }
}
