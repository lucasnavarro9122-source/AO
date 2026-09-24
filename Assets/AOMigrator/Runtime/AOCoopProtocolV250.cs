#nullable disable
using System;

// Shared by Unity and the private .NET room. Fields keep JsonUtility compatible.
[Serializable] public class AOCoopMessage
{
    public const int Protocol = 2;
    public string type, key, characterId, token, name, text, snapshot, request;
    public int version, id, map, x, y, heading, target, item, amount, spell, damage;
    public long ack;
    public bool ok;
    public AOCoopPlayer player;
    public AOCoopPlayer[] players;
    public AOCoopNpc[] npcs;
    public AOCoopLoot[] loot;
    public AOCoopDoor[] doors;
    public AOCoopStock[] stocks;
    public AOCoopEvent[] events;
}
[Serializable] public class AOCoopPlayer
{
    public int id, map, x, y, heading, race, gender, head, level, hp, maxHp, mana, maxMana;
    public int weapon, armor, helmet, shield, attack, evasion, defense, minHit, maxHit, strength;
    public float damageModifier = 1;
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
    public AOCoopItem[] items;
}

[Serializable] public class AOCoopStock { public int npc,item,amount; }
