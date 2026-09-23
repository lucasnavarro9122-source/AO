using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AOMapVisualRepairV063
{
    const string Root = "Assets/AOMigrator/MapVisualV063";
    const string DataPath = Root + "/Data/map.json";
    const string TextureRoot = Root + "/Textures";
    const string Generated = Root + "/Generated";
    const string DiagnosticPath =
        "Assets/AOMigrator/map_visual_v063_diagnostic.json";

    [Serializable] class Cell
    {
        public int x;
        public int y;
        public int layer;
        public int grh;
        public int sprite;
    }

    [Serializable] class SpriteDef
    {
        public int id;
        public int file;
        public string image;
        public int sx;
        public int sy;
        public int width;
        public int height;
    }

    [Serializable] class MapDocument
    {
        public string name;
        public int xmin;
        public int xmax;
        public int ymin;
        public int ymax;
        public Cell[] cells;
        public SpriteDef[] sprites;
    }

    [Serializable] class Diagnostic
    {
        public string version = "0.6.3-alpha";
        public string unity;
        public bool success;
        public string stage;
        public string message;
        public int cellsCreated;
        public int spritesCreated;
        public int texturesUsed;
        public int layer1;
        public int layer2;
        public int layer3;
        public int layer4;
    }

    [MenuItem("AO Migrador/Reparar visual del mapa v0.6.3")]
    public static void RepairMenu()
    {
        try
        {
            string report;
            RepairVisuals(out report);
            EditorUtility.DisplayDialog(
                "AO Migrador v0.6.3",
                "Visual del mapa reconstruido.\n\n" + report +
                "\n\nGuardá la escena con Ctrl+S si se ve bien.",
                "OK");
        }
        catch (Exception e)
        {
            WriteDiagnostic(
                false, "error", e.ToString(),
                0, 0, 0, 0, 0, 0, 0);
            Debug.LogError("AO Visual Repair v0.6.3: " + e);
            EditorUtility.DisplayDialog(
                "AO Migrador v0.6.3 - error",
                e.Message +
                "\n\nSubime Assets/AOMigrator/map_visual_v063_diagnostic.json.",
                "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    public static void EnsureVisuals(out string report)
    {
        GameObject root = FindSceneObjectByPrefix("AO_MAP_");
        if (root == null)
            throw new Exception(
                "No encontré AO_MAP_. Abrí la escena jugable de Ullathorpe.");

        int valid = CountValidMapRenderers(root.transform);
        // mapa1 tiene 11025 celdas gráficas. Si quedan casi todas,
        // no toca los visuales existentes.
        if (valid >= 10000)
        {
            report =
                "Visual existente conservado (" + valid +
                " SpriteRenderers válidos).";
            return;
        }

        RepairVisuals(out report);
    }

    public static void RepairVisuals(out string report)
    {
        if (EditorApplication.isPlaying)
            throw new Exception("Salí de Play antes de reparar el mapa.");

        Diagnostic diag = new Diagnostic();
        diag.unity = Application.unityVersion;
        diag.stage = "leer_datos";

        if (!File.Exists(DataPath))
            throw new Exception("Falta " + DataPath);

        MapDocument map = JsonUtility.FromJson<MapDocument>(
            File.ReadAllText(DataPath));

        if (map == null ||
            map.cells == null ||
            map.sprites == null ||
            map.cells.Length == 0)
        {
            throw new Exception("map.json no contiene un mapa válido.");
        }

        GameObject root = FindSceneObjectByPrefix("AO_MAP_");
        if (root == null)
            throw new Exception(
                "No encontré AO_MAP_. Abrí AO_Ciudad_de_Ullathorpe_Playable.");

        diag.stage = "preparar_generado";
        if (AssetDatabase.IsValidFolder(Generated))
            AssetDatabase.DeleteAsset(Generated);

        Directory.CreateDirectory(Generated + "/Sprites");
        Directory.CreateDirectory(Generated + "/PaddedTextures");
        AssetDatabase.Refresh();

        Dictionary<string, Texture2D> textures =
            new Dictionary<string, Texture2D>();
        Dictionary<int, Sprite> spriteById =
            new Dictionary<int, Sprite>();
        Dictionary<string, Vector2Int> requirements =
            new Dictionary<string, Vector2Int>();

        foreach (SpriteDef def in map.sprites)
        {
            int reqW = def.sx + def.width;
            int reqH = def.sy + def.height;

            if (requirements.TryGetValue(def.image, out Vector2Int old))
            {
                requirements[def.image] = new Vector2Int(
                    Math.Max(old.x, reqW),
                    Math.Max(old.y, reqH));
            }
            else
            {
                requirements[def.image] =
                    new Vector2Int(reqW, reqH);
            }
        }

        diag.stage = "crear_sprites";
        for (int i = 0; i < map.sprites.Length; i++)
        {
            if (i % 20 == 0)
            {
                EditorUtility.DisplayProgressBar(
                    "AO Migrador v0.6.3",
                    "Reconstruyendo sprites " +
                    (i + 1) + "/" + map.sprites.Length,
                    (float)i / Math.Max(1, map.sprites.Length));
            }

            SpriteDef def = map.sprites[i];
            Vector2Int req = requirements[def.image];
            Texture2D texture =
                PrepareTexture(def.image, req.x, req.y, textures);

            int unityY = texture.height - def.sy - def.height;

            if (def.sx < 0 ||
                unityY < 0 ||
                def.width <= 0 ||
                def.height <= 0 ||
                def.sx + def.width > texture.width ||
                unityY + def.height > texture.height)
            {
                throw new Exception(
                    "Recorte inválido GRH " + def.id +
                    " en " + def.image);
            }

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(
                    def.sx, unityY,
                    def.width, def.height),
                new Vector2(0.5f, 0f),
                32f,
                0,
                SpriteMeshType.FullRect);

            sprite.name = "GRH_" + def.id;

            string spritePath =
                Generated + "/Sprites/GRH_" +
                def.id + ".asset";

            AssetDatabase.CreateAsset(sprite, spritePath);
            spriteById[def.id] = sprite;
            diag.spritesCreated++;
        }

        AssetDatabase.SaveAssets();

        diag.stage = "reconstruir_layers";
        Transform[] layers = new Transform[5];

        for (int layer = 1; layer <= 4; layer++)
        {
            string layerName = "Layer_" + layer;
            Transform layerRoot = root.transform.Find(layerName);

            if (layerRoot == null)
            {
                GameObject go = new GameObject(layerName);
                go.transform.SetParent(root.transform, false);
                layerRoot = go.transform;
            }

            ClearChildren(layerRoot);
            layerRoot.gameObject.SetActive(true);
            layers[layer] = layerRoot;
        }

        int[] layerCounts = new int[5];

        for (int i = 0; i < map.cells.Length; i++)
        {
            if (i % 100 == 0)
            {
                EditorUtility.DisplayProgressBar(
                    "AO Migrador v0.6.3",
                    "Reconstruyendo mapa " +
                    (i + 1) + "/" + map.cells.Length,
                    (float)i / Math.Max(1, map.cells.Length));
            }

            Cell cell = map.cells[i];
            if (cell.layer < 1 || cell.layer > 4)
                throw new Exception(
                    "Layer inválido: " + cell.layer);

            if (!spriteById.TryGetValue(
                    cell.sprite, out Sprite sprite))
            {
                throw new Exception(
                    "No existe sprite " + cell.sprite +
                    " para GRH " + cell.grh);
            }

            GameObject go = new GameObject(
                "L" + cell.layer +
                "_" + cell.x +
                "_" + cell.y +
                "_GRH" + cell.grh,
                typeof(SpriteRenderer));

            go.transform.SetParent(
                layers[cell.layer], false);

            go.transform.localPosition =
                new Vector3(
                    cell.x - 0.5f,
                    -cell.y,
                    0f);

            go.isStatic = true;

            SpriteRenderer sr =
                go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.enabled = true;
            sr.color = Color.white;

            if (cell.layer == 1)
                sr.sortingOrder = -30000;
            else if (cell.layer == 2)
                sr.sortingOrder = -20000 + cell.y;
            else if (cell.layer == 3)
                sr.sortingOrder = 1000 + cell.y;
            else
                sr.sortingOrder = 20000 + cell.y;

            layerCounts[cell.layer]++;
            diag.cellsCreated++;
        }

        diag.layer1 = layerCounts[1];
        diag.layer2 = layerCounts[2];
        diag.layer3 = layerCounts[3];
        diag.layer4 = layerCounts[4];
        diag.texturesUsed = textures.Count;

        // Repara también el componente lógico sin destruir
        // personaje, NPCs, objetos ni cámara.
        diag.stage = "reparar_grid";
        string gridReport;
        AOSceneRepairV062.EnsureScene(out gridReport);

        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager.GetActiveScene());

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        SceneView.RepaintAll();

        diag.success = true;
        diag.stage = "completado";
        diag.message =
            "Visual reconstruido. " + gridReport;

        WriteDiagnostic(
            true,
            diag.stage,
            diag.message,
            diag.cellsCreated,
            diag.spritesCreated,
            diag.texturesUsed,
            diag.layer1,
            diag.layer2,
            diag.layer3,
            diag.layer4);

        report =
            "Celdas: " + diag.cellsCreated +
            "\nSprites: " + diag.spritesCreated +
            "\nTexturas: " + diag.texturesUsed +
            "\nLayer 1: " + diag.layer1 +
            "\nLayer 2: " + diag.layer2 +
            "\nLayer 3: " + diag.layer3 +
            "\nLayer 4: " + diag.layer4;
    }

    static Texture2D PrepareTexture(
        string filename,
        int requiredWidth,
        int requiredHeight,
        Dictionary<string, Texture2D> cache)
    {
        if (cache.TryGetValue(
                filename, out Texture2D cached))
            return cached;

        string path =
            TextureRoot + "/" + filename;

        if (!File.Exists(path))
            throw new Exception(
                "Falta textura base: " + path);

        AssetDatabase.ImportAsset(
            path,
            ImportAssetOptions.ForceSynchronousImport);

        TextureImporter importer =
            AssetImporter.GetAtPath(path) as TextureImporter;

        if (importer == null)
            throw new Exception(
                "No pude configurar " + path);

        importer.textureType =
            TextureImporterType.Default;
        importer.textureShape =
            TextureImporterShape.Texture2D;
        importer.mipmapEnabled = false;
        importer.textureCompression =
            TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaIsTransparency = true;
        importer.isReadable = true;
        importer.npotScale =
            TextureImporterNPOTScale.None;
        importer.maxTextureSize = 16384;
        importer.SaveAndReimport();

        Texture2D original =
            AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        if (original == null)
            throw new Exception(
                "No pude cargar " + path);

        if (requiredWidth <= original.width &&
            requiredHeight <= original.height)
        {
            cache[filename] = original;
            return original;
        }

        int paddedWidth =
            Math.Max(original.width, requiredWidth);
        int paddedHeight =
            Math.Max(original.height, requiredHeight);

        Texture2D padded =
            new Texture2D(
                paddedWidth,
                paddedHeight,
                TextureFormat.RGBA32,
                false);

        padded.name =
            Path.GetFileNameWithoutExtension(filename) +
            "_padded";

        Color32[] pixels =
            new Color32[paddedWidth * paddedHeight];

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] =
                new Color32(0, 0, 0, 0);

        Color32[] src = original.GetPixels32();
        int yOffset =
            paddedHeight - original.height;

        for (int y = 0; y < original.height; y++)
        {
            Array.Copy(
                src,
                y * original.width,
                pixels,
                (y + yOffset) * paddedWidth,
                original.width);
        }

        padded.SetPixels32(pixels);
        padded.Apply(false, false);
        padded.filterMode = FilterMode.Point;
        padded.wrapMode = TextureWrapMode.Clamp;

        string paddedPath =
            Generated +
            "/PaddedTextures/" +
            Path.GetFileNameWithoutExtension(filename) +
            "_padded.asset";

        AssetDatabase.CreateAsset(
            padded, paddedPath);

        cache[filename] = padded;
        return padded;
    }

    static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1;
             i >= 0; i--)
        {
            UnityEngine.Object.DestroyImmediate(
                parent.GetChild(i).gameObject);
        }
    }

    static int CountValidMapRenderers(Transform root)
    {
        int result = 0;

        for (int layer = 1; layer <= 4; layer++)
        {
            Transform layerRoot =
                root.Find("Layer_" + layer);

            if (layerRoot == null)
                continue;

            SpriteRenderer[] renderers =
                layerRoot.GetComponentsInChildren<SpriteRenderer>(
                    true);

            foreach (SpriteRenderer sr in renderers)
            {
                if (sr != null &&
                    sr.sprite != null &&
                    sr.enabled &&
                    sr.gameObject.activeInHierarchy)
                {
                    result++;
                }
            }
        }

        return result;
    }

    static GameObject FindSceneObjectByPrefix(
        string prefix)
    {
        foreach (
            GameObject go in
            Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (!go.scene.IsValid())
                continue;

            if (go.name.StartsWith(
                    prefix,
                    StringComparison.Ordinal))
                return go;
        }

        return null;
    }

    static void WriteDiagnostic(
        bool success,
        string stage,
        string message,
        int cells,
        int sprites,
        int textures,
        int l1,
        int l2,
        int l3,
        int l4)
    {
        try
        {
            Diagnostic diag = new Diagnostic {
                unity = Application.unityVersion,
                success = success,
                stage = stage,
                message = message,
                cellsCreated = cells,
                spritesCreated = sprites,
                texturesUsed = textures,
                layer1 = l1,
                layer2 = l2,
                layer3 = l3,
                layer4 = l4
            };

            File.WriteAllText(
                DiagnosticPath,
                JsonUtility.ToJson(diag, true));

            AssetDatabase.Refresh();
        }
        catch (Exception e)
        {
            Debug.LogError(
                "AO v0.6.3 diagnostic: " + e);
        }
    }
}
