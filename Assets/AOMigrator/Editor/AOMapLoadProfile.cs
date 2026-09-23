using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Runs once after import, in the playable scene, then restores Edit mode.
public static class AOMapLoadProfile
{
    [Serializable] class Sample
    {
        public int map;
        public AOWorldManagerV07.MapLoadMetrics timing;
    }

    [Serializable] class Report
    {
        public string utc;
        public string scene;
        public string error;
        public bool snowPacketVisible;
        public bool fogPacketVisible;
        public bool rainPacketVisible;
        public List<Sample> samples = new List<Sample>();
    }

    static readonly int[] Sequence = { 1, 2, 40, 119, 126, 1 };
    static readonly int[] SpawnX = { 68, 50, 50, 50, 50, 68 };
    static readonly int[] SpawnY = { 43, 50, 50, 50, 50, 43 };
    static Report report;
    static int next;
    static int pauseFrames;
    static DateTime deadline;

    static string Output => Path.GetFullPath(Path.Combine(
        Application.dataPath, "..", "MigrationReports",
        "unity_load_profile.json"));

    [InitializeOnLoadMethod]
    static void StartAfterImport()
    {
        if (File.Exists(Output))
            return;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        if (EditorApplication.isPlaying)
        {
            EditorApplication.delayCall += Begin;
            return;
        }
        if (SessionState.GetBool("AOMapLoadProfileAttemptedV3", false))
            return;
        EditorApplication.delayCall += () =>
        {
            if (File.Exists(Output) || EditorApplication.isPlaying)
                return;
            if (SceneManager.GetActiveScene().name !=
                "AO_Ciudad_de_Ullathorpe_Playable")
                return;
            SessionState.SetBool("AOMapLoadProfileAttemptedV3", true);
            EditorApplication.isPlaying = true;
        };
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode &&
            !File.Exists(Output))
            Begin();
    }

    static void Begin()
    {
        if (report != null)
            return;
        report = new Report {
            utc = DateTime.UtcNow.ToString("o"),
            scene = SceneManager.GetActiveScene().name
        };
        deadline = DateTime.UtcNow.AddMinutes(3);
        next = 0;
        pauseFrames = 2;
        EditorApplication.update += Step;
        Debug.Log("AO_LOAD_PROFILE_START");
    }

    static void Step()
    {
        if (!EditorApplication.isPlaying)
        {
            Finish("Play mode ended before profiling finished.");
            return;
        }
        if (DateTime.UtcNow > deadline)
        {
            Finish("Timed out waiting for world or map loads.");
            return;
        }
        if (pauseFrames-- > 0)
            return;
        pauseFrames = 2;

        AOWorldManagerV07 world =
            UnityEngine.Object.FindFirstObjectByType<AOWorldManagerV07>();
        if (world == null || world.IsLoading)
            return;
        if (next == 4)
        {
            report.snowPacketVisible = world.CurrentWeather != null &&
                world.CurrentWeather.PrecipitationVisible;
            if (!report.snowPacketVisible)
            {
                Finish("SnowToggle did not show snow on map 119.");
                return;
            }
        }
        if (next == 5)
        {
            report.fogPacketVisible = world.CurrentWeather != null &&
                world.CurrentWeather.FogVisible;
            if (!report.fogPacketVisible)
            {
                Finish("NieblaToggle did not show fog on map 126.");
                return;
            }
        }
        if (next >= Sequence.Length)
        {
            report.rainPacketVisible = world.CurrentWeather != null &&
                world.CurrentWeather.PrecipitationVisible;
            if (!report.rainPacketVisible)
            {
                Finish("RainToggle did not show rain on map 1.");
                return;
            }
            Finish(null);
            return;
        }

        try
        {
            var menu = UnityEngine.Object.FindFirstObjectByType<AOMainMenuV140>();
            if (menu != null)
                menu.HideForVisualQA();
            var creator = UnityEngine.Object.FindFirstObjectByType<AOCharacterCreationV170>();
            if (creator != null)
                creator.HideForVisualQA();
            Time.timeScale = 1f;
            int map = Sequence[next];
            world.LoadMap(map, SpawnX[next], SpawnY[next], true);
            if (world.CurrentMapNumber != map || world.LastLoadMetrics == null)
                throw new InvalidOperationException("Map " + map + " did not load.");
            report.samples.Add(new Sample {
                map = map,
                timing = world.LastLoadMetrics
            });
            if (map == 119)
                world.ApplySnowToggle(true);
            else if (map == 126)
            {
                world.ApplySnowToggle(false);
                world.ApplyFogToggle(75);
            }
            else if (next == Sequence.Length - 1)
            {
                world.ApplyFogToggle(75);
                world.ApplyRainToggle(true);
            }
            next++;
        }
        catch (Exception e)
        {
            Finish(e.GetType().Name + ": " + e.Message);
        }
    }

    static void Finish(string error)
    {
        EditorApplication.update -= Step;
        report.error = error;
        File.WriteAllText(Output, JsonUtility.ToJson(report, true));
        Debug.Log("AO_LOAD_PROFILE_DONE samples=" + report.samples.Count +
                  " error=" + (error ?? "none"));
        report = null;
        EditorApplication.isPlaying = false;
    }
}
