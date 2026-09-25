#nullable disable
// AONpcSpellRulesV902 (nube, número provisorio): IA mágica clásica de los NPC del original AO20 (ao-org server 08a5711):
// AI_AtacarUsuarioObjetivo, NpcLanzaUnSpell, NpcLanzaSpellSobreUser y NpcLanzaSpellSobreArea
// (docs/claude/contenido/npc_hechizos_original.md). Datos por NPC: MagicV129/npc_spells.json (Tools/export_npc_spells.py).
// Shared source: OnlineServer rolls the dice online; the Unity client uses the same rules offline. No UnityEngine.
// Not included: the Support/BG AI (Movement 11/13: heal allies first), summons (Invoca) and the NPC level filter.
using System;

public static class AONpcSpellRules
{
    public const int RangeX = 11, RangeY = 9;            // NPCSpellRangeX/Y (Example.Configuracion.ini [AI])
    public const int DefaultCastIntervalMs = 8000;       // IntervaloLanzarHechizo missing (MODULO_NPCs.bas:1506-1508)
    public const int AreaJitter = 2;                     // NpcLanzaSpellSobreArea: target position + RandomNumber(-2, 2)

    public enum Aim { None, Self, Player, Area }

    public static int CastIntervalMs(int fromData) => fromData > 0 ? fromData : DefaultCastIntervalMs;

    // NpcLanzaUnSpell (AI_NPC.bas:1297-1298): Abs(dx) <= 11 and Abs(dy) <= 9 against the target user.
    public static bool InRange(int dx, int dy) => Math.Abs(dx) <= RangeX && Math.Abs(dy) <= RangeY;

    // RandomNumber(1, LanzaSpells) (AI_NPC.bas:1299): uniform over the slots; repeated ids weigh more. 0-based slot.
    public static int PickSlot(AOPvpFormulas.IRandom random, int slots) => slots <= 1 ? 0 : random.Range(1, slots) - 1;

    // Target of the spell (AI_NPC.bas:1301-1330): 1 users; 2 NPC (itself only with AutoLanzar); 3 users and NPC
    // (itself with AutoLanzar, else the target user); 4 terrain (area around the target).
    public static Aim AimOf(int spellTarget, bool autoCast)
    {
        switch (spellTarget)
        {
            case 1: return Aim.Player;
            case 2: return autoCast ? Aim.Self : Aim.None;
            case 3: return autoCast ? Aim.Self : Aim.Player;
            case 4: return Aim.Area;
            default: return Aim.None;
        }
    }

    // AI_AtacarUsuarioObjetivo (AI_NPC.bas:721-726): magic has priority. Melee only when the NPC did not cast this turn,
    // is next to the target and (the target is hidden, or Magic_and_Punch = 1 and it lacks DontHitVisiblePlayers).
    // An NPC without spells always melees. The adjacency check stays with the caller.
    public static bool MayMelee(bool hasSpells, bool castThisTurn, bool dontHitVisiblePlayers, bool targetHidden) =>
        !castThisTurn && (!hasSpells || targetHidden || !dontHitVisiblePlayers);

    // NpcLanzaSpellSobreArea (modHechizos.bas:357-375): For x = 1 To AreaRadio, cell = center + x - CInt(AreaRadio / 2).
    public static bool InArea(int centerX, int centerY, int areaRadius, int x, int y)
    {
        if (areaRadius <= 0) return x == centerX && y == centerY;
        int half = (int)AOPvpFormulas.VbRound(areaRadius / 2.0);
        int ox = x - centerX + half, oy = y - centerY + half;
        return ox >= 1 && ox <= areaRadius && oy >= 1 && oy <= areaRadius;
    }

    // Integer assignment in VB6 rounds to even.
    static int VbInt(double value) => (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, AOPvpFormulas.VbRound(value)));

    // NpcLanzaSpellSobreUser, eDoDamage (modHechizos.bas:62-87): RandomNumber(MinHp, MaxHp) x (1 + MagicBonus),
    // minus Porcentaje(Damage, GetUserMRForNpc) unless AntiRm; never below 0. Modifiers without data (1) are left out.
    public static int Damage(AOPvpFormulas.IRandom random, int minHp, int maxHp, double magicBonus, int victimMagicResistance, bool antiRm)
    {
        int damage = VbInt(random.Range(Math.Min(minHp, maxHp), Math.Max(minHp, maxHp)) * (1 + magicBonus));
        if (!antiRm) damage = VbInt(damage - damage * (double)victimMagicResistance / 100.0);
        return Math.Max(0, damage);
    }

    // Paralysis / immobilization on a user last Duration / 2 (Counters.Paralisis = Hechizos(Spell).Duration / 2).
    public static double StatusSeconds(int duration) => Math.Max(1, duration) / 2.0;
}
