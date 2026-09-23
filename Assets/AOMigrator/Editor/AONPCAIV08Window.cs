using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public class AONPCAIV08Window : EditorWindow
{
    const string MapRoot =
        "Assets/Resources/AOMigrator/WorldV07/Maps";

    Vector2 scroll;

    [MenuItem("AO Migrador/NPC vivos v0.8 - verificar")]
    public static void Open()
    {
        GetWindow<AONPCAIV08Window>(
            "AO NPC v0.8");
    }

    void OnGUI()
    {
        scroll =
            EditorGUILayout.BeginScrollView(scroll);

        GUILayout.Label(
            "AO Migrador v0.8 - NPC vivos",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Esta etapa implementa movimiento local para los modos AO más usados " +
            "en los mapas actuales: Estatico(1), MueveAlAzar(2), FixedInPos(3) y Caminata(20). " +
            "Los NPC hostiles de MueveAlAzar persiguen/guardan distancia del jugador, " +
            "pero todavía NO infligen daño.",
            MessageType.Info);

        if (GUILayout.Button(
                "Verificar instalación v0.8",
                GUILayout.Height(36)))
        {
            Verify();
        }

        GUILayout.Space(8);

        EditorGUILayout.HelpBox(
            "En Play:\n" +
            "- NPC amistoso Movement=2: deambula cerca de su origen.\n" +
            "- NPC hostil Movement=2: detecta al jugador y se acerca/guarda rango.\n" +
            "- Movement=20: recorre Caminata1..N con sus esperas originales.\n" +
            "- F7 pausa/reanuda toda la IA de NPC.\n" +
            "- F5 sigue recargando el mapa.",
            MessageType.None);

        EditorGUILayout.EndScrollView();
    }

    void Verify()
    {
        try
        {
            string aiScript =
                "Assets/AOMigrator/Runtime/AONPCMovementV08.cs";

            if (!File.Exists(aiScript))
                throw new Exception(
                    "Falta " + aiScript);

            string[] maps =
                Directory.Exists(MapRoot)
                ? Directory.GetFiles(
                    MapRoot,
                    "map_*.json",
                    SearchOption.TopDirectoryOnly)
                : new string[0];

            int enhanced = 0;

            foreach (string path in maps)
            {
                string text =
                    File.ReadAllText(path);

                if (text.Contains(
                        "\"moveIntervalMs\"") &&
                    text.Contains(
                        "\"walkRoute\""))
                {
                    enhanced++;
                }
            }

            EditorUtility.DisplayDialog(
                "AO NPC v0.8",
                "Script IA: OK\n" +
                "Mapas encontrados: " +
                maps.Length + "\n" +
                "Mapas con datos IA v0.8: " +
                enhanced + "\n\n" +
                "Si coinciden, guardá la escena y probá Play.",
                "OK");
        }
        catch (Exception e)
        {
            Debug.LogError(
                "AO NPC v0.8 verify: " + e);

            EditorUtility.DisplayDialog(
                "AO NPC v0.8 - error",
                e.Message,
                "OK");
        }
    }
}
