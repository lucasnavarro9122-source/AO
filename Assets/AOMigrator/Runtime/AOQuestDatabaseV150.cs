using System;
using System.Collections.Generic;
using UnityEngine;

public static class AOQuestDatabaseV150
{
    const string ResourcePath =
        "AOMigrator/QuestsV150/quests";

    [Serializable]
    public class Requirement
    {
        public int index;
        public int amount;
    }

    [Serializable]
    public class QuestDef
    {
        public int id;
        public string name;
        public string description;
        public string finalDescription;

        public bool repeatable;

        public int requiredLevel;
        public int limitLevel;

        public int[] requiredClasses;
        public bool workerOnly;
        public int requiredQuest;

        public Requirement[] requiredObjects;
        public Requirement[] requiredNpcs;
        public Requirement[] requiredTargetNpcs;

        public int[] requiredSpells;

        public int requiredSkillIndex;
        public int requiredSkillValue;

        public int rewardExp;
        public int rewardGold;

        public Requirement[] rewardObjects;
        public int[] rewardSpells;

        public int nextQuest;
        public int positionMap;
        public int talkTo;

        public int permittedFactions;

        public int globalQuestIndex;
        public int globalQuestThresholdNeeded;

        public int descriptionAudio;
        public int finalAudio;
    }

    [Serializable]
    class Database
    {
        public string version;
        public int declaredQuests;
        public int definedQuests;
        public QuestDef[] quests;
    }

    static Database data;

    static readonly Dictionary<int, QuestDef>
        byId =
            new Dictionary<int, QuestDef>();

    static readonly List<QuestDef>
        ordered =
            new List<QuestDef>();

    public static int Count
    {
        get
        {
            Ensure();
            return ordered.Count;
        }
    }

    public static int DeclaredCount
    {
        get
        {
            Ensure();

            return data == null
                ? 0
                : data.declaredQuests;
        }
    }

    public static QuestDef Get(
        int id)
    {
        Ensure();

        return byId.TryGetValue(
            id,
            out QuestDef value)
            ? value
            : null;
    }

    public static QuestDef GetAt(
        int index)
    {
        Ensure();

        if (index < 0 ||
            index >= ordered.Count)
            return null;

        return ordered[index];
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
                "[AO v0.15] Falta " +
                ResourcePath +
                ".json");

            return;
        }

        data =
            JsonUtility.FromJson<Database>(
                asset.text);

        byId.Clear();
        ordered.Clear();

        if (data != null &&
            data.quests != null)
        {
            foreach (
                QuestDef quest in
                data.quests)
            {
                if (quest == null ||
                    quest.id <= 0)
                    continue;

                byId[quest.id] =
                    quest;

                ordered.Add(
                    quest);
            }
        }

        ordered.Sort(
            (a, b) =>
                a.id.CompareTo(
                    b.id));

        Debug.Log(
            "[AO v0.15] Quests cargadas: " +
            ordered.Count +
            "/" +
            (data == null
                ? 0
                : data.declaredQuests));
    }
}
