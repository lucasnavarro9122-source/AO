// AO Terrain HD v0.2.9
#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using AOMigrator.TerrainHD;

namespace AOMigrator.TerrainHD.Editor
{
    internal struct AOSemanticSpriteStatsV029
    {
        public float coverage;
        public float avgSaturation;
        public float avgValue;
    }

    internal sealed class AOCategoryEvidenceV029
    {
        public AOTerrainHDCategory category = AOTerrainHDCategory.Unclassified;
        public float support;
    }

    public static class AOTerrainSemanticClassifierV029
    {
        public const string Version = "0.2.9";

        public static void RefineRegistry(AOTerrainHDRegistry registry, bool applyCategories = true)
        {
            if (registry == null || registry.entries == null) return;

            Dictionary<string, AOTerrainHDEntry> byKey = registry.entries
                .Where(e => e != null && !String.IsNullOrEmpty(e.key))
                .GroupBy(e => e.key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (AOTerrainHDEntry entry in registry.entries)
            {
                if (entry == null) continue;

                bool currentIsSemantic =
                    !String.IsNullOrEmpty(entry.classificationReason) &&
                    entry.classificationReason.StartsWith(
                        "Semántica:",
                        StringComparison.OrdinalIgnoreCase);

                if (!currentIsSemantic || String.IsNullOrEmpty(entry.visualReason))
                {
                    entry.visualCategory = entry.suggestedCategory;
                    entry.visualConfidence = entry.classificationConfidence;
                    entry.visualReason = entry.classificationReason;
                }
            }

            Dictionary<string, AOCategoryEvidenceV029> familyEvidence = BuildFamilyEvidence(registry.entries);

            foreach (AOTerrainHDEntry entry in registry.entries)
            {
                if (entry == null) continue;

                AOCategoryEvidenceV029 family = GetFamilyEvidence(entry, familyEvidence);
                AOCategoryEvidenceV029 neighbors = GetNeighborEvidence(entry, byKey);
                AOSemanticSpriteStatsV029 stats = Measure(entry.classicSprite);

                entry.familySupport = family.support;
                entry.neighborSupport = neighbors.support;

                AOTerrainHDCategory finalCategory;
                float confidence;
                string reason;

                ClassifySemantic(entry, stats, family, neighbors, out finalCategory, out confidence, out reason);

                entry.suggestedCategory = finalCategory;
                entry.classificationConfidence = confidence;
                entry.classificationReason = reason;

                EvaluateRemaster(entry, stats);

                if (!entry.categoryWasManuallySet && applyCategories)
                {
                    entry.category = finalCategory != AOTerrainHDCategory.Unclassified && confidence >= 0.58f
                        ? finalCategory
                        : AOTerrainHDCategory.Unclassified;
                }
            }

            registry.pipelineVersion = Version;
            registry.RebuildLookup();
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();

            Debug.Log("[AO Terrain HD v0.2.9] Semantic classifier: " +
                      registry.entries.Count(e => e != null && e.remasterEligible) +
                      " candidatos aptos para remaster.");
        }

        static void ClassifySemantic(
            AOTerrainHDEntry entry,
            AOSemanticSpriteStatsV029 stats,
            AOCategoryEvidenceV029 family,
            AOCategoryEvidenceV029 neighbors,
            out AOTerrainHDCategory category,
            out float confidence,
            out string reason)
        {
            AOTerrainHDCategory visual = entry.visualCategory;
            float visualConfidence = entry.visualConfidence;

            int layerTotal = Mathf.Max(1, entry.layer1Uses + entry.layer2Uses + entry.layer3Uses + entry.layer4Uses);
            float layer1 = entry.layer1Uses / (float)layerTotal;

            if (stats.coverage <= 0.01f ||
                (!String.IsNullOrEmpty(entry.visualReason) &&
                 entry.visualReason.IndexOf("vacío", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                category = AOTerrainHDCategory.Other;
                confidence = 0.98f;
                reason = "Semántica: sprite vacío/transparente técnico; excluir de remaster.";
                return;
            }

            bool nearBlackNeutral = stats.avgValue <= 0.10f && stats.avgSaturation <= 0.12f && stats.coverage >= 0.90f;
            if (nearBlackNeutral)
            {
                category = AOTerrainHDCategory.Other;
                confidence = 0.94f;
                reason = "Semántica: superficie casi negra/neutra; probable fondo/máscara técnica. Se excluye de Road y del remaster.";
                return;
            }

            if (visual == AOTerrainHDCategory.Transition && visualConfidence >= 0.60f)
            {
                category = AOTerrainHDCategory.Transition;
                confidence = Mathf.Clamp(visualConfidence, 0.72f, 0.96f);
                reason = "Semántica: transición confirmada por Layer2/cobertura.";
                return;
            }

            if ((visual == AOTerrainHDCategory.Grass || visual == AOTerrainHDCategory.Water) && visualConfidence >= 0.72f)
            {
                category = visual;
                float support = Mathf.Max(
                    family.category == visual ? family.support : 0f,
                    neighbors.category == visual ? neighbors.support : 0f);
                confidence = Mathf.Clamp(0.78f + visualConfidence * 0.12f + support * 0.08f, 0.78f, 0.97f);
                reason = "Semántica: " + visual + " confirmada por señal visual" + (support >= 0.45f ? " + contexto." : ".");
                return;
            }

            if (visual == AOTerrainHDCategory.Dirt || visual == AOTerrainHDCategory.Floor)
            {
                bool floorContext =
                    (family.category == AOTerrainHDCategory.Floor && family.support >= 0.55f) ||
                    (neighbors.category == AOTerrainHDCategory.Floor && neighbors.support >= 0.58f) ||
                    (entry.sceneCount <= 3 && layer1 >= 0.55f && visual == AOTerrainHDCategory.Floor);

                bool dirtContext =
                    (family.category == AOTerrainHDCategory.Dirt && family.support >= 0.55f) ||
                    (neighbors.category == AOTerrainHDCategory.Dirt && neighbors.support >= 0.58f) ||
                    (entry.sceneCount >= 4 && visual == AOTerrainHDCategory.Dirt);

                if (floorContext && !dirtContext)
                {
                    category = AOTerrainHDCategory.Floor;
                    confidence = Mathf.Clamp(0.66f + Mathf.Max(family.support, neighbors.support) * 0.26f, 0.66f, 0.94f);
                    reason = "Semántica: piso por familia/vecinos + uso concentrado en pocos mapas.";
                    return;
                }

                category = dirtContext ? AOTerrainHDCategory.Dirt : visual;
                confidence = Mathf.Clamp(0.68f + visualConfidence * 0.14f + Mathf.Max(family.support, neighbors.support) * 0.12f, 0.66f, 0.95f);
                reason = "Semántica: " + category + " por color marrón + contexto de familia/mapas.";
                return;
            }

            if (visual == AOTerrainHDCategory.Road)
            {
                float roadSupport = Mathf.Max(
                    family.category == AOTerrainHDCategory.Road ? family.support : 0f,
                    neighbors.category == AOTerrainHDCategory.Road ? neighbors.support : 0f);

                if (roadSupport >= 0.48f && entry.sceneCount >= 2 && stats.avgValue > 0.10f)
                {
                    category = AOTerrainHDCategory.Road;
                    confidence = Mathf.Clamp(0.62f + roadSupport * 0.28f, 0.62f, 0.90f);
                    reason = "Semántica: Road confirmado por contexto; gris por sí solo no decide.";
                    return;
                }

                if (family.category == AOTerrainHDCategory.Floor && family.support >= 0.60f)
                {
                    category = AOTerrainHDCategory.Floor;
                    confidence = Mathf.Clamp(0.64f + family.support * 0.24f, 0.64f, 0.90f);
                    reason = "Semántica: visualmente neutro, pero familia consistente con Floor.";
                    return;
                }

                category = AOTerrainHDCategory.Other;
                confidence = 0.64f;
                reason = "Semántica: tile neutro sin evidencia suficiente para llamarlo Road.";
                return;
            }

            AOCategoryEvidenceV029 strongest = family.support >= neighbors.support ? family : neighbors;
            if (strongest.support >= 0.68f && IsTerrainCategory(strongest.category))
            {
                category = strongest.category;
                confidence = Mathf.Clamp(0.60f + strongest.support * 0.30f, 0.60f, 0.88f);
                reason = "Semántica: categoría inferida por contexto fuerte de familia/vecinos.";
                return;
            }

            category = visual == AOTerrainHDCategory.Unclassified ? AOTerrainHDCategory.Other : visual;
            confidence = visual == AOTerrainHDCategory.Unclassified ? 0.50f : Mathf.Clamp(visualConfidence, 0.45f, 0.72f);
            reason = "Semántica: evidencia contextual insuficiente; mantener como " + category + ".";
        }

        static Dictionary<string, AOCategoryEvidenceV029> BuildFamilyEvidence(IEnumerable<AOTerrainHDEntry> entries)
        {
            Dictionary<string, AOCategoryEvidenceV029> result =
                new Dictionary<string, AOCategoryEvidenceV029>(StringComparer.OrdinalIgnoreCase);

            foreach (IGrouping<string, AOTerrainHDEntry> group in entries
                .Where(e => e != null)
                .GroupBy(FamilyKey, StringComparer.OrdinalIgnoreCase))
            {
                Dictionary<AOTerrainHDCategory, float> weights = new Dictionary<AOTerrainHDCategory, float>();
                float total = 0f;

                foreach (AOTerrainHDEntry e in group)
                {
                    AOTerrainHDCategory c = e.visualCategory;
                    if (!IsTerrainCategory(c) || e.visualConfidence < 0.45f) continue;
                    float weight = Mathf.Sqrt(Mathf.Max(1, e.usageCount)) * Mathf.Clamp01(e.visualConfidence);
                    if (!weights.ContainsKey(c)) weights[c] = 0f;
                    weights[c] += weight;
                    total += weight;
                }

                AOCategoryEvidenceV029 ev = new AOCategoryEvidenceV029();
                if (total > 0f && weights.Count > 0)
                {
                    KeyValuePair<AOTerrainHDCategory, float> top = weights.OrderByDescending(p => p.Value).First();
                    ev.category = top.Key;
                    ev.support = top.Value / total;
                }
                result[group.Key] = ev;
            }
            return result;
        }

        static AOCategoryEvidenceV029 GetFamilyEvidence(
            AOTerrainHDEntry entry,
            Dictionary<string, AOCategoryEvidenceV029> familyEvidence)
        {
            AOCategoryEvidenceV029 ev;
            return familyEvidence.TryGetValue(FamilyKey(entry), out ev)
                ? ev
                : new AOCategoryEvidenceV029();
        }

        static AOCategoryEvidenceV029 GetNeighborEvidence(
            AOTerrainHDEntry entry,
            Dictionary<string, AOTerrainHDEntry> byKey)
        {
            string[] keys = { entry.northKey, entry.eastKey, entry.southKey, entry.westKey };
            Dictionary<AOTerrainHDCategory, float> weights = new Dictionary<AOTerrainHDCategory, float>();
            float total = 0f;

            foreach (string key in keys.Where(k => !String.IsNullOrEmpty(k)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                AOTerrainHDEntry neighbor;
                if (!byKey.TryGetValue(key, out neighbor) || neighbor == null) continue;
                AOTerrainHDCategory c = neighbor.visualCategory;
                if (!IsTerrainCategory(c) || neighbor.visualConfidence < 0.45f) continue;
                float w = Mathf.Max(0.15f, neighbor.visualConfidence);
                if (!weights.ContainsKey(c)) weights[c] = 0f;
                weights[c] += w;
                total += w;
            }

            AOCategoryEvidenceV029 result = new AOCategoryEvidenceV029();
            if (total > 0f && weights.Count > 0)
            {
                KeyValuePair<AOTerrainHDCategory, float> top = weights.OrderByDescending(p => p.Value).First();
                result.category = top.Key;
                result.support = top.Value / total;
            }
            return result;
        }

        static string FamilyKey(AOTerrainHDEntry entry)
        {
            if (entry == null) return "<null>";
            if (entry.originalFileNum > 0) return "FILE:" + entry.originalFileNum;
            if (!String.IsNullOrEmpty(entry.sourceTexturePath)) return "PATH:" + entry.sourceTexturePath;
            return "GRH:" + entry.graphicId;
        }

        static bool IsTerrainCategory(AOTerrainHDCategory category)
        {
            return category == AOTerrainHDCategory.Grass ||
                   category == AOTerrainHDCategory.Dirt ||
                   category == AOTerrainHDCategory.Road ||
                   category == AOTerrainHDCategory.Floor ||
                   category == AOTerrainHDCategory.Water ||
                   category == AOTerrainHDCategory.Transition;
        }

        static void EvaluateRemaster(AOTerrainHDEntry entry, AOSemanticSpriteStatsV029 stats)
        {
            bool technical =
                stats.coverage <= 0.01f ||
                entry.suggestedCategory == AOTerrainHDCategory.Other ||
                entry.suggestedCategory == AOTerrainHDCategory.Unclassified;

            bool validSource =
                entry.classicSprite != null &&
                entry.cropWidth > 0 && entry.cropHeight > 0 &&
                !String.IsNullOrEmpty(entry.sourceTexturePath);

            entry.remasterEligible =
                !technical &&
                validSource &&
                entry.classificationConfidence >= 0.70f &&
                IsTerrainCategory(entry.suggestedCategory);

            entry.remasterPriority = entry.remasterEligible
                ? Mathf.RoundToInt(entry.ImpactScore * entry.classificationConfidence)
                : 0;

            entry.remasterReason = entry.remasterEligible
                ? "Apto: terreno semántico >=70%, fuente/crop válidos."
                : technical
                    ? "Excluir: técnico/Other/Unclassified."
                    : !validSource
                        ? "Excluir: fuente o crop inválido."
                        : "Revisión: confianza semántica menor a 70%.";
        }

        static AOSemanticSpriteStatsV029 Measure(Sprite sprite)
        {
            AOSemanticSpriteStatsV029 result = new AOSemanticSpriteStatsV029();
            if (sprite == null || sprite.texture == null) return result;

            Texture2D source = sprite.texture;
            Rect rect;
            try { rect = sprite.textureRect; }
            catch { rect = sprite.rect; }

            int x = Mathf.Clamp(Mathf.RoundToInt(rect.x), 0, Mathf.Max(0, source.width - 1));
            int y = Mathf.Clamp(Mathf.RoundToInt(rect.y), 0, Mathf.Max(0, source.height - 1));
            int w = Mathf.Min(Mathf.Max(1, Mathf.RoundToInt(rect.width)), source.width - x);
            int h = Mathf.Min(Mathf.Max(1, Mathf.RoundToInt(rect.height)), source.height - y);
            if (w <= 0 || h <= 0) return result;

            Color32[] pixels = null;
            Texture2D temp = null;
            RenderTexture rt = null;
            RenderTexture previous = RenderTexture.active;

            try
            {
                if (source.isReadable)
                {
                    pixels = source.GetPixels32();
                }
                else
                {
                    rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
                    Graphics.Blit(source, rt);
                    RenderTexture.active = rt;
                    temp = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                    temp.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                    temp.Apply(false, false);
                    pixels = temp.GetPixels32();
                }

                int visible = 0;
                double sat = 0.0;
                double val = 0.0;

                for (int py = y; py < y + h; py++)
                for (int px = x; px < x + w; px++)
                {
                    Color32 raw = pixels[py * source.width + px];
                    if (raw.a <= 12) continue;
                    visible++;
                    Color c = raw;
                    Color.RGBToHSV(c, out float hue, out float saturation, out float value);
                    sat += saturation;
                    val += value;
                }

                result.coverage = visible / (float)(w * h);
                if (visible > 0)
                {
                    result.avgSaturation = (float)(sat / visible);
                    result.avgValue = (float)(val / visible);
                }
            }
            catch { }
            finally
            {
                RenderTexture.active = previous;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                if (temp != null) UnityEngine.Object.DestroyImmediate(temp);
            }

            return result;
        }
    }
}
#endif
