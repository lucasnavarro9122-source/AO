#nullable disable
// AOPvpFormulasV276: original AO20 player-vs-player formulas (docs/claude/demo/pvp-formulas.md, Contenido):
// SistemaCombate.bas (UsuarioImpacto, UserDamageToUser, AttackPower, PoderEvasion) and
// modHechizos.bas (HechizoPropUsuario, eDoDamage), Modulo_UsUaRiOs.bas (GetUserMR).
// Shared source: OnlineServer rolls the dice (authoritative); the Unity client only uses it for display.
// No UnityEngine. VB6 rounds to even when assigning to Long/Integer: Math.Round (ToEven) does the same.
// Not included (0 with the public data): backstab, critical hit, disarm, armor penetration. Area spells: pending.
using System;

public static class AOPvpFormulas
{
    // RandomNumber(a, b) of the original: integer, both ends included.
    public interface IRandom
    {
        int Range(int minInclusive, int maxInclusive);
    }

    // intervalos.ini (ms)
    public const int IntervalMeleeMs = 1165;
    public const int IntervalArrowMs = 1200;
    public const int IntervalSpellMs = 1230;
    public const int IntervalMeleeToSpellMs = 800;
    public const int IntervalSpellToMeleeMs = 800;
    public const int IntervalMeleeToUseMs = 800;
    public const int IntervalPotionKeyMs = 380;
    public const int IntervalPotionClickMs = 276;

    public const int HitChanceMin = 5;
    public const int HitChanceMax = 95;
    public const int BodyPlaces = 8;          // PvP: 1 = head, 2..8 = body

    public static long VbRound(double value) => (long)Math.Round(value, MidpointRounding.ToEven);

    static double Percent(double total, double percent) => total * percent / 100.0;

    // AttackPower: (skill + 3% x skill x AGI) x class modifier + 2.5 x max(level - 12, 0) + effect bonus.
    public static double AttackPower(int weaponSkill, int agility, double classAttackModifier, int level, int effectBonus = 0) =>
        (weaponSkill + Percent(weaponSkill * 3, agility)) * classAttackModifier + 2.5 * Math.Max(level - 12, 0) + effectBonus;

    // PoderEvasion, plus the shield bonus when the victim carries a shield with Porcentaje > 0.
    public static double Evasion(int tactics, int agility, double classEvasionModifier, int level, int effectBonus = 0,
        int shieldDefenseSkill = 0, double classShieldModifier = 0, int shieldPercent = 0)
    {
        double evasion = (tactics + Percent(tactics * 3, agility)) * classEvasionModifier + 2.5 * Math.Max(level - 12, 0) + effectBonus;
        if (shieldPercent > 0)
            evasion += shieldDefenseSkill * classShieldModifier / 2.0 * shieldPercent / 100.0;
        return evasion;
    }

    // UsuarioImpacto. Any attack cuts the victim's meditation before this roll, so the meditation branch never applies.
    public static int HitChance(double attackPower, double evasion) =>
        (int)Math.Max(HitChanceMin, Math.Min(HitChanceMax, VbRound(50 + (attackPower - evasion) * 0.4)));

    public static bool RollHit(IRandom random, double attackPower, double evasion, bool guaranteedHit = false) =>
        guaranteedHit || random.Range(1, 100) <= HitChance(attackPower, evasion);

    public struct MeleeAttacker
    {
        public int MinHit, MaxHit;                // own hit: (level-1) x GOLPE_PRE_36 + 1 / + 2
        public int WeaponMinHit, WeaponMaxHit;    // MinHit/MaxHit of the weapon (not the ToNPC values); 0 without weapon
        public bool Projectile;                   // bow with ammunition
        public int ArrowMinHit, ArrowMaxHit;
        public int Strength;
        public double ClassDamageModifier;        // MODDANOPROYECTILES | MODDANOWRESTLING | MODDANOARMAS
        public double LinearBonus;                // effects
        public double PhysicalModifier;           // 1 without effects
    }

    public struct MeleeVictim
    {
        public int HelmetMinDef, HelmetMaxDef;
        public int ArmorMinDef, ArmorMaxDef;
        public bool HasShield;
        public int ShieldMinDef, ShieldMaxDef;
        public int DefenseBonus;                  // effects
        public double PhysicalReduction;          // 1 without effects
    }

    // UserDamageToUser. Roll order (fixed, for the golden tests): own hit, weapon, arrow, body place, helmet | armor + shield.
    public static long MeleeDamage(IRandom random, MeleeAttacker a, MeleeVictim v)
    {
        int ownHit = random.Range(a.MinHit, a.MaxHit);
        int weapon = a.WeaponMaxHit > 0 ? random.Range(a.WeaponMinHit, a.WeaponMaxHit) : 0;
        int maxWeapon = a.WeaponMaxHit;
        if (a.Projectile && a.ArrowMaxHit > 0)
        {
            weapon += random.Range(a.ArrowMinHit, a.ArrowMaxHit);
            maxWeapon += a.ArrowMaxHit;
        }
        double baseDamage = (3.0 * weapon + maxWeapon * 0.2 * Math.Max(0, a.Strength - 15) + ownHit) * a.ClassDamageModifier + a.LinearBonus;

        int place = random.Range(1, BodyPlaces);
        int defense = place == 1
            ? RollDefense(random, v.HelmetMinDef, v.HelmetMaxDef)
            : RollDefense(random, v.ArmorMinDef, v.ArmorMaxDef) + (v.HasShield ? RollDefense(random, v.ShieldMinDef, v.ShieldMaxDef) : 0);
        defense += v.DefenseBonus;

        double damage = Math.Max(0.0, baseDamage - defense) * Or1(a.PhysicalModifier) * Or1(v.PhysicalReduction);
        return VbRound(damage);
    }

    static int RollDefense(IRandom random, int min, int max) => max > 0 ? random.Range(min, max) : 0;

    static double Or1(double value) => value == 0 ? 1.0 : value;

    public struct MagicItem
    {
        public int DamageBonusPercent;   // MagicDamageBonus
        public int AbsoluteBonus;        // MagicAbsoluteBonus
        public int Penetration;          // MagicPenetration
    }

    // HechizoPropUsuario (eDoDamage), single target. Items in the original order: weapon (staff), amulet, ring.
    public static long SpellDamage(IRandom random, int spellMinHp, int spellMaxHp, int casterLevel,
        MagicItem weapon, MagicItem amulet, MagicItem ring, bool antiRm, int victimMagicResistance,
        double casterMagicModifier = 1, double victimMagicReduction = 1)
    {
        long damage = random.Range(spellMinHp, spellMaxHp);
        damage = VbRound(damage + Percent(damage, 3 * casterLevel));
        int penetration = 0;
        foreach (MagicItem item in new[] { weapon, amulet, ring })
        {
            damage = VbRound(damage + Percent(damage, item.DamageBonusPercent));
            damage += item.AbsoluteBonus;
            penetration += item.Penetration;
        }
        if (!antiRm)
        {
            int resistance = Math.Max(0, victimMagicResistance - penetration);
            damage = VbRound(damage - Percent(damage, resistance));
        }
        damage = VbRound(damage * Or1(casterMagicModifier) * Or1(victimMagicReduction));
        return Math.Max(0, damage);
    }

    // GetUserMR ("magicDefense"): ResistenciaMagica of armor + ring + shield + helmet, + skill x modifier (0 with
    // public data), + 100 x MODRESISTENCIAMAGICA of the class (0 for warrior, mage, cleric and hunter).
    public static int MagicResistance(int armor, int ring, int shield, int helmet, int skillPart = 0, double classModifier = 0) =>
        armor + ring + shield + helmet + skillPart + (int)VbRound(100 * classModifier);
}
