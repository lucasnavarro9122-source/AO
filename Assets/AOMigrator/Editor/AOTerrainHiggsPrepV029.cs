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
    public sealed class AOHiggsPrepItemV029
    {
        public int graphicId;
        public string key;
        public string category;
        public float semanticConfidence;
        public string semanticReason;
        public string visualCategory;
        public float visualConfidence;
        public int usageCount;
        public int mapCount;
        public int impactScore;
        public int remasterPriority;
        public int originalFileNum;
        public string sourceTexturePath;
        public int cropX;
        public int cropY;
        public int cropWidth;
        public int cropHeight;
        public string referencePng;
    }

    [Serializable]
    public sealed class AOHiggsPrepManifestV029
    {
        public string pipelineVersion = "0.2.9";
        public string generatedUtc;
        public int maxPerCategory;
        public int totalCandidates;
        public AOHiggsPrepItemV029[] items;
    }

    public static class AOTerrainHiggsPrepV029
    {
        public const string Version = "0.2.9";
        const string Root = "Assets/AOMigrator/TerrainHDPipelineData/HiggsPrep_v029";

        static readonly AOTerrainHDCategory[] Categories =
        {
            AOTerrainHDCategory.Grass,
            AOTerrainHDCategory.Dirt,
            AOTerrainHDCategory.Road,
            AOTerrainHDCategory.Floor,
            AOTerrainHDCategory.Water,
            AOTerrainHDCategory.Transition
        };

        public static void Export(AOTerrainHDRegistry registry, int maxPerCategory)
        {
            if (registry == null || registry.entries == null)
                return;

            maxPerCategory = Mathf.Clamp(maxPerCategory, 1, 64);
            AOTerrainSemanticClassifierV029.RefineRegistry(registry, true);

            EnsureFolder(Root);
            CleanGeneratedFiles();

            List<AOHiggsPrepItemV029> manifestItems = new List<AOHiggsPrepItemV029>();

            foreach (AOTerrainHDCategory category in Categories)
            {
                string categoryFolder = Root + "/" + category;
                EnsureFolder(categoryFolder);

                List<AOTerrainHDEntry> selected = registry.entries
                    .Where(e => e != null &&
                                e.remasterEligible &&
                                e.suggestedCategory == category)
                    .OrderByDescending(e => e.remasterPriority)
                    .ThenByDescending(e => e.ImpactScore)
                    .Take(maxPerCategory)
                    .ToList();

                foreach (AOTerrainHDEntry entry in selected)
                {
                    Texture2D crop = ReadSprite(entry.classicSprite);
                    if (crop == null) continue;

                    string assetPath = categoryFolder + "/GRH_" + entry.graphicId + "_reference.png";
                    File.WriteAllBytes(AbsolutePath(assetPath), crop.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(crop);
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

                    manifestItems.Add(new AOHiggsPrepItemV029
                    {
                        graphicId = entry.graphicId,
                        key = entry.key,
                        category = category.ToString(),
                        semanticConfidence = entry.classificationConfidence,
                        semanticReason = entry.classificationReason,
                        visualCategory = entry.visualCategory.ToString(),
                        visualConfidence = entry.visualConfidence,
                        usageCount = entry.usageCount,
                        mapCount = entry.sceneCount,
                        impactScore = entry.ImpactScore,
                        remasterPriority = entry.remasterPriority,
                        originalFileNum = entry.originalFileNum,
                        sourceTexturePath = entry.sourceTexturePath,
                        cropX = entry.cropX,
                        cropY = entry.cropY,
                        cropWidth = entry.cropWidth,
                        cropHeight = entry.cropHeight,
                        referencePng = assetPath
                    });
                }
            }

            WriteManifest(manifestItems, maxPerCategory);
            WriteCsv(manifestItems);
            WritePromptGuide();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "AO Terrain HD v0.2.9",
                "Higgsfield Prep generado.\n\n" +
                "Candidatos exportados: " + manifestItems.Count +
                "\nCarpeta:\n" + Root +
                "\n\nEl siguiente paso puede hacerse directamente con estas referencias.",
                "Aceptar");
        }

        static Texture2D ReadSprite(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return null;
            Texture2D source = sprite.texture;
            Rect rect;
            try { rect = sprite.textureRect; } catch { rect = sprite.rect; }

            int x = Mathf.Clamp(Mathf.RoundToInt(rect.x), 0, Mathf.Max(0, source.width - 1));
            int y = Mathf.Clamp(Mathf.RoundToInt(rect.y), 0, Mathf.Max(0, source.height - 1));
            int w = Mathf.Min(Mathf.Max(1, Mathf.RoundToInt(rect.width)), source.width - x);
            int h = Mathf.Min(Mathf.Max(1, Mathf.RoundToInt(rect.height)), source.height - y);
            if (w <= 0 || h <= 0) return null;

            if (source.isReadable)
            {
                Texture2D crop = new Texture2D(w, h, TextureFormat.RGBA32, false);
                crop.SetPixels(source.GetPixels(x, y, w, h));
                crop.Apply(false, false);
                return crop;
            }

            RenderTexture rt = null;
            RenderTexture previous = RenderTexture.active;
            Texture2D full = null;
            try
            {
                rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                full = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                full.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                full.Apply(false, false);

                Texture2D crop = new Texture2D(w, h, TextureFormat.RGBA32, false);
                crop.SetPixels(full.GetPixels(x, y, w, h));
                crop.Apply(false, false);
                return crop;
            }
            catch
            {
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                if (full != null) UnityEngine.Object.DestroyImmediate(full);
            }
        }

        static void WriteManifest(List<AOHiggsPrepItemV029> items, int maxPerCategory)
        {
            AOHiggsPrepManifestV029 manifest = new AOHiggsPrepManifestV029
            {
                generatedUtc = DateTime.UtcNow.ToString("o"),
                maxPerCategory = maxPerCategory,
                totalCandidates = items.Count,
                items = items.ToArray()
            };

            File.WriteAllText(
                AbsolutePath(Root + "/higgs_prep_manifest_v029.json"),
                JsonUtility.ToJson(manifest, true),
                new UTF8Encoding(false));
        }

        static void WriteCsv(List<AOHiggsPrepItemV029> items)
        {
            StringBuilder csv = new StringBuilder();
            csv.AppendLine("graphicId,category,semanticConfidence,visualCategory,visualConfidence,usage,maps,impact,priority,originalFileNum,sourceTexturePath,cropX,cropY,cropWidth,cropHeight,referencePng");
            foreach (AOHiggsPrepItemV029 i in items)
            {
                csv.Append(i.graphicId).Append(',')
                   .Append(Csv(i.category)).Append(',')
                   .Append(i.semanticConfidence.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                   .Append(Csv(i.visualCategory)).Append(',')
                   .Append(i.visualConfidence.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                   .Append(i.usageCount).Append(',')
                   .Append(i.mapCount).Append(',')
                   .Append(i.impactScore).Append(',')
                   .Append(i.remasterPriority).Append(',')
                   .Append(i.originalFileNum).Append(',')
                   .Append(Csv(i.sourceTexturePath)).Append(',')
                   .Append(i.cropX).Append(',')
                   .Append(i.cropY).Append(',')
                   .Append(i.cropWidth).Append(',')
                   .Append(i.cropHeight).Append(',')
                   .Append(Csv(i.referencePng)).Append('\n');
            }
            File.WriteAllText(AbsolutePath(Root + "/higgs_prep_v029.csv"), csv.ToString(), new UTF8Encoding(false));
        }

        static void WritePromptGuide()
        {
            string guide =
                "AO Terrain HD v0.2.9 - Higgsfield Prep\n\n" +
                "Objetivo: remasterizar cada tile manteniendo exactamente la forma, encuadre, función y bordes compatibles con sus vecinos.\n\n" +
                "REGLAS GENERALES\n" +
                "- No cambiar la geometría funcional del tile.\n" +
                "- No agregar objetos nuevos.\n" +
                "- Mantener el patrón tileable y continuidad de bordes.\n" +
                "- Mantener la paleta y lectura del original, pero con más detalle y limpieza.\n" +
                "- No introducir texto, UI, personajes ni iluminación direccional fuerte.\n" +
                "- Para transiciones, conservar exactamente la máscara/contorno original.\n\n" +
                "PROMPT BASE\n" +
                "Enhance this top-down fantasy RPG terrain tile while preserving its exact layout, edge continuity, material identity and gameplay readability. Keep it seamless/tileable, faithful to the original colors and silhouette, with cleaner high-detail texture, subtle natural variation and no new objects.\n\n" +
                "Categorías exportadas: Grass, Dirt, Road, Floor, Water, Transition.\n";
            File.WriteAllText(AbsolutePath(Root + "/PROMPT_GUIDE.txt"), guide, new UTF8Encoding(false));
        }

        static string Csv(string value)
        {
            if (value == null) value = "";
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        static void CleanGeneratedFiles()
        {
            string absolute = AbsolutePath(Root);
            if (!Directory.Exists(absolute)) return;
            foreach (string file in Directory.GetFiles(absolute, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                try { File.Delete(file); } catch { }
            }
        }

        static string AbsolutePath(string assetPath)
        {
            return Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder)) return;
            string[] parts = folder.Split('/');
            string current = "Assets";
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
