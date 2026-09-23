using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Drop Temp/run_environment_v210_qa.flag to run a save-safe Play test.
public static class AOEnvironmentV210QA
{
    [Serializable]
    class Report
    {
        public bool doorOpened;
        public bool doorPassable;
        public bool doorClosed;
        public bool roofHiddenInsideChurch;
        public bool characterBehindFoliage;
        public bool cityMusicPlaying;
        public bool saveSessionInactive;
        public float roofAlpha;
        public string error;
    }

    static readonly string Root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    static readonly string Request = Path.Combine(Root, "Temp", "run_environment_v210_qa.flag");
    static readonly string Output = Path.Combine(Root, "MigrationReports", "unity_environment_v210_qa.json");
    const string ScenePath = "Assets/Scenes/AOMigrator/Generated/AO_Ciudad_de_Ullathorpe_Playable.unity";
    static Report report;
    static int frames;
    static bool started;

    [InitializeOnLoadMethod]
    static void OnImport()
    {
        if (!File.Exists(Request)) return;
        EditorApplication.playModeStateChanged += OnMode;
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(Request)) return;
            if (EditorApplication.isPlaying) { Start(); return; }
            if (SceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.isPlaying = true;
        };
    }

    static void OnMode(PlayModeStateChange mode)
    {
        if (mode == PlayModeStateChange.EnteredPlayMode && File.Exists(Request))
            Start();
    }

    static void Start()
    {
        if (started) return;
        started = true;
        report = new Report();
        frames = 30;
        EditorApplication.update += Step;
    }

    static void Step()
    {
        if (!EditorApplication.isPlaying)
        {
            Finish();
            return;
        }
        if (frames-- > 0) return;
        try
        {
            if (AOMainMenuV140.SessionActive)
                throw new Exception("Sesión guardable activa; prueba cancelada.");
            report.saveSessionInactive = true;
            AOMainMenuV140 menu = UnityEngine.Object.FindFirstObjectByType<AOMainMenuV140>();
            if (menu != null) menu.HideForVisualQA();
            AOCharacterCreationV170 creator =
                UnityEngine.Object.FindFirstObjectByType<AOCharacterCreationV170>();
            if (creator != null) creator.HideForVisualQA();

            AOWorldManagerV07 world =
                UnityEngine.Object.FindFirstObjectByType<AOWorldManagerV07>();
            AOGridMap grid = UnityEngine.Object.FindFirstObjectByType<AOGridMap>();
            if (world == null || grid == null)
                throw new Exception("Faltan mundo o grilla.");
            world.LoadMap(1, 72, 36, true);

            AODoorV210 door = AODoorV210.FindForInteraction(71, 35);
            if (door == null) throw new Exception("Falta puerta comercial 72,35.");
            door.TryToggle(out _);
            report.doorOpened = door.IsOpen;
            report.doorPassable = grid.CanEnter(71, 35, AOGridMap.NORTH) &&
                grid.CanEnter(72, 35, AOGridMap.NORTH) &&
                !grid.CanEnter(70, 35, AOGridMap.NORTH);
            door.TryToggle(out _);
            report.doorClosed = !door.IsOpen &&
                !grid.CanEnter(71, 35, AOGridMap.NORTH) &&
                !grid.CanEnter(72, 35, AOGridMap.NORTH);

            world.LoadMap(1, 78, 67, true);
            report.cityMusicPlaying = AOAudioV190.CurrentMapMusicId == 4;

            SpriteRenderer roof = null;
            foreach (SpriteRenderer renderer in
                     UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                if (renderer.name.StartsWith("L4_79_65_G", StringComparison.Ordinal))
                {
                    roof = renderer;
                    break;
                }
            if (roof == null) throw new Exception("Falta techo de iglesia 79,65.");

            report.characterBehindFoliage = AORenderOrderV210.Layer3(50) >
                AORenderOrderV210.Character(50);
            frames = 30;
            EditorApplication.update -= Step;
            void CheckRoof()
            {
                if (!EditorApplication.isPlaying) { EditorApplication.update -= CheckRoof; Finish(); return; }
                if (frames-- > 0) return;
                report.roofAlpha = roof == null ? -1f : roof.color.a;
                report.roofHiddenInsideChurch = roof != null && roof.color.a < 0.2f;
                EditorApplication.update -= CheckRoof;
                Finish();
            }
            EditorApplication.update += CheckRoof;
        }
        catch (Exception error)
        {
            report.error = error.ToString();
            Finish();
        }
    }

    static void Finish()
    {
        EditorApplication.update -= Step;
        EditorApplication.playModeStateChanged -= OnMode;
        started = false;
        if (report == null) report = new Report { error = "Play terminó antes de iniciar." };
        Directory.CreateDirectory(Path.GetDirectoryName(Output));
        File.WriteAllText(Output, JsonUtility.ToJson(report, true));
        if (File.Exists(Request)) File.Delete(Request);
        bool passed = report.saveSessionInactive && report.doorOpened &&
            report.doorPassable && report.doorClosed &&
            report.roofHiddenInsideChurch && report.characterBehindFoliage &&
            report.cityMusicPlaying;
        Debug.Log("AO_ENVIRONMENT_V210_QA_DONE passed=" + passed +
                  " error=" + report.error);
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
    }
}
