using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AOConsumablesV114Verifier
{
    const string Diagnostic =
        "Assets/AOMigrator/consumables_v114_diagnostic.json";

    [Serializable]
    class Report
    {
        public string version =
            "0.11.4-alpha";

        public string unity;
        public bool success;
        public string message;

        public bool playerFound;
        public bool rpgFound;
        public bool combatFound;
        public bool inventoryFound;

        public bool apple;
        public bool water;
        public bool hpPotion;
        public bool manaPotion;
        public bool staminaPotion;
        public bool strengthPotion;
        public bool agilityPotion;
        public bool fullBottle;

        public int audioFiles;
    }

    [MenuItem(
        "AO Migrador/Verificar consumibles v0.11.4")]
    public static void Verify()
    {
        Report r =
            new Report();

        try
        {
            r.unity =
                Application.unityVersion;

            AOTestPlayer player =
                UnityEngine.Object
                    .FindFirstObjectByType
                        <AOTestPlayer>();

            r.playerFound =
                player != null;

            r.rpgFound =
                player != null &&
                player.GetComponent
                    <AOPlayerRPGV11>() !=
                null;

            r.combatFound =
                player != null &&
                player.GetComponent
                    <AOPlayerCombatV09>() !=
                null;

            r.inventoryFound =
                player != null &&
                player.GetComponent
                    <AOInventoryV10>() !=
                null;

            r.apple =
                ValidConsumable(1);

            r.water =
                ValidConsumable(43);

            r.hpPotion =
                ValidConsumable(38);

            r.manaPotion =
                ValidConsumable(37);

            r.staminaPotion =
                ValidConsumable(169);

            r.strengthPotion =
                ValidConsumable(39);

            r.agilityPotion =
                ValidConsumable(36);

            r.fullBottle =
                ValidConsumable(533);

            string audioPath =
                "Assets/Resources/AOMigrator/ConsumablesV114/Audio";

            r.audioFiles =
                Directory.Exists(audioPath)
                ? Directory.GetFiles(
                    audioPath,
                    "*.wav",
                    SearchOption.TopDirectoryOnly)
                    .Length
                : 0;

            r.success =
                r.playerFound &&
                r.rpgFound &&
                r.combatFound &&
                r.inventoryFound &&
                r.apple &&
                r.water &&
                r.hpPotion &&
                r.manaPotion &&
                r.staminaPotion &&
                r.strengthPotion &&
                r.agilityPotion &&
                r.fullBottle &&
                r.audioFiles >= 3;

            r.message =
                "Player=" +
                r.playerFound +
                " RPG=" +
                r.rpgFound +
                " Combat=" +
                r.combatFound +
                " Inventory=" +
                r.inventoryFound +
                " | Items=" +
                r.apple +
                "/" +
                r.water +
                "/" +
                r.hpPotion +
                "/" +
                r.manaPotion +
                "/" +
                r.staminaPotion +
                "/" +
                r.strengthPotion +
                "/" +
                r.agilityPotion +
                "/" +
                r.fullBottle +
                " | Audio=" +
                r.audioFiles;

            File.WriteAllText(
                Diagnostic,
                JsonUtility.ToJson(
                    r,
                    true));

            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "AO Consumibles v0.11.4",
                (r.success
                    ? "OK\n\n"
                    : "Hay algo para revisar\n\n") +
                r.message +
                "\n\nEn Play: F10 kit, F11 bajar recursos y doble click para consumir.",
                "OK");
        }
        catch (Exception e)
        {
            r.success = false;
            r.message =
                e.ToString();

            File.WriteAllText(
                Diagnostic,
                JsonUtility.ToJson(
                    r,
                    true));

            AssetDatabase.Refresh();

            Debug.LogError(
                "AO v0.11.4 verifier: " +
                e);
        }
    }

    static bool ValidConsumable(
        int id)
    {
        AOItemDatabaseV10.ItemDef item =
            AOItemDatabaseV10.Get(id);

        return item != null &&
            item.Consumable;
    }
}
