using System;
using System.Collections.Generic;
using UnityEngine;

public static class AOCityNPCDatabaseV130
{
    const string ResourcePath =
        "AOMigrator/CityV130/city_npcs";

    [Serializable]
    public class ShopEntry
    {
        public int itemIndex;
        public int amount;
        public bool infinite;
    }

    [Serializable]
    public class NPCDef
    {
        public int npcIndex;
        public string name;
        public string description;
        public int npcType;
        public bool trades;
        public int itemType;
        public int craftType;
        public int soundOpen;
        public int soundClose;
        public ShopEntry[] stock;

        // v0.15: misiones ofrecidas por este NPC.
        public int[] questNumbers;
    }

    [Serializable]
    class Database
    {
        public string version;
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
                "[AO v0.13] Falta " +
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
            "[AO v0.13] NPC city DB: " +
            byId.Count);
    }
}
