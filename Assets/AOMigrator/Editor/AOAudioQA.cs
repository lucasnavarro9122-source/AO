using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Create Temp/run_audio_qa.flag to run one short Play-mode audio check.
public static class AOAudioQA
{
    [Serializable]
    class Report
    {
        public string utc;
        public int listeners;
        public int generalClips;
        public int cityClips;
        public int magicClips;
        public bool clickPlaying;
        public bool weatherLoop;
        public bool passed;
        public string error;
    }

    static readonly string Root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    static readonly string Request = Path.Combine(Root, "Temp", "run_audio_qa.flag");
    static readonly string Output = Path.Combine(Root, "MigrationReports", "unity_audio_qa.json");
    const string ScenePath =
        "Assets/Scenes/AOMigrator/Generated/AO_Ciudad_de_Ullathorpe_Playable.unity";

    static int frames;
    static bool checking;

    [InitializeOnLoadMethod]
    static void OnImport()
    {
        if (!File.Exists(Request))
            return;

        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(Request))
                return;
            if (EditorApplication.isPlaying)
            {
                StartCheck();
                return;
            }
            if (SceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.isPlaying = true;
        };
    }

    static void OnPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !File.Exists(Request))
            return;
        StartCheck();
    }

    static void StartCheck()
    {
        if (checking)
            return;
        checking = true;
        frames = 10;
        EditorApplication.update += Check;
    }

    static void Check()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorApplication.update -= Check;
            checking = false;
            return;
        }
        if (frames-- > 0)
            return;

        EditorApplication.update -= Check;
        checking = false;
        EditorApplication.playModeStateChanged -= OnPlayMode;
        Report report = new Report { utc = DateTime.UtcNow.ToString("o") };
        try
        {
            report.listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(
                FindObjectsSortMode.None).Length;

            report.generalClips = CountClips("AudioV190");
            report.cityClips = CountClips("CityV130/Audio");
            report.magicClips = CountClips("MagicV129/Audio");

            AOAudioV190.PlayEffect(500, 0.3f);
            AOAudioV190 audio = UnityEngine.Object.FindFirstObjectByType<AOAudioV190>();
            AudioSource[] sources = audio == null
                ? Array.Empty<AudioSource>()
                : audio.GetComponents<AudioSource>();
            report.clickPlaying = sources.Length >= 2 && sources[0].isPlaying;

            AOAudioV190.SetWeather(true);
            report.weatherLoop = sources.Length >= 2 &&
                                 sources[1].isPlaying && sources[1].loop;
            AOAudioV190.SetWeather(false);

            report.passed = report.listeners == 1 &&
                            report.generalClips == 41 &&
                            report.cityClips == 34 &&
                            report.magicClips == 25 &&
                            report.clickPlaying && report.weatherLoop;
        }
        catch (Exception e)
        {
            report.error = e.ToString();
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Output));
        File.WriteAllText(Output, JsonUtility.ToJson(report, true));
        if (File.Exists(Request)) File.Delete(Request);
        Debug.Log("AO_AUDIO_QA_DONE passed=" + report.passed +
                  " listeners=" + report.listeners +
                  " clips=" + report.generalClips + "/" + report.cityClips +
                  "/" + report.magicClips +
                  " error=" + report.error);
        EditorApplication.isPlaying = false;
    }

    static int CountClips(string subfolder)
    {
        string folder = Path.Combine(Application.dataPath, "Resources", "AOMigrator", subfolder);
        int loaded = 0;
        foreach (string path in Directory.GetFiles(folder))
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension != ".wav" && extension != ".ogg")
                continue;
            string resource = "AOMigrator/" + subfolder + "/" +
                              Path.GetFileNameWithoutExtension(path);
            if (Resources.Load<AudioClip>(resource) != null)
                loaded++;
        }
        return loaded;
    }
}
