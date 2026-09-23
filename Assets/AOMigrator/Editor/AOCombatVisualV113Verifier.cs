using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AOCombatVisualV113Verifier
{
    const string Diagnostic =
        "Assets/AOMigrator/combat_visual_v113_diagnostic.json";

    [Serializable]
    class Report
    {
        public string version =
            "0.11.3-alpha";
        public string unity;
        public bool success;
        public string message;
        public bool playerFound;
        public bool combatFound;
        public bool rendererFound;
        public bool npcCombatFound;
        public bool feedbackScriptFound;
        public int audioFiles;
        public int bodyFrames;
        public int weaponFrames;
        public int shieldFrames;
    }

    [MenuItem(
        "AO Migrador/Verificar combate visual v0.11.3")]
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

            AOPlayerCombatV09 combat =
                player == null
                ? null
                : player.GetComponent
                    <AOPlayerCombatV09>();

            r.combatFound =
                combat != null;

            AOCharacterRenderer renderer =
                player == null
                ? null
                : player.GetComponentInChildren
                    <AOCharacterRenderer>(true);

            r.rendererFound =
                renderer != null;

            r.npcCombatFound =
                UnityEngine.Object
                    .FindFirstObjectByType
                        <AONPCCombatV09>() !=
                null;

            r.feedbackScriptFound =
                File.Exists(
                    "Assets/AOMigrator/Runtime/AOCombatFeedbackV113.cs");

            string audio =
                "Assets/Resources/AOMigrator/CombatV113/Audio";

            r.audioFiles =
                Directory.Exists(audio)
                ? Directory.GetFiles(
                    audio,
                    "*.wav",
                    SearchOption.TopDirectoryOnly)
                    .Length
                : 0;

            if (renderer != null)
            {
                renderer.ForceRefreshVisuals();

                r.bodyFrames =
                    renderer.CurrentBodyFrameCount;

                r.weaponFrames =
                    renderer.CurrentWeaponFrameCount;

                r.shieldFrames =
                    renderer.CurrentShieldFrameCount;
            }

            r.success =
                r.playerFound &&
                r.combatFound &&
                r.rendererFound &&
                r.feedbackScriptFound &&
                r.audioFiles >= 6 &&
                r.bodyFrames > 1;

            r.message =
                "Player=" +
                r.playerFound +
                " Combat=" +
                r.combatFound +
                " Renderer=" +
                r.rendererFound +
                " NPCCombat=" +
                r.npcCombatFound +
                " Audio=" +
                r.audioFiles +
                " Frames B/W/S=" +
                r.bodyFrames +
                "/" +
                r.weaponFrames +
                "/" +
                r.shieldFrames;

            File.WriteAllText(
                Diagnostic,
                JsonUtility.ToJson(
                    r,
                    true));

            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "AO Combat Visual v0.11.3",
                (r.success
                    ? "OK\n\n"
                    : "Hay algo para revisar\n\n") +
                r.message +
                "\n\nEn Play probá atacar, fallar, recibir daño, matar un NPC y equipar/quitar.",
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
                "AO v0.11.3 verifier: " +
                e);
        }
    }
}
