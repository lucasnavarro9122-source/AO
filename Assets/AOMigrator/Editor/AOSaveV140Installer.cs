using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AOSaveV140Installer
{
    const string Diagnostic =
        "Assets/AOMigrator/save_v140_diagnostic.json";

    [Serializable]
    class Report
    {
        public string version =
            "0.14.0-alpha";

        public string unity;
        public bool success;
        public string message;

        public bool playerFound;
        public bool saveFound;
        public bool menuFound;
        public bool rpgFound;
        public bool inventoryFound;
        public bool combatFound;
        public bool magicFound;
        public bool worldFound;
        public bool bankFound;
    }

    [MenuItem(
        "AO Migrador/Guardado y carga v0.14 - instalar/verificar")]
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
            {
                throw new Exception(
                    "No encontré AO Test Player.");
            }

            AOSaveGameV140 save =
                player.GetComponent
                    <AOSaveGameV140>();

            if (save == null)
            {
                save =
                    Undo.AddComponent
                        <AOSaveGameV140>(
                            player.gameObject);
            }

            AOMainMenuV140 menu =
                player.GetComponent
                    <AOMainMenuV140>();

            if (menu == null)
            {
                menu =
                    Undo.AddComponent
                        <AOMainMenuV140>(
                            player.gameObject);
            }

            r.saveFound =
                save != null;

            r.menuFound =
                menu != null;

            r.rpgFound =
                player.GetComponent
                    <AOPlayerRPGV11>() !=
                null;

            r.inventoryFound =
                player.GetComponent
                    <AOInventoryV10>() !=
                null;

            r.combatFound =
                player.GetComponent
                    <AOPlayerCombatV09>() !=
                null;

            r.magicFound =
                player.GetComponent
                    <AOPlayerMagicV120>() !=
                null;

            r.bankFound =
                player.GetComponent
                    <AOCityBankV130>() !=
                null;

            r.worldFound =
                UnityEngine.Object
                    .FindFirstObjectByType
                        <AOWorldManagerV07>() !=
                null;

            r.success =
                r.playerFound &&
                r.saveFound &&
                r.menuFound &&
                r.rpgFound &&
                r.inventoryFound &&
                r.combatFound &&
                r.magicFound &&
                r.worldFound &&
                r.bankFound;

            r.message =
                "Player=" +
                r.playerFound +
                " Save=" +
                r.saveFound +
                " Menu=" +
                r.menuFound +
                "\nRPG=" +
                r.rpgFound +
                " Inv=" +
                r.inventoryFound +
                " Combat=" +
                r.combatFound +
                " Magic=" +
                r.magicFound +
                " World=" +
                r.worldFound +
                " Bank=" +
                r.bankFound;

            EditorUtility.SetDirty(
                save);

            EditorUtility.SetDirty(
                menu);

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
                "AO Save/Load v0.14",
                (r.success
                    ? "OK\n\n"
                    : "Hay algo para revisar\n\n") +
                r.message +
                "\n\nGuardá Ctrl+S y entrá en Play. " +
                "Debería aparecer Nueva partida / Continuar.",
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
                "AO v0.14 installer: " +
                e);

            EditorUtility.DisplayDialog(
                "AO Save/Load v0.14 - error",
                e.Message +
                "\n\nSubime " +
                Diagnostic,
                "OK");
        }
    }

    [MenuItem(
        "AO Migrador/Guardado v0.14 - abrir carpeta de saves")]
    public static void OpenSaveFolder()
    {
        string folder =
            Path.Combine(
                Application.persistentDataPath,
                "AO_Demo");

        Directory.CreateDirectory(
            folder);

        EditorUtility.RevealInFinder(
            folder);
    }

    [MenuItem(
        "AO Migrador/Guardado v0.14 - borrar slot local")]
    public static void DeleteLocalSave()
    {
        string folder =
            Path.Combine(
                Application.persistentDataPath,
                "AO_Demo");

        string save =
            Path.Combine(
                folder,
                "save_slot_1.json");

        string backup =
            Path.Combine(
                folder,
                "save_slot_1.bak.json");

        if (File.Exists(save))
            File.Delete(save);

        if (File.Exists(backup))
            File.Delete(backup);

        EditorUtility.DisplayDialog(
            "AO Save/Load v0.14",
            "Slot local borrado.",
            "OK");
    }
}
