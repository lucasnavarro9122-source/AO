using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class AOMapFullAudit
{
    [Serializable] class MapRow
    {
        public int number;
        public string name;
        public int cells;
        public int npcs;
        public int objects;
        public int exits;
        public double parseMs;
    }

    [Serializable] class Issue
    {
        public int map;
        public string kind;
        public string detail;
    }

    [Serializable] class Report
    {
        public string utc;
        public int expectedMaps;
        public int checkedMaps;
        public long cells;
        public int checkedFrames;
        public int loadedTextures;
        public int exits;
        public int validExits;
        public int missingDestinationExits;
        public int specialExits;
        public int invalidDestinationCoordinates;
        public int issuesTotal;
        public double elapsedSeconds;
        public List<MapRow> maps = new List<MapRow>();
        public List<Issue> issues = new List<Issue>();
    }

    static readonly string MapResourceRoot = "AOMigrator/WorldV07/Maps/map_";
    static readonly string TextureResourceRoot = "AOMigrator/WorldV07/Textures/tex_";
    static readonly Dictionary<int, Vector2Int> textureSizes =
        new Dictionary<int, Vector2Int>();
    static readonly Dictionary<int, RectInt> bounds =
        new Dictionary<int, RectInt>();
    static readonly List<Tuple<int, AOWorldManagerV07.Exit>> exits =
        new List<Tuple<int, AOWorldManagerV07.Exit>>();
    static readonly Stopwatch timer = new Stopwatch();
    static List<int> numbers;
    static Report report;
    static int next;

    [InitializeOnLoadMethod]
    static void RunOnceAfterImport()
    {
        string target = Path.GetFullPath(Path.Combine(Application.dataPath,
            "..", "MigrationReports", "unity_full_audit.json"));
        if (!File.Exists(target))
            EditorApplication.delayCall += Run;
    }

    [MenuItem("AO Migrador/QA datos/Barrer mapas en Resources")]
    public static void Run()
    {
        if (numbers != null)
        {
            UnityEngine.Debug.LogWarning("El barrido ya está en curso.");
            return;
        }

        string folder = Path.Combine(Application.dataPath,
                                     "Resources", "AOMigrator", "WorldV07", "Maps");
        numbers = Directory.GetFiles(folder, "map_*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name.StartsWith("map_", StringComparison.Ordinal) &&
                           int.TryParse(name.Substring(4), out _))
            .Select(name => int.Parse(name.Substring(4)))
            .Distinct().OrderBy(number => number).ToList();
        report = new Report { utc = DateTime.UtcNow.ToString("o"),
                              expectedMaps = numbers.Count };
        textureSizes.Clear();
        bounds.Clear();
        exits.Clear();
        next = 0;
        timer.Restart();
        EditorApplication.update += Step;
        UnityEngine.Debug.Log("AO_FULL_AUDIT_START maps=" + numbers.Count);
    }

    static void Step()
    {
        if (next >= numbers.Count)
        {
            Finish();
            return;
        }

        int number = numbers[next++];
        EditorUtility.DisplayProgressBar("AO: barrido de mapas",
            number + " (" + next + "/" + numbers.Count + ")",
            next / (float)numbers.Count);
        TextAsset asset = null;
        try
        {
            asset = Resources.Load<TextAsset>(MapResourceRoot + number);
            if (asset == null)
            {
                AddIssue(number, "missing_map", "Resources.Load devolvió null");
                return;
            }
            long start = Stopwatch.GetTimestamp();
            var data = JsonUtility.FromJson<AOWorldManagerV07.WorldMapData>(
                asset.text);
            double parseMs = (Stopwatch.GetTimestamp() - start) *
                             1000.0 / Stopwatch.Frequency;
            Validate(number, data, parseMs);
            report.checkedMaps++;
        }
        catch (Exception e)
        {
            AddIssue(number, "exception", e.GetType().Name + ": " + e.Message);
        }
        finally
        {
            if (asset != null)
                Resources.UnloadAsset(asset);
        }
    }

    static void Validate(int number,
                         AOWorldManagerV07.WorldMapData data,
                         double parseMs)
    {
        if (data == null || data.mapNumber != number ||
            data.cells == null || data.sprites == null ||
            data.exits == null || data.npcs == null || data.objects == null)
        {
            AddIssue(number, "invalid_map", "JSON o listas principales inválidas");
            return;
        }
        if (data.xmin > data.xmax || data.ymin > data.ymax)
            AddIssue(number, "invalid_bounds", "Límites invertidos");
        bounds[number] = new RectInt(data.xmin, data.ymin,
            data.xmax - data.xmin + 1, data.ymax - data.ymin + 1);

        HashSet<int> spriteIds = new HashSet<int>();
        foreach (var sprite in data.sprites)
        {
            if (!spriteIds.Add(sprite.id))
                AddIssue(number, "duplicate_sprite", sprite.id.ToString());
            if (sprite.frames != null && sprite.frames.Length > 0)
                foreach (var frame in sprite.frames)
                    CheckFrame(number, frame);
            else
                CheckFrame(number, new AOWorldManagerV07.FrameSpec {
                    fileNum = sprite.fileNum, sx = sprite.sx, sy = sprite.sy,
                    width = sprite.width, height = sprite.height });
        }

        foreach (var cell in data.cells)
        {
            if (cell.x < data.xmin || cell.x > data.xmax ||
                cell.y < data.ymin || cell.y > data.ymax ||
                cell.layer < 1 || cell.layer > 4 ||
                !spriteIds.Contains(cell.sprite))
                AddIssue(number, "invalid_cell", cell.x + "," + cell.y +
                         " layer=" + cell.layer + " sprite=" + cell.sprite);
        }
        foreach (var npc in data.npcs)
        {
            if (npc.directions == null)
                continue;
            foreach (var direction in npc.directions)
            {
                CheckFrames(number, direction.body);
                CheckFrames(number, direction.head);
                CheckFrames(number, direction.helmet);
                CheckFrames(number, direction.weapon);
                CheckFrames(number, direction.shield);
            }
        }
        foreach (var obj in data.objects)
            CheckFrames(number, obj.frames);
        foreach (var exit in data.exits)
            exits.Add(Tuple.Create(number, exit));

        report.cells += data.cells.Length;
        report.maps.Add(new MapRow {
            number = number, name = data.mapName, cells = data.cells.Length,
            npcs = data.npcs.Length, objects = data.objects.Length,
            exits = data.exits.Length, parseMs = parseMs });
    }

    static void CheckFrames(int number,
                            AOWorldManagerV07.FrameSpec[] frames)
    {
        if (frames == null)
            return;
        foreach (var frame in frames)
            CheckFrame(number, frame);
    }

    static void CheckFrame(int number,
                           AOWorldManagerV07.FrameSpec frame)
    {
        report.checkedFrames++;
        if (frame == null)
        {
            AddIssue(number, "null_frame", "Cuadro nulo");
            return;
        }
        if (!textureSizes.TryGetValue(frame.fileNum, out Vector2Int size))
        {
            Texture2D texture = Resources.Load<Texture2D>(
                TextureResourceRoot + frame.fileNum);
            if (texture == null)
            {
                AddIssue(number, "missing_texture", frame.fileNum.ToString());
                return;
            }
            size = new Vector2Int(texture.width, texture.height);
            textureSizes[frame.fileNum] = size;
        }
        if (frame.sx < 0 || frame.sy < 0 || frame.width <= 0 ||
            frame.height <= 0 || frame.sx + frame.width > size.x ||
            frame.sy + frame.height > size.y)
            AddIssue(number, "invalid_crop", "tex=" + frame.fileNum +
                     " " + frame.sx + "," + frame.sy + " " +
                     frame.width + "x" + frame.height);
    }

    static void Finish()
    {
        EditorApplication.update -= Step;
        EditorUtility.ClearProgressBar();
        foreach (var entry in exits)
        {
            int map = entry.Item1;
            var exit = entry.Item2;
            report.exits++;
            if (exit.destMap <= 0)
            {
                report.specialExits++;
                continue;
            }
            if (!bounds.TryGetValue(exit.destMap, out RectInt destination))
            {
                report.missingDestinationExits++;
                AddIssue(map, "missing_destination", "map=" + exit.destMap);
            }
            else if (!destination.Contains(new Vector2Int(exit.destX, exit.destY)))
            {
                report.invalidDestinationCoordinates++;
                AddIssue(map, "invalid_destination_coordinates",
                         "map=" + exit.destMap +
                         " @ " + exit.destX + "," + exit.destY);
            }
            else
                report.validExits++;
        }
        report.loadedTextures = textureSizes.Count;
        timer.Stop();
        report.elapsedSeconds = timer.Elapsed.TotalSeconds;
        string target = Path.GetFullPath(Path.Combine(Application.dataPath,
            "..", "MigrationReports", "unity_full_audit.json"));
        File.WriteAllText(target, JsonUtility.ToJson(report, true));
        UnityEngine.Debug.Log("AO_FULL_AUDIT_DONE maps=" + report.checkedMaps +
            "/" + report.expectedMaps + " frames=" + report.checkedFrames +
            " textures=" + report.loadedTextures +
            " exits=" + report.exits + " issues=" + report.issuesTotal +
            " seconds=" + report.elapsedSeconds.ToString("F1"));
        numbers = null;
    }

    static void AddIssue(int map, string kind, string detail)
    {
        report.issuesTotal++;
        if (report.issues.Count < 200)
            report.issues.Add(new Issue { map = map, kind = kind, detail = detail });
    }
}
