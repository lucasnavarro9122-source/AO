using System;
using System.Collections.Generic;
using UnityEngine;

public static class AORPGDatabaseV11
{
    const string ResourcePath =
        "AOMigrator/RPGV11/rpg_balance";

    [Serializable]
    public class ClassDef
    {
        public int id;
        public string name;
        public float evasion;
        public float attackWeapons;
        public float attackProjectiles;
        public float damageWeapons;
        public float damageProjectiles;
        public float damageWrestling;
        public float shield;
        public float life;
        public float initialMana;
        public float multiMana;
        public int staminaPerLevel;
        public int hitPre36;
        public int hitPost36;
        public int skillPointsPerLevel;
    }

    [Serializable]
    public class RaceDef
    {
        public int id;
        public string name;
        public int strength;
        public int agility;
        public int intelligence;
        public int constitution;
        public int charisma;
    }

    [Serializable]
    public class SkillDef
    {
        public int id;
        public string name;
    }

    [Serializable]
    public class ExpDef
    {
        public int level;
        public long required;
    }

    [Serializable]
    class BalanceData
    {
        public string version;
        public int baseAttribute;
        public int initialSkillPoints;
        public int maxSkill;
        public int maxLevel;
        public int newbieLimit;
        public float lifeRange;
        public float lifeCapMax;
        public float lifeCapMin;
        public ClassDef[] classes;
        public RaceDef[] races;
        public SkillDef[] skills;
        public ExpDef[] experience;
    }

    static BalanceData data;
    static readonly Dictionary<int, ClassDef> classes =
        new Dictionary<int, ClassDef>();
    static readonly Dictionary<int, RaceDef> races =
        new Dictionary<int, RaceDef>();
    static readonly Dictionary<int, SkillDef> skills =
        new Dictionary<int, SkillDef>();
    static readonly Dictionary<int, long> experience =
        new Dictionary<int, long>();

    public static int BaseAttribute
    {
        get
        {
            Ensure();
            return data == null ? 18 : data.baseAttribute;
        }
    }

    public static int InitialSkillPoints
    {
        get
        {
            Ensure();
            return data == null ? 10 : data.initialSkillPoints;
        }
    }

    public static int MaxSkill
    {
        get
        {
            Ensure();
            return data == null ? 100 : data.maxSkill;
        }
    }

    public static int MaxLevel
    {
        get
        {
            Ensure();
            return data == null ? 47 : data.maxLevel;
        }
    }

    public static float LifeRange
    {
        get
        {
            Ensure();
            return data == null ? 2f : data.lifeRange;
        }
    }

    public static float LifeCapMax
    {
        get
        {
            Ensure();
            return data == null ? 10f : data.lifeCapMax;
        }
    }

    public static float LifeCapMin
    {
        get
        {
            Ensure();
            return data == null ? -10f : data.lifeCapMin;
        }
    }

    public static ClassDef GetClass(int id)
    {
        Ensure();
        return classes.TryGetValue(
            id,
            out ClassDef value)
            ? value
            : null;
    }

    public static RaceDef GetRace(int id)
    {
        Ensure();
        return races.TryGetValue(
            id,
            out RaceDef value)
            ? value
            : null;
    }

    public static SkillDef GetSkill(int id)
    {
        Ensure();
        return skills.TryGetValue(
            id,
            out SkillDef value)
            ? value
            : null;
    }

    public static long ExpForLevel(int level)
    {
        Ensure();
        return experience.TryGetValue(
            level,
            out long value)
            ? value
            : 0;
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
                "[AO v0.11] Falta " +
                ResourcePath + ".json");
            return;
        }

        data =
            JsonUtility.FromJson<BalanceData>(
                asset.text);

        classes.Clear();
        races.Clear();
        skills.Clear();
        experience.Clear();

        if (data.classes != null)
        {
            foreach (ClassDef entry in data.classes)
                classes[entry.id] = entry;
        }

        if (data.races != null)
        {
            foreach (RaceDef entry in data.races)
                races[entry.id] = entry;
        }

        if (data.skills != null)
        {
            foreach (SkillDef entry in data.skills)
                skills[entry.id] = entry;
        }

        if (data.experience != null)
        {
            foreach (ExpDef entry in data.experience)
                experience[entry.level] = entry.required;
        }
    }
}
