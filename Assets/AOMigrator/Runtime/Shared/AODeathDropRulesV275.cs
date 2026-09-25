#nullable disable
// AODeathDropRulesV275: what a character loses when dying in the demo dungeon (decision 18,
// docs/claude/contenido/muerte-reglas.md; AO20 InvUsuario.ItemSeCae / TirarTodosLosItems, UserDie).
// Shared source: Unity client (offline demo) and OnlineServer (online). No UnityEngine, no float.
// Only maps with "dropOnDeath" (demo floors 1011-1017) use it; nothing falls on a fight-zone tile (trigger 6).
using System;

public static class AODeathDropRules
{
    public const long GoldProtectedPerLevel = 1000;   // OroPorNivelBilletera (Configuracion.ini)
    public const int NewbieMaxLevel = 12;             // LimiteNewbie: newbie items are kept up to this level
    public const int FightZoneTrigger = 6;            // ZONAPELEA
    public const int ObjTypeKeys = 9, ObjTypeShips = 31, ObjTypeSaddles = 44;

    [Flags]
    public enum ItemFlags
    {
        None = 0,
        NoSeCae = 1,
        Intirable = 2,
        Destruye = 4,
        Instransferible = 8,
        Newbie = 16,
    }

    // From the items.json / catalog fields: noSeCae, intirable, destroyOnSell (= Destruye), untransferable (= Instransferible), newbie.
    public static ItemFlags FlagsFrom(bool noSeCae, bool intirable, bool destroyOnSell, bool untransferable, bool newbie) =>
        (noSeCae ? ItemFlags.NoSeCae : 0) | (intirable ? ItemFlags.Intirable : 0) | (destroyOnSell ? ItemFlags.Destruye : 0) |
        (untransferable ? ItemFlags.Instransferible : 0) | (newbie ? ItemFlags.Newbie : 0);

    public static bool DropsOnTile(bool mapDropsOnDeath, int trigger) =>
        mapDropsOnDeath && trigger != FightZoneTrigger;

    // Gold that goes to the floor: everything above 1.000 per level.
    public static long GoldToDrop(long gold, int level)
    {
        if (gold <= 0) return 0;
        long protectedGold = GoldProtectedPerLevel * Math.Max(0, level);
        return gold > protectedGold ? gold - protectedGold : 0;
    }

    // Whole stack, equipped items included (DropAmmount). Same conditions as ItemSeCae plus the newbie protection.
    public static bool ItemFalls(int objType, ItemFlags flags, int level)
    {
        if (objType == ObjTypeKeys || objType == ObjTypeShips || objType == ObjTypeSaddles) return false;
        if ((flags & (ItemFlags.NoSeCae | ItemFlags.Intirable | ItemFlags.Destruye | ItemFlags.Instransferible)) != 0) return false;
        if ((flags & ItemFlags.Newbie) != 0 && level <= NewbieMaxLevel) return false;
        return true;
    }
}
