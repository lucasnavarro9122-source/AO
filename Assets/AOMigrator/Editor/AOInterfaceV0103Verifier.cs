using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AOInterfaceV0103Verifier
{
    const string MiniFolder =
        "Assets/Resources/AOMigrator/MinimapsV0103";
    const string MapFolder =
        "Assets/Resources/AOMigrator/WorldV07/Maps";

    const string Diagnostic =
        "Assets/AOMigrator/interface_v0103_diagnostic.json";

    [Serializable]
    class Report
    {
        public string version =
            "0.10.3-alpha";
        public string unity;
        public bool success;
        public string message;
        public int minimaps;
        public int maps;
        public int missingMinimaps;
        public bool interfaceScript;
        public bool inventoryScript;
        public bool playerFound;
        public bool worldFound;
    }

    [MenuItem(
        "AO Migrador/Verificar interfaz v0.10.3")]
    public static void Verify()
    {
        Report report =
            new Report();

        try
        {
            report.unity =
                Application.unityVersion;

            report.interfaceScript =
                File.Exists(
                    "Assets/AOMigrator/Runtime/AOInterfaceV0101.cs");

            report.inventoryScript =
                File.Exists(
                    "Assets/AOMigrator/Runtime/AOInventoryV10.cs");

            report.minimaps =
                Directory.Exists(MiniFolder)
                ? Directory.GetFiles(
                    MiniFolder,
                    "map_*.png",
                    SearchOption.TopDirectoryOnly)
                    .Length
                : 0;

            if (Directory.Exists(MapFolder))
            {
                string[] mapFiles = Directory.GetFiles(
                    MapFolder, "map_*.json", SearchOption.TopDirectoryOnly);
                report.maps = mapFiles.Length;
                foreach (string mapFile in mapFiles)
                {
                    string number = Path.GetFileNameWithoutExtension(mapFile);
                    if (!File.Exists(Path.Combine(MiniFolder, number + ".png")))
                        report.missingMinimaps++;
                }
            }

            report.playerFound =
                UnityEngine.Object
                    .FindFirstObjectByType
                        <AOTestPlayer>() !=
                null;

            report.worldFound =
                UnityEngine.Object
                    .FindFirstObjectByType
                        <AOWorldManagerV07>() !=
                null;

            report.success =
                report.interfaceScript &&
                report.inventoryScript &&
                report.maps > 0 &&
                report.missingMinimaps == 0 &&
                report.playerFound &&
                report.worldFound;

            report.message =
                "Minimapas=" +
                report.minimaps +
                "/" + report.maps +
                ", Faltantes=" + report.missingMinimaps +
                ", Player=" +
                report.playerFound +
                ", World=" +
                report.worldFound;

            File.WriteAllText(
                Diagnostic,
                JsonUtility.ToJson(
                    report,
                    true));

            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "AO UI v0.10.3",
                (report.success
                    ? "OK\n\n"
                    : "Falta algo\n\n") +
                report.message +
                "\n\nEn Play probá M, Enter y drag & drop.",
                "OK");
        }
        catch (Exception e)
        {
            report.success = false;
            report.message =
                e.ToString();

            File.WriteAllText(
                Diagnostic,
                JsonUtility.ToJson(
                    report,
                    true));

            AssetDatabase.Refresh();

            Debug.LogError(
                "AO UI v0.10.3 verify: " +
                e);
        }
    }
}
