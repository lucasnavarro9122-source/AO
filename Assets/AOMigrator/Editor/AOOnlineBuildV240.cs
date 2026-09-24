#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class AOOnlineBuildV240
{
    [MenuItem("AO Migrator/Build private room client")]
    public static void Build()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath,
            "..", "..", "AO_Online", "Release", "Client"));
        Directory.CreateDirectory(root);
        var scenes = new System.Collections.Generic.List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            if (scene.enabled) scenes.Add(scene.path);
        if (scenes.Count == 0)
            throw new System.InvalidOperationException("No hay escenas de juego activas.");
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = Path.Combine(root, "ArgentumOnline.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new System.InvalidOperationException("Build Windows falló: " +
                report.summary.result);
        Debug.Log("AO Online Windows build: " + root);
    }
}
#endif
