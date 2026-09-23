using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class AOInitialLoadoutV170
{
    const string ResourcePath =
        "AOMigrator/CharacterCreationV170/starter_loadout";

    [Serializable]
    public class ItemEntry
    {
        public int itemIndex;
        public int amount;
        public bool equip;
    }

    [Serializable]
    public class ClassLoadout
    {
        public int classId;
        public ItemEntry[] items;
        public int[] spells;
    }

    [Serializable]
    class Data
    {
        public string version;
        public string source;
        public ItemEntry[] common;
        public ClassLoadout[] classes;
    }

    static Data data;

    public static bool Apply(
        int classId,
        AOInventoryV10 inventory,
        AOPlayerMagicV120 magic,
        out string message)
    {
        message = "";

        Ensure();

        if (data == null ||
            inventory == null ||
            magic == null)
        {
            message =
                "No pude cargar el equipamiento inicial.";
            return false;
        }

        ClassLoadout loadout =
            GetClassLoadout(
                classId);

        if (loadout == null)
        {
            message =
                "No existe loadout para la clase " +
                classId +
                ".";
            return false;
        }

        List<ItemEntry> entries =
            new List<ItemEntry>();

        if (data.common != null)
        {
            entries.AddRange(
                data.common);
        }

        if (loadout.items != null)
        {
            entries.AddRange(
                loadout.items);
        }

        if (entries.Count >
            inventory.SlotCount)
        {
            message =
                "El loadout inicial supera los slots de inventario.";
            return false;
        }

        int[] itemIndices =
            new int[
                inventory.SlotCount];

        int[] amounts =
            new int[
                inventory.SlotCount];

        int weapon = 0;
        int armor = 0;
        int shield = 0;
        int helmet = 0;
        int amulet = 0;
        int magicAccessory = 0;

        for (int i = 0;
             i < entries.Count;
             i++)
        {
            ItemEntry entry =
                entries[i];

            if (entry == null ||
                entry.itemIndex <= 0 ||
                entry.amount <= 0)
                continue;

            AOItemDatabaseV10.ItemDef item =
                AOItemDatabaseV10.Get(
                    entry.itemIndex);

            if (item == null)
            {
                message =
                    "Falta OBJ " +
                    entry.itemIndex +
                    " del equipamiento inicial.";
                return false;
            }

            itemIndices[i] =
                entry.itemIndex;

            amounts[i] =
                entry.amount;

            if (!entry.equip)
                continue;

            switch (item.objType)
            {
                case 2:
                    weapon =
                        item.index;
                    break;

                case 3:
                    armor =
                        item.index;
                    break;

                case 16:
                    shield =
                        item.index;
                    break;

                case 17:
                    helmet =
                        item.index;
                    break;

                case 21:
                    amulet =
                        item.index;
                    break;

                case 30:
                case 35:
                    magicAccessory =
                        item.index;
                    break;
            }
        }

        inventory.RestoreSaveState(
            itemIndices,
            amounts,
            weapon,
            armor,
            shield,
            helmet,
            amulet,
            magicAccessory);

        int[] spells =
            loadout.spells == null
            ? new int[0]
            : loadout.spells;

        magic.RestoreSpellbookForSave(
            spells,
            spells.Length > 0
                ? spells[0]
                : 0);

        message =
            "Equipamiento inicial aplicado.";

        return true;
    }

    public static string Describe(
        int classId)
    {
        Ensure();

        ClassLoadout loadout =
            GetClassLoadout(
                classId);

        if (loadout == null)
            return "";

        StringBuilder sb =
            new StringBuilder();

        if (loadout.items != null)
        {
            foreach (
                ItemEntry entry in
                loadout.items)
            {
                if (entry == null)
                    continue;

                AOItemDatabaseV10.ItemDef item =
                    AOItemDatabaseV10.Get(
                        entry.itemIndex);

                if (item == null)
                    continue;

                if (item.objType == 2 ||
                    item.objType == 3 ||
                    item.objType == 16 ||
                    item.objType == 17 ||
                    item.objType == 30 ||
                    item.objType == 35)
                {
                    sb.Append(
                        item.name);

                    if (entry.amount > 1)
                    {
                        sb.Append(
                            " x" +
                            entry.amount);
                    }

                    sb.AppendLine();
                }
            }
        }

        if (loadout.spells != null &&
            loadout.spells.Length > 0)
        {
            sb.AppendLine(
                "Hechizos:");

            foreach (
                int spellId in
                loadout.spells)
            {
                AOSpellDatabaseV120.SpellDef spell =
                    AOSpellDatabaseV120.Get(
                        spellId);

                sb.AppendLine(
                    "• " +
                    (spell == null
                        ? "Spell " +
                          spellId
                        : spell.name));
            }
        }

        return sb.ToString().TrimEnd();
    }

    static ClassLoadout GetClassLoadout(
        int classId)
    {
        Ensure();

        if (data == null ||
            data.classes == null)
            return null;

        foreach (
            ClassLoadout loadout in
            data.classes)
        {
            if (loadout != null &&
                loadout.classId ==
                    classId)
            {
                return loadout;
            }
        }

        return null;
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
                "[AO v0.17] Falta " +
                ResourcePath +
                ".json");
            return;
        }

        data =
            JsonUtility.FromJson<Data>(
                asset.text);
    }
}
