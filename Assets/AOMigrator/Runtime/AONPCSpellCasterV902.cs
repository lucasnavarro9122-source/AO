using System;
using System.Collections.Generic;
using UnityEngine;

// AONPCSpellCasterV902 (nube, número provisorio): los NPC que lanzan hechizos en el original, sin conexión.
// Reglas compartidas con el servidor: Shared/AONpcSpellRulesV902.cs. Datos: MagicV129/npc_spells.json
// (Tools/export_npc_spells.py, desde npcs.dat). En línea decide el servidor (CoopRoom.NpcMagic.cs) y el cliente solo
// muestra el efecto (AOOnlineClientV240, evento "hurt" con spell).
public static class AONPCSpellCasterV902
{
    const string DataPath = "AOMigrator/MagicV129/npc_spells";

    [Serializable] public class Entry
    {
        public int npcIndex, castIntervalMs, movement;
        public int[] spells;
        public float magicBonus;
        public bool dontHitVisiblePlayers;
    }
    [Serializable] class Database { public string version; public Entry[] npcs; }

    sealed class UnityRoller : AOPvpFormulas.IRandom
    {
        public int Range(int minInclusive, int maxInclusive) => UnityEngine.Random.Range(minInclusive, maxInclusive + 1);
    }

    static readonly UnityRoller roller = new UnityRoller();
    static readonly Dictionary<int, Entry> byNpc = new Dictionary<int, Entry>();
    static readonly Dictionary<int, float> nextCastAt = new Dictionary<int, float>();   // by NPC instance
    static bool loaded;

    public static Entry Get(int npcIndex)
    {
        if (!loaded)
        {
            loaded = true;
            TextAsset asset = Resources.Load<TextAsset>(DataPath);
            Database db = asset == null ? null : JsonUtility.FromJson<Database>(asset.text);
            if (db != null && db.npcs != null)
                foreach (Entry e in db.npcs)
                    if (e != null && e.npcIndex > 0 && e.spells != null && e.spells.Length > 0) byNpc[e.npcIndex] = e;
        }
        return byNpc.TryGetValue(npcIndex, out Entry found) ? found : null;
    }

    // True when the NPC may still melee this turn (AI_AtacarUsuarioObjetivo: If magic ... ElseIf melee).
    public static bool MayMelee(AONPCCombatV09 npc, bool castThisTurn)
    {
        AONPCMetadata meta = npc == null ? null : npc.GetComponent<AONPCMetadata>();
        Entry e = meta == null ? null : Get(meta.NpcIndex);
        if (e == null) return true;   // an NPC without spells always melees (and skips the search below)
        AOPlayerMagicStatusV120 target = castThisTurn || !e.dontHitVisiblePlayers ? null
            : UnityEngine.Object.FindFirstObjectByType<AOPlayerMagicStatusV120>();
        return AONpcSpellRules.MayMelee(true, castThisTurn, e.dontHitVisiblePlayers, target != null && target.IsInvisible);
    }

    // One AI turn against the player at (dx, dy). Returns true when the NPC used the turn on magic.
    public static bool TryCast(AONPCCombatV09 npc, int dx, int dy)
    {
        if (AOOnlineClientV240.Requested || npc == null || !npc.IsAlive) return false;
        AONPCMetadata meta = npc.GetComponent<AONPCMetadata>();
        Entry e = meta == null ? null : Get(meta.NpcIndex);
        if (e == null) return false;

        int key = npc.GetInstanceID();
        if (nextCastAt.TryGetValue(key, out float readyAt) && Time.time < readyAt) return false;
        // IntervaloPermiteLanzarHechizo restarts the timer when it is checked, even if the spell does not go out.
        nextCastAt[key] = Time.time + AONpcSpellRules.CastIntervalMs(e.castIntervalMs) / 1000f;

        AONPCMagicStatusV120 npcStatus = npc.GetComponent<AONPCMagicStatusV120>();
        AOPlayerCombatV09 player = UnityEngine.Object.FindFirstObjectByType<AOPlayerCombatV09>();
        if (player == null || player.IsDead) return true;
        AOPlayerMagicStatusV120 playerStatus = player.GetComponent<AOPlayerMagicStatusV120>();
        if (!AONpcSpellRules.InRange(dx, dy)) return true;

        AOSpellDatabaseV120.SpellDef spell = AOSpellDatabaseV120.Get(e.spells[AONpcSpellRules.PickSlot(roller, e.spells.Length)]);
        if (spell == null) return true;   // empty slot (Sp = 0): the turn is spent
        Vector3 from = npc.transform.position, to = player.transform.position;

        switch (AONpcSpellRules.AimOf(spell.target, spell.autoCast != 0))
        {
            case AONpcSpellRules.Aim.Self:
                if (spell.removeParalysis != 0 && npcStatus != null) npcStatus.RemoveParalysis();
                if (spell.raiseHp == 1) npc.HealMagic(roller.Range(spell.minHp, spell.maxHp));
                AOSpellFXV120.PlayFromNpc(npc.gameObject, spell, from, from);
                return true;
            case AONpcSpellRules.Aim.Player:
                // A paralyzed NPC cannot hurt users (PuedeDanarAlUsuario); invisible users are not valid targets.
                if (npcStatus != null && !npcStatus.CanAttack) return true;
                if (playerStatus != null && playerStatus.IsInvisible) return true;
                break;
            case AONpcSpellRules.Aim.Area:
                if (npcStatus != null && !npcStatus.CanAttack) return true;
                int cx = dx + roller.Range(-AONpcSpellRules.AreaJitter, AONpcSpellRules.AreaJitter);
                int cy = dy + roller.Range(-AONpcSpellRules.AreaJitter, AONpcSpellRules.AreaJitter);
                AOSpellFXV120.PlayFromNpc(npc.gameObject, spell, from, to);
                // Offline there is one user: the square is measured from the NPC (dx, dy are the player's offsets).
                if (!AONpcSpellRules.InArea(cx, cy, spell.areaRadius, dx, dy)) return true;
                Hit(npc, e, spell, player, from, false);
                return true;
            default:
                return true;
        }
        Hit(npc, e, spell, player, from, true);
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

    // GetUserMRForNpc: ResistenciaMagica of armor + ring + shield + helmet (as the PvP formulas of the server).
    public static int MagicResistance(AOInventoryV10 inv)
    {
        if (inv == null) return 0;
        int R(int id) { var item = AOItemDatabaseV10.Get(id); return item == null ? 0 : item.magicResistance; }
        return AOPvpFormulas.MagicResistance(R(inv.EquippedArmor), R(inv.EquippedMagicAccessory), R(inv.EquippedShield), R(inv.EquippedHelmet));
    }
}
