using System.Text.Json.Nodes;

// Original magic AI of the NPCs (nube, V902 provisional; docs/claude/contenido/npc_hechizos_original.md):
// AI_AtacarUsuarioObjetivo + NpcLanzaUnSpell + NpcLanzaSpellSobreUser/Area, the Support/BG AI (Movement 11/13,
// TrySupportThenAttackSpells) and summons (Invoca). The pure rules are shared with the Unity client
// (Runtime/Shared/AONpcSpellRulesV902.cs); the data per NPC is catalog "npcSpells" (npc_spells.json).
// NPCs have no mana; only IntervaloLanzarHechizo (and the support slots' Cd) limit them. The server rolls the damage
// (with the player's magic resistance) and sends a "hurt" event with the spell and the caster: the client applies the
// other effects of the spell. Every cast is also pushed to the map ("npcCast") so everybody sees the animation.
// Not included: NPC-vs-NPC attacks, invisibility checks (the room does not know who is invisible), the level filter.
sealed partial class CoopRoom
{
    Dictionary<int, JsonObject>? npcSpellDefs, summonedDefs;
    Dictionary<int, JsonObject> NpcSpellDefs => npcSpellDefs ??= Index(catalog["npcSpells"]?["npcs"], "npcIndex");
    Dictionary<int, JsonObject> SummonedDefs => summonedDefs ??= Index(catalog["npcSpells"]?["summoned"], "npcIndex");
    readonly List<(MapRecord map, NpcRecord npc)> newSummons = new();
    int summonSeq = 100000;   // summoned creatures get ids far from the catalog positions

    static double Number(JsonNode? n, string k) => n?[k] is JsonValue v && v.TryGetValue(out double d) ? d : 0;
    static int Slot(JsonNode? n, string list, int i) => n?[list] is JsonArray a && i < a.Count ? a[i]?.GetValue<int>() ?? 0 : 0;

    bool CastsSpells(NpcRecord n) => NpcSpellDefs.TryGetValue(n.state.npc, out var def) && def["spells"] is JsonArray { Count: > 0 };
    bool SupportAi(NpcRecord n) => NpcSpellDefs.TryGetValue(n.state.npc, out var def) && AONpcSpellRules.IsSupportAi(Int(def, "movement"));

    // AI_AtacarUsuarioObjetivo: magic first; melee only when it did not cast (Magic_and_Punch = 1, DontHitVisiblePlayers).
    bool NpcMayMelee(NpcRecord n, bool castThisTurn) =>
        AONpcSpellRules.MayMelee(CastsSpells(n), castThisTurn, Bool(NpcSpellDefs.GetValueOrDefault(n.state.npc), "dontHitVisiblePlayers"), false);

    // One classic AI turn: true when the NPC spent it on magic (even if the spell did not go out, as the original timer does).
    bool NpcCast(MapRecord map, NpcRecord n, Session target, Session[] present)
    {
        if (!NpcSpellDefs.TryGetValue(n.state.npc, out var def) || def["spells"] is not JsonArray slots || slots.Count == 0) return false;
        if (Now < n.nextCast) return false;
        n.nextCast = Now + AONpcSpellRules.CastIntervalMs(Int(def, "castIntervalMs"));
        int dx = target.State.x - n.state.x, dy = target.State.y - n.state.y;
        if (!AONpcSpellRules.InRange(dx, dy)) return true;
        int id = slots[AONpcSpellRules.PickSlot(Roller, slots.Count)]?.GetValue<int>() ?? 0;
        var spell = spells.GetValueOrDefault(id);
        if (spell == null) return true;                                   // empty slot (Sp = 0)
        n.state.heading = Heading(dx, dy);
        switch (AONpcSpellRules.AimOf(Int(spell, "target"), Int(spell, "autoCast") != 0))
        {
            case AONpcSpellRules.Aim.Self:
                HelpNpc(n, spell);
                BroadcastNpcCast(map, n, spell, 0, n.state.x, n.state.y);
                break;
            case AONpcSpellRules.Aim.Player:
                BroadcastNpcCast(map, n, spell, target.Id, target.State.x, target.State.y);
                NpcSpellHit(n, def, spell, target);
                break;
            case AONpcSpellRules.Aim.Area:
                int cx = target.State.x + Roller.Range(-AONpcSpellRules.AreaJitter, AONpcSpellRules.AreaJitter);
                int cy = target.State.y + Roller.Range(-AONpcSpellRules.AreaJitter, AONpcSpellRules.AreaJitter);
                BroadcastNpcCast(map, n, spell, 0, cx, cy);
                foreach (var p in present)
                    if (!p.State.dead && AONpcSpellRules.InArea(cx, cy, Int(spell, "areaRadius"), p.State.x, p.State.y))
                        NpcSpellHit(n, def, spell, p);
                break;
        }
        if (Int(spell, "summonNpc") > 0) Summon(map, n, def, spell);
        return true;
    }

    void NpcSpellHit(NpcRecord n, JsonObject def, JsonObject spell, Session p)
    {
        int damage = 0;
        if (Int(spell, "raiseHp") == 2)
            damage = AONpcSpellRules.Damage(Roller, Int(spell, "minHp"), Int(spell, "maxHp"), Number(def, "magicBonus"),
                UserMagicResistance(p), Int(spell, "antiRm") != 0);
        // Same order as the melee hit: the event (journal) first, then the room's copy of the life.
        Event(p, new AOCoopEvent { type = "hurt", damage = damage, spell = Int(spell, "id"), npc = n.state.id, text = Text(n.source, "name") }, true);
        p.State.hp = Math.Max(0, p.State.hp - damage); p.State.dead = p.State.hp <= 0;
    }

    // Help spells on an NPC (itself or an ally): remove paralysis, heal (NpcLanzaSpellSobreNpc).
    void HelpNpc(NpcRecord target, JsonNode spell)
    {
        if (Int(spell, "removeParalysis") != 0) target.paralyzedUntil = target.immobileUntil = 0;
        if (Int(spell, "raiseHp") == 1) target.state.hp = Math.Min(target.state.maxHp, target.state.hp + Roll(Int(spell, "minHp"), Int(spell, "maxHp")));
        dirty = true;
    }

    // Everybody on the map sees the cast (ephemeral, like the duel fx): the caster's animation and the spell's travel.
    void BroadcastNpcCast(MapRecord map, NpcRecord n, JsonNode spell, int targetSession, int x, int y)
    {
        foreach (var s in sessions.Values.Where(v => v.State.map == map.id))
            Push(s, new AOCoopMessage { type = "npcCast", id = n.state.id, spell = Int(spell, "id"), target = targetSession, x = x, y = y });
    }

    // GetUserMRForNpc: ResistenciaMagica of armor + ring + shield + helmet (the same items the PvP spell damage reads).
    int UserMagicResistance(Session p)
    {
        int Resist(int item) => Int(items.GetValueOrDefault(item), "magicResistance");
        int ring = 0;
        try { ring = Int(JsonNode.Parse(p.Record.snapshot)?["inventory"], "magicAccessory"); } catch (System.Text.Json.JsonException) { }
        return AOPvpFormulas.MagicResistance(Resist(p.State.armor), Resist(ring), Resist(p.State.shield), Resist(p.State.helmet));
    }

    // ---- Support / BG AI (TrySupportThenAttackSpells): help first, attack only if it helped nobody ----
    void NpcSupportTurn(MapRecord map, NpcRecord n, Session[] present)
    {
        if (!NpcSpellDefs.TryGetValue(n.state.npc, out var def) || def["spells"] is not JsonArray slots || slots.Count == 0) return;
        if (Now < n.nextCast) return;
        n.nextCast = Now + AONpcSpellRules.CastIntervalMs(Int(def, "castIntervalMs"));
        n.slotUse ??= new long[slots.Count];
        var defs = new JsonObject?[slots.Count]; var ready = new bool[slots.Count];
        for (int i = 0; i < slots.Count; i++)
        {
            defs[i] = spells.GetValueOrDefault(slots[i]?.GetValue<int>() ?? 0);
            ready[i] = defs[i] != null && AONpcSpellRules.SlotReady(n.slotUse[i], Slot(def, "cooldowns", i), Now);
        }
        bool[] With(Func<JsonObject, bool> effect) => defs.Select(s => s != null && effect(s)).ToArray();
        int range = Math.Max(1, Int(def, "rangeSpell"));
        bool Near(int x, int y) => AONpcSpellRules.InSupportRange(x - n.state.x, y - n.state.y, range);
        bool Sees(Session p) => Math.Abs(p.State.x - n.state.x) <= 15 && Math.Abs(p.State.y - n.state.y) <= 13;
        void Use(int slot) { n.slotUse![slot] = Now; }

        int help = Int(def, "help");
        // 1) Remove paralysis (NPC allies: the room does not know which users are paralyzed), 2) heal users, then NPCs.
        int free = AONpcSpellRules.LastReadySlot(ready, With(s => Int(s, "removeParalysis") != 0));
        if (free >= 0 && AONpcSpellRules.HelpsNpcs(help))
        {
            var ally = map.npcs.FirstOrDefault(o => !o.state.dead && (Now < o.paralyzedUntil || Now < o.immobileUntil) && Near(o.state.x, o.state.y));
            if (ally != null) { HelpNpc(ally, defs[free]!); Use(free); BroadcastNpcCast(map, n, defs[free]!, 0, ally.state.x, ally.state.y); return; }
        }
        int heal = AONpcSpellRules.LastReadySlot(ready, With(s => Int(s, "raiseHp") == 1));
        if (heal >= 0)
        {
            var hurtUser = AONpcSpellRules.HelpsUsers(help)
                ? present.FirstOrDefault(p => !p.State.dead && p.State.hp < p.State.maxHp && Sees(p) && Near(p.State.x, p.State.y)) : null;
            if (hurtUser != null)
            {
                Use(heal); BroadcastNpcCast(map, n, defs[heal]!, hurtUser.Id, hurtUser.State.x, hurtUser.State.y);
                Event(hurtUser, new AOCoopEvent { type = "spell", spell = Int(defs[heal], "id"), text = Text(n.source, "name") }, true);
                return;
            }
            var hurtNpc = AONpcSpellRules.HelpsNpcs(help)
                ? map.npcs.FirstOrDefault(o => !o.state.dead && o.state.hp < o.state.maxHp && Near(o.state.x, o.state.y)) : null;
            if (hurtNpc != null) { HelpNpc(hurtNpc, defs[heal]!); Use(heal); BroadcastNpcCast(map, n, defs[heal]!, 0, hurtNpc.state.x, hurtNpc.state.y); return; }
        }
        // 3) Attack: its aggressor if still seen and in range, else the first user it sees in range; paralyze first, then damage.
        if (!AONpcSpellRules.AttacksUsers(Int(def, "attack"))) return;
        var aggressor = sessions.GetValueOrDefault(n.provoked);
        var target = aggressor != null && present.Contains(aggressor) && Sees(aggressor) && Near(aggressor.State.x, aggressor.State.y) ? aggressor
            : present.FirstOrDefault(p => !p.State.dead && Sees(p) && Near(p.State.x, p.State.y));
        if (target == null) return;
        int slot = AONpcSpellRules.LastReadySlot(ready, With(s => Int(s, "paralyze") != 0 || Int(s, "immobilize") != 0));
        if (slot < 0) slot = AONpcSpellRules.LastReadySlot(ready, With(s => Int(s, "raiseHp") == 2));
        if (slot < 0) return;
        Use(slot);
        n.state.heading = Heading(target.State.x - n.state.x, target.State.y - n.state.y);
        BroadcastNpcCast(map, n, defs[slot]!, target.Id, target.State.x, target.State.y);
        NpcSpellHit(n, def, defs[slot]!, target);
    }

    // AI_CaminarSinRumboCercaDeOrigen: back toward its origin when more than 4 tiles away, else an occasional random step.
    void WanderNearOrigin(MapRecord map, NpcRecord n)
    {
        if (Now < n.immobileUntil || IsStationary(n)) return;
        int ox = Int(n.source, "x"), oy = Int(n.source, "y");
        if (Distance(ox, oy, n.state.x, n.state.y) > 4) MoveToward(map, n, ox, oy);
        else if (random.Next(6) == 2) Move(map, n, random.Next(1, 5));
    }

    // ---- Summons (Invoca = 1) ----
    void Summon(MapRecord map, NpcRecord caster, JsonObject def, JsonNode spell)
    {
        if (!SummonedDefs.TryGetValue(Int(spell, "summonNpc"), out var entry)) return;
        int alive = map.npcs.Count(o => o.summoner == caster && !o.state.dead) + newSummons.Count(s => s.npc.summoner == caster);
        int count = AONpcSpellRules.SummonsToCreate(Math.Max(1, Int(spell, "summonCount")), alive, Int(def, "summonLimit"));
        for (int i = 0; i < count; i++)
        {
            if (!FreeSummonTile(map, caster.state.x, caster.state.y, out int x, out int y)) break;
            var source = entry.DeepClone().AsObject(); source["x"] = x; source["y"] = y;
            int hp = Math.Max(1, Int(source, "maxHp"));
            newSummons.Add((map, new NpcRecord { source = source, summoned = true, summoner = caster,
                state = new AOCoopNpc { id = ++summonSeq, npc = Int(source, "npcIndex"), x = x, y = y, heading = 3, hp = hp, maxHp = hp } }));
        }
    }

    // SpawnNpc(..., FullSearch): the nearest free tile around the summoner (rings of 1 to 3 tiles).
    bool FreeSummonTile(MapRecord map, int cx, int cy, out int x, out int y)
    {
        for (int r = 1; r <= 3; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    x = cx + dx; y = cy + dy;
                    if (!ValidPosition(map.id, x, y)) continue;
                    int flags = map.flags.GetValueOrDefault(y * 101 + x), trigger = map.triggers.GetValueOrDefault(y * 101 + x);
                    if ((flags & 15) != 0 || ((flags & 32) != 0 && (flags & 128) == 0) || (flags & 256) != 0 || trigger == 3 || map.exits.Contains(y * 101 + x)) continue;
                    int tx = x, ty = y;
                    if (map.npcs.Any(o => !o.state.dead && o.state.x == tx && o.state.y == ty) || newSummons.Any(s => s.npc.state.x == tx && s.npc.state.y == ty) ||
                        sessions.Values.Any(p => p.State.map == map.id && p.State.x == tx && p.State.y == ty)) continue;
                    return true;
                }
        x = y = 0; return false;
    }

    // MuereNpc: a dead summoner takes its summons with it (no killer, no experience).
    void NpcDied(MapRecord map, NpcRecord n)
    {
        foreach (var o in map.npcs.Where(o => o.summoner == n && !o.state.dead)) { o.state.dead = true; dirty = true; }
        newSummons.RemoveAll(s => s.npc.summoner == n);
    }

    // After the map's tick: dead summons leave, new ones enter (the NPC list cannot change while it is being walked).
    void FlushSummons(MapRecord map)
    {
        if (map.npcs.RemoveAll(o => o.summoned && o.state.dead) > 0) dirty = true;
        foreach (var s in newSummons.Where(s => s.map == map)) { map.npcs.Add(s.npc); dirty = true; }
        newSummons.RemoveAll(s => s.map == map);
    }
}
