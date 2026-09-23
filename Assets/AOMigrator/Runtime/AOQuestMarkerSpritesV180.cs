using System.Collections.Generic;
using UnityEngine;

public static class AOQuestMarkerSpritesV180
{
    static readonly Dictionary<int, Sprite>
        sprites =
            new Dictionary<int, Sprite>();

    public static Sprite Get(
        int state)
    {
        if (state < 1 ||
            state > 4)
            return null;

        if (sprites.TryGetValue(
                state,
                out Sprite cached))
            return cached;

        Texture2D texture =
            Resources.Load<Texture2D>(
                "AOMigrator/QuestMarkersV180/symbol_" +
                state);

        if (texture == null)
            return null;

        texture.filterMode =
            FilterMode.Point;

        Sprite sprite =
            Sprite.Create(
                texture,
                new Rect(
                    0,
                    0,
                    texture.width,
                    texture.height),
                new Vector2(
                    0.5f,
                    0.5f),
                32f,
                0,
                SpriteMeshType.FullRect);

        sprite.name =
            "AOQuestSymbol_" +
            state;

        sprites[state] =
            sprite;

        return sprite;
    }
}
