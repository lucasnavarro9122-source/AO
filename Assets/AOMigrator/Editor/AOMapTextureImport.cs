using UnityEditor;
using UnityEngine;

public sealed class AOMapTextureImport : AssetPostprocessor
{
    const string TextureRoot =
        "Assets/Resources/AOMigrator/WorldV07/Textures/";

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(
                TextureRoot,
                System.StringComparison.Ordinal))
            return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.textureShape = TextureImporterShape.Texture2D;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 16384;
    }
}
