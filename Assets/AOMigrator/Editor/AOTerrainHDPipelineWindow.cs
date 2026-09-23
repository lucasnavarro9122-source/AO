// AO Terrain HD v0.2.9
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using AOMigrator.TerrainHD;

namespace AOMigrator.TerrainHD.Editor
{
    [Serializable]
    public sealed class AOTerrainHDManifest
    {
        public string pipelineVersion = "0.2.0";
        public string category;
        public string batchName;
        public int cellSize;
        public int columns;
        public int rows;
        public int atlasWidth;
        public int atlasHeight;
        public AOTerrainHDManifestItem[] items;
    }

    [Serializable]
    public sealed class AOTerrainHDManifestItem
    {
        public string key;
        public string displayName;
        public string sourceAssetPath;
        public string spriteName;
        public int usageCount;
        public int sceneCount;
        public int row;
        public int column;
        public int sourceWidth;
        public int sourceHeight;
        public float pixelsPerUnit;
        public float pivotX;
        public float pivotY;
        public float originalAlphaCoverage;
        public float originalCenterX;
        public float originalCenterY;
        public string northKey;
        public string eastKey;
        public string southKey;
        public string westKey;
    }

    [Serializable]
    public sealed class AOTerrainHDValidationItem
    {
        public string key;
        public string severity;
        public string message;
        public float alphaCoverage;
        public float centerDrift;
        public bool touchesCellEdge;
        public float worstSeamScore;
    }

    [Serializable]
    public sealed class AOTerrainHDValidationReport
    {
        public string pipelineVersion = "0.2.0";
        public string createdUtc;
        public string category;
        public string batchName;
        public int totalItems;
        public int validItems;
        public int warningItems;
        public int errorItems;
        public AOTerrainHDValidationItem[] items;
    }

    [Serializable]
    public sealed class AOTerrainHDImpactReport
    {
        public string pipelineVersion = "0.2.0";
        public string createdUtc;
        public int scannedScenes;
        public AOTerrainHDImpactItem[] topAssets;
    }

    [Serializable]
    public sealed class AOTerrainHDImpactItem
    {
        public string key;
        public string name;
        public string category;
        public int uses;
        public int scenes;
        public int impact;
        public string northKey;
        public string eastKey;
        public string southKey;
        public string westKey;
    }

    internal struct AOTerrainPixelStats
    {
        public float coverage;
        public Vector2 center;
        public bool touchesEdge;
        public float avgR;
        public float avgG;
        public float avgB;
    }

    internal sealed class NeighborCounts
    {
        public readonly Dictionary<string, int> north =
            new Dictionary<string, int>();
        public readonly Dictionary<string, int> east =
            new Dictionary<string, int>();
        public readonly Dictionary<string, int> south =
            new Dictionary<string, int>();
        public readonly Dictionary<string, int> west =
            new Dictionary<string, int>();

        public void Add(
            Dictionary<string, int> table,
            string key)
        {
            if (string.IsNullOrEmpty(key))
                return;

            int value;
            table.TryGetValue(key, out value);
            table[key] = value + 1;
        }

        public string Top(Dictionary<string, int> table)
        {
            if (table.Count == 0)
                return null;

            return table
                .OrderByDescending(p => p.Value)
                .First().Key;
        }
    }

    public sealed class AOTerrainHDPipelineWindow :
        EditorWindow
    {
        const string Version = "0.2.9";

        const string RegistryPath =
            "Assets/Resources/AOMigratorTerrainHD/" +
            "AOTerrainHDRegistry.asset";

        const string DataRoot =
            "Assets/AOMigrator/TerrainHDPipelineData";

        AOTerrainHDRegistry registry;

        bool useBuildScenes = true;
        bool scanLayer1 = true;
        bool scanLayer2 = true;
        bool scanLayer3 = false;
        bool scanLayer4 = false;

        int topRows = 40;

        AOTerrainHDCategory exportCategory =
            AOTerrainHDCategory.Grass;

        int exportMaxItems;
        int higgsTopPerCategory = 24;
        int cellSize = 128;
        int columns = 8;
        int rows = 8;

        Vector2 scroll;

        [MenuItem(
            "AO Migrador/Terrain HD Pipeline v0.2.9")]
        public static void Open()
        {
            AOTerrainHDPipelineWindow window =
                GetWindow<AOTerrainHDPipelineWindow>(
                    "AO Terrain HD v0.2.9");

            window.minSize =
                new Vector2(760, 680);
        }

        void OnEnable()
        {
            titleContent = new GUIContent("AO Terrain HD v0.2.9");
            LoadOrCreateRegistry();
        }

        void OnGUI()
        {
            if (registry == null)
                LoadOrCreateRegistry();

            scroll =
                EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField(
                "AO Terrain HD Pipeline v0.2.9",
                EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Escanea Ullathorpe y los mapas activos, " +
                "mide frecuencia de uso y vecinos de cada tile, " +
                "clasifica terreno, exporta atlas, importa HD " +
                "y valida costuras/transiciones. " +
                "No modifica colisiones ni sprites clásicos.",
                MessageType.Info);

            DrawScanner();
            EditorGUILayout.Space(12);
            DrawImpactTable();
            EditorGUILayout.Space(12);
            DrawExporter();
            EditorGUILayout.Space(12);
            DrawHiggsPrep();
            EditorGUILayout.Space(12);
            DrawImporter();
            EditorGUILayout.Space(12);
            DrawStyleControls();

            EditorGUILayout.EndScrollView();
        }

        void DrawScanner()
        {
            EditorGUILayout.LabelField(
                "1. Analizar Connected World",
                EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "v0.2.9 deduplica los map JSON por número de mapa y analiza directamente el Connected World. " +
                "No depende de que exista una escena Unity por mapa.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();

            scanLayer1 =
                EditorGUILayout.ToggleLeft(
                    "Layer 1",
                    scanLayer1,
                    GUILayout.Width(90));

            scanLayer2 =
                EditorGUILayout.ToggleLeft(
                    "Layer 2",
                    scanLayer2,
                    GUILayout.Width(90));

            scanLayer3 =
                EditorGUILayout.ToggleLeft(
                    "Layer 3",
                    scanLayer3,
                    GUILayout.Width(90));

            scanLayer4 =
                EditorGUILayout.ToggleLeft(
                    "Layer 4",
                    scanLayer4,
                    GUILayout.Width(90));

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(
                "Preset terreno: Layer 1 + 2"))
            {
                scanLayer1 = true;
                scanLayer2 = true;
                scanLayer3 = false;
                scanLayer4 = false;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(
                "Escanear map JSON + impacto"))
            {
                AOTerrainHDConnectedWorldScannerV022.Scan(
                    registry,
                    scanLayer1,
                    scanLayer2,
                    scanLayer3,
                    scanLayer4);
            }

            if (GUILayout.Button(
                "Auto-clasificar pendientes"))
            {
                AutoClassifyPending();
            }

            if (GUILayout.Button(
                "Exportar informe de impacto"))
            {
                ExportImpactReport();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(
                "Generar diagnóstico completo"))
            {
                AOTerrainHDDiagnosticV029.Generate(
                    registry,
                    true);
            }

            if (GUILayout.Button(
                "Seleccionar último diagnóstico"))
            {
                AOTerrainHDDiagnosticV029.SelectLastReport();
            }

            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button(
                "Reparar crops + resolver GRH originales"))
            {
                AOOriginalGrhResolverV029.RepairRegistry(
                    registry,
                    true);

                AOTerrainSemanticClassifierV029.RefineRegistry(
                    registry,
                    true);

                AOTerrainHDDiagnosticV029.Generate(
                    registry,
                    false);
            }

            if (registry != null)
            {
                EditorGUILayout.LabelField(
                    "Map JSON analizados",
                    registry.scannedSceneCount.ToString());

                foreach (AOTerrainHDCategory category
                         in Enum.GetValues(
                             typeof(AOTerrainHDCategory)))
                {
                    if (category ==
                        AOTerrainHDCategory.Unclassified)
                        continue;

                    EditorGUILayout.LabelField(
                        category.ToString(),
                        registry.CountCategory(category) +
                        " tiles | " +
                        registry.CountHD(category) +
                        " HD");
                }

                int unclassified =
                    registry.CountCategory(
                        AOTerrainHDCategory.Unclassified);

                if (unclassified > 0)
                {
                    EditorGUILayout.LabelField(
                        "Unclassified",
                        unclassified + " tiles");
                }

                int higgsReady =
                    registry.entries != null
                        ? registry.entries.Count(e => e != null && e.remasterEligible)
                        : 0;

                EditorGUILayout.LabelField(
                    "Aptos para remaster/Higgsfield",
                    higgsReady + " tiles");
            }
        }

        void DrawImpactTable()
        {
            EditorGUILayout.LabelField(
                "2. Prioridad visual",
                EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Impact = usos + 25 × cantidad de escenas. " +
                "Podés corregir manualmente la categoría; " +
                "la herramienta conservará esa decisión en futuros escaneos.",
                MessageType.None);

            topRows =
                EditorGUILayout.IntSlider(
                    "Mostrar top",
                    topRows,
                    10,
                    150);

            if (registry == null ||
                registry.entries == null)
                return;

            List<AOTerrainHDEntry> top =
                registry.entries
                    .Where(e => e != null)
                    .OrderByDescending(
                        e => e.ImpactScore)
                    .Take(topRows)
                    .ToList();

            foreach (AOTerrainHDEntry entry in top)
            {
                EditorGUILayout.BeginHorizontal();

                DrawSpritePreview(entry.classicSprite, 54f);

                EditorGUILayout.BeginVertical();

                EditorGUILayout.LabelField(
                    entry.displayName +
                    "  | usos " + entry.usageCount +
                    " | mapas " + entry.sceneCount +
                    " | impacto " + entry.ImpactScore,
                    EditorStyles.miniBoldLabel);

                string visual =
                    entry.visualCategory +
                    "  " +
                    Mathf.RoundToInt(entry.visualConfidence * 100f) +
                    "%";

                string semantic =
                    entry.suggestedCategory +
                    "  " +
                    Mathf.RoundToInt(entry.classificationConfidence * 100f) +
                    "%";

                EditorGUILayout.LabelField(
                    "Visual: " + visual,
                    EditorStyles.miniLabel);

                EditorGUILayout.LabelField(
                    "Semántica final: " + semantic +
                    " | familia " + Mathf.RoundToInt(entry.familySupport * 100f) +
                    "% | vecinos " + Mathf.RoundToInt(entry.neighborSupport * 100f) + "%",
                    EditorStyles.miniLabel);

                if (!string.IsNullOrEmpty(entry.classificationReason))
                {
                    EditorGUILayout.LabelField(
                        entry.classificationReason,
                        EditorStyles.wordWrappedLabel);
                }

                EditorGUILayout.LabelField(
                    (entry.remasterEligible ? "✓ APTO HIGGSFIELD" : "• No exportar todavía") +
                    (entry.remasterEligible ? " | prioridad " + entry.remasterPriority : "") +
                    " | " + entry.remasterReason,
                    EditorStyles.miniLabel);

                EditorGUILayout.BeginHorizontal();

                AOTerrainHDCategory before = entry.category;

                AOTerrainHDCategory after =
                    (AOTerrainHDCategory)
                    EditorGUILayout.EnumPopup(
                        before,
                        GUILayout.Width(170));

                if (after != before)
                {
                    Undo.RecordObject(
                        registry,
                        "Change terrain category");

                    entry.category = after;
                    entry.categoryWasManuallySet = true;
                    EditorUtility.SetDirty(registry);
                }

                GUI.enabled =
                    entry.suggestedCategory !=
                        AOTerrainHDCategory.Unclassified &&
                    entry.suggestedCategory != entry.category;

                if (GUILayout.Button(
                    "Usar sugerencia",
                    GUILayout.Width(120)))
                {
                    Undo.RecordObject(
                        registry,
                        "Apply terrain suggestion");

                    entry.category = entry.suggestedCategory;
                    entry.categoryWasManuallySet = true;
                    EditorUtility.SetDirty(registry);
                }

                GUI.enabled = true;

                if (entry.categoryWasManuallySet &&
                    GUILayout.Button(
                        "Volver a auto",
                        GUILayout.Width(105)))
                {
                    Undo.RecordObject(
                        registry,
                        "Return terrain category to auto");

                    entry.categoryWasManuallySet = false;
                    entry.category =
                        entry.classificationConfidence >= 0.58f
                            ? entry.suggestedCategory
                            : AOTerrainHDCategory.Unclassified;
                    EditorUtility.SetDirty(registry);
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(3);
            }

            if (GUI.changed)
                AssetDatabase.SaveAssets();
        }

        void DrawSpritePreview(Sprite sprite, float size)
        {
            Rect rect = GUILayoutUtility.GetRect(
                size,
                size,
                GUILayout.Width(size),
                GUILayout.Height(size));

            EditorGUI.DrawRect(
                rect,
                new Color(0.12f, 0.12f, 0.12f, 1f));

            if (sprite == null || sprite.texture == null)
            {
                GUI.Label(rect, "sin\npreview", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            Rect textureRect = sprite.textureRect;
            Rect uv = new Rect(
                textureRect.x / sprite.texture.width,
                textureRect.y / sprite.texture.height,
                textureRect.width / sprite.texture.width,
                textureRect.height / sprite.texture.height);

            GUI.DrawTextureWithTexCoords(
                rect,
                sprite.texture,
                uv,
                true);
        }

        void DrawExporter()
        {
            EditorGUILayout.LabelField(
                "3. Exportar atlas de terreno",
                EditorStyles.boldLabel);

            exportCategory =
                (AOTerrainHDCategory)
                EditorGUILayout.EnumPopup(
                    "Categoría",
                    exportCategory);

            exportMaxItems =
                EditorGUILayout.IntField(
                    "Máximo (0 = todos)",
                    exportMaxItems);

            cellSize =
                EditorGUILayout.IntSlider(
                    "Cell size",
                    cellSize,
                    64,
                    512);

            columns =
                EditorGUILayout.IntSlider(
                    "Columnas",
                    columns,
                    2,
                    16);

            rows =
                EditorGUILayout.IntSlider(
                    "Filas",
                    rows,
                    2,
                    16);

            EditorGUILayout.LabelField(
                "Tiles por atlas",
                (columns * rows).ToString());

            if (GUILayout.Button(
                "Exportar atlas + manifest"))
            {
                ExportCategory();
            }
        }

        void DrawHiggsPrep()
        {
            EditorGUILayout.LabelField(
                "4. Preparar referencias para Higgsfield",
                EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Exporta únicamente tiles semánticamente aptos para remaster, " +
                "ordenados por impacto × confianza. No llama a Higgsfield todavía.",
                MessageType.Info);

            higgsTopPerCategory = EditorGUILayout.IntSlider(
                "Top por categoría",
                higgsTopPerCategory,
                4,
                64);

            int eligible = registry != null && registry.entries != null
                ? registry.entries.Count(e => e != null && e.remasterEligible)
                : 0;

            EditorGUILayout.LabelField(
                "Candidatos aptos",
                eligible.ToString());

            if (GUILayout.Button(
                "GENERAR HIGGSFIELD PREP"))
            {
                AOTerrainHiggsPrepV029.Export(
                    registry,
                    higgsTopPerCategory);
            }
        }

        void DrawImporter()
        {
            EditorGUILayout.LabelField(
                "5. Importar terreno HD",
                EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "El PNG debe conservar exactamente la grilla " +
                "del REFERENCE. El validador revisa celdas vacías, " +
                "desplazamiento y costuras con los vecinos más frecuentes.",
                MessageType.None);

            if (GUILayout.Button(
                "Importar atlas HD + validar costuras"))
            {
                ImportAtlas();
            }
        }

        void DrawStyleControls()
        {
            EditorGUILayout.LabelField(
                "6. Aplicar / restaurar mundo",
                EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Como los 21 mapas se cargan dinámicamente, v0.2.9 aplica los recortes HD " +
                "sobre las PNG fuente usadas por el World Manager. Antes crea un backup " +
                "reversible de cada textura modificada.",
                MessageType.None);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(
                "APLICAR HD A TEXTURAS FUENTE"))
            {
                AOTerrainHDConnectedWorldScannerV022.ApplyHD(
                    registry);
            }

            if (GUILayout.Button(
                "RESTAURAR CLASSIC"))
            {
                AOTerrainHDConnectedWorldScannerV022.RestoreClassic();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(
                "Preview en Play (sprites ya registrados)",
                EditorStyles.miniBoldLabel);

            GUI.enabled =
                EditorApplication.isPlaying;

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("CLASSIC"))
            {
                AOTerrainHDStyleManager.SetStyle(
                    AOTerrainHDVisualStyle.Classic);
            }

            if (GUILayout.Button("HD"))
            {
                AOTerrainHDStyleManager.SetStyle(
                    AOTerrainHDVisualStyle.HD);
            }

            EditorGUILayout.EndHorizontal();

            GUI.enabled = true;
        }

        void LoadOrCreateRegistry()
        {
            registry = AssetDatabase.LoadAssetAtPath<AOTerrainHDRegistry>(RegistryPath);
            if (registry != null) { registry.RebuildLookup(); return; }

            EnsureFolder("Assets/Resources/AOMigratorTerrainHD");
            registry = CreateInstance<AOTerrainHDRegistry>();
            AssetDatabase.CreateAsset(registry, RegistryPath);
            AssetDatabase.SaveAssets();
        }

        void ScanScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            string previousScene = SceneManager.GetActiveScene().path;
            List<string> scenes = CollectScenePaths();

            if (scenes.Count == 0)
            {
                EditorUtility.DisplayDialog("AO Terrain HD", "No encontré escenas AO para analizar.", "Aceptar");
                return;
            }

            var old = new Dictionary<string, AOTerrainHDEntry>(StringComparer.OrdinalIgnoreCase);
            if (registry.entries != null)
            {
                foreach (var e in registry.entries)
                    if (e != null && !string.IsNullOrEmpty(e.key))
                        old[e.key] = e;
            }

            var found = new Dictionary<string, AOTerrainHDEntry>(StringComparer.OrdinalIgnoreCase);
            var entryScenes = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var neighbors = new Dictionary<string, NeighborCounts>(StringComparer.OrdinalIgnoreCase);

            try
            {
                for (int si = 0; si < scenes.Count; si++)
                {
                    string scenePath = scenes[si];

                    EditorUtility.DisplayProgressBar(
                        "AO Terrain HD",
                        "Analizando " + Path.GetFileNameWithoutExtension(scenePath),
                        si / (float)scenes.Count);

                    Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                    SpriteRenderer[] renderers = scene
                        .GetRootGameObjects()
                        .SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>(true))
                        .ToArray();

                    var tileToKey = new Dictionary<string, string>();

                    foreach (SpriteRenderer renderer in renderers)
                    {
                        if (renderer == null || renderer.sprite == null)
                            continue;

                        int layer = DetectAOLayer(renderer.transform);
                        if (!ShouldScanLayer(layer))
                            continue;

                        string key = SpriteKey(renderer.sprite);
                        if (string.IsNullOrEmpty(key))
                            continue;

                        if (!found.TryGetValue(key, out AOTerrainHDEntry entry))
                        {
                            if (old.TryGetValue(key, out AOTerrainHDEntry oldEntry))
                            {
                                entry = oldEntry;
                                entry.usageCount = entry.sceneCount = 0;
                                entry.layer1Uses = entry.layer2Uses = entry.layer3Uses = entry.layer4Uses = 0;
                                entry.northKey = entry.eastKey = entry.southKey = entry.westKey = null;
                            }
                            else
                            {
                                entry = new AOTerrainHDEntry
                                {
                                    key = key,
                                    displayName = renderer.sprite.name,
                                    classicSprite = renderer.sprite,
                                    classicAssetPath = AssetDatabase.GetAssetPath(renderer.sprite)
                                };
                            }

                            UpdateSpriteGeometry(entry, renderer.sprite);
                            found[key] = entry;
                        }

                        entry.classicSprite = renderer.sprite;
                        entry.classicAssetPath = AssetDatabase.GetAssetPath(renderer.sprite);
                        entry.displayName = renderer.sprite.name;
                        entry.usageCount++;

                        if (layer == 1) entry.layer1Uses++;
                        else if (layer == 2) entry.layer2Uses++;
                        else if (layer == 3) entry.layer3Uses++;
                        else if (layer == 4) entry.layer4Uses++;

                        if (!entryScenes.TryGetValue(key, out HashSet<string> seen))
                        {
                            seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            entryScenes[key] = seen;
                        }

                        seen.Add(scenePath);

                        Vector2Int tile = ReadTile(renderer);
                        tileToKey[layer + ":" + tile.x + ":" + tile.y] = key;
                    }

                    BuildAdjacency(tileToKey, neighbors);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();

                if (!string.IsNullOrEmpty(previousScene) &&
                    File.Exists(AbsolutePath(previousScene)))
                {
                    EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
                }
            }

            foreach (var pair in found)
            {
                AOTerrainHDEntry entry = pair.Value;

                if (entryScenes.TryGetValue(pair.Key, out HashSet<string> seen))
                    entry.sceneCount = seen.Count;

                if (neighbors.TryGetValue(pair.Key, out NeighborCounts n))
                {
                    entry.northKey = n.Top(n.north);
                    entry.eastKey = n.Top(n.east);
                    entry.southKey = n.Top(n.south);
                    entry.westKey = n.Top(n.west);
                }

                if (!entry.categoryWasManuallySet)
                    entry.category = SuggestCategory(entry);
            }

            registry.entries = found.Values
                .OrderByDescending(e => e.ImpactScore)
                .ThenBy(e => e.displayName)
                .ToList();

            registry.pipelineVersion = Version;
            registry.scannedSceneCount = scenes.Count;
            registry.scannedUtc = DateTime.UtcNow.ToString("o");
            registry.RebuildLookup();

            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            ExportImpactReport(false);

            EditorUtility.DisplayDialog(
                "AO Terrain HD",
                "Análisis terminado.\n\nEscenas: " + scenes.Count +
                "\nTiles únicos: " + registry.entries.Count +
                "\nGrass: " + registry.CountCategory(AOTerrainHDCategory.Grass) +
                "\nDirt: " + registry.CountCategory(AOTerrainHDCategory.Dirt) +
                "\nRoad: " + registry.CountCategory(AOTerrainHDCategory.Road) +
                "\nFloor: " + registry.CountCategory(AOTerrainHDCategory.Floor) +
                "\nWater: " + registry.CountCategory(AOTerrainHDCategory.Water) +
                "\nTransition: " + registry.CountCategory(AOTerrainHDCategory.Transition),
                "Aceptar");
        }

        List<string> CollectScenePaths()
        {
            List<string> result = new List<string>();

            if (useBuildScenes)
            {
                result = EditorBuildSettings.scenes
                    .Where(s => s.enabled)
                    .Select(s => s.path)
                    .Where(p => !string.IsNullOrEmpty(p) &&
                                p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            if (result.Count > 0)
                return result;

            foreach (string guid in AssetDatabase.FindAssets("t:Scene"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string lower = path.ToLowerInvariant();

                if (lower.Contains("ao_") ||
                    lower.Contains("playable") ||
                    lower.Contains("ullathorpe") ||
                    lower.Contains("mapa"))
                    result.Add(path);
            }

            return result
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p)
                .ToList();
        }

        bool ShouldScanLayer(int layer)
        {
            if (layer == 1) return scanLayer1;
            if (layer == 2) return scanLayer2;
            if (layer == 3) return scanLayer3;
            if (layer == 4) return scanLayer4;
            return false;
        }

        int DetectAOLayer(Transform transform)
        {
            Transform current = transform;
            while (current != null)
            {
                string value = current.name.Replace(" ", "").Replace("-", "").ToLowerInvariant();
                Match match = Regex.Match(value, @"(?:layer|capa)_?([1-4])");
                if (match.Success)
                    return int.Parse(match.Groups[1].Value);
                current = current.parent;
            }

            return 0;
        }

        Vector2Int ReadTile(SpriteRenderer renderer)
        {
            Match match = Regex.Match(
                renderer.gameObject.name,
                @"(?:^|_)(\d{1,3})[_x,](\d{1,3})(?:_|$)",
                RegexOptions.IgnoreCase);

            if (match.Success)
                return new Vector2Int(
                    int.Parse(match.Groups[1].Value),
                    int.Parse(match.Groups[2].Value));

            return new Vector2Int(
                Mathf.RoundToInt(renderer.transform.position.x + 0.5f),
                Mathf.RoundToInt(-renderer.transform.position.y));
        }

        void BuildAdjacency(
            Dictionary<string, string> tiles,
            Dictionary<string, NeighborCounts> all)
        {
            foreach (var pair in tiles)
            {
                string[] parts = pair.Key.Split(':');
                if (parts.Length != 3)
                    continue;

                int layer = int.Parse(parts[0]);
                int x = int.Parse(parts[1]);
                int y = int.Parse(parts[2]);

                if (!all.TryGetValue(pair.Value, out NeighborCounts counts))
                {
                    counts = new NeighborCounts();
                    all[pair.Value] = counts;
                }

                AddNeighbor(tiles, counts, counts.north, layer, x, y - 1);
                AddNeighbor(tiles, counts, counts.east, layer, x + 1, y);
                AddNeighbor(tiles, counts, counts.south, layer, x, y + 1);
                AddNeighbor(tiles, counts, counts.west, layer, x - 1, y);
            }
        }

        void AddNeighbor(
            Dictionary<string, string> tiles,
            NeighborCounts counts,
            Dictionary<string, int> table,
            int layer,
            int x,
            int y)
        {
            string tileKey = layer + ":" + x + ":" + y;

            if (tiles.TryGetValue(tileKey, out string neighbor))
                counts.Add(table, neighbor);
        }

        void UpdateSpriteGeometry(
            AOTerrainHDEntry entry,
            Sprite sprite)
        {
            Rect rect = sprite.rect;

            entry.sourceWidth = Mathf.Max(1, Mathf.RoundToInt(rect.width));
            entry.sourceHeight = Mathf.Max(1, Mathf.RoundToInt(rect.height));
            entry.sourcePixelsPerUnit = sprite.pixelsPerUnit;

            entry.sourcePivotNormalized = new Vector2(
                rect.width > 0 ? sprite.pivot.x / rect.width : 0.5f,
                rect.height > 0 ? sprite.pivot.y / rect.height : 0.5f);
        }

        string SpriteKey(Sprite sprite)
        {
            if (sprite == null)
                return null;

            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                sprite,
                out string guid,
                out long localId))
            {
                return guid + ":" + localId;
            }

            return AssetDatabase.GetAssetPath(sprite) + ":" + sprite.name;
        }

        void AutoClassifyPending()
        {
            AOTerrainHDConnectedWorldScannerV022.ApplySuggestions(
                registry,
                0.40f);
        }

        AOTerrainHDCategory SuggestCategory(
            AOTerrainHDEntry entry)
        {
            string text =
                ((entry.classicAssetPath ?? "") + "/" + (entry.displayName ?? ""))
                .ToLowerInvariant();

            if (Regex.IsMatch(text, @"water|agua|river|rio|lake|lago|sea|mar"))
                return AOTerrainHDCategory.Water;

            if (Regex.IsMatch(text, @"grass|cesped|pasto|lawn"))
                return AOTerrainHDCategory.Grass;

            if (Regex.IsMatch(text, @"dirt|tierra|mud|barro|soil|arena|sand"))
                return AOTerrainHDCategory.Dirt;

            if (Regex.IsMatch(text, @"road|camino|path|sendero|street|calle"))
                return AOTerrainHDCategory.Road;

            if (Regex.IsMatch(text, @"floor|piso|wood|madera|brick|ladrillo|stone|piedra"))
                return AOTerrainHDCategory.Floor;

            if (entry.classicSprite == null)
                return AOTerrainHDCategory.Unclassified;

            Texture2D texture = ReadSprite(entry.classicSprite);
            if (texture == null)
                return AOTerrainHDCategory.Unclassified;

            AOTerrainPixelStats stats = Measure(texture);
            DestroyImmediate(texture);

            if (entry.layer2Uses > entry.layer1Uses &&
                stats.coverage < 0.86f)
                return AOTerrainHDCategory.Transition;

            if (stats.avgB > stats.avgG * 1.12f &&
                stats.avgB > stats.avgR * 1.22f)
                return AOTerrainHDCategory.Water;

            if (stats.avgG > stats.avgR * 1.10f &&
                stats.avgG > stats.avgB * 1.08f)
                return AOTerrainHDCategory.Grass;

            if (stats.avgR > stats.avgB * 1.18f &&
                stats.avgG > stats.avgB * 1.08f &&
                stats.avgR < stats.avgG * 1.35f)
                return AOTerrainHDCategory.Dirt;

            return AOTerrainHDCategory.Unclassified;
        }

        void ExportImpactReport(bool notify = true)
        {
            if (registry == null || registry.entries == null)
                return;

            EnsureFolder(DataRoot + "/Diagnostics");

            AOTerrainHDImpactReport report =
                new AOTerrainHDImpactReport
                {
                    createdUtc = DateTime.UtcNow.ToString("o"),
                    scannedScenes = registry.scannedSceneCount,
                    topAssets = registry.entries
                        .Where(e => e != null)
                        .OrderByDescending(e => e.ImpactScore)
                        .Take(300)
                        .Select(e => new AOTerrainHDImpactItem
                        {
                            key = e.key,
                            name = e.displayName,
                            category = e.category.ToString(),
                            uses = e.usageCount,
                            scenes = e.sceneCount,
                            impact = e.ImpactScore,
                            northKey = e.northKey,
                            eastKey = e.eastKey,
                            southKey = e.southKey,
                            westKey = e.westKey
                        })
                        .ToArray()
                };

            string jsonPath =
                DataRoot + "/Diagnostics/terrain_impact_v02.json";

            File.WriteAllText(
                AbsolutePath(jsonPath),
                JsonUtility.ToJson(report, true),
                new UTF8Encoding(false));

            string csvPath =
                DataRoot + "/Diagnostics/terrain_impact_v02.csv";

            StringBuilder csv =
                new StringBuilder(
                    "rank,category,name,uses,scenes,impact,key\n");

            int rank = 1;

            foreach (AOTerrainHDEntry entry
                     in registry.entries
                         .Where(e => e != null)
                         .OrderByDescending(e => e.ImpactScore)
                         .Take(500))
            {
                csv.Append(rank++).Append(',')
                    .Append(entry.category).Append(',')
                    .Append('"').Append(
                        (entry.displayName ?? "")
                            .Replace("\"", "\"\""))
                    .Append('"').Append(',')
                    .Append(entry.usageCount).Append(',')
                    .Append(entry.sceneCount).Append(',')
                    .Append(entry.ImpactScore).Append(',')
                    .Append('"').Append(entry.key).Append('"')
                    .Append('\n');
            }

            File.WriteAllText(
                AbsolutePath(csvPath),
                csv.ToString(),
                new UTF8Encoding(false));

            AssetDatabase.Refresh();

            if (notify)
            {
                EditorUtility.DisplayDialog(
                    "AO Terrain HD",
                    "Informe exportado:\n" +
                    jsonPath + "\n" +
                    csvPath,
                    "Aceptar");
            }
        }

        void ExportCategory()
        {
            if (registry == null || registry.entries == null)
                return;

            List<AOTerrainHDEntry> list =
                registry.entries
                    .Where(e => e != null &&
                                e.category == exportCategory &&
                                e.classicSprite != null)
                    .OrderByDescending(e => e.ImpactScore)
                    .ToList();

            if (exportMaxItems > 0)
                list = list.Take(exportMaxItems).ToList();

            if (list.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "AO Terrain HD",
                    "No hay tiles en " + exportCategory + ".",
                    "Aceptar");
                return;
            }

            string dir =
                DataRoot + "/Exports/" + exportCategory;

            EnsureFolder(dir);

            int batchSize =
                Mathf.Max(1, columns * rows);

            int batches =
                Mathf.CeilToInt(
                    list.Count / (float)batchSize);

            for (int batch = 0;
                 batch < batches;
                 batch++)
            {
                List<AOTerrainHDEntry> part =
                    list.Skip(batch * batchSize)
                        .Take(batchSize)
                        .ToList();

                string batchName =
                    exportCategory.ToString().ToUpperInvariant() +
                    "_TERRAIN_" +
                    (batch + 1).ToString("000");

                ExportBatch(part, dir, batchName);
            }

            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "AO Terrain HD",
                "Exportados " + list.Count +
                " tiles en " + batches +
                " atlas.\n" + dir,
                "Aceptar");
        }

        void ExportBatch(
            List<AOTerrainHDEntry> list,
            string dir,
            string batchName)
        {
            int atlasWidth = columns * cellSize;
            int atlasHeight = rows * cellSize;

            Texture2D atlas =
                new Texture2D(
                    atlasWidth,
                    atlasHeight,
                    TextureFormat.RGBA32,
                    false);

            atlas.SetPixels32(
                new Color32[
                    atlasWidth * atlasHeight]);

            List<AOTerrainHDManifestItem> items =
                new List<AOTerrainHDManifestItem>();

            for (int i = 0; i < list.Count; i++)
            {
                AOTerrainHDEntry entry = list[i];

                int column = i % columns;
                int row = i / columns;

                Texture2D source =
                    ReadSprite(entry.classicSprite);

                AOTerrainPixelStats stats =
                    Measure(source);

                Texture2D fitted =
                    FitToCell(
                        source,
                        cellSize,
                        cellSize);

                atlas.SetPixels32(
                    column * cellSize,
                    atlasHeight -
                    (row + 1) * cellSize,
                    cellSize,
                    cellSize,
                    fitted.GetPixels32());

                items.Add(
                    new AOTerrainHDManifestItem
                    {
                        key = entry.key,
                        displayName = entry.displayName,
                        sourceAssetPath =
                            entry.classicAssetPath,
                        spriteName =
                            entry.classicSprite.name,
                        usageCount =
                            entry.usageCount,
                        sceneCount =
                            entry.sceneCount,
                        row = row,
                        column = column,
                        sourceWidth =
                            entry.sourceWidth,
                        sourceHeight =
                            entry.sourceHeight,
                        pixelsPerUnit =
                            entry.sourcePixelsPerUnit,
                        pivotX =
                            entry.sourcePivotNormalized.x,
                        pivotY =
                            entry.sourcePivotNormalized.y,
                        originalAlphaCoverage =
                            stats.coverage,
                        originalCenterX =
                            stats.center.x,
                        originalCenterY =
                            stats.center.y,
                        northKey = entry.northKey,
                        eastKey = entry.eastKey,
                        southKey = entry.southKey,
                        westKey = entry.westKey
                    });

                DestroyImmediate(source);
                DestroyImmediate(fitted);
            }

            atlas.Apply();

            string pngPath =
                dir + "/" +
                batchName +
                "_REFERENCE.png";

            string manifestPath =
                dir + "/" +
                batchName +
                "_MANIFEST.json";

            File.WriteAllBytes(
                AbsolutePath(pngPath),
                atlas.EncodeToPNG());

            AOTerrainHDManifest manifest =
                new AOTerrainHDManifest
                {
                    category =
                        exportCategory.ToString(),
                    batchName = batchName,
                    cellSize = cellSize,
                    columns = columns,
                    rows = rows,
                    atlasWidth = atlasWidth,
                    atlasHeight = atlasHeight,
                    items = items.ToArray()
                };

            File.WriteAllText(
                AbsolutePath(manifestPath),
                JsonUtility.ToJson(
                    manifest,
                    true),
                new UTF8Encoding(false));

            DestroyImmediate(atlas);
        }

        void ImportAtlas()
        {
            string png = EditorUtility.OpenFilePanel("Seleccioná atlas HD", "", "png");
            if (string.IsNullOrEmpty(png)) return;

            string manifestPath = EditorUtility.OpenFilePanel(
                "Seleccioná manifest", Path.GetDirectoryName(png), "json");
            if (string.IsNullOrEmpty(manifestPath)) return;

            AOTerrainHDManifest manifest;
            try
            {
                manifest = JsonUtility.FromJson<AOTerrainHDManifest>(
                    File.ReadAllText(manifestPath));
            }
            catch (Exception error)
            {
                EditorUtility.DisplayDialog(
                    "AO Terrain HD", "Manifest inválido:\n" + error.Message, "Aceptar");
                return;
            }

            if (manifest == null || manifest.items == null || manifest.items.Length == 0)
            {
                EditorUtility.DisplayDialog("AO Terrain HD", "El manifest no contiene tiles.", "Aceptar");
                return;
            }

            Texture2D atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            atlas.LoadImage(File.ReadAllBytes(png));

            if (atlas.width != manifest.atlasWidth || atlas.height != manifest.atlasHeight)
            {
                DestroyImmediate(atlas);
                EditorUtility.DisplayDialog(
                    "AO Terrain HD",
                    "Dimensiones incorrectas. Esperado: " +
                    manifest.atlasWidth + "x" + manifest.atlasHeight,
                    "Aceptar");
                return;
            }

            if (!Enum.TryParse(manifest.category, true, out AOTerrainHDCategory category))
                category = AOTerrainHDCategory.Other;

            string importDir = DataRoot + "/ImportedHD/" + category + "/" + manifest.batchName;
            EnsureFolder(importDir);

            var reportItems = new List<AOTerrainHDValidationItem>();
            var validationByKey = new Dictionary<string, AOTerrainHDValidationItem>(
                StringComparer.OrdinalIgnoreCase);

            try
            {
                for (int i = 0; i < manifest.items.Length; i++)
                {
                    AOTerrainHDManifestItem item = manifest.items[i];

                    EditorUtility.DisplayProgressBar(
                        "AO Terrain HD",
                        "Importando " + item.displayName,
                        i / (float)manifest.items.Length);

                    AOTerrainHDEntry entry = registry.FindByKey(item.key);

                    if (entry == null)
                    {
                        var missing = new AOTerrainHDValidationItem
                        {
                            key = item.key,
                            severity = "ERROR",
                            message = "El tile ya no existe en el registry."
                        };
                        reportItems.Add(missing);
                        validationByKey[item.key] = missing;
                        continue;
                    }

                    Texture2D cell = ExtractCell(
                        atlas, item.column, item.row, manifest.cellSize, manifest.atlasHeight);

                    AOTerrainPixelStats stats = Measure(cell);

                    float drift = Vector2.Distance(
                        new Vector2(item.originalCenterX, item.originalCenterY),
                        stats.center);

                    string severity = "OK";
                    List<string> messages = new List<string>();

                    if (stats.coverage < 0.005f)
                    {
                        severity = "ERROR";
                        messages.Add("Celda prácticamente vacía.");
                    }

                    if (stats.touchesEdge)
                    {
                        if (severity != "ERROR") severity = "WARNING";
                        messages.Add("El dibujo toca el borde.");
                    }

                    if (drift > 0.18f)
                    {
                        if (severity != "ERROR") severity = "WARNING";
                        messages.Add("Centro visual desplazado.");
                    }

                    if (messages.Count == 0) messages.Add("OK");

                    Texture2D resized = ResizePoint(
                        cell,
                        Mathf.Max(1, item.sourceWidth),
                        Mathf.Max(1, item.sourceHeight));

                    string assetPath =
                        importDir + "/" + SafeName(item.key) + "_HD.png";

                    File.WriteAllBytes(AbsolutePath(assetPath), resized.EncodeToPNG());

                    AssetDatabase.ImportAsset(
                        assetPath, ImportAssetOptions.ForceSynchronousImport);

                    ConfigureImporter(assetPath, item);

                    entry.hdSprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                    entry.hdAssetPath = assetPath;
                    entry.validated = severity != "ERROR";

                    var validation = new AOTerrainHDValidationItem
                    {
                        key = item.key,
                        severity = severity,
                        message = string.Join(" ", messages),
                        alphaCoverage = stats.coverage,
                        centerDrift = drift,
                        touchesCellEdge = stats.touchesEdge
                    };

                    reportItems.Add(validation);
                    validationByKey[item.key] = validation;
                    entry.validationMessage = validation.message;

                    DestroyImmediate(cell);
                    DestroyImmediate(resized);
                }
            }
            finally
            {
                DestroyImmediate(atlas);
                EditorUtility.ClearProgressBar();
            }

            registry.RebuildLookup();
            ValidateSeams(manifest, validationByKey);

            foreach (AOTerrainHDEntry entry in registry.entries)
            {
                if (entry != null &&
                    validationByKey.TryGetValue(entry.key, out AOTerrainHDValidationItem item))
                {
                    entry.validated = item.severity != "ERROR";
                    entry.validationMessage = item.message;
                }
            }

            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();

            EnsureFolder(DataRoot + "/Diagnostics");

            AOTerrainHDValidationReport report = new AOTerrainHDValidationReport
            {
                createdUtc = DateTime.UtcNow.ToString("o"),
                category = manifest.category,
                batchName = manifest.batchName,
                totalItems = reportItems.Count,
                validItems = reportItems.Count(x => x.severity == "OK"),
                warningItems = reportItems.Count(x => x.severity == "WARNING"),
                errorItems = reportItems.Count(x => x.severity == "ERROR"),
                items = reportItems.ToArray()
            };

            string reportPath =
                DataRoot + "/Diagnostics/" + manifest.batchName + "_VALIDATION.json";

            File.WriteAllText(
                AbsolutePath(reportPath),
                JsonUtility.ToJson(report, true),
                new UTF8Encoding(false));

            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "AO Terrain HD",
                "Importación finalizada.\n\nOK: " + report.validItems +
                "\nWarnings: " + report.warningItems +
                "\nErrores: " + report.errorItems +
                "\n\n" + reportPath,
                "Aceptar");
        }

        void ValidateSeams(
            AOTerrainHDManifest manifest,
            Dictionary<string, AOTerrainHDValidationItem> validations)
        {
            foreach (AOTerrainHDManifestItem item in manifest.items)
            {
                CheckSeam(item.key, item.eastKey, true, validations);
                CheckSeam(item.key, item.southKey, false, validations);
            }
        }

        void CheckSeam(
            string keyA,
            string keyB,
            bool horizontal,
            Dictionary<string, AOTerrainHDValidationItem> validations)
        {
            if (string.IsNullOrEmpty(keyA) || string.IsNullOrEmpty(keyB))
                return;

            AOTerrainHDEntry a = registry.FindByKey(keyA);
            AOTerrainHDEntry b = registry.FindByKey(keyB);

            if (a == null || b == null ||
                a.hdSprite == null || b.hdSprite == null ||
                a.classicSprite == null || b.classicSprite == null)
                return;

            float classicDiff = EdgeDifference(a.classicSprite, b.classicSprite, horizontal);
            float hdDiff = EdgeDifference(a.hdSprite, b.hdSprite, horizontal);
            float allowed = Mathf.Max(0.28f, classicDiff + 0.12f);

            if (hdDiff <= allowed) return;

            if (!validations.TryGetValue(keyA, out AOTerrainHDValidationItem result))
                return;

            if (result.severity == "OK") result.severity = "WARNING";

            result.worstSeamScore = Mathf.Max(result.worstSeamScore, hdDiff);
            result.message +=
                " Costura con vecino peor que original (" +
                hdDiff.ToString("0.00") + " vs " +
                classicDiff.ToString("0.00") + ").";
        }

        float EdgeDifference(Sprite a, Sprite b, bool horizontal)
        {
            Texture2D ta = ReadSprite(a);
            Texture2D tb = ReadSprite(b);

            if (ta == null || tb == null)
            {
                if (ta != null) DestroyImmediate(ta);
                if (tb != null) DestroyImmediate(tb);
                return 0f;
            }

            const int samples = 24;
            float total = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)(samples - 1);

                int ax, ay, bx, by;

                if (horizontal)
                {
                    ax = ta.width - 1;
                    bx = 0;
                    ay = Mathf.Clamp(
                        Mathf.RoundToInt(t * (ta.height - 1)),
                        0, ta.height - 1);
                    by = Mathf.Clamp(
                        Mathf.RoundToInt(t * (tb.height - 1)),
                        0, tb.height - 1);
                }
                else
                {
                    ay = 0;
                    by = tb.height - 1;
                    ax = Mathf.Clamp(
                        Mathf.RoundToInt(t * (ta.width - 1)),
                        0, ta.width - 1);
                    bx = Mathf.Clamp(
                        Mathf.RoundToInt(t * (tb.width - 1)),
                        0, tb.width - 1);
                }

                Color ca = ta.GetPixel(ax, ay);
                Color cb = tb.GetPixel(bx, by);

                total +=
                    (Mathf.Abs(ca.r - cb.r) +
                     Mathf.Abs(ca.g - cb.g) +
                     Mathf.Abs(ca.b - cb.b) +
                     Mathf.Abs(ca.a - cb.a)) / 4f;
            }

            DestroyImmediate(ta);
            DestroyImmediate(tb);

            return total / samples;
        }

        void ConfigureImporter(
            string assetPath,
            AOTerrainHDManifestItem item)
        {
            TextureImporter importer =
                AssetImporter.GetAtPath(assetPath)
                as TextureImporter;

            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression =
                TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.spritePixelsPerUnit =
                Mathf.Max(1f, item.pixelsPerUnit);
            importer.spritePivot = new Vector2(
                Mathf.Clamp01(item.pivotX),
                Mathf.Clamp01(item.pivotY));

            importer.SaveAndReimport();
        }

        static Texture2D ReadSprite(Sprite original)
        {
            if (original == null) return null;

            string path = AssetDatabase.GetAssetPath(original.texture);
            string spriteName = original.name;

            TextureImporter importer =
                AssetImporter.GetAtPath(path) as TextureImporter;

            bool restore = false;
            bool oldReadable = false;

            if (importer != null)
            {
                oldReadable = importer.isReadable;

                if (!oldReadable)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                    restore = true;
                }
            }

            try
            {
                Sprite sprite = AssetDatabase
                    .LoadAllAssetsAtPath(path)
                    .OfType<Sprite>()
                    .FirstOrDefault(s => s.name == spriteName);

                if (sprite == null)
                    sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

                if (sprite == null)
                    return null;

                Rect rect = sprite.rect;
                int x = Mathf.RoundToInt(rect.x);
                int y = Mathf.RoundToInt(rect.y);
                int w = Mathf.Max(1, Mathf.RoundToInt(rect.width));
                int h = Mathf.Max(1, Mathf.RoundToInt(rect.height));

                Color[] pixels = sprite.texture.GetPixels(x, y, w, h);

                Texture2D result =
                    new Texture2D(w, h, TextureFormat.RGBA32, false);

                result.SetPixels(pixels);
                result.Apply(false, false);
                return result;
            }
            finally
            {
                if (restore && importer != null)
                {
                    importer.isReadable = oldReadable;
                    importer.SaveAndReimport();
                }
            }
        }

        static Texture2D FitToCell(
            Texture2D source,
            int width,
            int height)
        {
            Texture2D destination =
                new Texture2D(
                    width,
                    height,
                    TextureFormat.RGBA32,
                    false);

            destination.SetPixels32(
                new Color32[width * height]);

            if (source == null)
            {
                destination.Apply();
                return destination;
            }

            float scale = Mathf.Min(
                width / (float)source.width,
                height / (float)source.height);

            int targetW = Mathf.Max(
                1,
                Mathf.RoundToInt(source.width * scale));

            int targetH = Mathf.Max(
                1,
                Mathf.RoundToInt(source.height * scale));

            Texture2D resized =
                ResizePoint(source, targetW, targetH);

            int offsetX = (width - targetW) / 2;
            int offsetY = (height - targetH) / 2;

            destination.SetPixels(
                offsetX,
                offsetY,
                targetW,
                targetH,
                resized.GetPixels());

            destination.Apply(false, false);
            DestroyImmediate(resized);

            return destination;
        }

        static Texture2D ExtractCell(
            Texture2D atlas,
            int column,
            int row,
            int size,
            int atlasHeight)
        {
            int x = column * size;
            int y = atlasHeight - (row + 1) * size;

            Texture2D result =
                new Texture2D(
                    size,
                    size,
                    TextureFormat.RGBA32,
                    false);

            result.SetPixels(
                atlas.GetPixels(x, y, size, size));

            result.Apply(false, false);
            return result;
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

            Color32[] sourcePixels =
                source.GetPixels32();

            Color32[] output =
                new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                int sy = Mathf.Clamp(
                    Mathf.FloorToInt(
                        y * source.height /
                        (float)height),
                    0,
                    source.height - 1);

                for (int x = 0; x < width; x++)
                {
                    int sx = Mathf.Clamp(
                        Mathf.FloorToInt(
                            x * source.width /
                            (float)width),
                        0,
                        source.width - 1);

                    output[y * width + x] =
                        sourcePixels[
                            sy * source.width + sx];
                }
            }

            result.SetPixels32(output);
            result.Apply(false, false);
            return result;
        }

        static AOTerrainPixelStats Measure(
            Texture2D texture)
        {
            if (texture == null)
                return new AOTerrainPixelStats
                {
                    center = new Vector2(0.5f, 0.5f)
                };

            Color32[] pixels = texture.GetPixels32();

            int visible = 0;
            double sumX = 0;
            double sumY = 0;
            double r = 0;
            double g = 0;
            double b = 0;
            bool edge = false;

            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    Color32 p =
                        pixels[
                            y * texture.width + x];

                    if (p.a <= 12) continue;

                    visible++;
                    sumX += x;
                    sumY += y;
                    r += p.r / 255.0;
                    g += p.g / 255.0;
                    b += p.b / 255.0;

                    if (x == 0 || y == 0 ||
                        x == texture.width - 1 ||
                        y == texture.height - 1)
                        edge = true;
                }
            }

            if (visible == 0)
                return new AOTerrainPixelStats
                {
                    coverage = 0f,
                    center = new Vector2(0.5f, 0.5f),
                    touchesEdge = edge
                };

            return new AOTerrainPixelStats
            {
                coverage =
                    visible / (float)pixels.Length,
                center = new Vector2(
                    (float)(sumX / visible) /
                    Mathf.Max(1, texture.width - 1),
                    (float)(sumY / visible) /
                    Mathf.Max(1, texture.height - 1)),
                touchesEdge = edge,
                avgR = (float)(r / visible),
                avgG = (float)(g / visible),
                avgB = (float)(b / visible)
            };
        }

        static string SafeName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "tile";

            foreach (char invalid
                     in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');

            value = value
                .Replace('/', '_')
                .Replace('\\', '_')
                .Replace(':', '_')
                .Replace(' ', '_');

            return value.Length > 120
                ? value.Substring(0, 120)
                : value;
        }

        static string AbsolutePath(
            string assetPath)
        {
            string root =
                Directory
                    .GetParent(Application.dataPath)
                    .FullName;

            return Path.Combine(
                root,
                assetPath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
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
                string next =
                    current + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(
                        current,
                        parts[i]);

                current = next;
            }
        }
    }
}
#endif
