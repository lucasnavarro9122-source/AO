using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AOVisualV111Verifier
{
    const string Diagnostic =
        "Assets/AOMigrator/visual_v111_diagnostic.json";

    [Serializable]
    class Report
    {
        public string version =
            "0.11.1-alpha";
        public string unity;
        public bool success;
        public string message;
        public bool playerFound;
        public bool rpgFound;
        public bool visualSyncFound;
        public bool visualDatabaseFound;
        public int characterTextures;
        public int validHeadsForCurrentProfile;
    }

    [MenuItem(
        "AO Migrador/Verificar apariencia RPG v0.11.1")]
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

            AOPlayerRPGV11 rpg =
                player == null
                ? null
                : player.GetComponent
                    <AOPlayerRPGV11>();

            r.rpgFound =
                rpg != null;

            r.visualSyncFound =
                player != null &&
                player.GetComponent
                    <AOCharacterProfileVisualV111>() !=
                null;

            r.visualDatabaseFound =
                File.Exists(
                    "Assets/Resources/AOMigrator/CharacterV111/character_visuals.json");

            string textureFolder =
                "Assets/Resources/AOMigrator/CharacterV111/Textures";

            r.characterTextures =
                Directory.Exists(
                    textureFolder)
                ? Directory.GetFiles(
                    textureFolder,
                    "tex_*.png",
                    SearchOption.TopDirectoryOnly)
                    .Length
                : 0;

            r.validHeadsForCurrentProfile =
                rpg == null
                ? 0
                : AOCharacterVisualDatabaseV111
                    .ValidHeads(
                        rpg.RaceId,
                        rpg.GenderId)
                    .Length;

            r.success =
                r.playerFound &&
                r.rpgFound &&
                r.visualSyncFound &&
                r.visualDatabaseFound &&
                r.characterTextures > 0 &&
                r.validHeadsForCurrentProfile > 0;

            r.message =
                "Player=" +
                r.playerFound +
                " RPG=" +
                r.rpgFound +
                " Visual=" +
                r.visualSyncFound +
                " DB=" +
                r.visualDatabaseFound +
                " Texturas=" +
                r.characterTextures +
                " Heads=" +
                r.validHeadsForCurrentProfile;

            File.WriteAllText(
                Diagnostic,
                JsonUtility.ToJson(
                    r,
                    true));

            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "AO v0.11.1",
                (r.success
                    ? "OK\n\n"
                    : "Falta algo\n\n") +
                r.message,
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
                "AO v0.11.1 verify: " +
                e);
        }
    }
}
