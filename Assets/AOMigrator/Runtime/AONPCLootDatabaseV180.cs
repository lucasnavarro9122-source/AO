using System;
using System.Collections.Generic;
using UnityEngine;

public static class AONPCLootDatabaseV180
{
    const string ResourcePath =
        "AOMigrator/LootV180/npc_loot";

    [Serializable]
    public class ItemDrop
    {
        public int itemIndex;
        public int amount;
        public string name;
    }

    [Serializable]
    public class QuestDrop
    {
        public int questId;
        public int itemIndex;
        public int amount;
        public int probabilityDenominator;
        public string name;
    }

    [Serializable]
    public class NPCDef
    {
        public int npcIndex;
        public string name;
        public bool attackable;

        public int giveExp;
        public int giveGold;

        public int respawnMinSeconds;
        public int respawnMaxSeconds;
        public bool respawnDisabled;
        public bool respawnOrigPos;

        public int deathSound;
        public int respawnSound;

        public ItemDrop[] inventoryDrops;

        public int randomDropDenominator;
        public ItemDrop[] randomDrops;

        public QuestDrop[] questDrops;
    }

    [Serializable]
    class Database
    {
        public string version;
        public int goldItemIndex;
        public int dropMultiplier;
        public string sourceMode;
        public NPCDef[] npcs;
    }

    static Database data;

    static readonly Dictionary<int, NPCDef>
        byId =
            new Dictionary<int, NPCDef>();

    public static int Count
    {
        get
        {
            Ensure();
            return byId.Count;
        }
    }

    public static int GoldItemIndex
    {
        get
        {
            Ensure();

            return data == null ||
                data.goldItemIndex <= 0
                ? 12
                : data.goldItemIndex;
        }
    }

    public static NPCDef Get(
        int npcIndex)
    {
        Ensure();

        return byId.TryGetValue(
            npcIndex,
            out NPCDef value)
            ? value
            : null;
    }

    static void Ensure()
    {
        if (data != null)
            return;

        TextAsset asset =
            Resources.Load<TextAsset>(
                ResourcePath);

        if (asset == null)
        {
            Debug.LogError(
                "[AO v0.18] Falta " +
                ResourcePath +
                ".json");
            return;
        }

        data =
            JsonUtility.FromJson<Database>(
                asset.text);

        byId.Clear();

        if (data != null &&
            data.npcs != null)
        {
            foreach (
                NPCDef npc in
                data.npcs)
            {
                if (npc != null &&
                    npc.npcIndex > 0)
                {
                    byId[npc.npcIndex] =
                        npc;
                }
            }
        }

        Debug.Log(
            "[AO v0.18] Loot DB NPCs: " +
            byId.Count);
    }
}
