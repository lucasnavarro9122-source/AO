using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class AOInterfaceV0101SetupWindow : EditorWindow
{
    const string UiFolder =
        "Assets/Resources/AOMigrator/InterfaceV0101";

    const string DiagnosticPath =
        "Assets/AOMigrator/interface_v0101_setup_diagnostic.json";

    [Serializable]
    class Diagnostic
    {
        public string version =
            "0.10.1-alpha";
        public string unity;
        public bool success;
        public string message;
        public bool playerFound;
        public bool combatFound;
        public bool inventoryFound;
        public bool worldFound;
        public bool cameraFound;
        public bool interfaceCreated;
        public int uiTextures;
    }

    string status =
        "Listo para agregar la interfaz original AO.";

    Vector2 scroll;

    [MenuItem(
        "AO Migrador/Interfaz original v0.10.1 - activar")]
    public static void Open()
    {
        GetWindow
            <AOInterfaceV0101SetupWindow>(
                "AO UI v0.10.1");
    }

    void OnGUI()
    {
        scroll =
            EditorGUILayout.BeginScrollView(
                scroll);

        GUILayout.Label(
            "AO Migrador v0.10.1 — Interfaz original",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Integra la ventana principal original de AO en Unity: " +
            "viewport 736x608, chat, inventario/hechizos, panel Stats/Menu, " +
            "vida, mana, stamina, hambre, sed y experiencia. " +
            "El inventario v0.10 se dibuja dentro del panel original.",
            MessageType.Info);

        if (GUILayout.Button(
                "Activar / reparar interfaz original",
                GUILayout.Height(42)))
        {
            Setup();
        }

        GUILayout.Space(8);

        EditorGUILayout.HelpBox(
            "Después:\n" +
            "1. Guardá Ctrl+S.\n" +
            "2. Play.\n" +
            "3. F8 entrega el kit de prueba.\n" +
            "4. Inventario: click selecciona, doble click equipa/quita.\n" +
            "5. Los botones INVENTARIO/HECHIZOS y STATS/MENU son clickeables.\n\n" +
            "Mana/stamina/hambre/sed todavía son placeholders hasta el sistema RPG v0.11.",
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
        Diagnostic diag =
            new Diagnostic();

        try
        {
            if (EditorApplication.isPlaying)
            {
                throw new Exception(
                    "Salí de Play antes de activar la interfaz.");
            }

            diag.unity =
                Application.unityVersion;

            if (!Directory.Exists(
                    UiFolder))
            {
                throw new Exception(
                    "Falta " +
                    UiFolder);
            }

            string[] textures =
                Directory.GetFiles(
                    UiFolder,
                    "*.png",
                    SearchOption.TopDirectoryOnly);

            diag.uiTextures =
                textures.Length;

            if (textures.Length < 10)
            {
                throw new Exception(
                    "Faltan assets de interfaz. PNG encontrados: " +
                    textures.Length);
            }

            ConfigureTextures(
                textures);

            AOTestPlayer player =
                UnityEngine.Object
                    .FindFirstObjectByType
                        <AOTestPlayer>();

            diag.playerFound =
                player != null;

            if (player == null)
            {
                throw new Exception(
                    "No encontré AO Test Player.");
            }

            AOPlayerCombatV09 combat =
                player.GetComponent
                    <AOPlayerCombatV09>();

            AOInventoryV10 inventory =
                player.GetComponent
                    <AOInventoryV10>();

            AOWorldManagerV07 world =
                UnityEngine.Object
                    .FindFirstObjectByType
                        <AOWorldManagerV07>();

            AOCameraFollow follow =
                UnityEngine.Object
                    .FindFirstObjectByType
                        <AOCameraFollow>();

            Camera cam =
                follow != null
                ? follow.GetComponent<Camera>()
                : Camera.main;

            diag.combatFound =
                combat != null;
            diag.inventoryFound =
                inventory != null;
            diag.worldFound =
                world != null;
            diag.cameraFound =
                cam != null;

            if (combat == null)
            {
                throw new Exception(
                    "No encontré AOPlayerCombatV09. " +
                    "Activá v0.9 primero.");
            }

            if (inventory == null)
            {
                throw new Exception(
                    "No encontré AOInventoryV10. " +
                    "Activá v0.10 primero.");
            }

            GameObject go =
                GameObject.Find(
                    "AO Interface v0.10.1");

            if (go == null)
            {
                go =
                    new GameObject(
                        "AO Interface v0.10.1");

                Undo.RegisterCreatedObjectUndo(
                    go,
                    "Activar interfaz AO");

                diag.interfaceCreated =
                    true;
            }

            AOInterfaceV0101 ui =
                go.GetComponent
                    <AOInterfaceV0101>();

            if (ui == null)
            {
                ui =
                    Undo.AddComponent
                        <AOInterfaceV0101>(
                            go);
            }

            ui.Configure(
                player,
                combat,
                inventory,
                world,
                cam);

            EditorUtility.SetDirty(ui);
            EditorSceneManager.MarkSceneDirty(
                EditorSceneManager
                    .GetActiveScene());

            diag.success = true;

            diag.message =
                "Interfaz original AO activada. " +
                "Assets=" +
                textures.Length +
                ". Guardá Ctrl+S.";

            WriteDiagnostic(diag);

            status =
                "OK. Interfaz lista con " +
                textures.Length +
                " texturas originales/adaptadas.";

            Selection.activeGameObject =
                go;

            EditorUtility.DisplayDialog(
                "AO UI v0.10.1",
                status +
                "\n\nGuardá la escena con Ctrl+S y probá Play.",
                "OK");
        }
        catch (Exception e)
        {
            diag.success = false;
            diag.message =
                e.ToString();

            WriteDiagnostic(
                diag);

            status =
                "ERROR: " +
                e.Message;

            Debug.LogError(
                "AO UI v0.10.1 setup: " +
                e);

            EditorUtility.DisplayDialog(
                "AO UI v0.10.1 - error",
                e.Message +
                "\n\nSubime " +
                DiagnosticPath + ".",
                "OK");
        }
        finally
        {
            EditorUtility
                .ClearProgressBar();
        }
    }

    static void ConfigureTextures(
        string[] files)
    {
        for (int i = 0;
             i < files.Length;
             i++)
        {
            if (i % 5 == 0)
            {
                EditorUtility
                    .DisplayProgressBar(
                        "AO UI v0.10.1",
                        "Configurando assets " +
                        (i + 1) +
                        "/" +
                        files.Length,
                        files.Length == 0
                        ? 1f
                        : (float)i /
                          files.Length);
            }

            string path =
                files[i]
                    .Replace("\\", "/");

            int idx =
                path.IndexOf(
                    "Assets/",
                    StringComparison
                        .OrdinalIgnoreCase);

            if (idx >= 0)
                path =
                    path.Substring(idx);

            AssetDatabase.ImportAsset(
                path,
                ImportAssetOptions
                    .ForceSynchronousImport);

            TextureImporter importer =
                AssetImporter.GetAtPath(
                    path)
                as TextureImporter;

            if (importer == null)
                continue;

            importer.textureType =
                TextureImporterType.Default;

            importer.textureShape =
                TextureImporterShape.Texture2D;

            importer.mipmapEnabled =
                false;

            importer.textureCompression =
                TextureImporterCompression
                    .Uncompressed;

            importer.filterMode =
                FilterMode.Point;

            importer.wrapMode =
                TextureWrapMode.Clamp;

            importer.alphaIsTransparency =
                true;

            importer.npotScale =
                TextureImporterNPOTScale.None;

            importer.maxTextureSize =
                2048;

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
                    diag,
                    true));

            AssetDatabase.Refresh();
        }
        catch (Exception e)
        {
            Debug.LogError(
                "AO UI diagnostic: " +
                e);
        }
    }
}
