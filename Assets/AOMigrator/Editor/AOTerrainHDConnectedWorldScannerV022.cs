// AO Terrain HD v0.2.9
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using AOMigrator.TerrainHD;

namespace AOMigrator.TerrainHD.Editor
{
    [Serializable]
    internal sealed class AOMapCellV022
    {
        public int x;
        public int y;
        public int layer;
        public int grh;
        public int sprite;
    }

    [Serializable]
    internal sealed class AOMapGraphicV022
    {
        public int id;
        public int x;
        public int y;
        public int w;
        public int h;
        public string image;
    }

    [Serializable]
    internal sealed class AOMapDocumentV022
    {
        public string name;
        public int xmin;
        public int xmax;
        public int ymin;
        public int ymax;
        public AOMapCellV022[] cells;
        public AOMapGraphicV022[] sprites;
    }

    [Serializable]
    internal sealed class AOTextureBackupRecordV022
    {
        public string sourceAssetPath;
        public string backupAssetPath;
    }

    [Serializable]
    internal sealed class AOTextureBackupManifestV022
    {
        public string version = "0.2.9";
        public string createdUtc;
        public AOTextureBackupRecordV022[] records;
    }

    internal sealed class AONeighborCounterV022
    {
        public readonly Dictionary<string, int> north =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        public readonly Dictionary<string, int> east =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        public readonly Dictionary<string, int> south =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        public readonly Dictionary<string, int> west =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        public void Add(
            Dictionary<string, int> table,
            string key)
        {
            if (string.IsNullOrEmpty(key))
                return;

            int count;
            table.TryGetValue(key, out count);
            table[key] = count + 1;
        }

        public string Top(
            Dictionary<string, int> table)
        {
            if (table.Count == 0)
                return null;

            return table
                .OrderByDescending(p => p.Value)
                .First().Key;
        }
    }

    internal struct AOCropStatsV022
    {
        public float coverage;
        public float avgR;
        public float avgG;
        public float avgB;
        public float brightness;
        public float saturation;
        public float edgeDensity;
    }

    internal struct AOSpriteStatsV026
    {
        public float coverage;
        public float greenRatio;
        public float brownRatio;
        public float grayRatio;
        public float blueRatio;
        public float avgHue;
        public float avgSat;
        public float avgVal;
        public float edgeDensity;
        public float variance;
    }

    internal struct AOClassificationResultV023
    {
        public AOTerrainHDCategory category;
        public float confidence;
        public string reason;
    }

    internal sealed class AOMapCandidateV023
    {
        public int mapNumber;
        public string path;
        public int versionScore;
        public int cellCount;
        public int spriteCount;
        public long bytes;
        public int visualSpriteCount;
        public float visualSpriteRatio;
    }

    [Serializable]
    internal sealed class AOMapSourceChoiceV023
    {
        public int mapNumber;
        public string selected;
        public int duplicateCount;
        public int selectedVisualSprites;
        public float selectedVisualRatio;
        public string[] skipped;
    }

    [Serializable]
    internal sealed class AOMapSourceReportV023
    {
        public string version = "0.2.9";
        public string createdUtc;
        public int validJsonFiles;
        public int uniqueMaps;
        public int duplicateFilesSkipped;
        public AOMapSourceChoiceV023[] maps;
    }

    public static class
        AOTerrainHDConnectedWorldScannerV022
    {
        const string DataRoot =
            "Assets/AOMigrator/TerrainHDPipelineData";

        const string GeneratedClassicRoot =
            DataRoot + "/GeneratedClassic";

        const string BackupRoot =
            DataRoot + "/Backups/ClassicTextures";

        const string BackupManifestPath =
            BackupRoot + "/backup_manifest_v022.json";

        static readonly HashSet<int> ActiveWorldMaps =
            new HashSet<int>
            {
                1, 2, 3, 5, 6, 8, 9, 11, 12, 14,
                39, 40, 74, 77, 168, 395,
                600, 601, 602, 603, 748
            };

        public static void Scan(
            AOTerrainHDRegistry registry,
            bool layer1,
            bool layer2,
            bool layer3,
            bool layer4)
        {
            if (registry == null)
                return;

            List<string> mapJsons =
                FindMapJsonFiles();

            if (mapJsons.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "AO Terrain HD v0.2.9",
                    "No encontré map JSON válidos con cells[] y sprites[] dentro de Assets/AOMigrator.",
                    "Aceptar");
                return;
            }

            Dictionary<string, AOTerrainHDEntry> old =
                new Dictionary<string, AOTerrainHDEntry>(
                    StringComparer.OrdinalIgnoreCase);

            if (registry.entries != null)
            {
                foreach (AOTerrainHDEntry entry
                         in registry.entries)
                {
                    if (entry != null &&
                        !string.IsNullOrEmpty(entry.key))
                    {
                        old[entry.key] = entry;
                    }
                }
            }

            Dictionary<string, AOTerrainHDEntry> entries =
                new Dictionary<string, AOTerrainHDEntry>(
                    StringComparer.OrdinalIgnoreCase);

            Dictionary<string, HashSet<string>> mapsByKey =
                new Dictionary<string, HashSet<string>>(
                    StringComparer.OrdinalIgnoreCase);

            Dictionary<string, AONeighborCounterV022>
                neighborCounts =
                    new Dictionary<
                        string,
                        AONeighborCounterV022>(
                            StringComparer.OrdinalIgnoreCase);

            Dictionary<string, string> textureCache =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);

            Dictionary<int, Sprite> existingGrhSprites =
                BuildExistingGrhSpriteIndex();

            int validMaps = 0;

            try
            {
                for (int index = 0;
                     index < mapJsons.Count;
                     index++)
                {
                    string jsonPath =
                        mapJsons[index];

                    EditorUtility.DisplayProgressBar(
                        "AO Terrain HD v0.2.9",
                        "Analizando " +
                        Path.GetFileName(jsonPath),
                        index /
                        (float)mapJsons.Count);

                    AOMapDocumentV022 doc =
                        ReadMap(jsonPath);

                    if (doc == null ||
                        doc.cells == null ||
                        doc.sprites == null ||
                        doc.cells.Length == 0 ||
                        doc.sprites.Length == 0)
                        continue;

                    validMaps++;

                    Dictionary<int, AOMapGraphicV022>
                        graphics =
                            doc.sprites
                                .Where(g => g != null)
                                .GroupBy(g => g.id)
                                .ToDictionary(
                                    g => g.Key,
                                    g => g.First());

                    Dictionary<string, string>
                        tileKeys =
                            new Dictionary<
                                string,
                                string>();

                    HashSet<string> mapSeen =
                        new HashSet<string>(
                            StringComparer.OrdinalIgnoreCase);

                    foreach (AOMapCellV022 cell
                             in doc.cells)
                    {
                        if (cell == null ||
                            !LayerEnabled(
                                cell.layer,
                                layer1,
                                layer2,
                                layer3,
                                layer4))
                            continue;

                        int graphicId =
                            cell.sprite > 0
                            ? cell.sprite
                            : cell.grh;

                        if (graphicId <= 0)
                            continue;

                        AOMapGraphicV022 graphic;

                        if (!graphics.TryGetValue(
                            graphicId,
                            out graphic))
                            continue;

                        string key =
                            "GRH:" + graphicId;

                        AOTerrainHDEntry entry;

                        if (!entries.TryGetValue(
                            key,
                            out entry))
                        {
                            if (!old.TryGetValue(
                                key,
                                out entry))
                            {
                                entry =
                                    new AOTerrainHDEntry
                                    {
                                        key = key
                                    };
                            }

                            ResetCounts(entry);

                            entry.graphicId =
                                graphicId;

                            entry.displayName =
                                "GRH " +
                                graphicId +
                                " (" +
                                Path.GetFileName(
                                    graphic.image ??
                                    "") +
                                ")";

                            entry.imageFile =
                                graphic.image;

                            entry.cropX =
                                graphic.x;

                            entry.cropY =
                                graphic.y;

                            entry.cropWidth =
                                graphic.w;

                            entry.cropHeight =
                                graphic.h;

                            entry.sourceWidth =
                                Mathf.Max(
                                    1,
                                    graphic.w);

                            entry.sourceHeight =
                                Mathf.Max(
                                    1,
                                    graphic.h);

                            entry.sourcePixelsPerUnit =
                                32f;

                            entry.sourcePivotNormalized =
                                new Vector2(
                                    0.5f,
                                    0.5f);

                            ResolveEntryVisual(
                                entry,
                                graphic,
                                existingGrhSprites,
                                textureCache);

                            entries[key] = entry;
                        }

                        entry.usageCount++;

                        if (cell.layer == 1)
                            entry.layer1Uses++;
                        else if (cell.layer == 2)
                            entry.layer2Uses++;
                        else if (cell.layer == 3)
                            entry.layer3Uses++;
                        else if (cell.layer == 4)
                            entry.layer4Uses++;

                        mapSeen.Add(key);

                        string tileKey =
                            cell.layer +
                            ":" +
                            cell.x +
                            ":" +
                            cell.y;

                        tileKeys[tileKey] =
                            key;
                    }

                    foreach (string key in mapSeen)
                    {
                        HashSet<string> maps;

                        if (!mapsByKey.TryGetValue(
                            key,
                            out maps))
                        {
                            maps =
                                new HashSet<string>(
                                    StringComparer
                                        .OrdinalIgnoreCase);

                            mapsByKey[key] =
                                maps;
                        }

                        maps.Add(jsonPath);
                    }

                    AddAdjacency(
                        tileKeys,
                        neighborCounts);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            foreach (KeyValuePair<
                     string,
                     AOTerrainHDEntry> pair
                     in entries)
            {
                AOTerrainHDEntry entry =
                    pair.Value;

                HashSet<string> maps;

                if (mapsByKey.TryGetValue(
                    pair.Key,
                    out maps))
                {
                    entry.sceneCount =
                        maps.Count;
                }

                AONeighborCounterV022 n;

                if (neighborCounts.TryGetValue(
                    pair.Key,
                    out n))
                {
                    entry.northKey =
                        n.Top(n.north);

                    entry.eastKey =
                        n.Top(n.east);

                    entry.southKey =
                        n.Top(n.south);

                    entry.westKey =
                        n.Top(n.west);
                }

                AOOriginalGrhResolverV029.RepairOrResolveEntry(entry);

                AOClassificationResultV023 classification =
                    ClassifyEntry(entry);

                entry.suggestedCategory = classification.category;
                entry.classificationConfidence = classification.confidence;
                entry.classificationReason = classification.reason;

                if (!entry.categoryWasManuallySet)
                {
                    entry.category =
                        classification.category != AOTerrainHDCategory.Unclassified &&
                        classification.confidence >= 0.58f
                        ? classification.category
                        : AOTerrainHDCategory.Unclassified;
                }
            }

            registry.entries =
                entries.Values
                    .OrderByDescending(
                        e => e.ImpactScore)
                    .ThenBy(
                        e => e.graphicId)
                    .ToList();

            AOTerrainSemanticClassifierV029.RefineRegistry(
                registry,
                true);

            registry.pipelineVersion =
                "0.2.9";

            registry.scannedSceneCount =
                validMaps;

            registry.scannedUtc =
                DateTime.UtcNow.ToString("o");

            registry.RebuildLookup();

            EditorUtility.SetDirty(
                registry);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            AOTerrainHDDiagnosticV029.Generate(
                registry,
                false);

            int resolvedTextures =
                registry.entries.Count(
                    e => e != null &&
                         !string.IsNullOrEmpty(e.sourceTexturePath));

            int previewSprites =
                registry.entries.Count(
                    e => e != null && e.classicSprite != null);

            EditorUtility.DisplayDialog(
                "AO Terrain HD v0.2.9",
                "Connected World analizado.\n\n" +
                "Mapas activos esperados: 21" +
                "\nMapas activos encontrados: " + validMaps +
                "\nGRH/tile únicos: " + registry.entries.Count +
                "\nCeldas analizadas: " +
                registry.entries.Sum(e => e.usageCount) +
                "\nTexturas fuente resueltas: " + resolvedTextures +
                "\nPreviews reconstruidos: " + previewSprites +
                (validMaps == 21 ? "" :
                    "\n\nADVERTENCIA: faltan o sobran mapas activos."),
                "Aceptar");
        }

        static List<string> FindMapJsonFiles()
        {
            List<AOMapCandidateV023> candidates =
                new List<AOMapCandidateV023>();

            string[] files =
                Directory.GetFiles(
                    Application.dataPath,
                    "*.json",
                    SearchOption.AllDirectories);

            foreach (string absolute in files)
            {
                string normalized =
                    absolute.Replace('\\', '/');

                if (normalized.IndexOf(
                        "/TerrainHDPipelineData/",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    normalized.IndexOf(
                        "/Diagnostics/",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                string text;

                try
                {
                    text = File.ReadAllText(absolute);
                }
                catch
                {
                    continue;
                }

                if (text.IndexOf("\"cells\"", StringComparison.Ordinal) < 0 ||
                    text.IndexOf("\"sprites\"", StringComparison.Ordinal) < 0)
                    continue;

                AOMapDocumentV022 doc;

                try
                {
                    doc = JsonUtility.FromJson<AOMapDocumentV022>(text);
                }
                catch
                {
                    continue;
                }

                if (doc == null || doc.cells == null || doc.sprites == null ||
                    doc.cells.Length == 0 || doc.sprites.Length == 0)
                    continue;

                int mapNumber = ExtractMapNumber(normalized);
                if (mapNumber == int.MaxValue)
                    continue;

                long bytes = 0;
                try { bytes = new FileInfo(absolute).Length; }
                catch { }

                if (!ActiveWorldMaps.Contains(mapNumber))
                    continue;

                int visualSpriteCount =
                    doc.sprites.Count(
                        g => g != null &&
                             !String.IsNullOrEmpty(g.image) &&
                             g.w > 0 &&
                             g.h > 0);

                candidates.Add(
                    new AOMapCandidateV023
                    {
                        mapNumber = mapNumber,
                        path = absolute,
                        versionScore = ExtractVersionScore(normalized),
                        cellCount = doc.cells.Length,
                        spriteCount = doc.sprites.Length,
                        bytes = bytes,
                        visualSpriteCount = visualSpriteCount,
                        visualSpriteRatio = doc.sprites.Length > 0
                            ? visualSpriteCount / (float)doc.sprites.Length
                            : 0f
                    });
            }

            List<AOMapSourceChoiceV023> choices =
                new List<AOMapSourceChoiceV023>();

            List<string> selected =
                new List<string>();

            foreach (IGrouping<int, AOMapCandidateV023> group
                     in candidates.GroupBy(c => c.mapNumber).OrderBy(g => g.Key))
            {
                AOMapCandidateV023 best =
                    group
                        .OrderByDescending(c => c.visualSpriteRatio)
                        .ThenByDescending(c => c.visualSpriteCount)
                        .ThenByDescending(c => c.cellCount)
                        .ThenByDescending(c => c.spriteCount)
                        .ThenByDescending(c => c.versionScore)
                        .ThenByDescending(c => c.bytes)
                        .ThenBy(c => c.path.Length)
                        .First();

                selected.Add(best.path);

                choices.Add(
                    new AOMapSourceChoiceV023
                    {
                        mapNumber = group.Key,
                        selected = ToAssetRelative(best.path),
                        duplicateCount = Mathf.Max(0, group.Count() - 1),
                        selectedVisualSprites = best.visualSpriteCount,
                        selectedVisualRatio = best.visualSpriteRatio,
                        skipped = group
                            .Where(c => !String.Equals(
                                c.path,
                                best.path,
                                StringComparison.OrdinalIgnoreCase))
                            .Select(c => ToAssetRelative(c.path))
                            .ToArray()
                    });
            }

            WriteMapSourceReport(candidates.Count, choices);

            return selected;
        }

        static int ExtractMapNumber(string value)
        {
            if (string.IsNullOrEmpty(value))
                return int.MaxValue;

            System.Text.RegularExpressions.Match match =
                System.Text.RegularExpressions.Regex.Match(
                    value,
                    @"(?:mapa|map)[^0-9]*(\d+)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            int number;
            return match.Success &&
                   int.TryParse(match.Groups[1].Value, out number)
                ? number
                : int.MaxValue;
        }

        static int ExtractVersionScore(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0;

            int best = 0;

            System.Text.RegularExpressions.MatchCollection matches =
                System.Text.RegularExpressions.Regex.Matches(
                    value,
                    @"(?:world)?v(\d{1,3})(?:[_\.](\d{1,3}))?(?:[_\.](\d{1,3}))?",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                int major = 0;
                int minor = 0;
                int patch = 0;

                int.TryParse(match.Groups[1].Value, out major);
                if (match.Groups[2].Success)
                    int.TryParse(match.Groups[2].Value, out minor);
                if (match.Groups[3].Success)
                    int.TryParse(match.Groups[3].Value, out patch);

                int score = major * 1000000 + minor * 1000 + patch;
                if (score > best)
                    best = score;
            }

            return best;
        }

        static string ToAssetRelative(string absolute)
        {
            if (string.IsNullOrEmpty(absolute))
                return absolute;

            string normalized = absolute.Replace('\\', '/');
            string data = Application.dataPath.Replace('\\', '/');

            if (normalized.StartsWith(
                data + "/",
                StringComparison.OrdinalIgnoreCase))
            {
                return "Assets/" + normalized.Substring(data.Length + 1);
            }

            return normalized;
        }

        static void WriteMapSourceReport(
            int validJsonFiles,
            List<AOMapSourceChoiceV023> choices)
        {
            try
            {
                EnsureFolder(DataRoot + "/Diagnostics");

                AOMapSourceReportV023 report =
                    new AOMapSourceReportV023
                    {
                        createdUtc = DateTime.UtcNow.ToString("o"),
                        validJsonFiles = validJsonFiles,
                        uniqueMaps = choices.Count,
                        duplicateFilesSkipped = choices.Sum(c => c.duplicateCount),
                        maps = choices.ToArray()
                    };

                File.WriteAllText(
                    AbsolutePath(
                        DataRoot + "/Diagnostics/map_source_selection_v024.json"),
                    JsonUtility.ToJson(report, true),
                    new UTF8Encoding(false));
            }
            catch (Exception error)
            {
                Debug.LogWarning(
                    "[AO Terrain HD v0.2.9] No pude escribir selección de mapas: " +
                    error.Message);
            }
        }

        static AOMapDocumentV022 ReadMap(
            string path)
        {
            try
            {
                return JsonUtility.FromJson<
                    AOMapDocumentV022>(
                        File.ReadAllText(path));
            }
            catch
            {
                return null;
            }
        }

        static bool LayerEnabled(
            int layer,
            bool layer1,
            bool layer2,
            bool layer3,
            bool layer4)
        {
            if (layer == 1) return layer1;
            if (layer == 2) return layer2;
            if (layer == 3) return layer3;
            if (layer == 4) return layer4;

            return false;
        }

        static void ResetCounts(
            AOTerrainHDEntry entry)
        {
            entry.usageCount = 0;
            entry.sceneCount = 0;
            entry.layer1Uses = 0;
            entry.layer2Uses = 0;
            entry.layer3Uses = 0;
            entry.layer4Uses = 0;

            entry.northKey = null;
            entry.eastKey = null;
            entry.southKey = null;
            entry.westKey = null;
        }

        static Dictionary<int, Sprite> BuildExistingGrhSpriteIndex()
        {
            Dictionary<int, Sprite> result =
                new Dictionary<int, Sprite>();

            string[] guids =
                AssetDatabase.FindAssets(
                    "t:Sprite",
                    new[] { "Assets/AOMigrator" });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (String.IsNullOrEmpty(path))
                    continue;

                string normalized = path.Replace('\\', '/');

                if (normalized.IndexOf(
                        "/TerrainHDPipelineData/",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    normalized.IndexOf(
                        "/ImportedHD/",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    normalized.IndexOf(
                        "/GeneratedClassic/",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                UnityEngine.Object[] assets =
                    AssetDatabase.LoadAllAssetsAtPath(path);

                foreach (UnityEngine.Object asset in assets)
                {
                    Sprite sprite = asset as Sprite;
                    if (sprite == null)
                        continue;

                    int id = TryParseGrhId(
                        sprite.name,
                        Path.GetFileNameWithoutExtension(path));

                    if (id <= 0)
                        continue;

                    Sprite existing;

                    if (!result.TryGetValue(id, out existing))
                    {
                        result[id] = sprite;
                        continue;
                    }

                    string oldPath =
                        AssetDatabase.GetAssetPath(existing);

                    if (SpriteAssetScore(path) > SpriteAssetScore(oldPath))
                        result[id] = sprite;
                }
            }

            SpriteRenderer[] renderers =
                UnityEngine.Object.FindObjectsByType<SpriteRenderer>(
                    FindObjectsSortMode.None);

            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer == null || renderer.sprite == null)
                    continue;

                string path =
                    AssetDatabase.GetAssetPath(renderer.sprite);

                int id = TryParseGrhId(
                    renderer.sprite.name,
                    Path.GetFileNameWithoutExtension(path));

                if (id > 0 && !result.ContainsKey(id))
                    result[id] = renderer.sprite;
            }

            Debug.Log(
                "[AO Terrain HD v0.2.9] Sprite assets GRH indexados: " +
                result.Count);

            return result;
        }

        static int TryParseGrhId(
            string spriteName,
            string fileName)
        {
            string value =
                (spriteName ?? "") + " " + (fileName ?? "");

            System.Text.RegularExpressions.Match match =
                System.Text.RegularExpressions.Regex.Match(
                    value,
                    @"(?:GRH|SPRITE)[_\-\s]?(\d+)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            int id;

            if (match.Success &&
                int.TryParse(match.Groups[1].Value, out id))
                return id;

            return 0;
        }

        static Sprite TryResolveGrhSpriteFallback(
            int graphicId,
            Dictionary<int, Sprite> existingGrhSprites)
        {
            if (graphicId <= 0)
                return null;

            if (existingGrhSprites != null &&
                existingGrhSprites.TryGetValue(graphicId, out Sprite direct) &&
                direct != null)
                return direct;

            string[] queries =
            {
                "GRH_" + graphicId + " t:Sprite",
                "GRH" + graphicId + " t:Sprite",
                "Sprite_" + graphicId + " t:Sprite",
                graphicId + " t:Sprite"
            };

            Sprite best = null;
            int bestScore = Int32.MinValue;

            foreach (string query in queries)
            {
                string[] guids = AssetDatabase.FindAssets(query);

                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (String.IsNullOrEmpty(path))
                        continue;

                    string lower = path.Replace('\\', '/').ToLowerInvariant();
                    if (lower.Contains("terrainhdpipelinedata") ||
                        lower.Contains("importedhd") ||
                        lower.Contains("generatedclassic"))
                        continue;

                    UnityEngine.Object[] assets =
                        AssetDatabase.LoadAllAssetsAtPath(path);

                    foreach (UnityEngine.Object asset in assets)
                    {
                        Sprite sprite = asset as Sprite;
                        if (sprite == null)
                            continue;

                        int parsed = TryParseGrhId(
                            sprite.name,
                            Path.GetFileNameWithoutExtension(path));

                        bool numericExact =
                            sprite.name == graphicId.ToString() ||
                            Path.GetFileNameWithoutExtension(path) == graphicId.ToString();

                        if (parsed != graphicId && !numericExact)
                            continue;

                        int score = SpriteAssetScore(path);
                        if (lower.Contains("/world")) score += 50;
                        if (lower.Contains("/maps")) score += 40;
                        if (lower.Contains("/sprites/")) score += 35;
                        if (lower.Contains("character")) score -= 80;
                        if (lower.Contains("inventory")) score -= 80;

                        if (score > bestScore)
                        {
                            bestScore = score;
                            best = sprite;
                        }
                    }
                }
            }

            if (best != null && existingGrhSprites != null)
                existingGrhSprites[graphicId] = best;

            return best;
        }

        static int SpriteAssetScore(string path)
        {
            if (String.IsNullOrEmpty(path))
                return 0;

            string value = path.Replace('\\', '/').ToLowerInvariant();
            int score = 0;

            if (value.Contains("/sprites/")) score += 100;
            if (value.Contains("/world")) score += 70;
            if (value.Contains("/generated_")) score += 60;
            if (value.Contains("/textures/")) score += 20;
            if (value.Contains("character")) score -= 40;
            if (value.Contains("inventory")) score -= 30;
            if (value.Contains("terrainhdpipelinedata")) score -= 200;

            return score;
        }

        static void ResolveEntryVisual(
            AOTerrainHDEntry entry,
            AOMapGraphicV022 graphic,
            Dictionary<int, Sprite> existingGrhSprites,
            Dictionary<string, string> textureCache)
        {
            if (entry == null || graphic == null)
                return;

            entry.imageFile = graphic.image;
            entry.cropX = graphic.x;
            entry.cropY = graphic.y;
            entry.cropWidth = graphic.w;
            entry.cropHeight = graphic.h;
            entry.sourceWidth = Mathf.Max(1, graphic.w);
            entry.sourceHeight = Mathf.Max(1, graphic.h);
            entry.sourcePixelsPerUnit = 32f;
            entry.sourcePivotNormalized = new Vector2(0.5f, 0.5f);

            if (!String.IsNullOrEmpty(graphic.image))
            {
                entry.sourceTexturePath =
                    ResolveTexturePath(
                        graphic.image,
                        textureCache);
            }

            if (!String.IsNullOrEmpty(entry.sourceTexturePath) &&
                entry.cropWidth > 0 &&
                entry.cropHeight > 0)
            {
                entry.classicSprite = EnsureClassicSprite(entry);
            }

            Sprite existingSprite = entry.classicSprite == null
                ? TryResolveGrhSpriteFallback(
                    entry.graphicId,
                    existingGrhSprites)
                : null;

            if (entry.classicSprite == null &&
                existingSprite != null)
            {
                entry.classicSprite = existingSprite;
                entry.classicAssetPath =
                    AssetDatabase.GetAssetPath(existingSprite);

                Texture2D texture = existingSprite.texture;

                if (texture != null)
                {
                    string texturePath =
                        AssetDatabase.GetAssetPath(texture);

                    if (!String.IsNullOrEmpty(texturePath))
                    {
                        entry.sourceTexturePath = texturePath;
                        entry.imageFile = Path.GetFileName(texturePath);

                        Rect rect = existingSprite.textureRect;

                        entry.cropX = Mathf.RoundToInt(rect.x);
                        entry.cropWidth = Mathf.Max(
                            1,
                            Mathf.RoundToInt(rect.width));
                        entry.cropHeight = Mathf.Max(
                            1,
                            Mathf.RoundToInt(rect.height));

                        entry.cropY =
                            texture.height -
                            Mathf.RoundToInt(rect.y) -
                            entry.cropHeight;

                        entry.sourceWidth = entry.cropWidth;
                        entry.sourceHeight = entry.cropHeight;
                        entry.sourcePixelsPerUnit =
                            Mathf.Max(1f, existingSprite.pixelsPerUnit);

                        entry.sourcePivotNormalized =
                            new Vector2(
                                rect.width > 0f
                                    ? existingSprite.pivot.x / rect.width
                                    : 0.5f,
                                rect.height > 0f
                                    ? existingSprite.pivot.y / rect.height
                                    : 0.5f);
                    }
                }
            }

            if (entry.classicSprite == null &&
                !String.IsNullOrEmpty(entry.sourceTexturePath) &&
                entry.cropWidth > 0 &&
                entry.cropHeight > 0)
            {
                entry.classicSprite = EnsureClassicSprite(entry);
            }

            if (entry.classicSprite != null &&
                String.IsNullOrEmpty(entry.classicAssetPath))
            {
                entry.classicAssetPath =
                    AssetDatabase.GetAssetPath(entry.classicSprite);
            }

            string imageLabel =
                !String.IsNullOrEmpty(entry.imageFile)
                    ? Path.GetFileName(entry.imageFile)
                    : !String.IsNullOrEmpty(entry.sourceTexturePath)
                        ? Path.GetFileName(entry.sourceTexturePath)
                        : "sin textura";

            entry.displayName =
                "GRH " + entry.graphicId + " (" + imageLabel + ")";
        }

        static string ResolveTexturePath(
            string image,
            Dictionary<string, string> cache)
        {
            if (string.IsNullOrEmpty(image))
                return null;

            string cached;

            if (cache.TryGetValue(
                image,
                out cached))
                return cached;

            string filename =
                Path.GetFileName(image);

            string stem =
                Path.GetFileNameWithoutExtension(
                    filename);

            List<string> candidates =
                new List<string>();

            string[] guids =
                AssetDatabase.FindAssets(
                    stem + " t:Texture2D");

            foreach (string guid in guids)
            {
                string path =
                    AssetDatabase
                        .GUIDToAssetPath(guid);

                if (!string.Equals(
                    Path.GetFileName(path),
                    filename,
                    StringComparison.OrdinalIgnoreCase))
                    continue;

                candidates.Add(path);
            }

            if (candidates.Count == 0)
            {
                string[] files =
                    Directory.GetFiles(
                        Application.dataPath,
                        filename,
                        SearchOption.AllDirectories);

                foreach (string absolute
                         in files)
                {
                    string normalized =
                        absolute
                            .Replace('\\', '/');

                    string data =
                        Application.dataPath
                            .Replace('\\', '/');

                    if (!normalized.StartsWith(
                        data + "/",
                        StringComparison.OrdinalIgnoreCase))
                        continue;

                    candidates.Add(
                        "Assets/" +
                        normalized.Substring(
                            data.Length + 1));
                }
            }

            string selected =
                candidates
                    .OrderByDescending(
                        p =>
                            p.IndexOf(
                                "/Textures/",
                                StringComparison
                                    .OrdinalIgnoreCase)
                            >= 0)
                    .ThenBy(
                        p => p.Length)
                    .FirstOrDefault();

            cache[image] =
                selected;

            return selected;
        }

        static Sprite EnsureClassicSprite(
            AOTerrainHDEntry entry)
        {
            if (entry == null ||
                string.IsNullOrEmpty(
                    entry.sourceTexturePath) ||
                entry.cropWidth <= 0 ||
                entry.cropHeight <= 0)
                return null;

            EnsureFolder(
                GeneratedClassicRoot);

            string path =
                GeneratedClassicRoot +
                "/GRH_" +
                entry.graphicId +
                ".png";

            if (!File.Exists(
                AbsolutePath(path)))
            {
                Texture2D crop =
                    ReadCrop(entry);

                if (crop == null)
                    return null;

                File.WriteAllBytes(
                    AbsolutePath(path),
                    crop.EncodeToPNG());

                UnityEngine.Object
                    .DestroyImmediate(crop);

                AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions
                        .ForceSynchronousImport);

                ConfigureGeneratedSprite(
                    path,
                    32f);
            }

            Sprite sprite =
                AssetDatabase
                    .LoadAssetAtPath<Sprite>(
                        path);

            if (sprite == null)
            {
                AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions
                        .ForceSynchronousImport);

                ConfigureGeneratedSprite(
                    path,
                    32f);

                sprite =
                    AssetDatabase
                        .LoadAssetAtPath<Sprite>(
                            path);
            }

            return sprite;
        }

        static void ConfigureGeneratedSprite(
            string path,
            float pixelsPerUnit)
        {
            TextureImporter importer =
                AssetImporter.GetAtPath(
                    path)
                as TextureImporter;

            if (importer == null)
                return;

            importer.textureType =
                TextureImporterType.Sprite;

            importer.spriteImportMode =
                SpriteImportMode.Single;

            importer.mipmapEnabled = false;
            importer.filterMode =
                FilterMode.Point;

            importer.wrapMode =
                TextureWrapMode.Clamp;

            importer.textureCompression =
                TextureImporterCompression
                    .Uncompressed;

            importer.alphaIsTransparency =
                true;

            importer.npotScale =
                TextureImporterNPOTScale.None;

            importer.spritePixelsPerUnit =
                pixelsPerUnit;

            importer.spritePivot =
                new Vector2(
                    0.5f,
                    0.5f);

            importer.SaveAndReimport();
        }

        static Texture2D ReadCrop(
            AOTerrainHDEntry entry)
        {
            if (entry == null ||
                string.IsNullOrEmpty(
                    entry.sourceTexturePath))
                return null;

            string absolute =
                AbsolutePath(
                    entry.sourceTexturePath);

            if (!File.Exists(absolute))
                return null;

            Texture2D source =
                new Texture2D(
                    2,
                    2,
                    TextureFormat.RGBA32,
                    false);

            if (!source.LoadImage(
                File.ReadAllBytes(absolute),
                false))
            {
                UnityEngine.Object
                    .DestroyImmediate(source);
                return null;
            }

            int x =
                Mathf.Clamp(
                    entry.cropX,
                    0,
                    Mathf.Max(
                        0,
                        source.width - 1));

            int bottomY =
                source.height -
                entry.cropY -
                entry.cropHeight;

            bottomY =
                Mathf.Clamp(
                    bottomY,
                    0,
                    Mathf.Max(
                        0,
                        source.height - 1));

            int width =
                Mathf.Min(
                    entry.cropWidth,
                    source.width - x);

            int height =
                Mathf.Min(
                    entry.cropHeight,
                    source.height -
                    bottomY);

            if (width <= 0 ||
                height <= 0)
            {
                UnityEngine.Object
                    .DestroyImmediate(source);
                return null;
            }

            Texture2D crop =
                new Texture2D(
                    width,
                    height,
                    TextureFormat.RGBA32,
                    false);

            crop.SetPixels(
                source.GetPixels(
                    x,
                    bottomY,
                    width,
                    height));

            crop.Apply(
                false,
                false);

            UnityEngine.Object
                .DestroyImmediate(source);

            return crop;
        }

        static Texture2D ReadSpritePixelsUniversal(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
                return null;

            Texture2D source = sprite.texture;
            Rect rect = sprite.textureRect;
            int x = Mathf.Clamp(Mathf.RoundToInt(rect.x), 0, Mathf.Max(0, source.width - 1));
            int y = Mathf.Clamp(Mathf.RoundToInt(rect.y), 0, Mathf.Max(0, source.height - 1));
            int width = Mathf.Min(Mathf.Max(1, Mathf.RoundToInt(rect.width)), source.width - x);
            int height = Mathf.Min(Mathf.Max(1, Mathf.RoundToInt(rect.height)), source.height - y);
            if (width <= 0 || height <= 0) return null;

            try
            {
                if (source.isReadable)
                {
                    Texture2D direct = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    direct.SetPixels(source.GetPixels(x, y, width, height));
                    direct.Apply(false, false);
                    return direct;
                }
            }
            catch { }

            RenderTexture temporary = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
                temporary.filterMode = FilterMode.Point;
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;

                Texture2D full = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                full.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
                full.Apply(false, false);

                Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
                result.SetPixels(full.GetPixels(x, y, width, height));
                result.Apply(false, false);
                UnityEngine.Object.DestroyImmediate(full);
                return result;
            }
            catch (Exception error)
            {
                Debug.LogWarning("[AO Terrain HD v0.2.9] No pude leer pixels de " + sprite.name + ": " + error.Message);
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                if (temporary != null) RenderTexture.ReleaseTemporary(temporary);
            }
        }

        static AOSpriteStatsV026 MeasureSpriteStats(Sprite sprite)
        {
            Texture2D texture = ReadSpritePixelsUniversal(sprite);
            if (texture == null) return default(AOSpriteStatsV026);

            Color32[] pixels = texture.GetPixels32();
            int visible = 0, green = 0, brown = 0, gray = 0, blue = 0, comparisons = 0;
            double hueSum = 0, satSum = 0, valSum = 0, luminanceSum = 0, luminanceSqSum = 0, edgeCount = 0;
            int width = texture.width, height = texture.height;

            for (int py = 0; py < height; py++)
            for (int px = 0; px < width; px++)
            {
                Color32 raw = pixels[py * width + px];
                if (raw.a <= 12) continue;
                Color color = raw;
                visible++;
                Color.RGBToHSV(color, out float hue, out float saturation, out float value);
                hueSum += hue; satSum += saturation; valSum += value;
                float lum = (color.r + color.g + color.b) / 3f;
                luminanceSum += lum; luminanceSqSum += lum * lum;

                if (saturation < 0.13f) gray++;
                if (hue >= 0.19f && hue <= 0.45f && saturation >= 0.16f) green++;
                if (hue >= 0.50f && hue <= 0.73f && saturation >= 0.14f) blue++;
                if (hue >= 0.035f && hue <= 0.19f && saturation >= 0.17f && value <= 0.78f) brown++;

                if (px + 1 < width)
                {
                    Color32 rr = pixels[py * width + px + 1];
                    if (rr.a > 12)
                    {
                        Color c2 = rr;
                        if (Mathf.Abs(color.r-c2.r)+Mathf.Abs(color.g-c2.g)+Mathf.Abs(color.b-c2.b) > 0.18f) edgeCount++;
                        comparisons++;
                    }
                }
                if (py + 1 < height)
                {
                    Color32 dd = pixels[(py + 1) * width + px];
                    if (dd.a > 12)
                    {
                        Color c2 = dd;
                        if (Mathf.Abs(color.r-c2.r)+Mathf.Abs(color.g-c2.g)+Mathf.Abs(color.b-c2.b) > 0.18f) edgeCount++;
                        comparisons++;
                    }
                }
            }

            UnityEngine.Object.DestroyImmediate(texture);
            if (visible == 0) return default(AOSpriteStatsV026);
            float avgLum = (float)(luminanceSum / visible);
            return new AOSpriteStatsV026
            {
                coverage = visible / (float)pixels.Length,
                greenRatio = green / (float)visible,
                brownRatio = brown / (float)visible,
                grayRatio = gray / (float)visible,
                blueRatio = blue / (float)visible,
                avgHue = (float)(hueSum / visible),
                avgSat = (float)(satSum / visible),
                avgVal = (float)(valSum / visible),
                edgeDensity = comparisons > 0 ? (float)(edgeCount / comparisons) : 0f,
                variance = Mathf.Max(0f, (float)(luminanceSqSum / visible) - avgLum * avgLum)
            };
        }

        public static void ApplySuggestions(
            AOTerrainHDRegistry registry,
            float minimumConfidence = 0.45f)
        {
            if (registry == null || registry.entries == null)
                return;

            Undo.RecordObject(
                registry,
                "Apply terrain classifier suggestions v0.2.9");

            int changed = 0;

            foreach (AOTerrainHDEntry entry in registry.entries)
            {
                if (entry == null || entry.categoryWasManuallySet)
                    continue;

                AOOriginalGrhResolverV029.RepairOrResolveEntry(entry);

                AOClassificationResultV023 classification =
                    ClassifyEntry(entry);

                entry.suggestedCategory = classification.category;
                entry.classificationConfidence = classification.confidence;
                entry.classificationReason = classification.reason;

                if (classification.category != AOTerrainHDCategory.Unclassified &&
                    classification.confidence >= minimumConfidence)
                {
                    if (entry.category != classification.category)
                    {
                        entry.category = classification.category;
                        changed++;
                    }
                }
                else
                {
                    entry.category = AOTerrainHDCategory.Unclassified;
                }
            }

            AOTerrainSemanticClassifierV029.RefineRegistry(
                registry,
                true);

            registry.RebuildLookup();
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                "AO Terrain HD v0.2.9",
                "Clasificación visual + semántica aplicada.\n\n" +
                "Candidatos aptos para remaster: " +
                registry.entries.Count(e => e != null && e.remasterEligible),
                "Aceptar");
        }

        static AOClassificationResultV023 ClassifyEntry(AOTerrainHDEntry entry)
        {
            AOClassificationResultV023 result = new AOClassificationResultV023
            {
                category = AOTerrainHDCategory.Unclassified,
                confidence = 0.15f,
                reason = "No se pudo analizar el sprite."
            };

            if (entry == null || entry.classicSprite == null)
                return result;

            AOSpriteStatsV026 stats = MeasureSpriteStats(entry.classicSprite);
            if (stats.coverage <= 0.01f)
            {
                result.reason = "Sprite vacío o casi vacío.";
                return result;
            }

            int layerUses = Mathf.Max(1, entry.layer1Uses + entry.layer2Uses + entry.layer3Uses + entry.layer4Uses);
            float layer1Ratio = entry.layer1Uses / (float)layerUses;
            float layer2Ratio = entry.layer2Uses / (float)layerUses;

            if (layer2Ratio >= 0.28f && stats.coverage < 0.93f && stats.edgeDensity > 0.045f)
            {
                result.category = AOTerrainHDCategory.Transition;
                result.confidence = Mathf.Clamp01(0.64f + layer2Ratio * 0.16f + (1f - stats.coverage) * 0.18f);
                result.reason = "Transición: Layer2 " + (layer2Ratio * 100f).ToString("0") + "%, cobertura " + (stats.coverage * 100f).ToString("0") + "%, edge " + stats.edgeDensity.ToString("0.00");
                return result;
            }

            if (stats.blueRatio >= 0.26f && stats.avgSat >= 0.16f)
            {
                result.category = AOTerrainHDCategory.Water;
                result.confidence = Mathf.Clamp01(0.60f + stats.blueRatio * 0.85f);
                result.reason = "Predominio azul " + stats.blueRatio.ToString("0.00") + ", sat " + stats.avgSat.ToString("0.00");
                return result;
            }

            if (stats.greenRatio >= 0.22f && stats.avgSat >= 0.14f)
            {
                result.category = AOTerrainHDCategory.Grass;
                result.confidence = Mathf.Clamp01(0.58f + stats.greenRatio * 0.90f + (entry.sceneCount >= 4 ? 0.04f : 0f));
                result.reason = "Predominio verde " + stats.greenRatio.ToString("0.00") + ", sat " + stats.avgSat.ToString("0.00");
                return result;
            }

            if (stats.brownRatio >= 0.20f && stats.avgSat >= 0.14f)
            {
                bool likelyFloor = entry.sceneCount <= 4 && stats.coverage >= 0.95f && layer1Ratio >= 0.45f;
                result.category = likelyFloor ? AOTerrainHDCategory.Floor : AOTerrainHDCategory.Dirt;
                result.confidence = Mathf.Clamp01(0.55f + stats.brownRatio * 0.85f + (likelyFloor ? 0.05f : 0f));
                result.reason = likelyFloor
                    ? "Piso marrón sólido; marrón " + stats.brownRatio.ToString("0.00") + ", cobertura " + stats.coverage.ToString("0.00")
                    : "Predominio marrón " + stats.brownRatio.ToString("0.00") + ", sat " + stats.avgSat.ToString("0.00");
                return result;
            }

            if (stats.grayRatio >= 0.27f && stats.avgSat < 0.19f && stats.coverage >= 0.90f)
            {
                bool likelyRoad = entry.sceneCount >= 4 || entry.usageCount >= 500;
                result.category = likelyRoad ? AOTerrainHDCategory.Road : AOTerrainHDCategory.Floor;
                result.confidence = Mathf.Clamp01(0.52f + stats.grayRatio * 0.75f + (likelyRoad ? 0.05f : 0f));
                result.reason = likelyRoad
                    ? "Camino neutro; gris " + stats.grayRatio.ToString("0.00") + ", sat " + stats.avgSat.ToString("0.00")
                    : "Piso neutro; gris " + stats.grayRatio.ToString("0.00") + ", sat " + stats.avgSat.ToString("0.00");
                return result;
            }

            if (stats.coverage > 0.96f && layer1Ratio >= 0.55f && entry.sceneCount <= 3 && stats.avgSat < 0.25f)
            {
                result.category = AOTerrainHDCategory.Floor;
                result.confidence = 0.50f;
                result.reason = "Superficie sólida de pocos mapas; posible piso.";
                return result;
            }

            result.category = AOTerrainHDCategory.Other;
            result.confidence = 0.34f;
            result.reason = "Sin patrón fuerte. verde " + stats.greenRatio.ToString("0.00") + ", marrón " + stats.brownRatio.ToString("0.00") + ", gris " + stats.grayRatio.ToString("0.00") + ", azul " + stats.blueRatio.ToString("0.00");
            return result;
        }

        static AOCropStatsV022 Measure(Texture2D texture)
        {
            AOCropStatsV022 result = new AOCropStatsV022();

            if (texture == null)
                return result;

            Color32[] pixels = texture.GetPixels32();
            if (pixels.Length == 0)
                return result;

            int visible = 0;
            double r = 0;
            double g = 0;
            double b = 0;
            double edge = 0;
            int edgeSamples = 0;

            int width = texture.width;
            int height = texture.height;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color32 pixel = pixels[y * width + x];
                    if (pixel.a <= 12)
                        continue;

                    visible++;
                    r += pixel.r / 255.0;
                    g += pixel.g / 255.0;
                    b += pixel.b / 255.0;

                    if (x + 1 < width)
                    {
                        Color32 right = pixels[y * width + x + 1];
                        if (right.a > 12)
                        {
                            edge +=
                                (Math.Abs(pixel.r - right.r) +
                                 Math.Abs(pixel.g - right.g) +
                                 Math.Abs(pixel.b - right.b)) /
                                (255.0 * 3.0);
                            edgeSamples++;
                        }
                    }

                    if (y + 1 < height)
                    {
                        Color32 down = pixels[(y + 1) * width + x];
                        if (down.a > 12)
                        {
                            edge +=
                                (Math.Abs(pixel.r - down.r) +
                                 Math.Abs(pixel.g - down.g) +
                                 Math.Abs(pixel.b - down.b)) /
                                (255.0 * 3.0);
                            edgeSamples++;
                        }
                    }
                }
            }

            result.coverage = visible / (float)pixels.Length;

            if (visible > 0)
            {
                result.avgR = (float)(r / visible);
                result.avgG = (float)(g / visible);
                result.avgB = (float)(b / visible);

                float max = Mathf.Max(
                    result.avgR,
                    Mathf.Max(result.avgG, result.avgB));

                float min = Mathf.Min(
                    result.avgR,
                    Mathf.Min(result.avgG, result.avgB));

                result.saturation = max - min;
                result.brightness =
                    (result.avgR + result.avgG + result.avgB) / 3f;
            }

            result.edgeDensity =
                edgeSamples > 0
                    ? (float)(edge / edgeSamples)
                    : 0f;

            return result;
        }

        static void AddAdjacency(
            Dictionary<string, string> tiles,
            Dictionary<string,
                AONeighborCounterV022> all)
        {
            foreach (KeyValuePair<
                     string,
                     string> pair in tiles)
            {
                string[] parts =
                    pair.Key.Split(':');

                if (parts.Length != 3)
                    continue;

                int layer;
                int x;
                int y;

                if (!int.TryParse(
                        parts[0],
                        out layer) ||
                    !int.TryParse(
                        parts[1],
                        out x) ||
                    !int.TryParse(
                        parts[2],
                        out y))
                    continue;

                AONeighborCounterV022 n;

                if (!all.TryGetValue(
                    pair.Value,
                    out n))
                {
                    n =
                        new AONeighborCounterV022();

                    all[pair.Value] = n;
                }

                AddNeighbor(
                    tiles,
                    n,
                    n.north,
                    layer,
                    x,
                    y - 1);

                AddNeighbor(
                    tiles,
                    n,
                    n.east,
                    layer,
                    x + 1,
                    y);

                AddNeighbor(
                    tiles,
                    n,
                    n.south,
                    layer,
                    x,
                    y + 1);

                AddNeighbor(
                    tiles,
                    n,
                    n.west,
                    layer,
                    x - 1,
                    y);
            }
        }

        static void AddNeighbor(
            Dictionary<string, string> tiles,
            AONeighborCounterV022 counter,
            Dictionary<string, int> table,
            int layer,
            int x,
            int y)
        {
            string tileKey =
                layer +
                ":" +
                x +
                ":" +
                y;

            string neighbor;

            if (tiles.TryGetValue(
                tileKey,
                out neighbor))
            {
                counter.Add(
                    table,
                    neighbor);
            }
        }

        public static void ApplyHD(
            AOTerrainHDRegistry registry)
        {
            if (registry == null ||
                registry.entries == null)
                return;

            List<AOTerrainHDEntry> entries =
                registry.entries
                    .Where(
                        e =>
                            e != null &&
                            e.hdSprite != null &&
                            !string.IsNullOrEmpty(
                                e.sourceTexturePath) &&
                            e.cropWidth > 0 &&
                            e.cropHeight > 0)
                    .ToList();

            if (entries.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "AO Terrain HD v0.2.9",
                    "Todavía no hay recortes HD importados para aplicar.",
                    "Aceptar");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                "Aplicar Terrain HD",
                "Se modificarán las PNG fuente usadas por el World Manager.\n\n" +
                "Antes se guardará una copia Classic reversible.\n\n" +
                "Recortes HD: " +
                entries.Count +
                "\nTexturas afectadas: " +
                entries
                    .Select(
                        e =>
                            e.sourceTexturePath)
                    .Distinct(
                        StringComparer
                            .OrdinalIgnoreCase)
                    .Count(),
                "Aplicar HD",
                "Cancelar"))
                return;

            EnsureFolder(
                BackupRoot);

            AOTextureBackupManifestV022 manifest =
                LoadBackupManifest();

            Dictionary<string,
                AOTextureBackupRecordV022>
                backupRecords =
                    new Dictionary<
                        string,
                        AOTextureBackupRecordV022>(
                            StringComparer
                                .OrdinalIgnoreCase);

            if (manifest.records != null)
            {
                foreach (AOTextureBackupRecordV022
                         record in manifest.records)
                {
                    if (record != null &&
                        !string.IsNullOrEmpty(
                            record.sourceAssetPath))
                    {
                        backupRecords[
                            record.sourceAssetPath] =
                            record;
                    }
                }
            }

            IGrouping<string,
                AOTerrainHDEntry>[] groups =
                    entries
                        .GroupBy(
                            e =>
                                e.sourceTexturePath,
                            StringComparer
                                .OrdinalIgnoreCase)
                        .ToArray();

            try
            {
                for (int i = 0;
                     i < groups.Length;
                     i++)
                {
                    IGrouping<string,
                        AOTerrainHDEntry> group =
                            groups[i];

                    string sourcePath =
                        group.Key;

                    EditorUtility.DisplayProgressBar(
                        "AO Terrain HD v0.2.9",
                        "Aplicando " +
                        sourcePath,
                        i /
                        (float)groups.Length);

                    EnsureBackup(
                        sourcePath,
                        backupRecords);

                    PatchSourceTexture(
                        sourcePath,
                        group.ToList());
                }
            }
            finally
            {
                EditorUtility
                    .ClearProgressBar();
            }

            manifest.createdUtc =
                DateTime.UtcNow.ToString("o");

            manifest.records =
                backupRecords.Values
                    .OrderBy(
                        r =>
                            r.sourceAssetPath)
                    .ToArray();

            WriteBackupManifest(
                manifest);

            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "AO Terrain HD v0.2.9",
                "Terrain HD aplicado a " +
                groups.Length +
                " texturas fuente.\n\n" +
                "Si el mapa está en Play, salí y volvé a entrar o recargá el mapa para ver todos los cambios.",
                "Aceptar");
        }

        public static void RestoreClassic()
        {
            AOTextureBackupManifestV022 manifest =
                LoadBackupManifest();

            if (manifest.records == null ||
                manifest.records.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "AO Terrain HD v0.2.9",
                    "No hay backup Classic para restaurar.",
                    "Aceptar");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                "Restaurar Terrain Classic",
                "Se restaurarán " +
                manifest.records.Length +
                " texturas originales desde el backup.",
                "Restaurar Classic",
                "Cancelar"))
                return;

            int restored = 0;

            try
            {
                for (int i = 0;
                     i < manifest.records.Length;
                     i++)
                {
                    AOTextureBackupRecordV022 record =
                        manifest.records[i];

                    if (record == null ||
                        string.IsNullOrEmpty(
                            record.sourceAssetPath) ||
                        string.IsNullOrEmpty(
                            record.backupAssetPath))
                        continue;

                    string sourceAbs =
                        AbsolutePath(
                            record.sourceAssetPath);

                    string backupAbs =
                        AbsolutePath(
                            record.backupAssetPath);

                    if (!File.Exists(backupAbs))
                        continue;

                    EditorUtility.DisplayProgressBar(
                        "AO Terrain HD v0.2.9",
                        "Restaurando " +
                        record.sourceAssetPath,
                        i /
                        (float)manifest.records.Length);

                    File.Copy(
                        backupAbs,
                        sourceAbs,
                        true);

                    AssetDatabase.ImportAsset(
                        record.sourceAssetPath,
                        ImportAssetOptions
                            .ForceUpdate);

                    restored++;
                }
            }
            finally
            {
                EditorUtility
                    .ClearProgressBar();
            }

            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "AO Terrain HD v0.2.9",
                "Texturas Classic restauradas: " +
                restored,
                "Aceptar");
        }

        static void EnsureBackup(
            string sourceAssetPath,
            Dictionary<string,
                AOTextureBackupRecordV022>
                records)
        {
            if (records.ContainsKey(
                sourceAssetPath))
                return;

            string sourceAbs =
                AbsolutePath(
                    sourceAssetPath);

            if (!File.Exists(sourceAbs))
                return;

            string guid =
                AssetDatabase.AssetPathToGUID(
                    sourceAssetPath);

            if (string.IsNullOrEmpty(guid))
            {
                guid =
                    SafeName(
                        sourceAssetPath);
            }

            string extension =
                Path.GetExtension(
                    sourceAssetPath);

            if (string.IsNullOrEmpty(extension))
                extension = ".png";

            string backupAssetPath =
                BackupRoot +
                "/" +
                guid +
                extension;

            string backupAbs =
                AbsolutePath(
                    backupAssetPath);

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    backupAbs));

            File.Copy(
                sourceAbs,
                backupAbs,
                true);

            records[sourceAssetPath] =
                new AOTextureBackupRecordV022
                {
                    sourceAssetPath =
                        sourceAssetPath,
                    backupAssetPath =
                        backupAssetPath
                };
        }

        static void PatchSourceTexture(
            string sourceAssetPath,
            List<AOTerrainHDEntry> entries)
        {
            string sourceAbs =
                AbsolutePath(
                    sourceAssetPath);

            if (!File.Exists(sourceAbs))
                return;

            Texture2D source =
                new Texture2D(
                    2,
                    2,
                    TextureFormat.RGBA32,
                    false);

            if (!source.LoadImage(
                File.ReadAllBytes(
                    sourceAbs),
                false))
            {
                UnityEngine.Object
                    .DestroyImmediate(source);
                return;
            }

            foreach (AOTerrainHDEntry entry
                     in entries)
            {
                Texture2D hd =
                    ReadSpriteFile(
                        entry.hdSprite);

                if (hd == null)
                    continue;

                Texture2D fitted =
                    hd.width ==
                        entry.cropWidth &&
                    hd.height ==
                        entry.cropHeight
                    ? hd
                    : ResizePoint(
                        hd,
                        entry.cropWidth,
                        entry.cropHeight);

                int x =
                    Mathf.Clamp(
                        entry.cropX,
                        0,
                        Mathf.Max(
                            0,
                            source.width -
                            1));

                int bottomY =
                    source.height -
                    entry.cropY -
                    entry.cropHeight;

                bottomY =
                    Mathf.Clamp(
                        bottomY,
                        0,
                        Mathf.Max(
                            0,
                            source.height -
                            1));

                int width =
                    Mathf.Min(
                        fitted.width,
                        source.width - x);

                int height =
                    Mathf.Min(
                        fitted.height,
                        source.height -
                        bottomY);

                if (width > 0 &&
                    height > 0)
                {
                    source.SetPixels(
                        x,
                        bottomY,
                        width,
                        height,
                        fitted.GetPixels(
                            0,
                            0,
                            width,
                            height));
                }

                if (fitted != hd)
                {
                    UnityEngine.Object
                        .DestroyImmediate(fitted);
                }

                UnityEngine.Object
                    .DestroyImmediate(hd);
            }

            source.Apply(
                false,
                false);

            File.WriteAllBytes(
                sourceAbs,
                source.EncodeToPNG());

            UnityEngine.Object
                .DestroyImmediate(source);

            AssetDatabase.ImportAsset(
                sourceAssetPath,
                ImportAssetOptions
                    .ForceUpdate);
        }

        static Texture2D ReadSpriteFile(
            Sprite sprite)
        {
            if (sprite == null)
                return null;

            string assetPath =
                AssetDatabase.GetAssetPath(
                    sprite);

            string absolute =
                AbsolutePath(
                    assetPath);

            if (!File.Exists(absolute))
                return null;

            Texture2D texture =
                new Texture2D(
                    2,
                    2,
                    TextureFormat.RGBA32,
                    false);

            if (!texture.LoadImage(
                File.ReadAllBytes(
                    absolute),
                false))
            {
                UnityEngine.Object
                    .DestroyImmediate(texture);
                return null;
            }

            return texture;
        }

        static Texture2D ResizePoint(
            Texture2D source,
            int width,
            int height)
        {
            Texture2D result =
                new Texture2D(
                    width,
                    height,
                    TextureFormat.RGBA32,
                    false);

            Color32[] input =
                source.GetPixels32();

            Color32[] output =
                new Color32[
                    width * height];

            for (int y = 0;
                 y < height;
                 y++)
            {
                int sy =
                    Mathf.Clamp(
                        Mathf.FloorToInt(
                            y *
                            source.height /
                            (float)height),
                        0,
                        source.height -
                        1);

                for (int x = 0;
                     x < width;
                     x++)
                {
                    int sx =
                        Mathf.Clamp(
                            Mathf.FloorToInt(
                                x *
                                source.width /
                                (float)width),
                            0,
                            source.width -
                            1);

                    output[
                        y * width + x] =
                        input[
                            sy *
                            source.width +
                            sx];
                }
            }

            result.SetPixels32(
                output);

            result.Apply(
                false,
                false);

            return result;
        }

        static AOTextureBackupManifestV022
            LoadBackupManifest()
        {
            string absolute =
                AbsolutePath(
                    BackupManifestPath);

            if (!File.Exists(absolute))
            {
                return new
                    AOTextureBackupManifestV022
                    {
                        records =
                            new
                            AOTextureBackupRecordV022[
                                0]
                    };
            }

            try
            {
                AOTextureBackupManifestV022
                    manifest =
                        JsonUtility.FromJson<
                            AOTextureBackupManifestV022>(
                                File.ReadAllText(
                                    absolute));

                if (manifest != null)
                    return manifest;
            }
            catch
            {
            }

            return new
                AOTextureBackupManifestV022
                {
                    records =
                        new
                        AOTextureBackupRecordV022[
                            0]
                };
        }

        static void WriteBackupManifest(
            AOTextureBackupManifestV022 manifest)
        {
            EnsureFolder(
                BackupRoot);

            File.WriteAllText(
                AbsolutePath(
                    BackupManifestPath),
                JsonUtility.ToJson(
                    manifest,
                    true),
                new UTF8Encoding(false));
        }

        static string SafeName(
            string value)
        {
            if (string.IsNullOrEmpty(value))
                return "texture";

            foreach (char invalid
                     in Path
                        .GetInvalidFileNameChars())
            {
                value =
                    value.Replace(
                        invalid,
                        '_');
            }

            return value
                .Replace('/', '_')
                .Replace('\\', '_')
                .Replace(':', '_')
                .Replace(' ', '_');
        }

        static string AbsolutePath(
            string assetPath)
        {
            string projectRoot =
                Directory
                    .GetParent(
                        Application.dataPath)
                    .FullName;

            return Path.Combine(
                projectRoot,
                assetPath.Replace(
                    '/',
                    Path
                        .DirectorySeparatorChar));
        }

        static void EnsureFolder(
            string folder)
        {
            folder =
                folder.Replace(
                    '\\',
                    '/');

            if (AssetDatabase
                .IsValidFolder(folder))
                return;

            string[] parts =
                folder.Split('/');

            string current =
                "Assets";

            for (int i = 1;
                 i < parts.Length;
                 i++)
            {
                string next =
                    current +
                    "/" +
                    parts[i];

                if (!AssetDatabase
                    .IsValidFolder(next))
                {
                    AssetDatabase
                        .CreateFolder(
                            current,
                            parts[i]);
                }

                current = next;
            }
        }
    }
}
#endif
