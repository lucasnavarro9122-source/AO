using System;
using UnityEditor;
using UnityEngine;

public static class AOMapMigrationSmoke
{
    public static void Run()
    {
        int checkedMaps = 0;
        int checkedFrames = 0;
        foreach (int number in new[] { 1, 2, 40 })
        {
            string mapPath = "AOMigrator/WorldV07/Maps/map_" + number;
            TextAsset asset = Resources.Load<TextAsset>(mapPath);
            if (asset == null)
                throw new Exception("Mapa no importado: " + mapPath);

            var data =
                JsonUtility.FromJson<AOWorldManagerV07.WorldMapData>(
                    asset.text);
            if (data == null || data.mapNumber != number ||
                data.cells == null || data.cells.Length == 0 ||
                data.sprites == null || data.sprites.Length == 0)
                throw new Exception("Mapa JSON inválido: " + number);

            foreach (var definition in data.sprites)
            {
                if (definition.frames != null &&
                    definition.frames.Length > 1)
                {
                    foreach (var frame in definition.frames)
                        CheckFrame(frame);
                    checkedFrames += definition.frames.Length;
                }
                else
                {
                    CheckFrame(new AOWorldManagerV07.FrameSpec {
                        fileNum = definition.fileNum,
                        sx = definition.sx,
                        sy = definition.sy,
                        width = definition.width,
                        height = definition.height
                    });
                    checkedFrames++;
                }
            }
            checkedMaps++;
        }
        Debug.Log("AO_MAP_SMOKE_OK maps=" + checkedMaps +
                  " frames=" + checkedFrames);
    }

    static void CheckFrame(AOWorldManagerV07.FrameSpec frame)
    {
        string texturePath =
            "AOMigrator/WorldV07/Textures/tex_" + frame.fileNum;
        Texture2D texture =
            Resources.Load<Texture2D>(texturePath);
        if (texture == null)
            throw new Exception("Textura no importada: " + texturePath);

        int unityY = texture.height - frame.sy - frame.height;
        if (frame.sx < 0 || unityY < 0 ||
            frame.width <= 0 || frame.height <= 0 ||
            frame.sx + frame.width > texture.width)
            throw new Exception("Recorte inválido: " + texturePath);

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(frame.sx, unityY,
                     frame.width, frame.height),
            new Vector2(0.5f, 0f), 32f);
        if (sprite == null)
            throw new Exception("No se pudo crear sprite: " + texturePath);
        UnityEngine.Object.DestroyImmediate(sprite);
    }
}
