using System.Text.Json;
using System.Text.Json.Nodes;

// Gold operations of protocol 3. Every gold change is a ledger line; the client only shows wallet/bank.
sealed partial class CoopRoom
{
    const long MaxGoldMove = 2_000_000_000;

    // A full journal must fail before any gold moves, never after.
    static void RoomForEvent(Session s)
    { if (s.Record.events.Count >= 256) throw new InvalidOperationException("Demasiadas recompensas pendientes; liberá inventario y reconectá."); }

    static string RequestKey(AOCoopMessage m) => string.IsNullOrEmpty(m.request) ? Guid.NewGuid().ToString("N") : m.request;

    // gold > 0 deposits, gold < 0 withdraws.
    void BankMove(Session s, AOCoopMessage m)
    {
        Alive(s);
        if (m.gold == 0 || Math.Abs(m.gold) > MaxGoldMove) throw new InvalidOperationException("Cantidad inválida.");
        string wallet = Ledger.Wallet(s.CharacterId), bank = Ledger.Bank(s.CharacterId), op = "bank:" + RequestKey(m);
        if (m.gold > 0) ledger.Transfer(op, wallet, bank, m.gold, "depósito");
        else
        {
            if (ledger.Balance(bank) < -m.gold && !ledger.Has(op)) throw new InvalidOperationException("No tenés ese oro en el banco.");
            ledger.Transfer(op, bank, wallet, -m.gold, "retiro");
        }
    }

    // id = quest; amount = completion number for repeatable quests (1, 2, 3... in order).
    // Non-repeatable quests pay once per character. The amount comes from the catalog, never from the client.
    void QuestReward(Session s, AOCoopMessage m)
    {
        if (!quests.TryGetValue(m.id, out var quest)) throw new InvalidOperationException("Misión inexistente.");
        long gold = Math.Max(0, Int(quest, "rewardGold"));
        if (gold == 0) return;
        bool repeatable = Bool(quest, "repeatable");
        int time = repeatable ? Math.Max(1, m.amount) : 1;
        string prefix = "quest:" + s.CharacterId + ":" + m.id + ":";
        if (ledger.Has(prefix + time)) return;
        if (time > 1 && !ledger.Has(prefix + (time - 1))) throw new InvalidOperationException("Recompensa de misión fuera de orden.");
        if (!repeatable && JsonNode.Parse(s.Record.snapshot)?["quests"]?["completed"] is JsonArray done && !done.Any(q => q?.GetValue<int>() == m.id))
            throw new InvalidOperationException("La misión todavía no figura como completada.");
        ledger.Transfer(prefix + time, Ledger.World, Ledger.Wallet(s.CharacterId), gold, "misión " + m.id);
    }

    // Decision 18 (docs/claude/contenido/muerte-reglas.md), demo dungeon only (maps with dropOnDeath, never trigger 6):
    // everything falls except 1,000 gold per level; newbie items stay up to level 12 (AODeathDropRules, shared with the
    // offline client). Gold leaves the wallet through the ledger; items leave the inventory with "remove" events and
    // wait on the floor as loot.
    void DeathDrop(Session s, JsonObject data)
    {
        s.Record.deathDropped = true; dirty = true;
        if (!Options.Demo || !templates.ContainsKey(s.State.map)) return;
        var map = Map(s.State.map); int x = s.State.x, y = s.State.y;
        if (!AODeathDropRules.DropsOnTile(Bool(map.source, "dropOnDeath"), map.triggers.GetValueOrDefault(y * 101 + x))) return;
        int death = ++s.Record.deaths;
        var drops = new List<(int item, int amount)>();
        long gold = AODeathDropRules.GoldToDrop(WalletOf(s), s.State.level);
        if (gold > 0 && ledger.Transfer($"death:{s.CharacterId}:{death}", Ledger.Wallet(s.CharacterId), Ledger.World, gold, "muerte"))
            drops.Add((Int(catalog["loot"], "goldItemIndex"), (int)Math.Min(int.MaxValue, gold)));
        var lost = new List<AOCoopItem>();
        var inv = data["inventory"]!; var ids = inv["itemIndices"]!.AsArray(); var amounts = inv["amounts"]!.AsArray();
        for (int i = 0; i < ids.Count; i++)
        {
            int item = ids[i]?.GetValue<int>() ?? 0, amount = amounts[i]?.GetValue<int>() ?? 0;
            if (item <= 0 || amount <= 0 || !items.TryGetValue(item, out var def)) continue;
            var flags = AODeathDropRules.FlagsFrom(Bool(def, "noSeCae"), Bool(def, "intirable"), Bool(def, "destroyOnSell"), Bool(def, "untransferable"), Bool(def, "newbie"));
            if (!AODeathDropRules.ItemFalls(Int(def, "objType"), flags, s.State.level)) continue;
            Event(s, new AOCoopEvent { type = "remove", item = item, amount = amount }, true);
            drops.Add((item, amount)); lost.Add(new AOCoopItem { item = item, amount = amount });
        }
        // TirarItemAlPiso: each stack on its own free tile; with no room nearby it is lost, as in the original.
        foreach (var (item, amount) in drops)
            if (FreeTileNear(map, x, y, out int tx, out int ty)) AddLoot(map, item, amount, tx, ty);
        if (drops.Count > 0) Event(s, new AOCoopEvent { type = "deathDrop", gold = gold, items = lost.ToArray() }, true);
    }
    // Tilelibre: rings around the dead player, top-left first; the first tile where something can lie.
    bool FreeTileNear(MapRecord map, int x, int y, out int fx, out int fy)
    {
        for (int r = 1; r <= 10; r++)
            for (int ty = y - r; ty <= y + r; ty++)
                for (int tx = x - r; tx <= x + r; tx++)
                    if ((Math.Abs(tx - x) == r || Math.Abs(ty - y) == r) && CanDropAt(map, tx, ty)) { fx = tx; fy = ty; return true; }
        fx = fy = 0; return false;
    }
    // Walkable land, no exit, nobody standing and nothing already lying there (nube: muerte-online, CanDropAt).
    bool CanDropAt(MapRecord map, int x, int y)
    {
        if (!ValidPosition(map.id, x, y) || map.exits.Contains(y * 101 + x)) return false;
        int flags = map.flags.GetValueOrDefault(y * 101 + x), trigger = map.triggers.GetValueOrDefault(y * 101 + x);
        if ((flags & 15) != 0 || trigger is 3 or 13 or 201) return false;
        if ((flags & 32) != 0 && (flags & 128) == 0 && trigger != 17) return false;
        return !map.loot.Any(l => l.x == x && l.y == y) && !map.npcs.Any(n => !n.state.dead && n.state.x == x && n.state.y == y) &&
               !sessions.Values.Any(p => p.State.map == map.id && p.State.x == x && p.State.y == y);
    }

    // --test only: balances per account plus the invariant (all accounts add up to 0).
    string TestLedger()
    {
        if (!Options.Test) throw new InvalidOperationException("Acción desconocida.");
        long sum = ledger.Balances.Values.Sum();
        return JsonSerializer.Serialize(new { balances = ledger.Balances, sum, lastTx = ledger.LastTx }, Json);
    }
}
