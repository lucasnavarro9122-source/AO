using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AOCityV130Installer
{
    const string Diagnostic =
        "Assets/AOMigrator/city_v130_diagnostic.json";

    [Serializable]
    class Report
    {
        public string version =
            "0.13.0-alpha";

        public string unity;
        public bool success;
        public string message;

        public bool playerFound;
        public bool cityFound;
        public bool bankFound;
        public bool uiFound;
        public bool databaseFound;

        public int npcDefinitions;
        public int audioFiles;
    }

    [MenuItem(
        "AO Migrador/NPC ciudad v0.13 - instalar/verificar")]
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

            AOCityBankV130 bank =
                player.GetComponent
                    <AOCityBankV130>();

            if (bank == null)
            {
                bank =
                    Undo.AddComponent
                        <AOCityBankV130>(
                            player.gameObject);
            }

            AOCityNPCSystemV130 city =
                player.GetComponent
                    <AOCityNPCSystemV130>();

            if (city == null)
            {
                city =
                    Undo.AddComponent
                        <AOCityNPCSystemV130>(
                            player.gameObject);
            }

            AOCityUIV130 ui =
                player.GetComponent
                    <AOCityUIV130>();

            if (ui == null)
            {
                ui =
                    Undo.AddComponent
                        <AOCityUIV130>(
                            player.gameObject);
            }

            ui.Configure(
                city,
                bank);

            r.cityFound =
                city != null;

            r.bankFound =
                bank != null;

            r.uiFound =
                ui != null;

            string dbPath =
                "Assets/Resources/AOMigrator/CityV130/city_npcs.json";

            r.databaseFound =
                File.Exists(
                    dbPath);

            r.npcDefinitions =
                AOCityNPCDatabaseV130.Count;

            string audio =
                "Assets/Resources/AOMigrator/CityV130/Audio";

            r.audioFiles =
                Directory.Exists(
                    audio)
                ? Directory.GetFiles(
                    audio,
                    "wav_*.wav",
                    SearchOption.TopDirectoryOnly)
                    .Length
                : 0;

            r.success =
                r.playerFound &&
                r.cityFound &&
                r.bankFound &&
                r.uiFound &&
                r.databaseFound &&
                r.npcDefinitions > 100;

            r.message =
                "Player=" +
                r.playerFound +
                " City=" +
                r.cityFound +
                " Bank=" +
                r.bankFound +
                " UI=" +
                r.uiFound +
                "\nNPC defs=" +
                r.npcDefinitions +
                " Audio=" +
                r.audioFiles;

            EditorUtility.SetDirty(
                bank);

            EditorUtility.SetDirty(
                city);

            EditorUtility.SetDirty(
                ui);

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
                "AO NPC ciudad v0.13",
                (r.success
                    ? "OK\n\n"
                    : "Hay algo para revisar\n\n") +
                r.message +
                "\n\nGuardá Ctrl+S. " +
                "En Play acercate a un NPC y apretá E. " +
                "F4 da 20.000 de oro de prueba.",
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
                "AO v0.13 installer: " +
                e);

            EditorUtility.DisplayDialog(
                "AO NPC ciudad v0.13 - error",
                e.Message +
                "\n\nSubime " +
                Diagnostic,
                "OK");
        }
    }
}
