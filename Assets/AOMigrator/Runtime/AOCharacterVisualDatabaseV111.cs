using System;
using System.Collections.Generic;
using UnityEngine;

public static class AOCharacterVisualDatabaseV111
{
    const string DataPath =
        "AOMigrator/CharacterV111/character_visuals";
    const string TextureRoot =
        "AOMigrator/CharacterV111/Textures/tex_";

    [Serializable]
    public class FrameSpec
    {
        public int fileNum;
        public int sx;
        public int sy;
        public int width;
        public int height;
        public string key;
    }

    [Serializable]
    public class DirectionFrames
    {
        public int heading;
        public FrameSpec[] frames;
    }

    [Serializable]
    public class BodyDef
    {
        public int id;
        public int headOffsetX;
        public int headOffsetY;
        public int bodyShiftX;
        public DirectionFrames[] directions;
    }

    [Serializable]
    public class HeadDef
    {
        public int id;
        public DirectionFrames[] directions;
    }

    [Serializable]
    public class ProfileDef
    {
        public int raceId;
        public int genderId;
        public int bodyId;
        public int[] validHeads;
        public int defaultHead;
    }

    [Serializable]
    class Database
    {
        public string version;
        public ProfileDef[] profiles;
        public BodyDef[] bodies;
        public HeadDef[] heads;
    }

    static Database data;
    static readonly Dictionary<int, BodyDef> bodies =
        new Dictionary<int, BodyDef>();
    static readonly Dictionary<int, HeadDef> heads =
        new Dictionary<int, HeadDef>();
    static readonly Dictionary<int, ProfileDef> profiles =
        new Dictionary<int, ProfileDef>();
    static readonly Dictionary<int, Texture2D> textures =
        new Dictionary<int, Texture2D>();
    static readonly Dictionary<string, Sprite> sprites =
        new Dictionary<string, Sprite>();

    static int ProfileKey(
        int race,
        int gender)
    {
        return gender * 100 + race;
    }

    public static ProfileDef GetProfile(
        int race,
        int gender)
    {
        Ensure();

        return profiles.TryGetValue(
            ProfileKey(race, gender),
            out ProfileDef profile)
            ? profile
            : null;
    }

    public static int[] ValidHeads(
        int race,
        int gender)
    {
        ProfileDef p =
            GetProfile(
                race,
                gender);

        return p == null ||
            p.validHeads == null
            ? new int[0]
            : p.validHeads;
    }

    public static bool IsValidHead(
        int race,
        int gender,
        int head)
    {
        int[] values =
            ValidHeads(
                race,
                gender);

        foreach (int value in values)
        {
            if (value == head)
                return true;
        }

        return false;
    }

    public static int DefaultHead(
        int race,
        int gender)
    {
        ProfileDef p =
            GetProfile(
                race,
                gender);

        return p == null
            ? 0
            : p.defaultHead;
    }

    public static bool TryBuildBase(
        int race,
        int gender,
        int headId,
        out AOCharacterRenderer.DirectionVisual[] visuals,
        out float headX,
        out float headY,
        out float bodyX)
    {
        visuals = null;
        headX = 0f;
        headY = 0f;
        bodyX = 0f;

        ProfileDef profile =
            GetProfile(
                race,
                gender);

        if (profile == null)
            return false;

        if (!IsValidHead(
                race,
                gender,
                headId))
        {
            headId =
                profile.defaultHead;
        }

        if (!bodies.TryGetValue(
                profile.bodyId,
                out BodyDef body))
            return false;

        heads.TryGetValue(
            headId,
            out HeadDef head);

        visuals =
            BuildDirections(
                body,
                head);

        headX =
            body.headOffsetX / 32f;

        headY =
            -body.headOffsetY / 32f;

        bodyX =
            body.bodyShiftX / 32f;

        return true;
    }

    public static bool TryBuildArmorBody(
        int bodyId,
        out AOCharacterRenderer.DirectionVisual[] visuals,
        out float headX,
        out float headY,
        out float bodyX)
    {
        visuals = null;
        headX = 0f;
        headY = 0f;
        bodyX = 0f;

        Ensure();

        if (bodyId <= 0 ||
            !bodies.TryGetValue(
                bodyId,
                out BodyDef body))
            return false;

        visuals =
            BuildDirections(
                body,
                null);

        headX =
            body.headOffsetX / 32f;

        headY =
            -body.headOffsetY / 32f;

        bodyX =
            body.bodyShiftX / 32f;

        return true;
    }

    static AOCharacterRenderer.DirectionVisual[]
        BuildDirections(
            BodyDef body,
            HeadDef head)
    {
        AOCharacterRenderer.DirectionVisual[] result =
            new AOCharacterRenderer.DirectionVisual[4];

        for (int h = 1; h <= 4; h++)
        {
            result[h - 1] =
                new AOCharacterRenderer.DirectionVisual {
                    heading = h,
                    body =
                        Frames(
                            Find(
                                body == null
                                ? null
                                : body.directions,
                                h)),
                    head =
                        Frames(
                            Find(
                                head == null
                                ? null
                                : head.directions,
                                h)),
                    helmet = new Sprite[0],
                    weapon = new Sprite[0],
                    shield = new Sprite[0]
                };
        }

        return result;
    }

    static FrameSpec[] Find(
        DirectionFrames[] dirs,
        int heading)
    {
        if (dirs == null)
            return new FrameSpec[0];

        foreach (DirectionFrames d in dirs)
        {
            if (d != null &&
                d.heading == heading)
            {
                return d.frames ??
                    new FrameSpec[0];
            }
        }

        return new FrameSpec[0];
    }

    static Sprite[] Frames(
        FrameSpec[] frames)
    {
        if (frames == null ||
            frames.Length == 0)
            return new Sprite[0];

        Sprite[] result =
            new Sprite[frames.Length];

        for (int i = 0;
             i < frames.Length;
             i++)
        {
            result[i] =
                SpriteFor(
                    frames[i]);
        }

        return result;
    }

    static Sprite SpriteFor(
        FrameSpec frame)
    {
        if (frame == null)
            return null;

        string key =
            string.IsNullOrEmpty(
                frame.key)
            ? "f" + frame.fileNum +
              "_" + frame.sx +
              "_" + frame.sy +
              "_" + frame.width +
              "_" + frame.height
            : frame.key;

        if (sprites.TryGetValue(
                key,
                out Sprite cached))
            return cached;

        Texture2D texture =
            Texture(
                frame.fileNum);

        if (texture == null)
            return null;

        int unityY =
            texture.height -
            frame.sy -
            frame.height;

        if (frame.sx < 0 ||
            unityY < 0 ||
            frame.width <= 0 ||
            frame.height <= 0 ||
            frame.sx + frame.width >
                texture.width ||
            unityY + frame.height >
                texture.height)
        {
            Debug.LogWarning(
                "[AO v0.11.1] Recorte inválido " +
                key);

            return null;
        }

        Sprite sprite =
            Sprite.Create(
                texture,
                new Rect(
                    frame.sx,
                    unityY,
                    frame.width,
                    frame.height),
                new Vector2(
                    0.5f,
                    0f),
                32f,
                0,
                SpriteMeshType.FullRect);

        sprite.name = key;
        sprites[key] = sprite;

        return sprite;
    }

    static Texture2D Texture(
        int fileNum)
    {
        if (textures.TryGetValue(
                fileNum,
                out Texture2D cached))
            return cached;

        Texture2D texture =
            Resources.Load<Texture2D>(
                TextureRoot +
                fileNum);

        if (texture == null)
        {
            Debug.LogWarning(
                "[AO v0.11.1] Falta tex_" +
                fileNum);

            return null;
        }

        textures[fileNum] =
            texture;

        return texture;
    }

    static void Ensure()
    {
        if (data != null)
            return;

        TextAsset asset =
            Resources.Load<TextAsset>(
                DataPath);

        if (asset == null)
        {
            Debug.LogError(
                "[AO v0.11.1] Falta " +
                DataPath + ".json");

            return;
        }

        data =
            JsonUtility.FromJson<Database>(
                asset.text);

        bodies.Clear();
        heads.Clear();
        profiles.Clear();

        if (data.bodies != null)
        {
            foreach (BodyDef body in data.bodies)
            {
                if (body != null &&
                    body.id > 0)
                    bodies[body.id] =
                        body;
            }
        }

        if (data.heads != null)
        {
            foreach (HeadDef head in data.heads)
            {
                if (head != null &&
                    head.id > 0)
                    heads[head.id] =
                        head;
            }
        }

        if (data.profiles != null)
        {
            foreach (ProfileDef profile in data.profiles)
            {
                if (profile != null)
                {
                    profiles[
                        ProfileKey(
                            profile.raceId,
                            profile.genderId)] =
                        profile;
                }
            }
        }

        Debug.Log(
            "[AO v0.11.1] Visual DB: " +
            bodies.Count +
            " bodies, " +
            heads.Count +
            " heads.");
    }
}
