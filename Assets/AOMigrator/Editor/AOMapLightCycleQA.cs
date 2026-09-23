using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AOMapLightCycleQA
{
    [Serializable] class Report
    {
        public string utc;
        public int map;
        public double dayBrightness;
        public double nightBrightness;
        public bool passed;
        public float syncedHourAfterWait;
        public bool clockAdvanced;
        public int particleGroups;
        public int visibleParticleSprites;
        public int particleTypesWithCorners;
        public bool particleColorsValid;
        public double particleSceneBrightness;
        public int lateMapsCaptured;
        public string error;
    }

    static readonly string Root = Path.GetFullPath(Path.Combine(
        Application.dataPath, "..", "MigrationReports"));
    static readonly string Tour = Path.Combine(Root, "unity_map_tour.json");
    static readonly string Output = Path.Combine(Root, "unity_light_cycle_qa.json");
    static readonly string ScenePath =
        "Assets/Scenes/AOMigrator/Generated/AO_Ciudad_de_Ullathorpe_Playable.unity";
    static bool active;
    static int frames;
    static int stage;
    static Report result;
    static AOWorldManagerV07 world;
    static Camera camera;

    [InitializeOnLoadMethod]
    static void OnImport()
    {
        if (!File.Exists(Tour) || File.Exists(Output)) return;
        EditorApplication.playModeStateChanged += OnPlayMode;
        if (EditorApplication.isPlaying)
        { EditorApplication.delayCall += Begin; return; }
        if (SessionState.GetBool("AOLightCycleStartedV3", false)) return;
        EditorApplication.delayCall += () =>
        {
            if (File.Exists(Output) || EditorApplication.isPlaying) return;
            if (SceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath);
            SessionState.SetBool("AOLightCycleStartedV3", true);
            EditorApplication.isPlaying = true;
        };
    }

    static void OnPlayMode(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode) Begin();
    }

    static void Begin()
    {
        if (active || File.Exists(Output)) return;
        active = true;
        frames = 5;
        stage = 0;
        result = new Report { utc = DateTime.UtcNow.ToString("o"), map = 1 };
        EditorApplication.update += Step;
    }

    static void Step()
    {
        if (!EditorApplication.isPlaying)
        { EditorApplication.update -= Step; active = false; return; }
        if (frames-- > 0) return;
        try
        {
            if (stage == 0)
            {
                world = UnityEngine.Object.FindFirstObjectByType<AOWorldManagerV07>();
                AOCameraFollow follow =
                    UnityEngine.Object.FindFirstObjectByType<AOCameraFollow>();
                if (world == null || follow == null)
                    throw new Exception("World or follow camera missing");
                camera = follow.GetComponent<Camera>();
                world.LoadMap(1, 50, 50, true);
                world.SetWorldHour(13f);
                result.dayBrightness = Capture(camera, "light_day.png");
                world.SetWorldHour(0f);
                result.nightBrightness = Capture(camera, "light_night.png");
                result.passed = result.dayBrightness > result.nightBrightness * 1.15;
                if (!result.passed) result.error = "Night did not darken map 1";
                world.SetWorldHour(13f);
                world.SyncWorldTime(0, 60000);
                world.LoadMap(40, 46, 28, true);
                stage = 1;
                frames = 60;
                return;
            }
            AOMapParticleGroup[] groups =
                UnityEngine.Object.FindObjectsByType<AOMapParticleGroup>(
                    FindObjectsSortMode.None);
            TextAsset particleSource = Resources.Load<TextAsset>(
                "AOMigrator/WorldV07/particle_defs");
            AOMapParticleLibrary library = JsonUtility.FromJson<AOMapParticleLibrary>(
                particleSource.text);
            foreach (AOMapParticleDefinition definition in library.definitions)
                if (definition.cornerColors != null &&
                    definition.cornerColors.Length == 12)
                    result.particleTypesWithCorners++;
            result.particleColorsValid =
                result.particleTypesWithCorners == library.definitions.Length;
            if (!result.particleColorsValid)
                result.error = "Particle corner colors were not deserialized";
            result.syncedHourAfterWait = world.CurrentWorldHour;
            result.clockAdvanced = result.syncedHourAfterWait > 0.05f;
            if (!result.clockAdvanced)
                result.error = "Synced world clock did not advance";
            result.particleGroups = groups.Length;
            foreach (AOMapParticleGroup group in groups)
                foreach (SpriteRenderer renderer in
                         group.GetComponentsInChildren<SpriteRenderer>())
                    if (renderer.enabled) result.visibleParticleSprites++;
            result.particleSceneBrightness = Capture(camera, "map_40_particles.png");
            foreach (int map in new[] { 750, 780, 800, 840 })
            {
                world.LoadMap(map, 50, 50, true);
                if (world.CurrentMapNumber != map)
                    throw new Exception("Late map did not load: " + map);
                Capture(camera, "map_" + map + ".png");
                result.lateMapsCaptured++;
            }
        }
        catch (Exception ex)
        { result.error = ex.GetType().Name + ": " + ex.Message; }
        EditorApplication.update -= Step;
        File.WriteAllText(Output, JsonUtility.ToJson(result, true));
        Debug.Log("AO_LIGHT_CYCLE_QA_DONE passed=" + result.passed +
                  " day=" + result.dayBrightness +
                  " night=" + result.nightBrightness +
                  " error=" + (result.error ?? "none"));
        active = false;
        EditorApplication.isPlaying = false;
    }

    static double Capture(Camera camera, string file)
    {
        RenderTexture oldTarget = camera.targetTexture;
        RenderTexture oldActive = RenderTexture.active;
        RenderTexture render = new RenderTexture(960, 600, 16);
        Texture2D texture = new Texture2D(960, 600, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = render;
            RenderTexture.active = render;
            camera.Render();
            texture.ReadPixels(new Rect(0, 0, 960, 600), 0, 0);
            texture.Apply();
            double sum = 0;
            int count = 0;
            for (int y = 120; y < 590; y += 12)
            for (int x = 165; x < 620; x += 12)
            {
                Color32 pixel = texture.GetPixel(x, y);
                sum += pixel.r + pixel.g + pixel.b;
                count += 3;
            }
            File.WriteAllBytes(Path.Combine(Root, "VisualTour", file),
                               texture.EncodeToPNG());
            return sum / count;
        }
        finally
        {
            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            UnityEngine.Object.Destroy(render);
            UnityEngine.Object.Destroy(texture);
        }
    }
}
