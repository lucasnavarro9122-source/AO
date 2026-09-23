using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AOCharacterCreationV170Installer
{
    const string Diagnostic =
        "Assets/AOMigrator/character_creation_v170_diagnostic.json";

    [Serializable]
    class Report
    {
        public string version =
            "0.17.0-alpha";

        public string unity;

        public bool success;
        public string message;

        public bool playerFound;
        public bool identityFound;
        public bool creatorFound;
        public bool saveFound;
        public bool mainMenuFound;
        public bool rpgFound;
        public bool inventoryFound;
        public bool magicFound;
        public bool visualFound;
        public bool starterDataFound;

        public int validHeadsHumanMale;
        public int itemDatabaseCount;
        public int spellDatabaseCount;
    }

    [MenuItem(
        "AO Migrador/Creación personaje v0.17 - instalar/verificar")]
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

            AOCharacterIdentityV170 identity =
                player.GetComponent
                    <AOCharacterIdentityV170>();

            if (identity == null)
            {
                identity =
                    Undo.AddComponent
                        <AOCharacterIdentityV170>(
                            player.gameObject);
            }

            AOCharacterCreationV170 creator =
                player.GetComponent
                    <AOCharacterCreationV170>();

            if (creator == null)
            {
                creator =
                    Undo.AddComponent
                        <AOCharacterCreationV170>(
                            player.gameObject);
            }

            r.identityFound =
                identity != null;

            r.creatorFound =
                creator != null;

            r.saveFound =
                player.GetComponent
                    <AOSaveGameV140>() !=
                null;

            r.mainMenuFound =
                player.GetComponent
                    <AOMainMenuV140>() !=
                null;

            r.rpgFound =
                player.GetComponent
                    <AOPlayerRPGV11>() !=
                null;

            r.inventoryFound =
                player.GetComponent
                    <AOInventoryV10>() !=
                null;

            r.magicFound =
                player.GetComponent
                    <AOPlayerMagicV120>() !=
                null;

            r.visualFound =
                player.GetComponent
                    <AOCharacterProfileVisualV111>() !=
                null ||
                player.GetComponentInChildren
                    <AOCharacterProfileVisualV111>(true) !=
                null;

            r.starterDataFound =
                File.Exists(
                    "Assets/Resources/AOMigrator/CharacterCreationV170/starter_loadout.json");

            r.validHeadsHumanMale =
                AOCharacterVisualDatabaseV111
                    .ValidHeads(
                        1,
                        1)
                    .Length;

            r.itemDatabaseCount =
                AOItemDatabaseV10.Count;

            r.spellDatabaseCount =
                AOSpellDatabaseV120.Count;

            r.success =
                r.playerFound &&
                r.identityFound &&
                r.creatorFound &&
                r.saveFound &&
                r.mainMenuFound &&
                r.rpgFound &&
                r.inventoryFound &&
                r.magicFound &&
                r.visualFound &&
                r.starterDataFound &&
                r.validHeadsHumanMale > 0 &&
                r.itemDatabaseCount > 1000 &&
                r.spellDatabaseCount > 100;

            r.message =
                "Player=" +
                r.playerFound +
                " Identity=" +
                r.identityFound +
                " Creator=" +
                r.creatorFound +
                "\nSave=" +
                r.saveFound +
                " Menu=" +
                r.mainMenuFound +
                " RPG=" +
                r.rpgFound +
                " Inv=" +
                r.inventoryFound +
                " Magic=" +
                r.magicFound +
                "\nVisual=" +
                r.visualFound +
                " StarterData=" +
                r.starterDataFound +
                "\nHeads(Humano/H)=" +
                r.validHeadsHumanMale +
                " Items=" +
                r.itemDatabaseCount +
                " Spells=" +
                r.spellDatabaseCount;

            EditorUtility.SetDirty(
                identity);

            EditorUtility.SetDirty(
                creator);

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
                "AO Creación v0.17",
                (r.success
                    ? "OK\n\n"
                    : "Hay algo para revisar\n\n") +
                r.message +
                "\n\nGuardá Ctrl+S. En Play tocá Nueva partida.",
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
                "AO v0.17 installer: " +
                e);

            EditorUtility.DisplayDialog(
                "AO Creación v0.17 - error",
                e.Message +
                "\n\nSubime " +
                Diagnostic,
                "OK");
        }
    }
}
