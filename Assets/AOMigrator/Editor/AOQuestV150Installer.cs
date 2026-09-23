using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AOQuestV150Installer
{
    const string Diagnostic =
        "Assets/AOMigrator/quests_v150_diagnostic.json";

    [Serializable]
    class Report
    {
        public string version =
            "0.15.0-alpha";

        public string unity;
        public bool success;
        public string message;

        public bool playerFound;
        public bool questSystemFound;
        public bool questUiFound;
        public bool saveFound;
        public bool databaseFound;

        public int questDefinitions;
        public int declaredQuests;
    }

    [MenuItem(
        "AO Migrador/Quests v0.15 - instalar/verificar")]
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

            AOQuestSystemV150 quests =
                player.GetComponent
                    <AOQuestSystemV150>();

            if (quests == null)
            {
                quests =
                    Undo.AddComponent
                        <AOQuestSystemV150>(
                            player.gameObject);
            }

            AOQuestUIV150 ui =
                player.GetComponent
                    <AOQuestUIV150>();

            if (ui == null)
            {
                ui =
                    Undo.AddComponent
                        <AOQuestUIV150>(
                            player.gameObject);
            }

            r.questSystemFound =
                quests != null;

            r.questUiFound =
                ui != null;

            r.saveFound =
                player.GetComponent
                    <AOSaveGameV140>() !=
                null;

            string db =
                "Assets/Resources/AOMigrator/QuestsV150/quests.json";

            r.databaseFound =
                File.Exists(db);

            r.questDefinitions =
                AOQuestDatabaseV150.Count;

            r.declaredQuests =
                AOQuestDatabaseV150
                    .DeclaredCount;

            r.success =
                r.playerFound &&
                r.questSystemFound &&
                r.questUiFound &&
                r.saveFound &&
                r.databaseFound &&
                r.questDefinitions > 250;

            r.message =
                "Player=" +
                r.playerFound +
                " QuestSystem=" +
                r.questSystemFound +
                " UI=" +
                r.questUiFound +
                " Save=" +
                r.saveFound +
                "\nQuests=" +
                r.questDefinitions +
                "/" +
                r.declaredQuests;

            EditorUtility.SetDirty(
                quests);

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
                "AO Quests v0.15",
                (r.success
                    ? "OK\n\n"
                    : "Hay algo para revisar\n\n") +
                r.message +
                "\n\nGuardá Ctrl+S. " +
                "En Play: Q abre el diario y E interactúa con NPC de quest.",
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
                "AO v0.15 quests installer: " +
                e);

            EditorUtility.DisplayDialog(
                "AO Quests v0.15 - error",
                e.Message +
                "\n\nSubime " +
                Diagnostic,
                "OK");
        }
    }
}
