using System;
using System.Collections.Generic;
using UnityEngine;

// AONPCSpellCasterV902 (nube, número provisorio): los NPC que lanzan hechizos en el original, sin conexión.
// Reglas compartidas con el servidor: Shared/AONpcSpellRulesV902.cs. Datos: MagicV129/npc_spells.json
// (Tools/export_npc_spells.py, desde npcs.dat): hechizos por NPC, IA de apoyo (Movement 11/13) y las criaturas que
// se pueden invocar. En línea decide el servidor (CoopRoom.NpcMagic.cs); el cliente muestra el lanzamiento (npcCast),
// aplica los otros efectos ("hurt" con spell) y crea las criaturas invocadas que manda la sala (SpawnSummoned).
public static class AONPCSpellCasterV902
{
    const string DataPath = "AOMigrator/MagicV129/npc_spells";
    public const int FirstSummonId = 100001;   // CoopRoom.NpcMagic.cs: summonSeq starts at 100000

    [Serializable] public class Entry
    {
        public int npcIndex, castIntervalMs, movement, rangeSpell, help, attack, summonLimit;
        public int[] spells, cooldowns;
        public float magicBonus;
        public bool dontHitVisiblePlayers;
    }
    [Serializable] class Database { public string version; public Entry[] npcs; public AOWorldManagerV07.NPCEntry[] summoned; }

    sealed class UnityRoller : AOPvpFormulas.IRandom
    {
        public int Range(int minInclusive, int maxInclusive) => UnityEngine.Random.Range(minInclusive, maxInclusive + 1);
    }

    static readonly UnityRoller roller = new UnityRoller();
    static readonly Dictionary<int, Entry> byNpc = new Dictionary<int, Entry>();
    static readonly Dictionary<int, AOWorldManagerV07.NPCEntry> summonable = new Dictionary<int, AOWorldManagerV07.NPCEntry>();
    static readonly Dictionary<int, float> nextCastAt = new Dictionary<int, float>();   // by NPC instance
    static readonly Dictionary<int, float[]> slotUse = new Dictionary<int, float[]>();  // support AI: last use of each slot
    static readonly Dictionary<int, List<AONPCSummonLinkV902>> summonsOf = new Dictionary<int, List<AONPCSummonLinkV902>>();
    static bool loaded;

    static void Load()
    {
        if (loaded) return;
        loaded = true;
        TextAsset asset = Resources.Load<TextAsset>(DataPath);
        Database db = asset == null ? null : JsonUtility.FromJson<Database>(asset.text);
        if (db == null) return;
        if (db.npcs != null)
            foreach (Entry e in db.npcs)
                if (e != null && e.npcIndex > 0 && e.spells != null && e.spells.Length > 0) byNpc[e.npcIndex] = e;
        if (db.summoned != null)
            foreach (var s in db.summoned)
                if (s != null && s.npcIndex > 0) summonable[s.npcIndex] = s;
    }

    public static Entry Get(int npcIndex)
    {
        Load();
        return byNpc.TryGetValue(npcIndex, out Entry found) ? found : null;
    }

    static Entry EntryOf(AONPCCombatV09 npc)
    {
        AONPCMetadata meta = npc == null ? null : npc.GetComponent<AONPCMetadata>();
        return meta == null ? null : Get(meta.NpcIndex);
    }

    // True when the NPC may still melee this turn (AI_AtacarUsuarioObjetivo: If magic ... ElseIf melee).
    public static bool MayMelee(AONPCCombatV09 npc, bool castThisTurn)
    {
        Entry e = EntryOf(npc);
        if (e == null) return true;   // an NPC without spells always melees (and skips the search below)
        if (AONpcSpellRules.IsSupportAi(e.movement)) return false;   // the support AI never melees
        AOPlayerMagicStatusV120 target = castThisTurn || !e.dontHitVisiblePlayers ? null
            : UnityEngine.Object.FindFirstObjectByType<AOPlayerMagicStatusV120>();
        return AONpcSpellRules.MayMelee(true, castThisTurn, e.dontHitVisiblePlayers, target != null && target.IsInvisible);
    }

    // One AI turn against the player at (dx, dy). Returns true when the NPC used the turn on magic.
    public static bool TryCast(AONPCCombatV09 npc, int dx, int dy)
    {
        if (AOOnlineClientV240.Requested || npc == null || !npc.IsAlive) return false;
        Entry e = EntryOf(npc);
        if (e == null) return false;

        int key = npc.GetInstanceID();
        if (nextCastAt.TryGetValue(key, out float readyAt) && Time.time < readyAt) return false;
        // IntervaloPermiteLanzarHechizo restarts the timer when it is checked, even if the spell does not go out.
        nextCastAt[key] = Time.time + AONpcSpellRules.CastIntervalMs(e.castIntervalMs) / 1000f;

        AOPlayerCombatV09 player = UnityEngine.Object.FindFirstObjectByType<AOPlayerCombatV09>();
        if (player == null || player.IsDead) return true;
        if (AONpcSpellRules.IsSupportAi(e.movement)) { SupportTurn(npc, e, player, dx, dy); return true; }

        AONPCMagicStatusV120 npcStatus = npc.GetComponent<AONPCMagicStatusV120>();
        AOPlayerMagicStatusV120 playerStatus = player.GetComponent<AOPlayerMagicStatusV120>();
        if (!AONpcSpellRules.InRange(dx, dy)) return true;

        AOSpellDatabaseV120.SpellDef spell = AOSpellDatabaseV120.Get(e.spells[AONpcSpellRules.PickSlot(roller, e.spells.Length)]);
        if (spell == null) return true;   // empty slot (Sp = 0): the turn is spent
        Vector3 from = npc.transform.position, to = player.transform.position;

        switch (AONpcSpellRules.AimOf(spell.target, spell.autoCast != 0))
        {
            case AONpcSpellRules.Aim.Self:
                HelpNpc(npc, spell);
                AOSpellFXV120.PlayFromNpc(npc.gameObject, spell, from, from);
                break;
            case AONpcSpellRules.Aim.Player:
                // A paralyzed NPC cannot hurt users (PuedeDanarAlUsuario); invisible users are not valid targets.
                if (npcStatus != null && !npcStatus.CanAttack) return true;
                if (playerStatus != null && playerStatus.IsInvisible) return true;
                Hit(npc, e, spell, player, from, true);
                break;
            case AONpcSpellRules.Aim.Area:
                if (npcStatus != null && !npcStatus.CanAttack) return true;
                int cx = dx + roller.Range(-AONpcSpellRules.AreaJitter, AONpcSpellRules.AreaJitter);
                int cy = dy + roller.Range(-AONpcSpellRules.AreaJitter, AONpcSpellRules.AreaJitter);
                AOSpellFXV120.PlayFromNpc(npc.gameObject, spell, from, to);
                // Offline there is one user: the square is measured from the NPC (dx, dy are the player's offsets).
                if (AONpcSpellRules.InArea(cx, cy, spell.areaRadius, dx, dy)) Hit(npc, e, spell, player, from, false);
                break;
        }
        if (spell.summonNpc > 0) Summon(npc, e, spell);
        return true;
    }

    static void Hit(AONPCCombatV09 npc, Entry e, AOSpellDatabaseV120.SpellDef spell, AOPlayerCombatV09 player, Vector3 from, bool fx)
    {
        if (fx) AOSpellFXV120.PlayFromNpc(npc.gameObject, spell, from, player.transform.position);
        if (spell.raiseHp == 2)
        {
            int damage = AONpcSpellRules.Damage(roller, spell.minHp, spell.maxHp, e.magicBonus,
                MagicResistance(player.GetComponent<AOInventoryV10>()), spell.antiRm != 0);
            player.ReceiveMagicDamage(damage, npc.DisplayName + ": " + spell.name);
        }
        AOPlayerMagicV120 magic = player.GetComponent<AOPlayerMagicV120>();
        if (magic != null && !player.IsDead) magic.ApplyNpcSpell(spell.id, from, false);
    }

    // Help spells on an NPC (itself or an ally): remove paralysis, heal.
    static void HelpNpc(AONPCCombatV09 target, AOSpellDatabaseV120.SpellDef spell)
    {
        var status = target.GetComponent<AONPCMagicStatusV120>();
        if (spell.removeParalysis != 0 && status != null) status.RemoveParalysis();
        if (spell.raiseHp == 1) target.HealMagic(roller.Range(spell.minHp, spell.maxHp));
    }

    // ---- Support / BG AI (TrySupportThenAttackSpells): help first, attack only if it helped nobody ----
    static void SupportTurn(AONPCCombatV09 npc, Entry e, AOPlayerCombatV09 player, int dx, int dy)
    {
        int n = e.spells.Length, key = npc.GetInstanceID();
        if (!slotUse.TryGetValue(key, out float[] used) || used.Length != n) slotUse[key] = used = new float[n];
        var defs = new AOSpellDatabaseV120.SpellDef[n];
        var ready = new bool[n];
        long now = (long)(Time.time * 1000f);
        for (int i = 0; i < n; i++)
        {
            defs[i] = AOSpellDatabaseV120.Get(e.spells[i]);
            int cd = e.cooldowns != null && i < e.cooldowns.Length ? e.cooldowns[i] : 0;
            ready[i] = defs[i] != null && AONpcSpellRules.SlotReady((long)(used[i] * 1000f), cd, now);
        }
        bool[] With(Func<AOSpellDatabaseV120.SpellDef, bool> effect)
        {
            var r = new bool[n];
            for (int i = 0; i < n; i++) r[i] = defs[i] != null && effect(defs[i]);
            return r;
        }
        int range = Mathf.Max(1, e.rangeSpell);
        Vector3 from = npc.transform.position;
        AONPCMovementV08 me = npc.GetComponent<AONPCMovementV08>();

        // Allies: the other NPCs of the same map in range.
        AONPCCombatV09 AllyWhere(Func<AONPCCombatV09, bool> wanted)
        {
            if (me == null) return null;
            foreach (var other in UnityEngine.Object.FindObjectsByType<AONPCCombatV09>(FindObjectsSortMode.None))
            {
                if (other == null || !other.IsAlive) continue;
                AONPCMovementV08 m = other.GetComponent<AONPCMovementV08>();
                if (m == null || m.Grid != me.Grid || !AONpcSpellRules.InSupportRange(m.TileX - me.TileX, m.TileY - me.TileY, range)) continue;
                if (wanted(other)) return other;
            }
            return null;
        }

        if (AONpcSpellRules.HelpsNpcs(e.help))
        {
            int free = AONpcSpellRules.LastReadySlot(ready, With(s => s.removeParalysis != 0));
            AONPCCombatV09 frozen = free < 0 ? null : AllyWhere(o =>
            {
                var st = o.GetComponent<AONPCMagicStatusV120>();
                return st != null && (st.IsParalyzed || st.IsImmobilized);
            });
            if (frozen != null)
            {
                used[free] = Time.time; HelpNpc(frozen, defs[free]);
                AOSpellFXV120.PlayFromNpc(npc.gameObject, defs[free], from, frozen.transform.position);
                return;
            }
        }
        int heal = AONpcSpellRules.LastReadySlot(ready, With(s => s.raiseHp == 1));
        if (heal >= 0 && AONpcSpellRules.HelpsUsers(e.help) && player.HP < player.MaxHP && AONpcSpellRules.InSupportRange(dx, dy, range))
        {
            used[heal] = Time.time;
            AOSpellFXV120.PlayFromNpc(npc.gameObject, defs[heal], from, player.transform.position);
            var magic = player.GetComponent<AOPlayerMagicV120>();
            if (magic != null) magic.ApplyOnlineSpell(defs[heal].id);   // the healing applies like a companion's spell
            return;
        }
        if (heal >= 0 && AONpcSpellRules.HelpsNpcs(e.help))
        {
            AONPCCombatV09 hurt = AllyWhere(o => o.HP < o.MaxHP);
            if (hurt != null)
            {
                used[heal] = Time.time; HelpNpc(hurt, defs[heal]);
                AOSpellFXV120.PlayFromNpc(npc.gameObject, defs[heal], from, hurt.transform.position);
                return;
            }
        }

        if (!AONpcSpellRules.AttacksUsers(e.attack) || !AONpcSpellRules.InSupportRange(dx, dy, range)) return;
        var status = npc.GetComponent<AONPCMagicStatusV120>();
        var playerStatus = player.GetComponent<AOPlayerMagicStatusV120>();
        if ((status != null && !status.CanAttack) || (playerStatus != null && playerStatus.IsInvisible)) return;
        int slot = AONpcSpellRules.LastReadySlot(ready, With(s => s.paralyze != 0 || s.immobilize != 0));
        if (slot < 0) slot = AONpcSpellRules.LastReadySlot(ready, With(s => s.raiseHp == 2));
        if (slot < 0) return;
        used[slot] = Time.time;
        Hit(npc, e, defs[slot], player, from, true);
    }

    // ---- Summons (Invoca = 1): up to CantidadInvocaciones alive, next to the summoner ----
    static void Summon(AONPCCombatV09 npc, Entry e, AOSpellDatabaseV120.SpellDef spell)
    {
        int key = npc.GetInstanceID();
        if (!summonsOf.TryGetValue(key, out var list)) summonsOf[key] = list = new List<AONPCSummonLinkV902>();
        list.RemoveAll(s => s == null || !s.Alive);
        int count = AONpcSpellRules.SummonsToCreate(Mathf.Max(1, spell.summonCount), list.Count, e.summonLimit);
        AONPCMovementV08 at = npc.GetComponent<AONPCMovementV08>();
        if (at == null || at.Grid == null) return;
        for (int i = 0; i < count; i++)
        {
            if (!FreeTileNear(at.Grid, at.TileX, at.TileY, out int x, out int y)) break;
            AONPCCombatV09 created = SpawnSummoned(spell.summonNpc, x, y, 0);
            if (created == null) break;
            var link = created.gameObject.AddComponent<AONPCSummonLinkV902>();
            link.Configure(npc);
            list.Add(link);
        }
    }

    static bool FreeTileNear(AOGridMap grid, int cx, int cy, out int x, out int y)
    {
        for (int r = 1; r <= 3; r++)
            for (int oy = -r; oy <= r; oy++)
                for (int ox = -r; ox <= r; ox++)
                {
                    if (Mathf.Max(Mathf.Abs(ox), Mathf.Abs(oy)) != r) continue;
                    x = cx + ox; y = cy + oy;
                    if (grid.InBounds(x, y) && grid.CanEnter(x, y, AOGridMap.SOUTH) && !AOInteractionRegistry.IsBlocked(x, y)) return true;
                }
        x = y = 0;
        return false;
    }

    public static bool IsSummonId(int networkId) => networkId >= FirstSummonId;

    // A summoned creature from npc_spells.json, built by the world like any map NPC (networkId 0 = offline).
    public static AONPCCombatV09 SpawnSummoned(int npcIndex, int x, int y, int networkId)
    {
        Load();
        if (!summonable.TryGetValue(npcIndex, out var template)) return null;
        var world = UnityEngine.Object.FindFirstObjectByType<AOWorldManagerV07>();
        if (world == null) return null;
        var entry = JsonUtility.FromJson<AOWorldManagerV07.NPCEntry>(JsonUtility.ToJson(template));
        entry.x = x; entry.y = y;
        GameObject go = world.SpawnSummonedNpc(entry, networkId);
        if (go == null) return null;
        // Some summons only have their textures with the magic data (e.g. the demonic hyena): use those visuals.
        var def = AOSummonDatabaseV129.Get(npcIndex);
        var visual = go.GetComponentInChildren<AOCharacterRenderer>(true);
        if (def != null && visual != null)
        {
            visual.Configure(AOSummonDatabaseV129.BuildVisuals(def), def.walkFps <= 0f ? 18f : def.walkFps,
                def.headOffsetX / 32f, -def.headOffsetY / 32f, def.bodyShiftX / 32f);
            visual.SetHeading(entry.heading <= 0 ? 3 : entry.heading);
        }
        return go.GetComponent<AONPCCombatV09>();
    }

    // GetUserMRForNpc: ResistenciaMagica of armor + ring + shield + helmet (as the PvP formulas of the server).
    public static int MagicResistance(AOInventoryV10 inv)
    {
        if (inv == null) return 0;
        int R(int id) { var item = AOItemDatabaseV10.Get(id); return item == null ? 0 : item.magicResistance; }
        return AOPvpFormulas.MagicResistance(R(inv.EquippedArmor), R(inv.EquippedMagicAccessory), R(inv.EquippedShield), R(inv.EquippedHelmet));
    }
}
