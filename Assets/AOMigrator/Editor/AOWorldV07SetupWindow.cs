using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class AOWorldV07SetupWindow : EditorWindow
{
    const string TextureFolder =
        "Assets/Resources/AOMigrator/WorldV07/Textures";
    const string MapFolder =
        "Assets/Resources/AOMigrator/WorldV07/Maps";
    const string DiagnosticPath =
        "Assets/AOMigrator/world_v07_setup_diagnostic.json";

    [Serializable] class Diagnostic
    {
        public string version = "0.7.0-alpha";
        public string unity;
        public bool success;
        public string message;
        public int mapFiles;
        public int textures;
        public bool playerFound;
        public bool cameraFound;
        public bool managerCreated;
    }

    Vector2 scroll;
    string status =
        "Listo. Esta etapa activa transiciones entre los mapas incluidos.";

    [MenuItem("AO Migrador/Activar mundo conectado v0.7")]
    public static void OpenWindow()
    {
        GetWindow<AOWorldV07SetupWindow>(
            "AO World v0.7");
    }

    void OnGUI()
    {
        scroll =
            EditorGUILayout.BeginScrollView(scroll);

        GUILayout.Label(
            "AO Migrador v0.7 - Mundo conectado",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Configura las texturas del paquete, agrega AO World Manager v0.7 " +
            "y conserva tu jugador/personaje actual. En Play, las casillas TE del CSM " +
            "cambian de mapa automáticamente cuando el destino está incluido.",
            MessageType.Info);

        GUILayout.Space(6);

        if (GUILayout.Button(
                "Activar v0.7 en la escena actual",
                GUILayout.Height(42)))
        {
            Setup();
        }

        if (GUILayout.Button(
                "Verificar archivos v0.7",
                GUILayout.Height(28)))
        {
            VerifyOnly();
        }

        GUILayout.Space(8);

        EditorGUILayout.HelpBox(
            "Prueba recomendada:\n" +
            "1. Abrí AO_Ciudad_de_Ullathorpe_Playable.\n" +
            "2. Activá v0.7.\n" +
            "3. Guardá con Ctrl+S.\n" +
            "4. Play.\n" +
            "5. Caminá hasta una salida de Ullathorpe.\n\n" +
            "F5 recarga el mapa actual. F6 vuelve a Ullathorpe.\n" +
            "Los destinos todavía no incluidos muestran un aviso y no cambian de mapa.",
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

    void VerifyOnly()
    {
        try
        {
            int mapCount =
                Directory.Exists(MapFolder)
                ? Directory.GetFiles(
                    MapFolder,
                    "map_*.json",
                    SearchOption.TopDirectoryOnly).Length
                : 0;

            int textureCount =
                Directory.Exists(TextureFolder)
                ? Directory.GetFiles(
                    TextureFolder,
                    "tex_*.png",
                    SearchOption.TopDirectoryOnly).Length
                : 0;

            status =
                "Mapas: " + mapCount +
                " | Texturas: " + textureCount +
                ". Mapas disponibles.";

            Debug.Log("AO v0.7 verify: " + status);
        }
        catch (Exception e)
        {
            status = "ERROR: " + e.Message;
            Debug.LogError("AO v0.7 verify: " + e);
        }
    }

    void Setup()
    {
        Diagnostic diag = new Diagnostic();

        try
        {
            if (EditorApplication.isPlaying)
                throw new Exception(
                    "Salí de Play antes de activar v0.7.");

            diag.unity =
                Application.unityVersion;

            if (!Directory.Exists(MapFolder))
                throw new Exception(
                    "Falta " + MapFolder);

            if (!Directory.Exists(TextureFolder))
                throw new Exception(
                    "Falta " + TextureFolder);

            string[] maps =
                Directory.GetFiles(
                    MapFolder,
                    "map_*.json",
                    SearchOption.TopDirectoryOnly);

            string[] textures =
                Directory.GetFiles(
                    TextureFolder,
                    "tex_*.png",
                    SearchOption.TopDirectoryOnly);

            diag.mapFiles = maps.Length;
            diag.textures = textures.Length;

            if (maps.Length < 2)
                throw new Exception(
                    "No hay suficientes mapas v0.7.");

            ConfigureTextures(textures);

            AOTestPlayer player =
                UnityEngine.Object
                .FindFirstObjectByType<AOTestPlayer>();

            diag.playerFound =
                player != null;

            if (player == null)
                throw new Exception(
                    "No encontré AO Test Player.");

            AOCameraFollow camera =
                UnityEngine.Object
                .FindFirstObjectByType<AOCameraFollow>();

            diag.cameraFound =
                camera != null;

            GameObject managerGo =
                GameObject.Find(
                    "AO World Manager v0.7");

            if (managerGo == null)
            {
                managerGo =
                    new GameObject(
                        "AO World Manager v0.7");

                Undo.RegisterCreatedObjectUndo(
                    managerGo,
                    "Activar AO World v0.7");

                diag.managerCreated = true;
            }

            AOWorldManagerV07 manager =
                managerGo.GetComponent<AOWorldManagerV07>();

            if (manager == null)
                manager =
                    Undo.AddComponent<AOWorldManagerV07>(
                        managerGo);

            int startX =
                player.TileX > 0
                ? player.TileX
                : 68;

            int startY =
                player.TileY > 0
                ? player.TileY
                : 43;

            manager.Configure(
                player,
                camera,
                1,
                startX,
                startY);

            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(
                EditorSceneManager.GetActiveScene());

            diag.success = true;
            diag.message =
                "v0.7 activada. Guardá la escena y probá Play.";

            WriteDiagnostic(diag);

            status =
                "OK. " + maps.Length +
                " mapas conectables y " +
                textures.Length +
                " texturas configuradas. " +
                "Guardá la escena con Ctrl+S y tocá Play.";

            Selection.activeGameObject =
                managerGo;

            EditorUtility.DisplayDialog(
                "AO Migrador v0.7",
                status,
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
                "AO World v0.7 setup: " + e);

            EditorUtility.DisplayDialog(
                "AO Migrador v0.7 - error",
                e.Message +
                "\n\nSubime " +
                DiagnosticPath + ".",
                "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    static void ConfigureTextures(
        string[] textureFiles)
    {
        for (int i = 0;
             i < textureFiles.Length; i++)
        {
            if (i % 10 == 0)
            {
                EditorUtility.DisplayProgressBar(
                    "AO Migrador v0.7",
                    "Configurando texturas " +
                    (i + 1) + "/" +
                    textureFiles.Length,
                    textureFiles.Length == 0
                    ? 1f
                    : (float)i /
                      textureFiles.Length);
            }

            string assetPath =
                textureFiles[i]
                .Replace("\\", "/");

            int assetsIndex =
                assetPath.IndexOf(
                    "Assets/",
                    StringComparison.OrdinalIgnoreCase);

            if (assetsIndex >= 0)
                assetPath =
                    assetPath.Substring(
                        assetsIndex);

            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions
                    .ForceSynchronousImport);

            TextureImporter importer =
                AssetImporter.GetAtPath(
                    assetPath)
                as TextureImporter;

            if (importer == null)
                continue;

            importer.textureType =
                TextureImporterType.Default;
            importer.textureShape =
                TextureImporterShape.Texture2D;
            importer.mipmapEnabled = false;
            importer.textureCompression =
                TextureImporterCompression.Uncompressed;
            importer.filterMode =
                FilterMode.Point;
            importer.wrapMode =
                TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.npotScale =
                TextureImporterNPOTScale.None;
            importer.maxTextureSize = 16384;
            importer.SaveAndReimport();
        }

        AssetDatabase.Refresh();
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
                    diag, true));

            AssetDatabase.Refresh();
        }
        catch (Exception e)
        {
            Debug.LogError(
                "AO v0.7 diagnostic: " + e);
        }
    }
}
