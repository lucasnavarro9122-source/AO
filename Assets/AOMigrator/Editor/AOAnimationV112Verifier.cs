using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AOAnimationV112Verifier
{
    const string DiagnosticPath =
        "Assets/AOMigrator/animation_v112_diagnostic.json";

    [Serializable]
    class Report
    {
        public string version =
            "0.11.2-alpha";

        public string unity;
        public bool success;
        public string message;

        public bool playerFound;
        public bool rendererFound;
        public bool inventoryFound;

        public int currentBodyFrames;
        public int currentWeaponFrames;
        public int currentShieldFrames;

        public int testDaggerFramesNorth;
        public int testDaggerFramesSouth;
        public int testShieldFramesNorth;
        public int testShieldFramesSouth;
        public int testArmorBodyId;
    }

    [MenuItem(
        "AO Migrador/Verificar animaciones jugador v0.11.2")]
    public static void Verify()
    {
        Report report =
            new Report();

        try
        {
            report.unity =
                Application.unityVersion;

            AOTestPlayer player =
                UnityEngine.Object
                    .FindFirstObjectByType
                        <AOTestPlayer>();

            report.playerFound =
                player != null;

            AOCharacterRenderer renderer =
                player == null
                ? null
                : player.GetComponentInChildren
                    <AOCharacterRenderer>(true);

            report.rendererFound =
                renderer != null;

            AOInventoryV10 inventory =
                player == null
                ? null
                : player.GetComponent
                    <AOInventoryV10>();

            report.inventoryFound =
                inventory != null;

            if (renderer != null)
            {
                renderer.ForceRefreshVisuals();

                report.currentBodyFrames =
                    renderer.CurrentBodyFrameCount;

                report.currentWeaponFrames =
                    renderer.CurrentWeaponFrameCount;

                report.currentShieldFrames =
                    renderer.CurrentShieldFrameCount;
            }

            AOItemDatabaseV10.ItemDef dagger =
                AOItemDatabaseV10.Get(460);

            AOItemDatabaseV10.ItemDef shield =
                AOItemDatabaseV10.Get(3488);

            AOItemDatabaseV10.ItemDef armor =
                AOItemDatabaseV10.Get(464);

            report.testDaggerFramesNorth =
                CountPart(
                    dagger,
                    AOGridMap.NORTH,
                    "weapon");

            report.testDaggerFramesSouth =
                CountPart(
                    dagger,
                    AOGridMap.SOUTH,
                    "weapon");

            report.testShieldFramesNorth =
                CountPart(
                    shield,
                    AOGridMap.NORTH,
                    "shield");

            report.testShieldFramesSouth =
                CountPart(
                    shield,
                    AOGridMap.SOUTH,
                    "shield");

            AOPlayerRPGV11 rpg =
                player == null
                ? null
                : player.GetComponent
                    <AOPlayerRPGV11>();

            report.testArmorBodyId =
                armor == null ||
                rpg == null
                ? 0
                : armor.BodyForProfile(
                    rpg.RaceId,
                    rpg.GenderId);

            report.success =
                report.playerFound &&
                report.rendererFound &&
                report.inventoryFound &&
                report.currentBodyFrames > 1 &&
                report.testDaggerFramesNorth > 1 &&
                report.testDaggerFramesSouth > 1 &&
                report.testShieldFramesNorth > 1 &&
                report.testShieldFramesSouth > 1;

            report.message =
                "Body actual=" +
                report.currentBodyFrames +
                " | Daga N/S=" +
                report.testDaggerFramesNorth +
                "/" +
                report.testDaggerFramesSouth +
                " | Escudo N/S=" +
                report.testShieldFramesNorth +
                "/" +
                report.testShieldFramesSouth +
                " | ArmorBody=" +
                report.testArmorBodyId;

            File.WriteAllText(
                DiagnosticPath,
                JsonUtility.ToJson(
                    report,
                    true));

            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "AO Animaciones v0.11.2",
                (report.success
                    ? "OK\n\n"
                    : "Hay algo para revisar\n\n") +
                report.message +
                "\n\nProbá mantener W/A/S/D varios tiles seguidos y después equipar/quitar F8.",
                "OK");
        }
        catch (Exception e)
        {
            report.success = false;
            report.message =
                e.ToString();

            File.WriteAllText(
                DiagnosticPath,
                JsonUtility.ToJson(
                    report,
                    true));

            AssetDatabase.Refresh();

            Debug.LogError(
                "AO v0.11.2 verifier: " +
                e);
        }
    }

    static int CountPart(
        AOItemDatabaseV10.ItemDef item,
        int heading,
        string part)
    {
        if (item == null ||
            item.visualDirections == null)
            return 0;

        foreach (
            AOItemDatabaseV10.DirectionSpec d in
            item.visualDirections)
        {
            if (d == null ||
                d.heading != heading)
                continue;

            if (part == "weapon")
                return d.weapon == null
                    ? 0
                    : d.weapon.Length;

            if (part == "shield")
                return d.shield == null
                    ? 0
                    : d.shield.Length;

            if (part == "body")
                return d.body == null
                    ? 0
                    : d.body.Length;
        }

        return 0;
    }
}
