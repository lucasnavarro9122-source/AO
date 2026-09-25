#nullable disable
// AODemoRatesV273: EXP and gold rates and NPC respawn rule of the AO BATTLESERVER demo (docs/claude/demo/decisiones.md #1 and #2,
// progresion.md §6). Shared source: Unity client (offline demo) and OnlineServer (demo rules only).
// Outside the demo nothing changes: callers must check that demo rules are active before applying these.
// No UnityEngine, no float.
using System;

public static class AODemoRates
{
    // OroMult of the original server config: multiplies the gold dropped by NPCs.
    public const int GoldMultiplier = 2;

    // EXP multiplier by level tier of the player who receives the EXP: 1–9 ×1, 10–14 ×2, 15–19 ×7, 20–23 ×17, 24–26 ×30, 27+ ×44.
    static readonly int[] TierMinLevel = { 1, 10, 15, 20, 24, 27 };
    static readonly int[] TierMultiplier = { 1, 2, 7, 17, 30, 44 };

    public static int ExpMultiplier(int level)
    {
        int multiplier = 1;
        for (int i = 0; i < TierMinLevel.Length; i++)
            if (level >= TierMinLevel[i]) multiplier = TierMultiplier[i];
        return multiplier;
    }

    public static long ApplyExp(long baseExp, int level)
    {
        if (baseExp <= 0) return baseExp;
        int multiplier = ExpMultiplier(level);
        return baseExp > long.MaxValue / multiplier ? long.MaxValue : baseExp * multiplier;
    }

    // NPC respawn, same rule online and offline (Cerebro 24/09): demo maps (>= 1000) use the map times
    // (Contenido's design for farming); the normal game (< 1000) uses the original npcs.dat IntervaloRespawnMin/IntervaloRespawn.
    // max <= 0 means the original instant respawn.
    public const int DemoMinMap = 1000;

    public static void RespawnRange(int mapNumber, int mapMinSeconds, int mapMaxSeconds, int datMinSeconds, int datMaxSeconds,
        out int minSeconds, out int maxSeconds)
    {
        bool useMap = mapNumber >= DemoMinMap && mapMaxSeconds > 0;
        minSeconds = Math.Max(0, useMap ? mapMinSeconds : datMinSeconds);
        maxSeconds = Math.Max(minSeconds, useMap ? mapMaxSeconds : datMaxSeconds);
    }

    public static long ApplyGold(long baseGold)
    {
        if (baseGold <= 0) return baseGold;
        return baseGold > long.MaxValue / GoldMultiplier ? long.MaxValue : baseGold * GoldMultiplier;
    }
}
