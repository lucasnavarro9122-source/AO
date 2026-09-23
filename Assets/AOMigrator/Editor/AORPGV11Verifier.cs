using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AORPGV11Verifier
{
    const string Diagnostic =
        "Assets/AOMigrator/rpg_v11_diagnostic.json";

    [Serializable]
    class Report
    {
        public string version =
            "0.11.0-alpha";
        public string unity;
        public bool success;
        public string message;
        public bool playerFound;
        public bool rpgFound;
        public bool combatFound;
        public bool inventoryFound;
        public bool balanceFound;
        public bool itemsFound;
        public int itemDatabaseBytes;
    }

    [MenuItem(
        "AO Migrador/Verificar personaje RPG v0.11")]
    public static void Verify()
    {
        Report report =
            new Report();

        try
        {
            report.unity =
                Application.unityVersion;

            AOTestPlayer player =
                UnityEngine.Object
                    .FindFirstObjectByType
                        <AOTestPlayer>();

            report.playerFound =
                player != null;

            report.rpgFound =
                player != null &&
                player.GetComponent
                    <AOPlayerRPGV11>() !=
                null;

            report.combatFound =
                player != null &&
                player.GetComponent
                    <AOPlayerCombatV09>() !=
                null;

            report.inventoryFound =
                player != null &&
                player.GetComponent
                    <AOInventoryV10>() !=
                null;

            string balance =
                "Assets/Resources/AOMigrator/RPGV11/rpg_balance.json";

            string items =
                "Assets/Resources/AOMigrator/ItemsV10/items.json";

            report.balanceFound =
                File.Exists(balance);

            report.itemsFound =
                File.Exists(items);

            report.itemDatabaseBytes =
                report.itemsFound
                ? (int)new FileInfo(items).Length
                : 0;

            report.success =
                report.playerFound &&
                report.rpgFound &&
                report.combatFound &&
                report.inventoryFound &&
                report.balanceFound &&
                report.itemsFound;

            report.message =
                "Player=" +
                report.playerFound +
                " RPG=" +
                report.rpgFound +
                " Combat=" +
                report.combatFound +
                " Inventory=" +
                report.inventoryFound +
                " Balance=" +
                report.balanceFound +
                " Items=" +
                report.itemsFound;

            File.WriteAllText(
                Diagnostic,
                JsonUtility.ToJson(
                    report,
                    true));

            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "AO RPG v0.11",
                (report.success
                    ? "OK\n\n"
                    : "Falta algo\n\n") +
                report.message,
                "OK");
        }
        catch (Exception e)
        {
            report.success = false;
            report.message =
                e.ToString();

            File.WriteAllText(
                Diagnostic,
                JsonUtility.ToJson(
                    report,
                    true));

            AssetDatabase.Refresh();

            Debug.LogError(
                "AO RPG v0.11 verify: " +
                e);
        }
    }
}
