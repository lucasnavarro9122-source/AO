// AO Terrain HD v0.2.9
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using AOMigrator.TerrainHD;

namespace AOMigrator.TerrainHD.Editor
{
    [Serializable]
    public sealed class AOTerrainHDDiagnosticFileV029
    {
        public string path;
        public bool exists;
        public bool containsExpectedVersion;
        public long bytes;
        public string modifiedUtc;
    }

    [Serializable]
    public sealed class AOTerrainHDDiagnosticEntryV029
    {
        public int graphicId;
        public string key;
        public string displayName;
        public int usageCount;
        public int mapCount;
        public int impactScore;

        public string category;
        public string suggestedCategory;
        public float classificationConfidence;
        public string classificationReason;
        public bool categoryWasManuallySet;
        public string visualCategory;
        public float visualConfidence;
        public string visualReason;
        public float familySupport;
        public float neighborSupport;
        public bool remasterEligible;
        public int remasterPriority;
        public string remasterReason;

        public bool hasClassicSprite;
        public string classicSpriteName;
        public string classicAssetPath;
        public string spriteTexturePath;
        public bool spriteTextureReadable;
        public int spriteTextureWidth;
        public int spriteTextureHeight;

        public string imageFile;
        public string sourceTexturePath;
        public bool sourceAssetExists;
        public bool sourceLoadsAsTexture;
        public string sourceExtension;
        public int sourceTextureWidth;
        public int sourceTextureHeight;

        public int cropX;
        public int cropY;
        public int cropWidth;
        public int cropHeight;
        public bool cropDimensionsValid;
        public bool cropInsideTexture;

        public bool hasHD;
        public string hdAssetPath;

        public int fallbackCandidateCount;
        public string[] fallbackCandidates;

        public string severity;
        public string[] issues;
    }

    [Serializable]
    public sealed class AOTerrainHDDiagnosticReportV029
    {
        public string toolVersion = "0.2.9";
        public string generatedUtc;
        public string unityVersion;

        public string expectedPipelineVersion = "0.2.9";
        public string registryPipelineVersion;
        public bool versionMismatch;

        public int expectedActiveMaps = 21;
        public int scannedActiveMaps;
        public bool mapCountMismatch;

        public int totalEntries;
        public long totalUsage;
        public int withPreview;
        public int withoutPreview;
        public int withSourceTexturePath;
        public int withoutSourceTexturePath;
        public int withHD;

        public int classified;
        public int unclassified;
        public int staleClassifier;
        public int invalidCrop;
        public int missingSourceAsset;
        public int highImpactUnresolved;
        public int sourcePng;
        public int sourceAsset;
        public int sourceOther;
        public int remasterEligible;
        public int remasterExcluded;

        public bool mapSelectionReportExists;
        public string mapSelectionReportPath;

        public AOTerrainHDDiagnosticFileV029[] installedFiles;
        public string[] globalIssues;
        public string[] recommendedActions;
        public AOTerrainHDDiagnosticEntryV029[] entries;
    }

    public static class AOTerrainHDDiagnosticV029
    {
        public const string Version = "0.2.9";

        const string RegistryPath =
            "Assets/Resources/AOMigratorTerrainHD/AOTerrainHDRegistry.asset";

        const string DiagnosticRoot =
            "Assets/AOMigrator/TerrainHDPipelineData/Diagnostics";

        public const string JsonPath =
            DiagnosticRoot + "/terrain_full_diagnostic_v029.json";

        public const string CsvPath =
            DiagnosticRoot + "/terrain_full_diagnostic_v029.csv";

        const string MapSelectionPath =
            DiagnosticRoot + "/map_source_selection_v024.json";

        static readonly string[] ExpectedScripts =
        {
            "Assets/AOMigrator/Runtime/AOTerrainHDRegistry.cs",
            "Assets/AOMigrator/Runtime/AOTerrainHDStyleManager.cs",
            "Assets/AOMigrator/Editor/AOTerrainHDPipelineWindow.cs",
            "Assets/AOMigrator/Editor/AOTerrainHDConnectedWorldScannerV022.cs",
            "Assets/AOMigrator/Editor/AOOriginalGrhResolverV029.cs",
            "Assets/AOMigrator/Editor/AOTerrainSemanticClassifierV029.cs",
            "Assets/AOMigrator/Editor/AOTerrainHiggsPrepV029.cs",
            "Assets/AOMigrator/Editor/AOTerrainHDDiagnosticV029.cs"
        };

        [MenuItem("AO Migrador/Terrain HD/Generar diagnóstico completo v0.2.9")]
        public static void GenerateFromMenu()
        {
            AOTerrainHDRegistry registry =
                AssetDatabase.LoadAssetAtPath<AOTerrainHDRegistry>(RegistryPath);

            Generate(registry, true);
        }

        public static void Generate(
            AOTerrainHDRegistry registry,
            bool showDialog)
        {
            if (registry == null)
            {
                registry = AssetDatabase.LoadAssetAtPath<AOTerrainHDRegistry>(
                    RegistryPath);
            }

            EnsureFolder(DiagnosticRoot);

            Dictionary<int, List<string>> fallbackIndex =
                BuildFallbackIndex();

            List<string> globalIssues = new List<string>();
            List<string> actions = new List<string>();
            List<AOTerrainHDDiagnosticEntryV029> items =
                new List<AOTerrainHDDiagnosticEntryV029>();

            AOTerrainHDDiagnosticReportV029 report =
                new AOTerrainHDDiagnosticReportV029();

            report.generatedUtc = DateTime.UtcNow.ToString("o");
            report.unityVersion = Application.unityVersion;
            report.registryPipelineVersion =
                registry != null ? registry.pipelineVersion : "<missing>";

            report.versionMismatch =
                registry == null ||
                !String.Equals(
                    report.registryPipelineVersion,
                    Version,
                    StringComparison.OrdinalIgnoreCase);

            report.scannedActiveMaps =
                registry != null ? registry.scannedSceneCount : 0;

            report.mapCountMismatch =
                report.scannedActiveMaps != report.expectedActiveMaps;

            report.installedFiles =
                ExpectedScripts.Select(InspectScript).ToArray();

            if (report.versionMismatch)
            {
                globalIssues.Add(
                    "VERSION_MISMATCH: Registry=" +
                    report.registryPipelineVersion +
                    ", esperado=" + Version + ".");

                actions.Add(
                    "Reemplazar nuevamente los archivos del hotfix v0.2.9 y esperar la recompilación completa de Unity.");
            }

            foreach (AOTerrainHDDiagnosticFileV029 file in report.installedFiles)
            {
                if (!file.exists)
                {
                    globalIssues.Add(
                        "SCRIPT_MISSING: " + file.path);
                }
                else if (!file.containsExpectedVersion)
                {
                    globalIssues.Add(
                        "SCRIPT_OLD_VERSION: " + file.path);
                }
            }

            if (report.installedFiles.Any(f => !f.exists || !f.containsExpectedVersion))
            {
                actions.Add(
                    "Hay scripts ausentes o de otra versión; cerrar la ventana Terrain HD, reemplazar el hotfix y reabrirla.");
            }

            if (report.mapCountMismatch)
            {
                globalIssues.Add(
                    "MAP_COUNT_MISMATCH: encontrados=" +
                    report.scannedActiveMaps +
                    ", esperados=21.");

                actions.Add(
                    "Volver a ejecutar Preset terreno Layer 1+2 y Escanear map JSON + impacto.");
            }

            string mapSelectionAbsolute = AbsolutePath(MapSelectionPath);
            report.mapSelectionReportExists = File.Exists(mapSelectionAbsolute);
            report.mapSelectionReportPath = MapSelectionPath;

            if (!report.mapSelectionReportExists)
            {
                globalIssues.Add(
                    "MAP_SELECTION_REPORT_MISSING: no existe " +
                    MapSelectionPath + ".");
            }

            if (registry != null && registry.entries != null)
            {
                foreach (AOTerrainHDEntry entry in registry.entries)
                {
                    if (entry == null)
                        continue;

                    AOTerrainHDDiagnosticEntryV029 item =
                        InspectEntry(entry, fallbackIndex);

                    items.Add(item);
                }
            }

            report.entries = items
                .OrderByDescending(e => SeverityRank(e.severity))
                .ThenByDescending(e => e.impactScore)
                .ThenBy(e => e.graphicId)
                .ToArray();

            report.totalEntries = items.Count;
            report.totalUsage = items.Sum(e => (long)e.usageCount);
            report.withPreview = items.Count(e => e.hasClassicSprite);
            report.withoutPreview = items.Count(e => !e.hasClassicSprite);
            report.withSourceTexturePath = items.Count(e => !String.IsNullOrEmpty(e.sourceTexturePath));
            report.withoutSourceTexturePath = items.Count(e => String.IsNullOrEmpty(e.sourceTexturePath));
            report.withHD = items.Count(e => e.hasHD);
            report.classified = items.Count(e => !String.Equals(e.category, "Unclassified", StringComparison.OrdinalIgnoreCase));
            report.unclassified = items.Count(e => String.Equals(e.category, "Unclassified", StringComparison.OrdinalIgnoreCase));
            report.staleClassifier = items.Count(e =>
                e.hasClassicSprite &&
                e.classificationConfidence <= 0.151f &&
                !String.IsNullOrEmpty(e.classificationReason));
            report.invalidCrop = items.Count(e => e.hasClassicSprite &&
                (!e.cropDimensionsValid ||
                 (e.sourceLoadsAsTexture && !e.cropInsideTexture)));
            report.missingSourceAsset = items.Count(e =>
                !String.IsNullOrEmpty(e.sourceTexturePath) &&
                !e.sourceAssetExists);
            report.highImpactUnresolved = items.Count(e =>
                e.impactScore >= 1000 && !e.hasClassicSprite);
            report.sourcePng = items.Count(e => e.sourceExtension == ".png");
            report.sourceAsset = items.Count(e => e.sourceExtension == ".asset");
            report.sourceOther = items.Count(e =>
                !String.IsNullOrEmpty(e.sourceExtension) &&
                e.sourceExtension != ".png" &&
                e.sourceExtension != ".asset");
            report.remasterEligible = items.Count(e => e.remasterEligible);
            report.remasterExcluded = items.Count - report.remasterEligible;

            if (report.withoutPreview > 0)
            {
                globalIssues.Add(
                    "UNRESOLVED_PREVIEWS: " + report.withoutPreview +
                    " GRH sin classicSprite/preview.");

                actions.Add(
                    "Revisar primero los GRH HIGH_IMPACT_NO_PREVIEW del diagnóstico; incluyen candidatos de Sprite encontrados en el proyecto.");
            }

            if (report.staleClassifier > 0)
            {
                globalIssues.Add(
                    "STALE_CLASSIFIER: " + report.staleClassifier +
                    " sprites tienen preview pero siguen en confianza ~15%.");

                actions.Add(
                    "Si hay preview pero confianza 15%, confirmar que AOTerrainHDConnectedWorldScannerV022.cs sea v0.2.9 y volver a escanear.");
            }

            if (report.highImpactUnresolved > 0)
            {
                globalIssues.Add(
                    "HIGH_IMPACT_UNRESOLVED: " +
                    report.highImpactUnresolved +
                    " GRH de impacto >=1000 sin preview.");
            }

            if (report.invalidCrop > 0)
            {
                globalIssues.Add(
                    "INVALID_CROP: " + report.invalidCrop +
                    " entradas con rectángulo inválido/fuera de textura.");
            }

            if (report.missingSourceAsset > 0)
            {
                globalIssues.Add(
                    "MISSING_SOURCE_ASSET: " + report.missingSourceAsset +
                    " rutas fuente no existen en AssetDatabase.");
            }

            if (report.sourceAsset > 0)
            {
                actions.Add(
                    "Los sources .asset se diagnostican por classicSprite/texture; no deben tratarse como PNG crudo.");
            }

            report.globalIssues = globalIssues.Distinct().ToArray();
            report.recommendedActions = actions.Distinct().ToArray();

            File.WriteAllText(
                AbsolutePath(JsonPath),
                JsonUtility.ToJson(report, true),
                new UTF8Encoding(false));

            WriteCsv(report.entries);
            AssetDatabase.Refresh();

            Debug.Log(
                "[AO Terrain HD v0.2.9] Diagnóstico completo: " +
                JsonPath +
                " | entries=" + report.totalEntries +
                " | preview=" + report.withPreview +
                " | unresolved=" + report.withoutPreview +
                " | staleClassifier=" + report.staleClassifier);

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "AO Terrain HD v0.2.9 - Diagnóstico",
                    "Diagnóstico generado.\n\n" +
                    "Mapas: " + report.scannedActiveMaps + "/21" +
                    "\nGRH: " + report.totalEntries +
                    "\nCon preview: " + report.withPreview +
                    "\nSin preview: " + report.withoutPreview +
                    "\nClasificador stale: " + report.staleClassifier +
                    "\nImpacto alto sin resolver: " + report.highImpactUnresolved +
                    "\nAptos para remaster/Higgs: " + report.remasterEligible +
                    "\n\nJSON:\n" + JsonPath +
                    "\n\nCSV:\n" + CsvPath,
                    "Aceptar");
            }
        }

        public static void SelectLastReport()
        {
            UnityEngine.Object asset =
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(JsonPath);

            if (asset == null)
            {
                EditorUtility.DisplayDialog(
                    "AO Terrain HD v0.2.9",
                    "Todavía no existe el diagnóstico. Generarlo primero.",
                    "Aceptar");
                return;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        static AOTerrainHDDiagnosticEntryV029 InspectEntry(
            AOTerrainHDEntry entry,
            Dictionary<int, List<string>> fallbackIndex)
        {
            AOTerrainHDDiagnosticEntryV029 item =
                new AOTerrainHDDiagnosticEntryV029();

            List<string> issues = new List<string>();

            item.graphicId = entry.graphicId;
            item.key = entry.key;
            item.displayName = entry.displayName;
            item.usageCount = entry.usageCount;
            item.mapCount = entry.sceneCount;
            item.impactScore = entry.ImpactScore;
            item.category = entry.category.ToString();
            item.suggestedCategory = entry.suggestedCategory.ToString();
            item.classificationConfidence = entry.classificationConfidence;
            item.classificationReason = entry.classificationReason;
            item.categoryWasManuallySet = entry.categoryWasManuallySet;
            item.visualCategory = entry.visualCategory.ToString();
            item.visualConfidence = entry.visualConfidence;
            item.visualReason = entry.visualReason;
            item.familySupport = entry.familySupport;
            item.neighborSupport = entry.neighborSupport;
            item.remasterEligible = entry.remasterEligible;
            item.remasterPriority = entry.remasterPriority;
            item.remasterReason = entry.remasterReason;

            item.hasClassicSprite = entry.classicSprite != null;
            item.classicSpriteName =
                entry.classicSprite != null ? entry.classicSprite.name : null;
            item.classicAssetPath = entry.classicAssetPath;

            if (entry.classicSprite != null)
            {
                string spritePath = AssetDatabase.GetAssetPath(entry.classicSprite);
                if (String.IsNullOrEmpty(item.classicAssetPath))
                    item.classicAssetPath = spritePath;

                Texture2D spriteTexture = entry.classicSprite.texture;
                if (spriteTexture != null)
                {
                    item.spriteTexturePath = AssetDatabase.GetAssetPath(spriteTexture);
                    item.spriteTextureWidth = spriteTexture.width;
                    item.spriteTextureHeight = spriteTexture.height;
                    item.spriteTextureReadable = spriteTexture.isReadable;
                }
            }

            item.imageFile = entry.imageFile;
            item.sourceTexturePath = entry.sourceTexturePath;
            item.sourceExtension =
                !String.IsNullOrEmpty(entry.sourceTexturePath)
                    ? Path.GetExtension(entry.sourceTexturePath).ToLowerInvariant()
                    : "";

            if (!String.IsNullOrEmpty(entry.sourceTexturePath))
            {
                UnityEngine.Object sourceObject =
                    AssetDatabase.LoadMainAssetAtPath(entry.sourceTexturePath);

                item.sourceAssetExists = sourceObject != null;

                Texture2D sourceTexture =
                    AssetDatabase.LoadAssetAtPath<Texture2D>(entry.sourceTexturePath);

                if (sourceTexture == null && sourceObject is Texture2D)
                    sourceTexture = (Texture2D)sourceObject;

                item.sourceLoadsAsTexture = sourceTexture != null;

                if (sourceTexture != null)
                {
                    item.sourceTextureWidth = sourceTexture.width;
                    item.sourceTextureHeight = sourceTexture.height;
                }
            }

            item.cropX = entry.cropX;
            item.cropY = entry.cropY;
            item.cropWidth = entry.cropWidth;
            item.cropHeight = entry.cropHeight;
            item.cropDimensionsValid =
                entry.cropX >= 0 && entry.cropY >= 0 &&
                entry.cropWidth > 0 && entry.cropHeight > 0;

            item.cropInsideTexture = true;

            if (item.sourceLoadsAsTexture)
            {
                item.cropInsideTexture =
                    item.cropDimensionsValid &&
                    item.cropX + item.cropWidth <= item.sourceTextureWidth &&
                    item.cropY + item.cropHeight <= item.sourceTextureHeight;
            }

            item.hasHD = entry.hdSprite != null;
            item.hdAssetPath = entry.hdAssetPath;

            List<string> candidates;
            if (fallbackIndex.TryGetValue(entry.graphicId, out candidates))
            {
                item.fallbackCandidateCount = candidates.Count;
                item.fallbackCandidates = candidates.Take(8).ToArray();
            }
            else
            {
                item.fallbackCandidateCount = 0;
                item.fallbackCandidates = new string[0];
            }

            if (!item.hasClassicSprite)
            {
                issues.Add("NO_CLASSIC_SPRITE");

                if (item.impactScore >= 1000)
                    issues.Add("HIGH_IMPACT_NO_PREVIEW");
            }

            if (String.IsNullOrEmpty(item.sourceTexturePath))
                issues.Add("NO_SOURCE_TEXTURE_PATH");
            else if (!item.sourceAssetExists)
                issues.Add("SOURCE_ASSET_NOT_FOUND");

            if (item.hasClassicSprite && !item.cropDimensionsValid)
                issues.Add("INVALID_CROP_DIMENSIONS");
            else if (item.hasClassicSprite && item.sourceLoadsAsTexture && !item.cropInsideTexture)
                issues.Add("CROP_OUT_OF_BOUNDS");

            if (item.hasClassicSprite &&
                item.classificationConfidence <= 0.151f)
            {
                issues.Add("CLASSIFIER_STALE_15_PERCENT");
            }

            if (String.IsNullOrEmpty(item.classificationReason))
                issues.Add("CLASSIFICATION_REASON_EMPTY");

            if (item.impactScore >= 1000 &&
                String.Equals(
                    item.category,
                    "Unclassified",
                    StringComparison.OrdinalIgnoreCase))
            {
                issues.Add("HIGH_IMPACT_UNCLASSIFIED");
            }

            if (!item.hasClassicSprite && item.fallbackCandidateCount > 0)
                issues.Add("FALLBACK_CANDIDATES_AVAILABLE");

            if (item.sourceExtension == ".asset" &&
                item.hasClassicSprite &&
                item.classificationConfidence <= 0.151f)
            {
                issues.Add("ASSET_SOURCE_CLASSIFIER_FAILED");
            }

            if (item.hasHD && !item.hasClassicSprite)
                issues.Add("HD_WITHOUT_CLASSIC");

            item.issues = issues.Distinct().ToArray();
            item.severity = SeverityFor(item);
            return item;
        }

        static string SeverityFor(AOTerrainHDDiagnosticEntryV029 item)
        {
            if (item.issues.Contains("HIGH_IMPACT_NO_PREVIEW") ||
                item.issues.Contains("CROP_OUT_OF_BOUNDS") ||
                item.issues.Contains("SOURCE_ASSET_NOT_FOUND"))
                return "ERROR";

            if (item.issues.Length > 0)
                return "WARNING";

            return "OK";
        }

        static int SeverityRank(string severity)
        {
            if (severity == "ERROR") return 3;
            if (severity == "WARNING") return 2;
            return 1;
        }

        static AOTerrainHDDiagnosticFileV029 InspectScript(string path)
        {
            AOTerrainHDDiagnosticFileV029 result =
                new AOTerrainHDDiagnosticFileV029();

            result.path = path;
            string absolute = AbsolutePath(path);
            result.exists = File.Exists(absolute);

            if (!result.exists)
                return result;

            try
            {
                FileInfo info = new FileInfo(absolute);
                result.bytes = info.Length;
                result.modifiedUtc = info.LastWriteTimeUtc.ToString("o");
                string text = File.ReadAllText(absolute);
                result.containsExpectedVersion =
                    text.IndexOf("0.2.9", StringComparison.Ordinal) >= 0;
            }
            catch
            {
                result.containsExpectedVersion = false;
            }

            return result;
        }

        static Dictionary<int, List<string>> BuildFallbackIndex()
        {
            Dictionary<int, List<string>> result =
                new Dictionary<int, List<string>>();

            string[] guids = AssetDatabase.FindAssets("t:Sprite");

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

                    int id = ParseGrhId(sprite.name, Path.GetFileNameWithoutExtension(path));
                    if (id <= 0)
                        continue;

                    List<string> list;
                    if (!result.TryGetValue(id, out list))
                    {
                        list = new List<string>();
                        result[id] = list;
                    }

                    string label = path + " :: " + sprite.name;
                    if (!list.Contains(label))
                        list.Add(label);
                }
            }

            return result;
        }

        static int ParseGrhId(string spriteName, string fileName)
        {
            string value =
                (spriteName ?? "") + " " + (fileName ?? "");

            Match match = Regex.Match(
                value,
                @"(?:GRH|SPRITE)[_\-\s]?(\d+)",
                RegexOptions.IgnoreCase);

            int id;
            if (match.Success && Int32.TryParse(match.Groups[1].Value, out id))
                return id;

            if (Int32.TryParse(spriteName, out id))
                return id;

            if (Int32.TryParse(fileName, out id))
                return id;

            return 0;
        }

        static void WriteCsv(
            AOTerrainHDDiagnosticEntryV029[] entries)
        {
            StringBuilder csv = new StringBuilder();
            csv.AppendLine(
                "severity,graphicId,displayName,usage,maps,impact,category,semanticSuggested,semanticConfidence,visualCategory,visualConfidence,familySupport,neighborSupport,remasterEligible,remasterPriority,hasPreview,classicAssetPath,sourceTexturePath,sourceExt,sourceExists,sourceLoadsAsTexture,cropValid,cropInsideTexture,fallbackCandidates,issues,classificationReason,remasterReason");

            foreach (AOTerrainHDDiagnosticEntryV029 item in entries)
            {
                csv.Append(Csv(item.severity)).Append(',')
                    .Append(item.graphicId).Append(',')
                    .Append(Csv(item.displayName)).Append(',')
                    .Append(item.usageCount).Append(',')
                    .Append(item.mapCount).Append(',')
                    .Append(item.impactScore).Append(',')
                    .Append(Csv(item.category)).Append(',')
                    .Append(Csv(item.suggestedCategory)).Append(',')
                    .Append(item.classificationConfidence.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                    .Append(Csv(item.visualCategory)).Append(',')
                    .Append(item.visualConfidence.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                    .Append(item.familySupport.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                    .Append(item.neighborSupport.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                    .Append(item.remasterEligible ? "1" : "0").Append(',')
                    .Append(item.remasterPriority).Append(',')
                    .Append(item.hasClassicSprite ? "1" : "0").Append(',')
                    .Append(Csv(item.classicAssetPath)).Append(',')
                    .Append(Csv(item.sourceTexturePath)).Append(',')
                    .Append(Csv(item.sourceExtension)).Append(',')
                    .Append(item.sourceAssetExists ? "1" : "0").Append(',')
                    .Append(item.sourceLoadsAsTexture ? "1" : "0").Append(',')
                    .Append(item.cropDimensionsValid ? "1" : "0").Append(',')
                    .Append(item.cropInsideTexture ? "1" : "0").Append(',')
                    .Append(item.fallbackCandidateCount).Append(',')
                    .Append(Csv(String.Join("|", item.issues ?? new string[0]))).Append(',')
                    .Append(Csv(item.classificationReason)).Append(',')
                    .Append(Csv(item.remasterReason))
                    .Append('\n');
            }

            File.WriteAllText(
                AbsolutePath(CsvPath),
                csv.ToString(),
                new UTF8Encoding(false));
        }

        static string Csv(string value)
        {
            if (value == null)
                value = "";

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        static string AbsolutePath(string assetPath)
        {
            string projectRoot =
                Directory.GetParent(Application.dataPath).FullName;

            return Path.Combine(
                projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/');

            if (AssetDatabase.IsValidFolder(folder))
                return;

            string[] parts = folder.Split('/');
            string current = "Assets";

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }
    }
}
#endif
