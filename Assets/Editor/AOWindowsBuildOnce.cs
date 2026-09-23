using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

internal static class AOWindowsBuildOnce
{
    static readonly string Root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    static readonly string Status = Path.Combine(Root, "Builds", "Windows", "build_status.txt");

    [MenuItem("AO/Build Windows")]
    public static void BuildNow()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Status));
        File.WriteAllText(Status, "IN_PROGRESS");

        try
        {
            string[] scenes = Array.ConvertAll(
                Array.FindAll(EditorBuildSettings.scenes, scene => scene.enabled),
                scene => scene.path);
            if (scenes.Length == 0)
                throw new InvalidOperationException("No hay escenas habilitadas para el build.");

            string output = Path.Combine(Root, "Builds", "Windows", "Argentum-Unity.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });

            File.WriteAllText(Status,
                report.summary.result + Environment.NewLine +
                output + Environment.NewLine +
                "Errors: " + report.summary.totalErrors + Environment.NewLine +
                "Warnings: " + report.summary.totalWarnings);
        }
        catch (Exception error)
        {
            File.WriteAllText(Status, "FAILED" + Environment.NewLine + error);
            Debug.LogException(error);
        }
    }
}
