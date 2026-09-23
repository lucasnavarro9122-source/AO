// AO Terrain HD v0.2.9
// AO Terrain HD v0.2.9
// AO Terrain HD v0.2.9
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AOMigrator.TerrainHD
{
    public enum AOTerrainHDCategory
    {
        Unclassified = 0,
        Grass = 1,
        Dirt = 2,
        Road = 3,
        Floor = 4,
        Water = 5,
        Transition = 6,
        Other = 7
    }

    public enum AOTerrainHDVisualStyle
    {
        Classic = 0,
        HD = 1
    }

    [Serializable]
    public sealed class AOTerrainHDEntry
    {
        public string key;
        public string displayName;
        public AOTerrainHDCategory category;
        public bool categoryWasManuallySet;

        // v0.2.9 semantic classifier
        public AOTerrainHDCategory suggestedCategory;
        [Range(0f, 1f)] public float classificationConfidence;
        public string classificationReason;

        // v0.2.9: visual evidence kept separate from semantic result.
        public AOTerrainHDCategory visualCategory;
        [Range(0f, 1f)] public float visualConfidence;
        public string visualReason;
        [Range(0f, 1f)] public float familySupport;
        [Range(0f, 1f)] public float neighborSupport;
        public bool remasterEligible;
        public int remasterPriority;
        public string remasterReason;

        public Sprite classicSprite;
        public Sprite hdSprite;

        public string classicAssetPath;
        public string hdAssetPath;

        // Connected World / map JSON source
        public int graphicId;
        public string imageFile;
        public string sourceTexturePath;
        public int cropX;
        public int cropY;
        public int cropWidth;
        public int cropHeight;
        public int originalFileNum;
        public int visualFrameGrhId;
        public bool isAnimatedGrh;

        public int usageCount;
        public int sceneCount;
        public int layer1Uses;
        public int layer2Uses;
        public int layer3Uses;
        public int layer4Uses;

        public int sourceWidth;
        public int sourceHeight;
        public float sourcePixelsPerUnit = 32f;
        public Vector2 sourcePivotNormalized = new Vector2(0.5f, 0.5f);

        public string northKey;
        public string eastKey;
        public string southKey;
        public string westKey;

        public bool validated;
        [TextArea(2, 6)] public string validationMessage;

        public int ImpactScore
        {
            get
            {
                return usageCount + sceneCount * 25;
            }
        }
    }

    [CreateAssetMenu(
        fileName = "AOTerrainHDRegistry",
        menuName = "AO Migrador/Terrain HD Registry")]
    public sealed class AOTerrainHDRegistry : ScriptableObject
    {
        public string pipelineVersion = "0.2.9";
        public int scannedSceneCount;
        public string scannedUtc;
        public List<AOTerrainHDEntry> entries =
            new List<AOTerrainHDEntry>();

        Dictionary<Sprite, AOTerrainHDEntry> classicLookup;
        Dictionary<Sprite, AOTerrainHDEntry> hdLookup;
        Dictionary<string, AOTerrainHDEntry> keyLookup;

        public void RebuildLookup()
        {
            classicLookup =
                new Dictionary<Sprite, AOTerrainHDEntry>();
            hdLookup =
                new Dictionary<Sprite, AOTerrainHDEntry>();
            keyLookup =
                new Dictionary<string, AOTerrainHDEntry>(
                    StringComparer.OrdinalIgnoreCase);

            if (entries == null)
                return;

            foreach (AOTerrainHDEntry entry in entries)
            {
                if (entry == null)
                    continue;

                if (!string.IsNullOrEmpty(entry.key))
                    keyLookup[entry.key] = entry;

                if (entry.classicSprite != null)
                    classicLookup[entry.classicSprite] = entry;

                if (entry.hdSprite != null)
                    hdLookup[entry.hdSprite] = entry;
            }
        }

        public AOTerrainHDEntry FindByKey(string key)
        {
            if (keyLookup == null)
                RebuildLookup();

            AOTerrainHDEntry entry;
            return !string.IsNullOrEmpty(key) &&
                   keyLookup.TryGetValue(key, out entry)
                ? entry
                : null;
        }

        public Sprite Resolve(
            Sprite current,
            AOTerrainHDVisualStyle style)
        {
            if (current == null)
                return null;

            if (classicLookup == null || hdLookup == null)
                RebuildLookup();

            AOTerrainHDEntry entry;

            if (style == AOTerrainHDVisualStyle.HD)
            {
                if (classicLookup.TryGetValue(
                    current,
                    out entry))
                {
                    return entry.hdSprite != null
                        ? entry.hdSprite
                        : current;
                }

                return current;
            }

            if (hdLookup.TryGetValue(
                current,
                out entry))
            {
                return entry.classicSprite != null
                    ? entry.classicSprite
                    : current;
            }

            return current;
        }

        public int CountCategory(
            AOTerrainHDCategory category)
        {
            int count = 0;

            if (entries == null)
                return count;

            foreach (AOTerrainHDEntry entry in entries)
            {
                if (entry != null &&
                    entry.category == category)
                    count++;
            }

            return count;
        }

        public int CountHD(
            AOTerrainHDCategory category)
        {
            int count = 0;

            if (entries == null)
                return count;

            foreach (AOTerrainHDEntry entry in entries)
            {
                if (entry != null &&
                    entry.category == category &&
                    entry.hdSprite != null)
                    count++;
            }

            return count;
        }
    }
}
