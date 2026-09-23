using System;
using System.Collections.Generic;
using UnityEngine;

public static class AODeathVisualV160
{
    const string DataPath =
        "AOMigrator/DeathV160/death_visual";

    const string TextureRoot =
        "AOMigrator/DeathV160/Textures/tex_";

    [Serializable]
    class FrameSpec
    {
        public int fileNum;
        public int sx;
        public int sy;
        public int width;
        public int height;
        public string key;
    }

    [Serializable]
    class DirectionSpec
    {
        public int heading;
        public FrameSpec[] frames;
    }

    [Serializable]
    class Data
    {
        public string version;
        public int bodyId;
        public int headOffsetX;
        public int headOffsetY;
        public int bodyShiftX;
        public float walkFps;
        public DirectionSpec[] directions;
    }

    static Data data;

    static readonly Dictionary<int, Texture2D>
        textures =
            new Dictionary<int, Texture2D>();

    static readonly Dictionary<string, Sprite>
        sprites =
            new Dictionary<string, Sprite>();

    public static int BodyId
    {
        get
        {
            Ensure();
            return data == null
                ? 829
                : data.bodyId;
        }
    }

    public static bool TryBuild(
        out AOCharacterRenderer.DirectionVisual[] visuals,
        out float walkFps,
        out float headX,
        out float headY,
        out float bodyX)
    {
        Ensure();

        visuals = null;
        walkFps = 7.5f;
        headX = 0f;
        headY = 0f;
        bodyX = 0f;

        if (data == null ||
            data.directions == null ||
            data.directions.Length == 0)
            return false;

        visuals =
            new AOCharacterRenderer.DirectionVisual[
                data.directions.Length];

        for (int i = 0;
             i < data.directions.Length;
             i++)
        {
            DirectionSpec source =
                data.directions[i];

            Sprite[] body =
                source.frames == null
                ? new Sprite[0]
                : new Sprite[
                    source.frames.Length];

            for (int f = 0;
                 f < body.Length;
                 f++)
            {
                body[f] =
                    SpriteFor(
                        source.frames[f]);
            }

            visuals[i] =
                new AOCharacterRenderer.DirectionVisual {
                    heading =
                        source.heading,
                    body =
                        body,
                    head =
                        new Sprite[0],
                    helmet =
                        new Sprite[0],
                    weapon =
                        new Sprite[0],
                    shield =
                        new Sprite[0]
                };
        }

        walkFps =
            Mathf.Max(
                1f,
                data.walkFps);

        headX =
            data.headOffsetX /
            32f;

        headY =
            -data.headOffsetY /
            32f;

        bodyX =
            data.bodyShiftX /
            32f;

        return true;
    }

    public static bool TryApplyToRenderer(
        AOCharacterRenderer visual)
    {
        if (visual == null)
            return false;

        if (!TryBuild(
                out AOCharacterRenderer.DirectionVisual[]
                    dirs,
                out float walkFps,
                out float headX,
                out float headY,
                out float bodyX))
        {
            return false;
        }

        int heading =
            visual.Heading;

        bool walking =
            visual.Walking;

        visual.Configure(
            dirs,
            walkFps,
            headX,
            headY,
            bodyX);

        visual.ConfigureEquipment(
            null,
            false,
            0f,
            0f,
            0f);

        visual.SetHeading(
            heading);

        visual.SetWalking(
            walking);

        visual.ForceRefreshVisuals();

        return true;
    }

    static Sprite SpriteFor(
        FrameSpec frame)
    {
        if (frame == null)
            return null;

        string key =
            string.IsNullOrEmpty(
                frame.key)
            ? "f" +
              frame.fileNum +
              "_" +
              frame.sx +
              "_" +
              frame.sy +
              "_" +
              frame.width +
              "_" +
              frame.height
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
            frame.sx +
                frame.width >
                texture.width ||
            unityY +
                frame.height >
                texture.height)
        {
            Debug.LogWarning(
                "[AO v0.16] Recorte inválido BODY829: " +
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

        sprite.name =
            "Death_" +
            key;

        sprites[key] =
            sprite;

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

        if (texture != null)
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
                "[AO v0.16] Falta death_visual.json.");
            return;
        }

        data =
            JsonUtility.FromJson<Data>(
                asset.text);
    }
}
