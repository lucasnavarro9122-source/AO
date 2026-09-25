using UnityEditor;
using UnityEngine;

// AOHDTextureImportV279: import settings of the HD remaster atlases (Higgsfield -> Resources/AOMigratorHD).
// Same look as the originals (Point, no mipmaps) but uncompressed and up to 4096 (1024 atlases at 4x).
// AOWorldManagerV07 loads them first when UseHDTextures is on and keeps the world size (rect and pixelsPerUnit x4).
public class AOHDTextureImportV279 : AssetPostprocessor
{
    static bool IsHD(string path) => path.Replace("\\", "/").Contains("/Resources/AOMigratorHD/");

    void OnPreprocessTexture()
    {
        if (!IsHD(assetPath)) return;
        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 4096;
    }

    // Logged so the size can be checked in Editor.log (a downscaled atlas would break the 4x rects).
    void OnPostprocessTexture(Texture2D texture)
    {
        if (IsHD(assetPath))
            Debug.Log("[AO HD] " + System.IO.Path.GetFileNameWithoutExtension(assetPath) + " " + texture.width + "x" + texture.height);
    }
}
