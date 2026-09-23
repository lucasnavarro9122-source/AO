// AO Terrain HD v0.2.9
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using AOMigrator.TerrainHD;

namespace AOMigrator.TerrainHD.Editor
{
    internal sealed class AOGrhDefV029
    {
        public int id, frames, firstFrame, fileNum, sx, sy, width, height;
        public bool animated;
    }

    public static class AOOriginalGrhResolverV029
    {
        public const string Version = "0.2.9";
        const string IniPath = "Assets/AOMigrator/TerrainHDPipelineData/OriginalAO/graficos.ini";
        const string SpriteRoot = "Assets/AOMigrator/MapVisualV063/Generated/Sprites";
        const string RegistryPath = "Assets/Resources/AOMigratorTerrainHD/AOTerrainHDRegistry.asset";
        static Dictionary<int, AOGrhDefV029> defs;
        static readonly Dictionary<int, Texture2D> textures = new Dictionary<int, Texture2D>();

        [MenuItem("AO Migrador/Terrain HD/Reparar crops + resolver GRH original v0.2.9")]
        public static void RepairFromMenu()
        {
            var r = AssetDatabase.LoadAssetAtPath<AOTerrainHDRegistry>(RegistryPath);
            if (r == null)
            {
                EditorUtility.DisplayDialog("AO Terrain HD v0.2.9", "No encontré el registry. Ejecutá primero el scanner.", "Aceptar");
                return;
            }
            RepairRegistry(r, true);
        }

        public static void RepairRegistry(AOTerrainHDRegistry registry, bool showDialog)
        {
            if (registry == null || registry.entries == null) return;
            EnsureDefs(); textures.Clear();
            int beforePreview = registry.entries.Count(e => e != null && e.classicSprite != null);
            int beforeCrop = registry.entries.Count(ValidCrop);
            int newSprites = 0, repaired = 0;
            try
            {
                for (int i = 0; i < registry.entries.Count; i++)
                {
                    var e = registry.entries[i];
                    if (e == null) continue;
                    EditorUtility.DisplayProgressBar("AO Terrain HD v0.2.9", "Reparando GRH " + e.graphicId, i / (float)Mathf.Max(1, registry.entries.Count));
                    bool hadSprite = e.classicSprite != null;
                    bool hadCrop = ValidCrop(e);
                    RepairOrResolveEntry(e);
                    if (!hadSprite && e.classicSprite != null) newSprites++;
                    if (!hadCrop && ValidCrop(e)) repaired++;
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            registry.pipelineVersion = Version;
            registry.RebuildLookup();
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            int afterPreview = registry.entries.Count(e => e != null && e.classicSprite != null);
            int afterCrop = registry.entries.Count(ValidCrop);
            string msg = "Previews: " + beforePreview + " -> " + afterPreview +
                         "\nCrops válidos: " + beforeCrop + " -> " + afterCrop +
                         "\nNuevos GRH resueltos: " + newSprites +
                         "\nCrops reparados: " + repaired;
            Debug.Log("[AO Terrain HD v0.2.9] " + msg.Replace("\n", " | "));
            if (showDialog) EditorUtility.DisplayDialog("AO Terrain HD v0.2.9", "Reparación terminada.\n\n" + msg, "Aceptar");
        }

        public static void RepairOrResolveEntry(AOTerrainHDEntry e)
        {
            if (e == null) return;
            if (e.classicSprite != null) Normalize(e);
            if (e.classicSprite == null || !ValidCrop(e)) ResolveOriginal(e);
            if (e.classicSprite != null) Normalize(e);
        }

        static void ResolveOriginal(AOTerrainHDEntry e)
        {
            EnsureDefs();
            AOGrhDefV029 requested;
            if (defs == null || !defs.TryGetValue(e.graphicId, out requested)) return;
            var visual = Visual(requested, 0);
            if (visual == null || visual.fileNum <= 0 || visual.width <= 0 || visual.height <= 0) return;
            var tex = FindTexture(visual.fileNum);
            if (tex == null) return;
            e.originalFileNum = visual.fileNum;
            e.visualFrameGrhId = visual.id;
            e.isAnimatedGrh = requested.animated;
            e.sourceTexturePath = AssetDatabase.GetAssetPath(tex);
            e.imageFile = Path.GetFileName(e.sourceTexturePath);
            e.cropX = visual.sx; e.cropY = visual.sy;
            e.cropWidth = visual.width; e.cropHeight = visual.height;
            e.sourceWidth = visual.width; e.sourceHeight = visual.height;
            e.sourcePixelsPerUnit = 32f; e.sourcePivotNormalized = new Vector2(0.5f, 0.5f);
            if (e.classicSprite == null) e.classicSprite = CreateSprite(e, tex, visual);
            if (e.classicSprite != null) e.classicAssetPath = AssetDatabase.GetAssetPath(e.classicSprite);
        }

        static AOGrhDefV029 Visual(AOGrhDefV029 d, int depth)
        {
            if (d == null || depth > 16) return null;
            if (!d.animated) return d;
            AOGrhDefV029 f;
            return d.firstFrame > 0 && defs.TryGetValue(d.firstFrame, out f) ? Visual(f, depth + 1) : null;
        }

        static void Normalize(AOTerrainHDEntry e)
        {
            var s = e.classicSprite;
            if (s == null || s.texture == null) return;
            var t = s.texture;
            Rect r;
            try { r = s.textureRect; } catch { r = s.rect; }
            int w = Mathf.Max(1, Mathf.RoundToInt(r.width));
            int h = Mathf.Max(1, Mathf.RoundToInt(r.height));
            int x = Mathf.Max(0, Mathf.RoundToInt(r.x));
            int bottomY = Mathf.Max(0, Mathf.RoundToInt(r.y));
            e.cropX = x; e.cropY = Mathf.Max(0, t.height - bottomY - h);
            e.cropWidth = w; e.cropHeight = h;
            e.sourceWidth = w; e.sourceHeight = h;
            e.sourcePixelsPerUnit = Mathf.Max(1f, s.pixelsPerUnit);
            e.sourcePivotNormalized = new Vector2(r.width > 0 ? s.pivot.x / r.width : .5f, r.height > 0 ? s.pivot.y / r.height : .5f);
            e.classicAssetPath = AssetDatabase.GetAssetPath(s);
            e.sourceTexturePath = AssetDatabase.GetAssetPath(t);
            e.imageFile = String.IsNullOrEmpty(e.sourceTexturePath) ? "" : Path.GetFileName(e.sourceTexturePath);
            if (e.originalFileNum <= 0) e.originalFileNum = ParseFileNum(e.sourceTexturePath);
            if (e.visualFrameGrhId <= 0) e.visualFrameGrhId = e.graphicId;
            e.displayName = "GRH " + e.graphicId + " (" + (!String.IsNullOrEmpty(e.imageFile) ? e.imageFile : "sprite") + ")";
        }

        static Sprite CreateSprite(AOTerrainHDEntry e, Texture2D tex, AOGrhDefV029 d)
        {
            EnsureFolder(SpriteRoot);
            string path = SpriteRoot + "/GRH_" + e.graphicId + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;
            int unityY = tex.height - d.sy - d.height;
            if (d.sx < 0 || unityY < 0 || d.sx + d.width > tex.width || unityY + d.height > tex.height) return null;
            var sprite = Sprite.Create(tex, new Rect(d.sx, unityY, d.width, d.height), new Vector2(.5f, .5f), 32f, 0, SpriteMeshType.FullRect);
            if (sprite == null) return null;
            sprite.name = "GRH_" + e.graphicId;
            try
            {
                AssetDatabase.CreateAsset(sprite, path);
                AssetDatabase.SaveAssets();
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(sprite);
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
        }

        static Texture2D FindTexture(int fileNum)
        {
            Texture2D cached;
            if (textures.TryGetValue(fileNum, out cached)) return cached;
            var list = new List<Texture2D>();
            string[] guids = AssetDatabase.FindAssets("tex_" + fileNum + " t:Texture2D");
            foreach (var g in guids)
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(g));
                if (t != null) list.Add(t);
            }
            if (list.Count == 0)
            {
                guids = AssetDatabase.FindAssets(fileNum + " t:Texture2D");
                foreach (var g in guids)
                {
                    var t = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(g));
                    if (t != null) list.Add(t);
                }
            }
            var best = list.OrderByDescending(t => TextureScore(t, fileNum)).FirstOrDefault();
            textures[fileNum] = best;
            return best;
        }

        static int TextureScore(Texture2D t, int fileNum)
        {
            if (t == null) return Int32.MinValue;
            string path = AssetDatabase.GetAssetPath(t).Replace('\\', '/');
            string lower = path.ToLowerInvariant();
            string stem = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            int score = 0;
            if (stem == "tex_" + fileNum) score += 1000;
            if (stem == fileNum.ToString()) score += 900;
            if (stem == "tex_" + fileNum + "_padded") score += 850;
            if (lower.Contains("/mapvisualv063/textures/")) score += 500;
            if (lower.Contains("/generated/paddedtextures/")) score += 420;
            if (lower.Contains("/graficos/")) score += 350;
            if (lower.Contains("terrainhdpipelinedata") || lower.Contains("importedhd")) score -= 1000;
            return score;
        }

        static int ParseFileNum(string path)
        {
            if (String.IsNullOrEmpty(path)) return 0;
            string stem = Path.GetFileNameWithoutExtension(path);
            if (stem.EndsWith("_padded", StringComparison.OrdinalIgnoreCase)) stem = stem.Substring(0, stem.Length - 7);
            if (stem.StartsWith("tex_", StringComparison.OrdinalIgnoreCase)) stem = stem.Substring(4);
            int v; return Int32.TryParse(stem, out v) ? v : 0;
        }

        static bool ValidCrop(AOTerrainHDEntry e)
        {
            return e != null && e.cropX >= 0 && e.cropY >= 0 && e.cropWidth > 0 && e.cropHeight > 0;
        }

        static void EnsureDefs()
        {
            if (defs != null) return;
            defs = new Dictionary<int, AOGrhDefV029>();
            string absolute = AbsolutePath(IniPath);
            if (!File.Exists(absolute))
            {
                Debug.LogError("[AO Terrain HD v0.2.9] Falta " + IniPath);
                return;
            }
            foreach (string raw in File.ReadLines(absolute))
            {
                string line = raw.Trim();
                if (!line.StartsWith("Grh", StringComparison.OrdinalIgnoreCase)) continue;
                int eq = line.IndexOf('=');
                if (eq <= 3) continue;
                int id; if (!Int32.TryParse(line.Substring(3, eq - 3), out id)) continue;
                string[] f = line.Substring(eq + 1).Split('-');
                if (f.Length < 2) continue;
                int frames; if (!Int32.TryParse(f[0], out frames) || frames <= 0) continue;
                var d = new AOGrhDefV029 { id = id, frames = frames, animated = frames > 1 };
                if (frames == 1 && f.Length >= 6)
                {
                    Int32.TryParse(f[1], out d.fileNum); Int32.TryParse(f[2], out d.sx); Int32.TryParse(f[3], out d.sy);
                    Int32.TryParse(f[4], out d.width); Int32.TryParse(f[5], out d.height);
                }
                else if (frames > 1 && f.Length >= frames + 1) Int32.TryParse(f[1], out d.firstFrame);
                defs[id] = d;
            }
            Debug.Log("[AO Terrain HD v0.2.9] graficos.ini indexado: " + defs.Count + " GRH.");
        }

        static string AbsolutePath(string assetPath)
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder)) return;
            string[] parts = folder.Split('/'); string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
