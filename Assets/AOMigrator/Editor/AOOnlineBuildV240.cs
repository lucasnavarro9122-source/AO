#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class AOOnlineBuildV240
{
    // Explicit local marker allows rebuilding an already-open editor without a
    // second Unity instance or changing any player/character data.
    [InitializeOnLoadMethod]
    static void WatchBuildRequest()
    {
        EditorApplication.update += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string restart = Path.Combine(root, "Temp", "restart_online_editor");
            if (File.Exists(restart))
            {
                // Never save on someone's behalf: restart only when no open scene has unsaved changes.
                File.Delete(restart);
                var dirty = new System.Collections.Generic.List<string>();
                for (int i = 0; i < UnityEditor.SceneManagement.EditorSceneManager.sceneCount; i++)
                {
                    var scene = UnityEditor.SceneManagement.EditorSceneManager.GetSceneAt(i);
                    if (scene.isDirty) dirty.Add(string.IsNullOrEmpty(scene.path) ? scene.name : scene.path);
                }
                string resultPath = Path.Combine(root, "Temp", "restart_online_editor_result.txt");
                if (dirty.Count > 0)
                {
                    File.WriteAllText(resultPath, "Not restarted: unsaved scene changes in " + string.Join(", ", dirty));
                    Debug.LogWarning("AO: no reinicio Unity; hay escenas con cambios sin guardar: " + string.Join(", ", dirty));
                    return;
                }
                File.WriteAllText(resultPath, "Restarting");
                EditorApplication.Exit(0);
                return;
            }
            string refresh = Path.Combine(root, "Temp", "refresh_online_client");
            if (File.Exists(refresh))
            {
                File.Delete(refresh);
                AssetDatabase.Refresh();
                return;
            }
            string marker = Path.Combine(root, "Temp", "build_online_client");
            if (!File.Exists(marker)) return;
            File.Delete(marker);
            string result = Path.Combine(root, "Temp", "build_online_client_result.txt");
            try { Build(); File.WriteAllText(result, "Succeeded"); }
            catch (System.Exception error) { File.WriteAllText(result, error.ToString()); Debug.LogException(error); }
        };
    }

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
