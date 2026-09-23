using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class AORPGV11SetupWindow : EditorWindow
{
    const string TextureFolder =
        "Assets/Resources/AOMigrator/CharacterV111/Textures";

    string[] raceNames = {
        "Humano",
        "Elfo",
        "Elfo Oscuro",
        "Gnomo",
        "Enano",
        "Orco"
    };

    string[] genderNames = {
        "Hombre",
        "Mujer"
    };

    string[] classNames = {
        "Mago",
        "Clérigo",
        "Guerrero",
        "Asesino",
        "Bardo",
        "Druida",
        "Paladín",
        "Cazador",
        "Trabajador",
        "Pirata",
        "Ladrón",
        "Bandido"
    };

    int raceIndex;
    int genderIndex;
    int classIndex = 2;
    int headPopupIndex;

    int[] validHeads =
        new int[0];

    string[] headLabels =
        new string[0];

    [MenuItem(
        "AO Migrador/Personaje RPG v0.11 - configurar")]
    public static void Open()
    {
        GetWindow<AORPGV11SetupWindow>(
            "AO RPG v0.11.1");
    }

    void OnEnable()
    {
        AOPlayerRPGV11 rpg =
            UnityEngine.Object
                .FindFirstObjectByType
                    <AOPlayerRPGV11>();

        if (rpg != null)
        {
            raceIndex =
                Mathf.Clamp(
                    rpg.RaceId - 1,
                    0,
                    raceNames.Length - 1);

            genderIndex =
                Mathf.Clamp(
                    rpg.GenderId - 1,
                    0,
                    genderNames.Length - 1);

            classIndex =
                Mathf.Clamp(
                    rpg.ClassId - 1,
                    0,
                    classNames.Length - 1);
        }

        RefreshHeads();

        AOCharacterProfileVisualV111 visual =
            UnityEngine.Object
                .FindFirstObjectByType
                    <AOCharacterProfileVisualV111>();

        if (visual != null)
        {
            int current =
                visual.HeadIndex;

            for (int i = 0;
                 i < validHeads.Length;
                 i++)
            {
                if (validHeads[i] ==
                    current)
                {
                    headPopupIndex = i;
                    break;
                }
            }
        }
    }

    void OnGUI()
    {
        GUILayout.Label(
            "AO v0.11.1 — Perfil + apariencia real",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Raza/género ahora cambian el Body y Head usando DarCuerpo + ValidarCabeza del servidor. " +
            "Las armaduras usan RopajeHumano/Elfo/Drow/Gnomo/Enano/Orco y sus variantes femeninas.",
            MessageType.Info);

        int oldRace =
            raceIndex;

        int oldGender =
            genderIndex;

        raceIndex =
            EditorGUILayout.Popup(
                "Raza",
                raceIndex,
                raceNames);

        genderIndex =
            EditorGUILayout.Popup(
                "Género",
                genderIndex,
                genderNames);

        classIndex =
            EditorGUILayout.Popup(
                "Clase",
                classIndex,
                classNames);

        if (oldRace != raceIndex ||
            oldGender != genderIndex)
        {
            RefreshHeads();
            headPopupIndex = 0;
        }

        if (validHeads.Length > 0)
        {
            headPopupIndex =
                EditorGUILayout.Popup(
                    "Cabeza",
                    Mathf.Clamp(
                        headPopupIndex,
                        0,
                        validHeads.Length - 1),
                    headLabels);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "No hay heads renderizables para este perfil.",
                MessageType.Warning);
        }

        GUILayout.Space(8);

        if (GUILayout.Button(
                "Aplicar perfil + apariencia",
                GUILayout.Height(42)))
        {
            ApplyProfile();
        }

        if (GUILayout.Button(
                "Solo resincronizar apariencia",
                GUILayout.Height(30)))
        {
            SyncAppearance();
        }

        GUILayout.Space(8);

        EditorGUILayout.HelpBox(
            "La lista de heads está filtrada por las reglas oficiales y por los heads que realmente existen en este Recursos-master.\n\n" +
            "En Play el bootstrap v0.11.1 vuelve a sincronizar automáticamente la apariencia.",
            MessageType.None);
    }

    void RefreshHeads()
    {
        validHeads =
            AOCharacterVisualDatabaseV111
                .ValidHeads(
                    raceIndex + 1,
                    genderIndex + 1);

        headLabels =
            new string[
                validHeads.Length];

        for (int i = 0;
             i < validHeads.Length;
             i++)
        {
            headLabels[i] =
                "Head " +
                validHeads[i];
        }
    }

    void ApplyProfile()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "AO RPG v0.11.1",
                "Salí de Play antes de cambiar el perfil.",
                "OK");

            return;
        }

        ConfigureTextures();

        AOTestPlayer player =
            UnityEngine.Object
                .FindFirstObjectByType
                    <AOTestPlayer>();

        if (player == null)
        {
            EditorUtility.DisplayDialog(
                "AO RPG v0.11.1",
                "No encontré AO Test Player.",
                "OK");

            return;
        }

        AOPlayerRPGV11 rpg =
            player.GetComponent
                <AOPlayerRPGV11>();

        if (rpg == null)
        {
            rpg =
                Undo.AddComponent
                    <AOPlayerRPGV11>(
                        player.gameObject);
        }

        AOCharacterProfileVisualV111 visual =
            player.GetComponent
                <AOCharacterProfileVisualV111>();

        if (visual == null)
        {
            visual =
                Undo.AddComponent
                    <AOCharacterProfileVisualV111>(
                        player.gameObject);
        }

        rpg.InitializeProfile(
            raceIndex + 1,
            genderIndex + 1,
            classIndex + 1,
            true);

        int head =
            validHeads.Length > 0
            ? validHeads[
                Mathf.Clamp(
                    headPopupIndex,
                    0,
                    validHeads.Length - 1)]
            : 0;

        visual.ApplyProfile(
            rpg,
            head);

        EditorUtility.SetDirty(rpg);
        EditorUtility.SetDirty(visual);

        AOInventoryV10 inventory =
            player.GetComponent
                <AOInventoryV10>();

        if (inventory != null)
        {
            inventory
                .ValidateEquipmentForRPG();

            inventory
                .RefreshVisualEquipment();

            EditorUtility.SetDirty(
                inventory);
        }

        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager
                .GetActiveScene());

        EditorUtility.DisplayDialog(
            "AO RPG v0.11.1",
            "Perfil visual aplicado:\n" +
            raceNames[raceIndex] +
            " / " +
            genderNames[genderIndex] +
            " / " +
            classNames[classIndex] +
            "\nHead " +
            head +
            "\n\nGuardá Ctrl+S.",
            "OK");
    }

    void SyncAppearance()
    {
        ConfigureTextures();

        AOPlayerRPGV11 rpg =
            UnityEngine.Object
                .FindFirstObjectByType
                    <AOPlayerRPGV11>();

        AOCharacterProfileVisualV111 visual =
            UnityEngine.Object
                .FindFirstObjectByType
                    <AOCharacterProfileVisualV111>();

        if (rpg == null)
        {
            EditorUtility.DisplayDialog(
                "AO RPG v0.11.1",
                "No encontré AOPlayerRPGV11.",
                "OK");
            return;
        }

        if (visual == null)
        {
            visual =
                Undo.AddComponent
                    <AOCharacterProfileVisualV111>(
                        rpg.gameObject);
        }

        int head =
            validHeads.Length > 0
            ? validHeads[
                Mathf.Clamp(
                    headPopupIndex,
                    0,
                    validHeads.Length - 1)]
            : 0;

        visual.ApplyProfile(
            rpg,
            head);

        EditorUtility.SetDirty(
            visual);

        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager
                .GetActiveScene());

        SceneView.RepaintAll();
    }

    static void ConfigureTextures()
    {
        if (!Directory.Exists(
                TextureFolder))
            return;

        string[] files =
            Directory.GetFiles(
                TextureFolder,
                "tex_*.png",
                SearchOption.TopDirectoryOnly);

        for (int i = 0;
             i < files.Length;
             i++)
        {
            if (i % 10 == 0)
            {
                EditorUtility
                    .DisplayProgressBar(
                        "AO v0.11.1",
                        "Configurando character textures " +
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
                    .Replace(
                        "\\",
                        "/");

            int idx =
                path.IndexOf(
                    "Assets/",
                    StringComparison
                        .OrdinalIgnoreCase);

            if (idx >= 0)
                path =
                    path.Substring(
                        idx);

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

        EditorUtility
            .ClearProgressBar();

        AssetDatabase.Refresh();
    }
}
