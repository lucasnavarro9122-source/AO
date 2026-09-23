using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class AOInventoryV10SetupWindow : EditorWindow
{
    const string TextureFolder =
        "Assets/Resources/AOMigrator/ItemsV10/Textures";

    const string DatabaseFile =
        "Assets/Resources/AOMigrator/ItemsV10/items.json";

    const string DiagnosticPath =
        "Assets/AOMigrator/inventory_v10_setup_diagnostic.json";

    [Serializable]
    class Diagnostic
    {
        public string version =
            "0.10.0-alpha";
        public string unity;
        public bool success;
        public string message;
        public bool playerFound;
        public bool inventoryAdded;
        public bool combatFound;
        public int textureFiles;
        public long databaseBytes;
    }

    Vector2 scroll;

    string status =
        "Listo para activar inventario/equipo v0.10.";

    [MenuItem(
        "AO Migrador/Inventario y equipo v0.10 - activar")]
    public static void Open()
    {
        GetWindow<AOInventoryV10SetupWindow>(
            "AO Inventory v0.10");
    }

    void OnGUI()
    {
        scroll =
            EditorGUILayout.BeginScrollView(
                scroll);

        GUILayout.Label(
            "AO Migrador v0.10 - inventario + equipo",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Agrega 24 slots básicos (AO define 24 normales y 42 máximos), " +
            "base de objetos desde obj.dat, iconos, arma/armadura/escudo/casco " +
            "y conecta sus stats con el combate v0.9.",
            MessageType.Info);

        if (GUILayout.Button(
                "Activar / reparar v0.10",
                GUILayout.Height(42)))
        {
            Setup();
        }

        GUILayout.Space(8);

        EditorGUILayout.HelpBox(
            "En Play:\n" +
            "- I abre/cierra inventario.\n" +
            "- F8 entrega una sola vez un kit de prueba newbie.\n" +
            "- Click en slot > Equipar/Quitar.\n" +
            "- Ctrl/Espacio ataca usando el arma equipada.\n" +
            "- Loot recogido entra al inventario.\n\n" +
            "La armadura usa RopajeHumano en esta alfa; " +
            "restricciones de clase/raza se agregan después.",
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
                    "Salí de Play antes de activar v0.10.");
            }

            diag.unity =
                Application.unityVersion;

            if (!File.Exists(DatabaseFile))
            {
                throw new Exception(
                    "Falta " +
                    DatabaseFile);
            }

            diag.databaseBytes =
                new FileInfo(
                    DatabaseFile).Length;

            if (!Directory.Exists(
                    TextureFolder))
            {
                throw new Exception(
                    "Falta " +
                    TextureFolder);
            }

            string[] textures =
                Directory.GetFiles(
                    TextureFolder,
                    "tex_*.png",
                    SearchOption.TopDirectoryOnly);

            diag.textureFiles =
                textures.Length;

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

            diag.combatFound =
                combat != null;

            if (combat == null)
            {
                throw new Exception(
                    "No encontré AOPlayerCombatV09. " +
                    "Activá v0.9 primero.");
            }

            AOInventoryV10 inventory =
                player.GetComponent
                    <AOInventoryV10>();

            if (inventory == null)
            {
                inventory =
                    Undo.AddComponent
                        <AOInventoryV10>(
                            player.gameObject);

                diag.inventoryAdded =
                    true;
            }

            inventory
                .RefreshVisualEquipment();

            EditorUtility.SetDirty(
                inventory);

            EditorUtility.SetDirty(
                combat);

            EditorSceneManager.MarkSceneDirty(
                EditorSceneManager
                    .GetActiveScene());

            diag.success = true;

            diag.message =
                "Inventario v0.10 activado. " +
                "Texturas=" +
                textures.Length +
                ". Guardá Ctrl+S.";

            WriteDiagnostic(
                diag);

            status =
                "OK. Base de items encontrada, " +
                textures.Length +
                " texturas configuradas. " +
                (diag.inventoryAdded
                    ? "AOInventoryV10 agregado."
                    : "AOInventoryV10 ya existía.");

            EditorUtility.DisplayDialog(
                "AO Inventory v0.10",
                status +
                "\n\nGuardá Ctrl+S y probá Play.",
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
                "AO Inventory v0.10 setup: " +
                e);

            EditorUtility.DisplayDialog(
                "AO Inventory v0.10 - error",
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
        string[] textureFiles)
    {
        for (int i = 0;
             i < textureFiles.Length;
             i++)
        {
            if (i % 10 == 0)
            {
                EditorUtility
                    .DisplayProgressBar(
                        "AO v0.10",
                        "Configurando item textures " +
                        (i + 1) +
                        "/" +
                        textureFiles.Length,
                        textureFiles.Length == 0
                        ? 1f
                        : (float)i /
                          textureFiles.Length);
            }

            string path =
                textureFiles[i]
                    .Replace(
                        "\\",
                        "/");

            int idx =
                path.IndexOf(
                    "Assets/",
                    StringComparison
                        .OrdinalIgnoreCase);

            if (idx >= 0)
            {
                path =
                    path.Substring(
                        idx);
            }

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
                16384;

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
                "AO v0.10 diagnostic: " +
                e);
        }
    }
}
