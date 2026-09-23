using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class AOCombatV09SetupWindow : EditorWindow
{
    const string MapRoot =
        "Assets/Resources/AOMigrator/WorldV07/Maps";
    const string DiagnosticPath =
        "Assets/AOMigrator/combat_v09_setup_diagnostic.json";

    [Serializable] class Diagnostic
    {
        public string version = "0.9.1-alpha";
        public string unity;
        public bool success;
        public string message;
        public bool playerFound;
        public bool combatAdded;
        public int mapFiles;
        public int enhancedMaps;
        public int emptyNpcMaps;
    }

    Vector2 scroll;
    string status =
        "Listo para activar combate local v0.9.1.";

    [MenuItem("AO Migrador/Combate local v0.9 - activar")]
    public static void Open()
    {
        GetWindow<AOCombatV09SetupWindow>(
            "AO Combat v0.9");
    }

    void OnGUI()
    {
        scroll =
            EditorGUILayout.BeginScrollView(scroll);

        GUILayout.Label(
            "AO Migrador v0.9.1 - combate local",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Hotfix de setup: los mapas sin NPC también cuentan como mapas v0.9 válidos. " +
            "No cambia la lógica de combate.",
            MessageType.Info);

        if (GUILayout.Button(
                "Activar / reparar combate v0.9",
                GUILayout.Height(40)))
        {
            Setup();
        }

        GUILayout.Space(8);

        EditorGUILayout.HelpBox(
            "En Play:\n" +
            "- Ctrl o Espacio: atacar al NPC de la casilla frontal.\n" +
            "- G: recoger loot en tu casilla o delante.\n" +
            "- E también recoge loot si lo tenés delante.\n" +
            "- Los NPC hostiles infligen daño si están en rango.\n" +
            "- El jugador hace respawn local si muere.",
            MessageType.None);

        GUILayout.Space(8);
        EditorGUILayout.LabelField(
            "Estado",
            EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            status,
            MessageType.None);

        EditorGUILayout.EndScrollView();
    }

    void Setup()
    {
        Diagnostic diag = new Diagnostic();

        try
        {
            if (EditorApplication.isPlaying)
                throw new Exception(
                    "Salí de Play antes de activar v0.9.");

            diag.unity =
                Application.unityVersion;

            AOTestPlayer player =
                UnityEngine.Object
                    .FindFirstObjectByType<AOTestPlayer>();

            diag.playerFound =
                player != null;

            if (player == null)
                throw new Exception(
                    "No encontré AO Test Player.");

            AOPlayerCombatV09 combat =
                player.GetComponent<AOPlayerCombatV09>();

            if (combat == null)
            {
                combat =
                    Undo.AddComponent<AOPlayerCombatV09>(
                        player.gameObject);
                diag.combatAdded = true;
            }

            string[] maps =
                Directory.Exists(MapRoot)
                ? Directory.GetFiles(
                    MapRoot,
                    "map_*.json",
                    SearchOption.TopDirectoryOnly)
                : new string[0];

            diag.mapFiles = maps.Length;

            int enhanced = 0;
            int emptyNpcMaps = 0;

            foreach (string path in maps)
            {
                string text =
                    File.ReadAllText(path);

                // v0.9 agrega los campos de combate dentro de cada NPC.
                // Si el mapa no tiene NPC, no existirán esas claves en el JSON,
                // pero el mapa igualmente está correctamente actualizado.
                bool emptyNpcs =
                    text.Contains("\"npcs\":[]") ||
                    text.Contains("\"npcs\": []");

                bool hasCombatNpcData =
                    text.Contains("\"maxHp\"") &&
                    text.Contains("\"attackIntervalMs\"") &&
                    text.Contains("\"drops\"");

                if (emptyNpcs)
                {
                    emptyNpcMaps++;
                    enhanced++;
                }
                else if (hasCombatNpcData)
                {
                    enhanced++;
                }
            }

            diag.enhancedMaps =
                enhanced;
            diag.emptyNpcMaps =
                emptyNpcMaps;

            if (maps.Length == 0 ||
                enhanced != maps.Length)
            {
                throw new Exception(
                    "Los map JSON no están completamente actualizados a v0.9. " +
                    "Mapas=" + maps.Length +
                    ", v0.9=" + enhanced +
                    ", sin NPC=" + emptyNpcMaps);
            }

            EditorUtility.SetDirty(combat);
            EditorSceneManager.MarkSceneDirty(
                EditorSceneManager.GetActiveScene());

            diag.success = true;
            diag.message =
                "Combate v0.9 activado correctamente. " +
                "Mapas=" + maps.Length +
                ", mapas sin NPC=" + emptyNpcMaps +
                ". Guardá Ctrl+S y probá Play.";

            WriteDiagnostic(diag);

            status =
                "OK. " + maps.Length +
                " mapas válidos; " +
                emptyNpcMaps +
                " no tienen NPC. " +
                (diag.combatAdded
                    ? "AOPlayerCombatV09 agregado."
                    : "AOPlayerCombatV09 ya existía.");

            EditorUtility.DisplayDialog(
                "AO Combat v0.9.1",
                status +
                "\n\nGuardá la escena con Ctrl+S.",
                "OK");
        }
        catch (Exception e)
        {
            diag.success = false;
            diag.message = e.ToString();
            WriteDiagnostic(diag);

            status =
                "ERROR: " + e.Message;

            Debug.LogError(
                "AO Combat v0.9.1 setup: " + e);

            EditorUtility.DisplayDialog(
                "AO Combat v0.9.1 - error",
                e.Message +
                "\n\nSubime " +
                DiagnosticPath + ".",
                "OK");
        }
    }

    static void WriteDiagnostic(
        Diagnostic diag)
    {
        try
        {
            diag.unity =
                Application.unityVersion;

            File.WriteAllText(
                DiagnosticPath,
                JsonUtility.ToJson(
                    diag,
                    true));

            AssetDatabase.Refresh();
        }
        catch (Exception e)
        {
            Debug.LogError(
                "AO v0.9.1 diagnostic: " + e);
        }
    }
}
