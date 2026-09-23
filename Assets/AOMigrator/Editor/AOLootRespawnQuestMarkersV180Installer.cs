using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AOLootRespawnQuestMarkersV180Installer
{
    const string Diagnostic =
        "Assets/AOMigrator/loot_questmarkers_v180_diagnostic.json";

    [Serializable]
    class Report
    {
        public string version =
            "0.18.0-alpha";

        public string unity;

        public bool success;
        public string message;

        public int npcLootDefinitions;
        public bool lootDbFound;
        public bool dropSoundFound;

        public bool marker1;
        public bool marker2;
        public bool marker3;
        public bool marker4;

        public bool playerFound;
        public bool questSystemFound;
        public int npcCount;
        public int combatNpcCount;
    }

    [MenuItem(
        "AO Migrador/Loot + respawn + quest markers v0.18 - verificar")]
    public static void Verify()
    {
        Report r =
            new Report();

        try
        {
            r.unity =
                Application.unityVersion;

            r.lootDbFound =
                File.Exists(
                    "Assets/Resources/AOMigrator/LootV180/npc_loot.json");

            r.dropSoundFound =
                File.Exists(
                    "Assets/Resources/AOMigrator/LootV180/Audio/wav_132.wav");

            r.marker1 =
                File.Exists(
                    "Assets/Resources/AOMigrator/QuestMarkersV180/symbol_1.png");

            r.marker2 =
                File.Exists(
                    "Assets/Resources/AOMigrator/QuestMarkersV180/symbol_2.png");

            r.marker3 =
                File.Exists(
                    "Assets/Resources/AOMigrator/QuestMarkersV180/symbol_3.png");

            r.marker4 =
                File.Exists(
                    "Assets/Resources/AOMigrator/QuestMarkersV180/symbol_4.png");

            r.npcLootDefinitions =
                AONPCLootDatabaseV180.Count;

            AOTestPlayer player =
                UnityEngine.Object
                    .FindFirstObjectByType
                        <AOTestPlayer>();

            r.playerFound =
                player != null;

            r.questSystemFound =
                player != null &&
                player.GetComponent
                    <AOQuestSystemV150>() !=
                null;

            r.npcCount =
                UnityEngine.Object
                    .FindObjectsByType
                        <AONPCMetadata>(
                            FindObjectsSortMode.None)
                    .Length;

            r.combatNpcCount =
                UnityEngine.Object
                    .FindObjectsByType
                        <AONPCCombatV09>(
                            FindObjectsSortMode.None)
                    .Length;

            r.success =
                r.lootDbFound &&
                r.npcLootDefinitions > 500 &&
                r.marker1 &&
                r.marker2 &&
                r.marker3 &&
                r.marker4 &&
                r.playerFound &&
                r.questSystemFound;

            r.message =
                "LootDB=" +
                r.lootDbFound +
                " NPCDefs=" +
                r.npcLootDefinitions +
                "\nMarkers=" +
                r.marker1 +
                "/" +
                r.marker2 +
                "/" +
                r.marker3 +
                "/" +
                r.marker4 +
                " DropSound=" +
                r.dropSoundFound +
                "\nPlayer=" +
                r.playerFound +
                " QuestSystem=" +
                r.questSystemFound +
                " NPCs escena=" +
                r.npcCount +
                " CombatNPCs=" +
                r.combatNpcCount;

            File.WriteAllText(
                Diagnostic,
                JsonUtility.ToJson(
                    r,
                    true));

            AssetDatabase.Refresh();

            EditorSceneManager.MarkSceneDirty(
                EditorSceneManager
                    .GetActiveScene());

            EditorUtility.DisplayDialog(
                "AO v0.18",
                (r.success
                    ? "OK\n\n"
                    : "Hay algo para revisar\n\n") +
                r.message +
                "\n\nEn Play esperá ~1 s: los NPC de quest deben mostrar " +
                "!, ? o sus versiones grises según estado.",
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
                "AO v0.18 verifier: " +
                e);

            EditorUtility.DisplayDialog(
                "AO v0.18 - error",
                e.Message +
                "\n\nSubime " +
                Diagnostic,
                "OK");
        }
    }
}
