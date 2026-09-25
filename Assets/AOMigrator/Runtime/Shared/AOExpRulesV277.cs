#nullable disable
// AOExpRulesV277: original AO20 experience rules shared by the Unity client (offline) and OnlineServer (online kills).
// SistemaCombate.bas GetExpPenalty + Example.Configuracion.ini (NpcDeltaLevelPenalties=4, PenaltyExpUserPerLevel=0.05).
// No UnityEngine.
using System;

public static class AOExpRules
{
    public const int NpcDeltaLevelPenalties = 4;
    public const double PenaltyExpUserPerLevel = 0.05;

    // Multiplier 1..0 when the player is more than 4 levels above the NPC. NPC level 0 = no penalty (original: If nivel Then).
    public static double NpcLevelPenalty(int userLevel, int npcLevel)
    {
        if (npcLevel <= 0) return 1.0;
        int delta = userLevel - npcLevel;
        if (delta <= NpcDeltaLevelPenalties) return 1.0;
        return Math.Max(0.0, 1.0 - PenaltyExpUserPerLevel * (delta - NpcDeltaLevelPenalties));
    }

    // ExpaDar = ExpaDar * Penalty (Long: VB6 rounds to even).
    public static long ApplyNpcLevelPenalty(long exp, int userLevel, int npcLevel)
    {
        if (exp <= 0) return exp;
        return (long)Math.Round(exp * NpcLevelPenalty(userLevel, npcLevel), MidpointRounding.ToEven);
    }
}
