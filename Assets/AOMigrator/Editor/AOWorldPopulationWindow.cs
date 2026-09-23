using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class AOWorldPopulationWindow : EditorWindow
{
    const string DataPath = "Assets/AOMigrator/WorldV06/Data/world_entities_mapa1.json";
    const string TextureRoot = "Assets/AOMigrator/WorldV06/Textures";
    const string GeneratedRoot = "Assets/AOMigrator/WorldV06/Generated";
    const string DiagnosticPath = "Assets/AOMigrator/world_v06_diagnostic.json";

    [Serializable] class FrameSpec
    {
        public int fileNum;
        public int sx;
        public int sy;
        public int width;
        public int height;
        public string key;
    }

    [Serializable] class DirectionSpec
    {
        public int heading;
        public FrameSpec[] body;
        public FrameSpec[] head;
        public FrameSpec[] helmet;
        public FrameSpec[] weapon;
        public FrameSpec[] shield;
    }

    [Serializable] class NPCEntry
    {
        public int npcIndex;
        public int x;
        public int y;
        public string name;
        public string description;
        public int npcType;
        public int movement;
        public bool showName;
        public int heading;
        public int body;
        public int head;
        public int helmet;
        public int weapon;
        public int shield;
        public float walkFps;
        public int headOffsetX;
        public int headOffsetY;
        public int bodyShiftX;
        public DirectionSpec[] directions;
    }

    [Serializable] class ObjectEntry
    {
        public int objIndex;
        public int x;
        public int y;
        public int amount;
        public string name;
        public string description;
        public int objType;
        public int grhIndex;
        public float fps;
        public FrameSpec[] frames;
    }

    [Serializable] class WorldData
    {
        public string version;
        public int mapNumber;
        public string mapName;
        public string zone;
        public string terrain;
        public NPCEntry[] npcs;
        public ObjectEntry[] objects;
    }

    [Serializable] class Diagnostic
    {
        public string version = "0.6.0-alpha";
        public string unity;
        public bool success;
        public string stage;
        public string message;
        public int npcs;
        public int objects;
        public int spriteAssets;
        public int textureAssets;
    }

    bool importNPCs = true;
    bool importObjects = true;
    bool showNPCNames = true;
    Vector2 scroll;
    string status = "Listo para poblar la escena jugable actual.";

    readonly Dictionary<int, Texture2D> textureCache = new Dictionary<int, Texture2D>();
    readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
    int createdSprites;

    [MenuItem("AO Migrador/Poblar mapa v0.6 - NPCs y objetos")]
    public static void OpenWindow()
    {
        GetWindow<AOWorldPopulationWindow>("AO World v0.6");
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        GUILayout.Label("AO Migrador v0.6 - NPCs + objetos", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Esta etapa usa los 27 NPC y 131 objetos registrados en mapa1.csm (Ullathorpe). " +
            "Los NPC conservan nombre, descripcion, indices de apariencia, direccion y tipo de movimiento. " +
            "El movimiento autonomo de NPC todavia no se simula: quedan en su posicion original para validar apariencia, sorting, ocupacion e interaccion.",
            MessageType.Info);

        importNPCs = EditorGUILayout.ToggleLeft("Importar NPCs del mapa", importNPCs);
        importObjects = EditorGUILayout.ToggleLeft("Importar objetos dinamicos del mapa", importObjects);
        showNPCNames = EditorGUILayout.ToggleLeft("Mostrar nombres de NPC", showNPCNames);

        GUILayout.Space(8);
        if (GUILayout.Button("Poblar escena actual", GUILayout.Height(38)))
            Populate();

        if (GUILayout.Button("Limpiar NPCs/objetos v0.6", GUILayout.Height(28)))
            Cleanup();

        GUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "Prueba en Play:\n" +
            "- Los NPC bloquean su casilla.\n" +
            "- Mira a un NPC/objeto adyacente y pulsa E para leer su nombre/descripcion.\n" +
            "- Los objetos animados usan la velocidad GRH aproximada.\n" +
            "- Las rutas Movement/Caminata se conservan como datos pero se implementaran despues.",
            MessageType.None);

        GUILayout.Space(8);
        EditorGUILayout.LabelField("Estado", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(status, MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    void Cleanup()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("AO v0.6", "Sali de Play antes de limpiar.", "OK");
            return;
        }

        DestroyNamed("AO NPCs v0.6");
        DestroyNamed("AO Objects v0.6");
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        status = "NPCs y objetos v0.6 eliminados de la escena.";
    }

    static void DestroyNamed(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go != null) Undo.DestroyObjectImmediate(go);
    }

    void Populate()
    {
        var diag = new Diagnostic();
        try
        {
            diag.stage = "inicio";
            if (EditorApplication.isPlaying)
                throw new Exception("Sali de Play antes de poblar la escena.");

            string visualReport;
            AOMapVisualRepairV063.EnsureVisuals(out visualReport);
            Debug.Log("AO v0.6.3 visual: " + visualReport);

            string repairReport;
            AOGridMap grid = AOSceneRepairV062.EnsureScene(out repairReport);
            Debug.Log("AO v0.6.2 repair: " + repairReport);

            if (!File.Exists(DataPath))
                throw new Exception("Falta " + DataPath);

            diag.stage = "leer_datos";
            WorldData data = JsonUtility.FromJson<WorldData>(File.ReadAllText(DataPath));
            if (data == null) throw new Exception("world_entities_mapa1.json invalido.");

            PrepareGeneratedFolder();
            textureCache.Clear();
            spriteCache.Clear();
            createdSprites = 0;

            DestroyNamed("AO NPCs v0.6");
            DestroyNamed("AO Objects v0.6");

            int npcCount = 0;
            int objectCount = 0;

            if (importNPCs)
            {
                diag.stage = "crear_npcs";
                GameObject root = new GameObject("AO NPCs v0.6");
                Undo.RegisterCreatedObjectUndo(root, "Importar NPCs AO");

                if (data.npcs != null)
                {
                    for (int i = 0; i < data.npcs.Length; i++)
                    {
                        EditorUtility.DisplayProgressBar(
                            "AO Migrador v0.6",
                            "Creando NPC " + (i + 1) + "/" + data.npcs.Length,
                            data.npcs.Length == 0 ? 1f : (float)i / data.npcs.Length);
                        CreateNPC(data.npcs[i], grid, root.transform);
                        npcCount++;
                    }
                }
            }

            if (importObjects)
            {
                diag.stage = "crear_objetos";
                GameObject root = new GameObject("AO Objects v0.6");
                Undo.RegisterCreatedObjectUndo(root, "Importar objetos AO");

                if (data.objects != null)
                {
                    for (int i = 0; i < data.objects.Length; i++)
                    {
                        EditorUtility.DisplayProgressBar(
                            "AO Migrador v0.6",
                            "Creando objeto " + (i + 1) + "/" + data.objects.Length,
                            data.objects.Length == 0 ? 1f : (float)i / data.objects.Length);
                        CreateObject(data.objects[i], grid, root.transform);
                        objectCount++;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            SceneView.RepaintAll();

            diag.success = true;
            diag.stage = "completado";
            diag.message = "Poblacion creada. Guarda la escena con Ctrl+S si el resultado te sirve.";
            diag.npcs = npcCount;
            diag.objects = objectCount;
            diag.spriteAssets = createdSprites;
            diag.textureAssets = textureCache.Count;
            WriteDiagnostic(diag);

            status = "OK: " + npcCount + " NPC + " + objectCount +
                " objetos. Sprites: " + createdSprites +
                ". Entra en Play y prueba colisiones de NPC + tecla E.";
        }
        catch (Exception e)
        {
            diag.success = false;
            diag.message = e.ToString();
            WriteDiagnostic(diag);
            status = "ERROR: " + e.Message;
            Debug.LogError("AO World v0.6: " + e);
            EditorUtility.DisplayDialog(
                "AO Migrador v0.6 - error",
                e.Message + "\n\nSubime Assets/AOMigrator/world_v06_diagnostic.json.",
                "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    void PrepareGeneratedFolder()
    {
        if (AssetDatabase.IsValidFolder(GeneratedRoot))
            AssetDatabase.DeleteAsset(GeneratedRoot);

        Directory.CreateDirectory(GeneratedRoot + "/Sprites");
        AssetDatabase.Refresh();
    }

    Texture2D LoadTexture(int fileNum)
    {
        if (textureCache.TryGetValue(fileNum, out var cached)) return cached;

        string asset = TextureRoot + "/tex_" + fileNum + ".png";
        if (!File.Exists(asset)) throw new Exception("Falta textura " + asset);

        AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(asset) as TextureImporter;
        if (importer == null) throw new Exception("No pude configurar " + asset);

        importer.textureType = TextureImporterType.Default;
        importer.textureShape = TextureImporterShape.Texture2D;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaIsTransparency = true;
        importer.isReadable = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 16384;
        importer.SaveAndReimport();

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(asset);
        if (texture == null) throw new Exception("No pude cargar " + asset);
        textureCache[fileNum] = texture;
        return texture;
    }

    Sprite GetSprite(FrameSpec frame)
    {
        if (frame == null) return null;
        if (spriteCache.TryGetValue(frame.key, out var cached)) return cached;

        Texture2D texture = LoadTexture(frame.fileNum);
        int unityY = texture.height - frame.sy - frame.height;
        if (frame.sx < 0 || unityY < 0 || frame.width <= 0 || frame.height <= 0 ||
            frame.sx + frame.width > texture.width ||
            unityY + frame.height > texture.height)
        {
            throw new Exception(
                "Recorte fuera de textura: " + frame.key +
                " / tex=" + texture.width + "x" + texture.height);
        }

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(frame.sx, unityY, frame.width, frame.height),
            new Vector2(0.5f, 0f),
            32f, 0, SpriteMeshType.FullRect);

        sprite.name = frame.key;
        string path = GeneratedRoot + "/Sprites/" + frame.key + ".asset";
        AssetDatabase.CreateAsset(sprite, path);
        spriteCache[frame.key] = sprite;
        createdSprites++;
        return sprite;
    }

    Sprite[] GetSprites(FrameSpec[] frames)
    {
        if (frames == null || frames.Length == 0) return new Sprite[0];
        Sprite[] result = new Sprite[frames.Length];
        for (int i = 0; i < frames.Length; i++) result[i] = GetSprite(frames[i]);
        return result;
    }

    void CreateNPC(NPCEntry npc, AOGridMap grid, Transform parent)
    {
        GameObject go = new GameObject(
            "NPC_" + npc.npcIndex + "_" + SafeHierarchyName(npc.name));
        go.transform.SetParent(parent, false);
        go.transform.position = grid.TileToWorld(npc.x, npc.y);

        AONPCMetadata meta = go.AddComponent<AONPCMetadata>();
        meta.ConfigureNPC(
            npc.npcIndex, npc.x, npc.y, npc.name, npc.description,
            npc.npcType, npc.movement, npc.heading,
            npc.body, npc.head, npc.helmet, npc.weapon, npc.shield);

        GameObject visualGo = new GameObject("Visual");
        visualGo.transform.SetParent(go.transform, false);
        AOCharacterRenderer visual = visualGo.AddComponent<AOCharacterRenderer>();

        var dirs = new AOCharacterRenderer.DirectionVisual[4];
        for (int h = 1; h <= 4; h++)
        {
            DirectionSpec source = FindDirection(npc.directions, h);
            dirs[h - 1] = new AOCharacterRenderer.DirectionVisual {
                heading = h,
                body = source == null ? new Sprite[0] : GetSprites(source.body),
                head = source == null ? new Sprite[0] : GetSprites(source.head),
                helmet = source == null ? new Sprite[0] : GetSprites(source.helmet),
                weapon = source == null ? new Sprite[0] : GetSprites(source.weapon),
                shield = source == null ? new Sprite[0] : GetSprites(source.shield)
            };
        }

        visual.Configure(
            dirs,
            npc.walkFps <= 0f ? 18f : npc.walkFps,
            npc.headOffsetX / 32f,
            -npc.headOffsetY / 32f,
            npc.bodyShiftX / 32f);

        visual.SetHeading(npc.heading);
        visual.SetWalking(false);
        int baseOrder = 10000 + npc.y;
        visual.UpdateSorting(baseOrder);

        if (showNPCNames && npc.showName)
            CreateNameLabel(go.transform, npc.name, baseOrder + 20);
    }

    static DirectionSpec FindDirection(DirectionSpec[] list, int heading)
    {
        if (list == null) return null;
        for (int i = 0; i < list.Length; i++)
            if (list[i] != null && list[i].heading == heading) return list[i];
        return null;
    }

    void CreateNameLabel(Transform parent, string text, int sortingOrder)
    {
        GameObject label = new GameObject("Name");
        label.transform.SetParent(parent, false);
        label.transform.localPosition = new Vector3(0f, 1.15f, 0f);

        TextMesh tm = label.AddComponent<TextMesh>();
        tm.text = text ?? "";
        tm.anchor = TextAnchor.LowerCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontSize = 48;
        tm.characterSize = 0.025f;
        tm.richText = false;

        MeshRenderer renderer = label.GetComponent<MeshRenderer>();
        if (renderer != null) renderer.sortingOrder = sortingOrder;
    }

    void CreateObject(ObjectEntry obj, AOGridMap grid, Transform parent)
    {
        GameObject go = new GameObject(
            "OBJ_" + obj.objIndex + "_" + SafeHierarchyName(obj.name));
        go.transform.SetParent(parent, false);
        go.transform.position = grid.TileToWorld(obj.x, obj.y);

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        Sprite[] frames = GetSprites(obj.frames);
        if (frames.Length > 0) renderer.sprite = frames[0];

        // Entre layer 3 y personajes. Es una primera aproximacion visual;
        // el sorting exacto por tipo de objeto se refinara en la siguiente etapa.
        renderer.sortingOrder = 9000 + obj.y;

        if (frames.Length > 1)
        {
            AOAnimatedSprite anim = go.AddComponent<AOAnimatedSprite>();
            anim.Configure(frames, obj.fps);
        }

        AOWorldObjectMetadata meta = go.AddComponent<AOWorldObjectMetadata>();
        meta.ConfigureObject(
            obj.objIndex, obj.x, obj.y, obj.name, obj.description,
            obj.objType, obj.amount, obj.grhIndex);
    }

    static string SafeHierarchyName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "SinNombre";
        return value.Replace("/", "-").Replace("\\", "-").Replace("\n", " ");
    }

    static void WriteDiagnostic(Diagnostic diag)
    {
        try
        {
            diag.unity = Application.unityVersion;
            File.WriteAllText(DiagnosticPath, JsonUtility.ToJson(diag, true));
            AssetDatabase.Refresh();
        }
        catch (Exception e)
        {
            Debug.LogError("AO v0.6 diagnostic: " + e);
        }
    }
}
