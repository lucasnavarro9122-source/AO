using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// One Play run visits every imported map and renders representative snapshots.
public static class AOMapTourQA
{
    [Serializable] class Visit
    {
        public int map;
        public string name;
        public double loadMs;
        public string error;
        public string screenshot;
        public int sampledColors;
    }

    [Serializable] class Report
    {
        public string utc;
        public string scene;
        public int expected;
        public int visited;
        public int failed;
        public string fatalError;
        public List<Visit> maps = new List<Visit>();
    }

    static readonly HashSet<int> Captures = new HashSet<int>
        { 1, 2, 4, 40, 119, 126, 266, 289, 448, 462, 600, 700, 843 };
    static readonly string ScenePath =
        "Assets/Scenes/AOMigrator/Generated/AO_Ciudad_de_Ullathorpe_Playable.unity";
    static readonly string ReportPath = Path.GetFullPath(Path.Combine(
        Application.dataPath, "..", "MigrationReports", "unity_map_tour.json"));
    static readonly string ImageFolder = Path.GetFullPath(Path.Combine(
        Application.dataPath, "..", "MigrationReports", "VisualTour"));

    static Report report;
    static int[] mapNumbers;
    static int next;
    static int pauseFrames;
    static DateTime deadline;
    static string latestError;

    [InitializeOnLoadMethod]
    static void OnImport()
    {
        if (File.Exists(ReportPath)) return;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        if (EditorApplication.isPlaying)
        { EditorApplication.delayCall += Begin; return; }
        if (SessionState.GetBool("AOMapTourStartedV3", false)) return;
        EditorApplication.delayCall += () =>
        {
            if (File.Exists(ReportPath) || EditorApplication.isPlaying) return;
            if (SceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath);
            SessionState.SetBool("AOMapTourStartedV3", true);
            EditorApplication.isPlaying = true;
        };
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode &&
            !File.Exists(ReportPath)) Begin();
    }

    static void Begin()
    {
        if (report != null) return;
        string resourcePath = Path.GetFullPath(Path.Combine(
            Application.dataPath, "Resources", "AOMigrator", "WorldV07", "Maps"));
        string[] paths = Directory.GetFiles(resourcePath, "map_*.json");
        List<int> numbers = new List<int>();
        foreach (string path in paths)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (int.TryParse(name.Substring(4), out int number)) numbers.Add(number);
        }
        numbers.Sort();
        mapNumbers = numbers.ToArray();
        report = new Report { utc = DateTime.UtcNow.ToString("o"),
            scene = SceneManager.GetActiveScene().name, expected = mapNumbers.Length };
        next = 0;
        pauseFrames = 3;
        deadline = DateTime.UtcNow.AddMinutes(25);
        Directory.CreateDirectory(ImageFolder);
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Step;
        Debug.Log("AO_MAP_TOUR_START count=" + mapNumbers.Length);
    }

    static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception)
            latestError = message;
    }

    static void Step()
    {
        if (!EditorApplication.isPlaying)
        { Finish("Play mode ended early."); return; }
        if (DateTime.UtcNow > deadline)
        { Finish("Map tour exceeded 25 minutes."); return; }
        if (pauseFrames-- > 0) return;
        pauseFrames = 1;
        AOWorldManagerV07 world = UnityEngine.Object.FindFirstObjectByType<AOWorldManagerV07>();
        if (world == null || world.IsLoading) return;
        if (next >= mapNumbers.Length)
        { Finish(null); return; }

        int number = mapNumbers[next++];
        latestError = null;
        Visit visit = new Visit { map = number };
        try
        {
            AOMainMenuV140 menu = UnityEngine.Object.FindFirstObjectByType<AOMainMenuV140>();
            if (menu != null) menu.HideForVisualQA();
            AOCharacterCreationV170 creator =
                UnityEngine.Object.FindFirstObjectByType<AOCharacterCreationV170>();
            if (creator != null) creator.HideForVisualQA();
            Time.timeScale = 1f;
            int x = number == 40 ? 46 : 50;
            int y = number == 40 ? 28 : 50;
            world.LoadMap(number, x, y, true);
            if (world.CurrentMapNumber != number)
                throw new InvalidOperationException("CurrentMapNumber mismatch");
            visit.name = world.CurrentMapName;
            visit.loadMs = world.LastLoadMetrics == null ? -1 :
                world.LastLoadMetrics.totalMs;
            if (Captures.Contains(number)) Capture(number, visit);
            if (latestError != null) throw new Exception(latestError);
        }
        catch (Exception ex)
        {
            visit.error = ex.GetType().Name + ": " + ex.Message;
            report.failed++;
            Debug.LogWarning("AO_MAP_TOUR_VISIT_FAILED map=" + number +
                             " reason=" + visit.error);
        }
        report.maps.Add(visit);
        report.visited++;
        if (next % 100 == 0)
            Debug.Log("AO_MAP_TOUR_PROGRESS " + next + "/" + mapNumbers.Length +
                      " failed=" + report.failed);
    }

    static void Capture(int number, Visit visit)
    {
        AOCameraFollow follow =
            UnityEngine.Object.FindFirstObjectByType<AOCameraFollow>();
        Camera camera = follow != null ? follow.GetComponent<Camera>() : Camera.main;
        if (camera == null) throw new Exception("Camera.main missing");
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture render = new RenderTexture(960, 600, 16);
        Texture2D texture = new Texture2D(960, 600, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = render;
            RenderTexture.active = render;
            camera.Render();
            texture.ReadPixels(new Rect(0, 0, 960, 600), 0, 0);
            texture.Apply();
            HashSet<Color32> colors = new HashSet<Color32>();
            for (int y = 20; y < 600; y += 40)
                for (int x = 20; x < 960; x += 40)
                    colors.Add(texture.GetPixel(x, y));
            visit.sampledColors = colors.Count;
            string file = "map_" + number + ".png";
            File.WriteAllBytes(Path.Combine(ImageFolder, file), texture.EncodeToPNG());
            visit.screenshot = "VisualTour/" + file;
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            UnityEngine.Object.Destroy(render);
            UnityEngine.Object.Destroy(texture);
        }
    }

    static void Finish(string fatal)
    {
        EditorApplication.update -= Step;
        Application.logMessageReceived -= OnLog;
        report.fatalError = fatal;
        File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
        Debug.Log("AO_MAP_TOUR_DONE " + report.visited + "/" + report.expected +
                  " failed=" + report.failed + " fatal=" + (fatal ?? "none"));
        report = null;
        EditorApplication.isPlaying = false;
    }
}
