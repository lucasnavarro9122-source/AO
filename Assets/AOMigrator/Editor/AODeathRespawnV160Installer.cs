using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AODeathRespawnV160Installer
{
    const string Diagnostic =
        "Assets/AOMigrator/death_v160_diagnostic.json";

    [Serializable]
    class Report
    {
        public string version =
            "0.16.0-alpha";

        public string unity;
        public bool success;
        public string message;

        public bool playerFound;
        public bool deathFound;
        public bool combatFound;
        public bool profileFound;
        public bool inventoryFound;
        public bool rpgFound;
        public bool worldFound;
        public bool visualDataFound;

        public int deadBody;
        public int homeMap;
        public int homeX;
        public int homeY;
        public float homeSeconds;
    }

    [MenuItem(
        "AO Migrador/Muerte y respawn v0.16 - instalar/verificar")]
    public static void Install()
    {
        Report r =
            new Report();

        try
        {
            if (EditorApplication.isPlaying)
            {
                throw new Exception(
                    "Salí de Play antes de instalar/verificar.");
            }

            r.unity =
                Application.unityVersion;

            AOTestPlayer player =
                UnityEngine.Object
                    .FindFirstObjectByType
                        <AOTestPlayer>();

            r.playerFound =
                player != null;

            if (player == null)
                throw new Exception(
                    "No encontré AO Test Player.");

            AODeathRespawnV160 death =
                player.GetComponent
                    <AODeathRespawnV160>();

            if (death == null)
            {
                death =
                    Undo.AddComponent
                        <AODeathRespawnV160>(
                            player.gameObject);
            }

            r.deathFound =
                death != null;

            r.combatFound =
                player.GetComponent
                    <AOPlayerCombatV09>() !=
                null;

            r.profileFound =
                player.GetComponent
                    <AOCharacterProfileVisualV111>() !=
                null;

            r.inventoryFound =
                player.GetComponent
                    <AOInventoryV10>() !=
                null;

            r.rpgFound =
                player.GetComponent
                    <AOPlayerRPGV11>() !=
                null;

            r.worldFound =
                UnityEngine.Object
                    .FindFirstObjectByType
                        <AOWorldManagerV07>() !=
                null;

            r.visualDataFound =
                File.Exists(
                    "Assets/Resources/AOMigrator/DeathV160/death_visual.json");

            r.deadBody =
                AODeathRespawnV160
                    .DEAD_BODY_ID;

            r.homeMap =
                AODeathRespawnV160
                    .HOME_MAP;

            r.homeX =
                AODeathRespawnV160
                    .HOME_X;

            r.homeY =
                AODeathRespawnV160
                    .HOME_Y;

            r.homeSeconds =
                AODeathRespawnV160
                    .HOME_SECONDS;

            r.success =
                r.playerFound &&
                r.deathFound &&
                r.combatFound &&
                r.profileFound &&
                r.inventoryFound &&
                r.rpgFound &&
                r.worldFound &&
                r.visualDataFound &&
                r.deadBody == 829 &&
                Mathf.Approximately(
                    r.homeSeconds,
                    105f);

            r.message =
                "Player=" +
                r.playerFound +
                " Death=" +
                r.deathFound +
                " Combat=" +
                r.combatFound +
                "\nProfile=" +
                r.profileFound +
                " Inv=" +
                r.inventoryFound +
                " RPG=" +
                r.rpgFound +
                " World=" +
                r.worldFound +
                "\nBODY=" +
                r.deadBody +
                " Home=" +
                r.homeMap +
                ":" +
                r.homeX +
                "," +
                r.homeY +
                " Timer=" +
                r.homeSeconds +
                "s";

            EditorUtility.SetDirty(
                death);

            EditorSceneManager.MarkSceneDirty(
                EditorSceneManager
                    .GetActiveScene());

            File.WriteAllText(
                Diagnostic,
                JsonUtility.ToJson(
                    r,
                    true));

            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "AO Muerte v0.16",
                (r.success
                    ? "OK\n\n"
                    : "Hay algo para revisar\n\n") +
                r.message +
                "\n\nGuardá Ctrl+S. " +
                "En Play dejá que un NPC te mate: " +
                "el fantasma debe quedar en ese mismo tile.",
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
                "AO v0.16 installer: " +
                e);

            EditorUtility.DisplayDialog(
                "AO Muerte v0.16 - error",
                e.Message +
                "\n\nSubime " +
                Diagnostic,
                "OK");
        }
    }
}
