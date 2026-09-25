using System.Text.Json.Nodes;

// Original magic AI of the NPCs (nube, V902 provisional; docs/claude/contenido/npc_hechizos_original.md):
// AI_AtacarUsuarioObjetivo + NpcLanzaUnSpell + NpcLanzaSpellSobreUser/Area. The pure rules are shared with the
// Unity client (Runtime/Shared/AONpcSpellRulesV902.cs); the data per NPC is catalog "npcSpells" (npc_spells.json).
// NPCs have no mana; only IntervaloLanzarHechizo limits them. The server rolls the damage (with the player's magic
// resistance) and sends a "hurt" event with the spell and the caster: the client shows the cast and applies the
// other effects of the spell (paralysis, fire, curses...). Not included: Support/BG AI, summons, invisibility checks
// (the room does not know who is invisible) and the NPC level filter.
sealed partial class CoopRoom
{
    Dictionary<int, JsonObject>? npcSpellDefs;
    Dictionary<int, JsonObject> NpcSpellDefs => npcSpellDefs ??= Index(catalog["npcSpells"]?["npcs"], "npcIndex");

    static double Number(JsonNode? n, string k) => n?[k] is JsonValue v && v.TryGetValue(out double d) ? d : 0;

    bool CastsSpells(NpcRecord n) => NpcSpellDefs.TryGetValue(n.state.npc, out var def) && def["spells"] is JsonArray { Count: > 0 };

    // AI_AtacarUsuarioObjetivo: magic first; melee only when it did not cast (Magic_and_Punch = 1, DontHitVisiblePlayers).
    bool NpcMayMelee(NpcRecord n, bool castThisTurn) =>
        AONpcSpellRules.MayMelee(CastsSpells(n), castThisTurn, Bool(NpcSpellDefs.GetValueOrDefault(n.state.npc), "dontHitVisiblePlayers"), false);

    // One AI turn: true when the NPC spent it on magic (even if the spell did not go out, as the original timer does).
    bool NpcCast(NpcRecord n, Session target, Session[] present)
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
                if (Int(spell, "removeParalysis") != 0) n.paralyzedUntil = n.immobileUntil = 0;
                if (Int(spell, "raiseHp") == 1) n.state.hp = Math.Min(n.state.maxHp, n.state.hp + Roll(Int(spell, "minHp"), Int(spell, "maxHp")));
                dirty = true;
                break;
            case AONpcSpellRules.Aim.Player:
                NpcSpellHit(n, def, spell, target);
                break;
            case AONpcSpellRules.Aim.Area:
                int cx = target.State.x + Roller.Range(-AONpcSpellRules.AreaJitter, AONpcSpellRules.AreaJitter);
                int cy = target.State.y + Roller.Range(-AONpcSpellRules.AreaJitter, AONpcSpellRules.AreaJitter);
                foreach (var p in present)
                    if (!p.State.dead && AONpcSpellRules.InArea(cx, cy, Int(spell, "areaRadius"), p.State.x, p.State.y))
                        NpcSpellHit(n, def, spell, p);
                break;
        }
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

    // GetUserMRForNpc: ResistenciaMagica of armor + ring + shield + helmet (the same items the PvP spell damage reads).
    int UserMagicResistance(Session p)
    {
        int Resist(int item) => Int(items.GetValueOrDefault(item), "magicResistance");
        int ring = 0;
        try { ring = Int(JsonNode.Parse(p.Record.snapshot)?["inventory"], "magicAccessory"); } catch (System.Text.Json.JsonException) { }
        return AOPvpFormulas.MagicResistance(Resist(p.State.armor), Resist(ring), Resist(p.State.shield), Resist(p.State.helmet));
    }
}
