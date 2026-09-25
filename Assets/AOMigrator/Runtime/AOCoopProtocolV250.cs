#nullable disable
using System;

// Shared by Unity and the private .NET room. Fields keep JsonUtility compatible.
[Serializable] public class AOCoopMessage
{
    // 3: the server owns gold (wallet/bank + append-only ledger); gold in snapshots is ignored.
    public const int Protocol = 3;
    public string type, key, characterId, token, name, text, snapshot, request;
    public int version, id, map, x, y, heading, target, item, amount, spell, damage;
    public long ack;
    // Absolute server balances (welcome/result/state); gold = signed amount for "bank" (+ deposit, - withdraw)
    // and the bet of "duelChallenge".
    public long wallet, bank, gold;
    // Server clock in ms (welcome/state/duel messages): the client counts down locally.
    public long serverTime;
    public bool ok;
    public AOCoopPlayer player;
    public AOCoopPlayer[] players;
    public AOCoopNpc[] npcs;
    public AOCoopLoot[] loot;
    public AOCoopDoor[] doors;
    public AOCoopStock[] stocks;
    public AOCoopEvent[] events;
    public AOCoopDuel duel;
    public AOCoopDuel[] duels;
}
[Serializable] public class AOCoopPlayer
{
    public int id, map, x, y, heading, race, gender, head, level, hp, maxHp, mana, maxMana;
    public int weapon, armor, helmet, shield, attack, evasion, defense, minHit, maxHit, strength;
    public float damageModifier = 1;
    // Visual only: 0 = not meditating; castSeq changes on every local cast. castX/castY: aimed tile of a skill shot
    // (0 = a normal cast), so the others can draw the projectile (AOSkillShotProjectileV267.LaunchVisual).
    public int meditationFx, castSpell, castSeq, castX, castY;
    // Duels: arena = room (0 = none), team 0/1; magicDefense reported by the client (clamped by the server).
    public int arena, team, magicDefense;
    public bool paralyzed, immobile;
    public string name;
    public bool dead;
    public AOCoopPet[] pets;
}
[Serializable] public class AOCoopPet { public int id, npc, x, y, heading; }
[Serializable] public class AOCoopNpc
{
    public int id, npc, x, y, heading, hp, maxHp, target;
    public bool dead;
}
[Serializable] public class AOCoopLoot { public int id, item, amount, x, y; public string name; }
[Serializable] public class AOCoopDoor { public int x, y; public bool open; }
[Serializable] public class AOCoopItem { public int item, amount, quest; }
[Serializable] public class AOCoopEvent
{
    public long seq;
    public string type, text;
    public int exp, npc, item, amount, damage, spell;
    public long gold;
    // "warp" (map, x, y) and "duelEnd" (duel) are journal events: they survive a disconnection.
    public int map, x, y, hp;
    public AOCoopItem[] items;
    public AOCoopDuel duel;
}

[Serializable] public class AOCoopStock { public int npc,item,amount; }

// One challenge (ModRetos.bas). teamA = the challenger's team, teamB = the rivals.
// phase: invite, queued, countdown, fight, done (libre = free ring in duelRingState).
// result (duelRoundEnd/duelEnd, for the receiver): victoria, derrota, empate, tiempo, abandono.
// team = the receiver's team (0 = A, 1 = B); winner = winning team (-1 = none); winsA/winsB = rounds won
// (score = winsA − winsB).
[Serializable] public class AOCoopDuel
{
    public string id, phase, from, reason, result;
    public string[] teamA, teamB, missing;
    public int[] idsA, idsB;
    public int sala, map, x, y, width, height, theme, seed, genVersion, round, score, winsA, winsB, maxPotions, level, team, winner, hp, mana;
    public long bet, prize, tax, serverTime, startsAt, endsAt, expiresAt;
}
