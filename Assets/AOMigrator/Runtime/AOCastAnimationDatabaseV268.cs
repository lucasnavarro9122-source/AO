using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class AOCastAnimationDatabaseV268
{
    [Serializable] class Database
    {
        public string version;
        public NPCEntry[] npcs;
        public BodyEntry[] bodies;
    }

    [Serializable] class NPCEntry
    {
        public int npcIndex;
        public string name;
        public int castBody;
    }

    [Serializable] class BodyEntry
    {
        public int bodyId;
        public float fps;
        public int headOffsetX;
        public int headOffsetY;
        public int bodyOffsetX;
        public int bodyOffsetY;
        public DirectionEntry[] directions;
    }

    [Serializable] class DirectionEntry
    {
        public int heading;
        public string[] frames;
    }

    static bool loaded;
    static Database data;
    static readonly Dictionary<int, NPCEntry> npcById = new Dictionary<int, NPCEntry>();
    static readonly Dictionary<int, BodyEntry> bodyById = new Dictionary<int, BodyEntry>();
    static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

    static string Root => Path.Combine(Application.streamingAssetsPath, "AOMigrator", "CastV268");

    public static int CastBodyForNpc(int npcIndex)
    {
        Ensure();
        return npcById.TryGetValue(npcIndex, out NPCEntry entry) ? entry.castBody : 0;
    }

    public static bool TryBuildNpc(
        int npcIndex,
        out AOCharacterRenderer.DirectionVisual[] visuals,
        out float fps,
        out float headX,
        out float headY,
        out float bodyX)
    {
        visuals = null;
        fps = 10f;
        headX = headY = bodyX = 0f;
        Ensure();

        if (!npcById.TryGetValue(npcIndex, out NPCEntry npc) ||
            !bodyById.TryGetValue(npc.castBody, out BodyEntry body))
            return false;

        visuals = new AOCharacterRenderer.DirectionVisual[4];
        for (int heading = 1; heading <= 4; heading++)
        {
            DirectionEntry dir = Find(body.directions, heading);
            visuals[heading - 1] = new AOCharacterRenderer.DirectionVisual
            {
                heading = heading,
                body = LoadFrames(dir == null ? null : dir.frames),
                head = new Sprite[0],
                helmet = new Sprite[0],
                weapon = new Sprite[0],
                shield = new Sprite[0]
            };
        }

        fps = Mathf.Max(1f, body.fps);
        bodyX = body.bodyOffsetX / 32f;
        headX = body.headOffsetX / 32f;
        headY = -body.headOffsetY / 32f;
        return true;
    }

    static DirectionEntry Find(DirectionEntry[] dirs, int heading)
    {
        if (dirs == null) return null;
        foreach (DirectionEntry d in dirs)
            if (d != null && d.heading == heading) return d;
        return null;
    }

    static Sprite[] LoadFrames(string[] paths)
    {
        if (paths == null || paths.Length == 0) return new Sprite[0];
        List<Sprite> result = new List<Sprite>(paths.Length);
        foreach (string relative in paths)
        {
            Sprite s = LoadSprite(relative);
            if (s != null) result.Add(s);
        }
        return result.ToArray();
    }

    static Sprite LoadSprite(string relative)
    {
        if (string.IsNullOrEmpty(relative)) return null;
        if (spriteCache.TryGetValue(relative, out Sprite cached)) return cached;
        try
        {
            string full = Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full)) return null;
            byte[] bytes = File.ReadAllBytes(full);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            if (!texture.LoadImage(bytes, false)) return null;
            texture.name = Path.GetFileNameWithoutExtension(relative);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(.5f, 0f),
                32f,
                0,
                SpriteMeshType.FullRect);
            sprite.name = texture.name;
            spriteCache[relative] = sprite;
            return sprite;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[AO Cast v0.26.8] No pude cargar " + relative + ": " + ex.Message);
            return null;
        }
    }

    static void Ensure()
    {
        if (loaded) return;
        loaded = true;
        npcById.Clear();
        bodyById.Clear();
        try
        {
            string path = Path.Combine(Root, "cast_animations.json");
            if (!File.Exists(path))
            {
                Debug.LogWarning("[AO Cast v0.26.8] Falta " + path);
                return;
            }
            data = JsonUtility.FromJson<Database>(File.ReadAllText(path));
            if (data == null) return;
            if (data.npcs != null)
                foreach (NPCEntry e in data.npcs)
                    if (e != null && e.npcIndex > 0 && e.castBody > 0)
                        npcById[e.npcIndex] = e;
            if (data.bodies != null)
                foreach (BodyEntry b in data.bodies)
                    if (b != null && b.bodyId > 0)
                        bodyById[b.bodyId] = b;
            Debug.Log("[AO Cast v0.26.8] NPC CastAnimation: " + npcById.Count + " | bodies: " + bodyById.Count);
        }
        catch (Exception ex)
        {
            Debug.LogError("[AO Cast v0.26.8] Error cargando base: " + ex.Message);
        }
    }
}
