using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Create Temp/run_classic_ui_qa.flag for one Play-mode screenshot and audit.
public static class AOClassicUIQA
{
    [Serializable]
    class Report
    {
        public string utc;
        public int screenWidth;
        public int screenHeight;
        public int citiesWithMaps;
        public int raceGenderPairsWithHeads;
        public bool creatorOpen;
        public bool journalOpen;
        public int journalDrawCalls;
        public bool passed;
        public string error;
    }

    static readonly string Root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    static readonly string Request = Path.Combine(Root, "Temp", "run_classic_ui_qa.flag");
    static readonly string ReportPath = Path.Combine(Root, "MigrationReports", "unity_classic_ui_qa.json");
    static readonly string Screenshot = Path.Combine(Root, "MigrationReports", "unity_classic_ui_qa.png");
    static readonly string MenuScreenshot = Path.Combine(Root, "MigrationReports", "unity_classic_menu_qa.png");
    const string ScenePath = "Assets/Scenes/AOMigrator/Generated/AO_Ciudad_de_Ullathorpe_Playable.unity";
    static int frames;
    static bool checking;
    static bool captured;
    static bool menuCaptured;
    static bool journalCaptured;
    static Report report;

    [InitializeOnLoadMethod]
    static void OnImport()
    {
        if (!File.Exists(Request)) return;
        EditorApplication.playModeStateChanged += OnMode;
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(Request)) return;
            if (EditorApplication.isPlaying) { StartCheck(); return; }
            if (SceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.isPlaying = true;
        };
    }

    static void OnMode(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && File.Exists(Request))
            StartCheck();
    }

    static void StartCheck()
    {
        if (checking) return;
        checking = true;
        captured = false;
        menuCaptured = false;
        journalCaptured = false;
        frames = 30;
        report = new Report { utc = DateTime.UtcNow.ToString("o") };
        EditorApplication.update += Check;
    }

    static void Check()
    {
        if (!EditorApplication.isPlaying)
        {
            Finish(false);
            return;
        }
        if (frames-- > 0) return;

        if (!menuCaptured)
        {
            menuCaptured = true;
            frames = 25;
            ScreenCapture.CaptureScreenshot(MenuScreenshot);
            return;
        }

        if (!captured)
        {
            try
            {
                AOMainMenuV140 menu = UnityEngine.Object.FindFirstObjectByType<AOMainMenuV140>();
                AOCharacterCreationV170 creator = UnityEngine.Object.FindFirstObjectByType<AOCharacterCreationV170>();
                if (menu == null || creator == null)
                    throw new Exception("Falta menú o creador en escena jugable.");
                menu.HideForVisualQA();
                creator.Open(false);
                report.creatorOpen = AOCharacterCreationV170.ModalOpen;
                report.screenWidth = Screen.width;
                report.screenHeight = Screen.height;

                for (int id = 1; id <= AOHomeCityV200.Names.Length; id++)
                {
                    if (AOHomeCityV200.TryGet(id, out _, out int map,
                                                out _, out _) &&
                        Resources.Load<TextAsset>("AOMigrator/WorldV07/Maps/map_" + map) != null)
                        report.citiesWithMaps++;
                }
                for (int race = 1; race <= 6; race++)
                    for (int gender = 1; gender <= 2; gender++)
                        if (AOCharacterVisualDatabaseV111.ValidHeads(race, gender).Length > 0)
                            report.raceGenderPairsWithHeads++;

                captured = true;
                frames = 25;
                ScreenCapture.CaptureScreenshot(Screenshot);
            }
            catch (Exception e)
            {
                report.error = e.ToString();
                Finish(false);
            }
            return;
        }

        if (!journalCaptured)
        {
            AOCharacterCreationV170 creator =
                UnityEngine.Object.FindFirstObjectByType<AOCharacterCreationV170>();
            AOQuestUIV150 journal =
                UnityEngine.Object.FindFirstObjectByType<AOQuestUIV150>();
            if (creator == null || journal == null)
            {
                report.error = "Falta creador o diario de misiones.";
                Finish(false);
                return;
            }
            creator.HideForVisualQA();
            AOQuestUIV150.VisualQAOverride = true;
            journal.OpenJournal();
            report.journalOpen = AOQuestUIV150.ModalOpen;
            journalCaptured = true;
            frames = 20;
            return;
        }

        report.journalDrawCalls = AOQuestUIV150.VisualQADrawCalls;
        Finish(report.creatorOpen && report.journalOpen &&
               report.journalDrawCalls > 0 &&
               report.citiesWithMaps == 6 &&
               report.raceGenderPairsWithHeads == 12 &&
               File.Exists(MenuScreenshot) && File.Exists(Screenshot));
    }

    static void Finish(bool passed)
    {
        EditorApplication.update -= Check;
        EditorApplication.playModeStateChanged -= OnMode;
        checking = false;
        report.passed = passed;
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
        if (File.Exists(Request)) File.Delete(Request);
        Debug.Log("AO_CLASSIC_UI_QA_DONE passed=" + passed + " error=" + report.error);
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
    }
}
