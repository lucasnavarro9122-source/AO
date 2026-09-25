using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public partial class AOWorldManagerV07 : MonoBehaviour
{
    const string MapResourceRoot =
        "AOMigrator/WorldV07/Maps/map_";
    const string TextureResourceRoot =
        "AOMigrator/WorldV07/Textures/tex_";

    // Remaster HD (Tools/hd_remake): el mismo atlas a 4x en un árbol espejo.
    // Si existe, se usa con el mismo tamaño en el mundo (rect y pixelsPerUnit x4).
    const string HDTextureResourceRoot =
        "AOMigratorHD/WorldV07/Textures/tex_";
    const int HDScale = 4;
    public static bool UseHDTextures = true;

    // Raised after SetHDTextures rebuilt the map (synchronous: the change is done when the call returns).
    public static event Action HDTexturesApplied;

    // Remaster HD on/off without reloading the map (Interfaz: Ajustes > Video; QA). Same tiles and world size;
    // the grid, the player, the NPCs and the save are not touched. Map cells change at once; map objects keep
    // their sprite until the next map load. An HD sprite has pixelsPerUnit 128 (32 x 4).
    public static void SetHDTextures(bool on)
    {
        UseHDTextures = on;
        AOWorldManagerV07 world = UnityEngine.Object.FindFirstObjectByType<AOWorldManagerV07>();
        if (world != null)
            world.RebuildMapGraphics();
        HDTexturesApplied?.Invoke();
    }

    // How many atlases of the loaded map come from AOMigratorHD.
    public int HDTextureCount
    {
        get
        {
            int n = 0;
            foreach (KeyValuePair<int, int> kv in textureScale)
                if (kv.Value > 1) n++;
            return n;
        }
    }

    void RebuildMapGraphics()
    {
        textureCache.Clear();
        textureScale.Clear();
        spriteCache.Clear();
        if (currentMap == null || loading || mapRoot == null)
            return;
        for (int layer = 1; layer <= 4; layer++)
        {
            foreach (SpriteRenderer renderer in mapRendererPool[layer])
                if (renderer != null)
                    renderer.gameObject.SetActive(false);
            mapRendererUse[layer] = 0;
        }
        ResetRoofGroups(currentMap);
        ResetTreeVisuals();
        BuildVisuals(currentMap);
        UpdateRoofVisibility();
    }

    [Serializable] public class FrameSpec
    {
        public int fileNum;
        public int sx;
        public int sy;
        public int width;
        public int height;
        public string key;
    }

    [Serializable] public class DirectionSpec
    {
        public int heading;
        public FrameSpec[] body;
        public FrameSpec[] head;
        public FrameSpec[] helmet;
        public FrameSpec[] weapon;
        public FrameSpec[] shield;
    }

    [Serializable] public class WalkPoint
    {
        public int offsetX;
        public int offsetY;
        public int waitMs;
    }

    [Serializable] public class DropEntry
    {
        public int itemIndex;
        public string name;
        public int chanceDenominator;
        public int minAmount;
        public int maxAmount;
        public string source;
    }

    [Serializable] public class NPCEntry
    {
        public int npcIndex;
        public int x;
        public int y;
        public string name;
        public string description;
        public int npcType;
        public int movement;
        public int sourceMovement;
        public bool hostile;
        public int attackRange;
        public int preferredRange;
        public int visionRange;
        public int visionRangeX;
        public int visionRangeY;
        public int moveIntervalMs;
        public bool waterValid;
        public bool landInvalid;
        public bool lavaValid;
        public WalkPoint[] walkRoute;
        public int maxHp;
        public int minHit;
        public int maxHit;
        public int defense;
        public int attackPower;
        public int evasionPower;
        public bool attackable;
        public int attackIntervalMs;
        public int respawnMinSeconds;
        public int respawnMaxSeconds;
        public int giveExp;
        public int giveGold;
        public DropEntry[] drops;
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

    [Serializable] public class ObjectEntry
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

    [Serializable] public class Cell
    {
        public int x;
        public int y;
        public int layer;
        public int grh;
        public int sprite;
    }

    [Serializable] public class SpriteDef
    {
        public int id;
        public int fileNum;
        public int sx;
        public int sy;
        public int width;
        public int height;
        public string key;
        public FrameSpec[] frames;
        public float fps;
    }

    [Serializable] public class Block
    {
        public int x;
        public int y;
        public int flags;
    }

    [Serializable] public class Trigger
    {
        public int x;
        public int y;
        public int trigger;
    }

    [Serializable] public class Exit
    {
        public int x;
        public int y;
        public int destMap;
        public int destX;
        public int destY;
    }

    [Serializable] public class WorldMapData
    {
        public string version;
        public int mapNumber;
        public string mapName;
        public string zone;
        public string terrain;
        public string ambient;
        public bool safe;
        public int xmin;
        public int xmax;
        public int ymin;
        public int ymax;
        public Cell[] cells;
        public SpriteDef[] sprites;
        public Block[] blocks;
        public Trigger[] triggers;
        public Exit[] exits;
        public NPCEntry[] npcs;
        public ObjectEntry[] objects;
        public AOMapParticlePlacement[] particles;
        public AOMapLightPlacement[] lights;
        public bool dropOnDeath;   // demo dungeon floors (demo_map_builder): original death drop
    }

    [SerializeField] AOTestPlayer player;
    [SerializeField] AOCameraFollow followCamera;
    [SerializeField] int initialMap = 1;
    [SerializeField] int initialX = 68;
    [SerializeField] int initialY = 43;
    [SerializeField] bool loadOnStart = true;
    [SerializeField] bool showHud = true;
    [SerializeField] bool showNPCNames = true;

    AOGridMap grid;
    GameObject mapRoot;
    GameObject npcRoot;
    GameObject objectRoot;
    GameObject particleRoot;
    GameObject weatherRoot;
    AOMapWeather weather;
    GameObject runtimeDebugOverlay;

    WorldMapData currentMap;
    AOMapParticleLibrary particleLibrary;
    AOMapEnvironmentLibrary environmentLibrary;
    AOMapEnvironment currentEnvironment;
    AOMapWeather.Precipitation requestedPrecipitation;
    byte requestedFogAlpha;
    bool protocolFogEnabled;
    Color32[] mapLighting;
    int currentBaseLight;
    float worldHour = 13f;
    int worldDayLengthMs;
    int worldElapsedAtSyncMs;
    double worldSyncRealtime;
    double nextWorldClockUpdate;
    static Material mapVertexMaterial;
    MaterialPropertyBlock mapLightProperties;
    static readonly int AOColor0 = Shader.PropertyToID("_AOColor0");
    static readonly int AOColor1 = Shader.PropertyToID("_AOColor1");
    static readonly int AOColor2 = Shader.PropertyToID("_AOColor2");
    static readonly int AOColor3 = Shader.PropertyToID("_AOColor3");
    static readonly int AOBounds = Shader.PropertyToID("_AOBounds");
    int mapLightingXMin;
    int mapLightingYMin;
    int mapLightingWidth;
    int currentMapNumber;
    bool loading;
    bool suppressExit;
    int suppressX;
    int suppressY;

    string status = "Preparado.";
    float statusUntil;

    readonly Dictionary<int, Texture2D> textureCache =
        new Dictionary<int, Texture2D>();
    readonly Dictionary<int, int> textureScale =
        new Dictionary<int, int>();
    readonly Dictionary<string, Sprite> spriteCache =
        new Dictionary<string, Sprite>();
    readonly Dictionary<long, Exit> exitByTile =
        new Dictionary<long, Exit>();
    readonly Dictionary<int, bool> mapAvailability =
        new Dictionary<int, bool>();
    readonly List<SpriteRenderer>[] mapRendererPool = {
        null,
        new List<SpriteRenderer>(), new List<SpriteRenderer>(),
        new List<SpriteRenderer>(), new List<SpriteRenderer>()
    };
    readonly List<AOAnimatedSprite>[] mapAnimationPool = {
        null,
        new List<AOAnimatedSprite>(), new List<AOAnimatedSprite>(),
        new List<AOAnimatedSprite>(), new List<AOAnimatedSprite>()
    };
    readonly int[] mapRendererUse = new int[5];
    bool mapRendererPoolReady;

    static readonly HashSet<int> TreeGrh =
        new HashSet<int> {
            643,644,647,735,1121,2931,11903,11904,11905,
            14775,11906,70885,70884,71042,71041,15698,
            14504,14505,15697,15510,12581,12582,12583,
            12584,12585,12586,12164,12165,12166,12167,
            12168,12169,12170,12171,12172,12173,12174,
            12175,12176,12177,12178,12179,32142,32143,
            32144,32145,32146,32147,32148,32149,32150,
            32151,32152,32154,55626,55627,55628,55629,
            55630,55631,55632,55633,55634,55635,55636,
            55637,55638,55639,55640,55642,50985,50986,
            50987,50988,50989,50990,50991,2547,2548,
            2549,6597,6598,15108,15109,15110,12160,
            7220,462,463,1877,1878,1879,1880,1881,1890,
            1892,433,460,461,9513,9514,9515,9518,9519,
            9520,9529,14687,47726,12333,12330,20369,
            21120,21227,21352,12332,21226,8258,32118,
            32119,32129,32132,32133,32135
        };

    public int CurrentMapNumber => currentMapNumber;
    public bool CurrentMapDropsOnDeath => currentMap != null && currentMap.dropOnDeath;
    public string CurrentMapName =>
        currentMap == null ? "" : currentMap.mapName;
    public string CurrentTerrain =>
        currentMap == null ? "" : currentMap.terrain;
    public string CurrentZone =>
        currentMap == null ? "" : currentMap.zone;
    public bool IsLoading => loading;
    public float CurrentWorldHour => worldHour;

    // World clock can be supplied by the server when networking is integrated.
    public void SetWorldHour(float hour)
    {
        float next = Mathf.Repeat(hour, 24f);
        if (Mathf.Abs(next - worldHour) < 0.001f) return;
        worldHour = next;
        if (currentMap == null || loading || currentBaseLight != 0) return;
        mapLighting = AOMapLighting.Build(currentMap, currentBaseLight, worldHour);
        RefreshMapLighting();
    }

    // Same payload as VB6 WorldTime_HandleHora: elapsed ms and day length.
    public void SyncWorldTime(int elapsedFromServerMs, int dayLengthMs)
    {
        worldDayLengthMs = Mathf.Max(1, dayLengthMs);
        worldElapsedAtSyncMs = (int)((((long)elapsedFromServerMs %
                                      worldDayLengthMs) + worldDayLengthMs) %
                                      worldDayLengthMs);
        worldSyncRealtime = Time.realtimeSinceStartupAsDouble;
        nextWorldClockUpdate = 0d;
        UpdateWorldClock();
    }

    void UpdateWorldClock()
    {
        if (worldDayLengthMs <= 0 || loading) return;
        double now = Time.realtimeSinceStartupAsDouble;
        if (now < nextWorldClockUpdate) return;
        nextWorldClockUpdate = now + 0.25d;
        double elapsed = (worldElapsedAtSyncMs +
                          (now - worldSyncRealtime) * 1000d) % worldDayLengthMs;
        SetWorldHour((float)(elapsed * 24d / worldDayLengthMs));
    }

    [Serializable]
    public class MapLoadMetrics
    {
        public int map;
        public double jsonMs;
        public double clearMs;
        public double gridMs;
        public double lightingMs;
        public double visualsMs;
        public double overlayMs;
        public double populationMs;
        public double particlesMs;
        public double weatherMs;
        public double finishMs;
        public double totalMs;
    }

    public MapLoadMetrics LastLoadMetrics { get; private set; }

    static double ElapsedMs(long start, long end)
    {
        return (end - start) * 1000.0 /
               System.Diagnostics.Stopwatch.Frequency;
    }

    public void SetWeather(AOMapWeather.Precipitation precipitation,
                           byte fogAlpha)
    {
        requestedPrecipitation = precipitation;
        requestedFogAlpha = fogAlpha;
        protocolFogEnabled = fogAlpha > 0;
        if (weather != null)
            weather.SetState(precipitation, fogAlpha);
    }

    // Entry points for the original server's RainToggle/NieveToggle packets.
    public void ApplyRainToggle(bool enabled)
    {
        requestedPrecipitation = enabled
            ? AOMapWeather.Precipitation.Rain
            : AOMapWeather.Precipitation.None;
        if (weather != null)
            weather.SetTarget(requestedPrecipitation, requestedFogAlpha);
    }

    public void ApplySnowToggle(bool enabled)
    {
        requestedPrecipitation = enabled
            ? AOMapWeather.Precipitation.Snow
            : AOMapWeather.Precipitation.None;
        if (weather != null)
            weather.SetTarget(requestedPrecipitation, requestedFogAlpha);
    }

    // NieblaToggle carries a target byte and flips visibility each packet.
    public void ApplyFogToggle(byte targetAlpha)
    {
        protocolFogEnabled = !protocolFogEnabled;
        requestedFogAlpha = protocolFogEnabled ? targetAlpha : (byte)0;
        if (weather != null)
            weather.SetTarget(requestedPrecipitation, requestedFogAlpha);
    }

    public AOMapWeather CurrentWeather => weather;

    public bool MagicTeleport(
        int mapNumber,
        int x,
        int y,
        out string result)
    {
        result = "";

        if (loading)
        {
            result =
                "El mundo todavía está cargando.";
            return false;
        }

        if (!MapExists(
                mapNumber))
        {
            result =
                "El mapa " +
                mapNumber +
                " no está migrado al mundo Unity.";

            return false;
        }

        try
        {
            LoadMap(
                mapNumber,
                x,
                y,
                true);

            result =
                "Teletransporte al mapa " +
                mapNumber +
                ".";

            return true;
        }
        catch (Exception e)
        {
            result =
                "Falló el teletransporte: " +
                e.Message;

            return false;
        }
    }

    public void ReturnHomeFromUI()
    {
        if (loading)
            return;

        if (AOSaveGameV140.SessionIsDemo && AOSaveGameV140.TryDemoHub(out int hubMap, out int hubX, out int hubY))
        {
            LoadMap(hubMap, hubX, hubY, true);
            return;
        }
        LoadMap(
            initialMap,
            initialX,
            initialY,
            true);
    }

    public bool IsExitTile(int x, int y)
    {
        return exitByTile.ContainsKey(
            TileKey(x, y));
    }

    public void Configure(
        AOTestPlayer newPlayer,
        AOCameraFollow newCamera,
        int startMap,
        int startX,
        int startY)
    {
        player = newPlayer;
        followCamera = newCamera;
        initialMap = startMap;
        initialX = startX;
        initialY = startY;
    }

    void OnDestroy()
    {
        AOLighting2DV283.ModeChanged -= OnLightingModeChanged;
    }

    void Start()
    {
        AOLighting2DV283.ModeChanged -= OnLightingModeChanged;
        AOLighting2DV283.ModeChanged += OnLightingModeChanged;
        if (!loadOnStart)
            return;

        try
        {
            LoadMap(initialMap, initialX, initialY, true);
        }
        catch (Exception e)
        {
            status = "ERROR inicial: " + e.Message;
            statusUntil = float.PositiveInfinity;
            Debug.LogError("AO World v0.7: " + e);
        }
    }

    void Update()
    {
        UpdateWorldClock();
        if (loading || player == null || currentMap == null)
            return;

        UpdateRoofVisibility();
        UpdateTreeVisibility();

        if (AOInterfaceV0101.InputCaptured)
            return;

        if (player.IsMoving)
            return;

        int x = player.TileX;
        int y = player.TileY;

        if (suppressExit)
        {
            if (x == suppressX && y == suppressY)
                return;
            suppressExit = false;
        }

        if (!exitByTile.TryGetValue(TileKey(x, y), out Exit tileExit))
            return;

        suppressExit = true;
        suppressX = x;
        suppressY = y;

        if (tileExit.destMap <= 0)
        {
            ShowStatus(
                "Salida especial/dinamica no implementada: " +
                tileExit.destMap + ".");
            return;
        }

        if (!MapExists(tileExit.destMap))
        {
            ShowStatus(
                "Mapa " + tileExit.destMap +
                " todavía no está incluido en v0.7.");
            return;
        }

        try
        {
            LoadMap(
                tileExit.destMap,
                tileExit.destX,
                tileExit.destY,
                true);
        }
        catch (Exception e)
        {
            ShowStatus("Error de transición: " + e.Message, 8f);
            Debug.LogError("AO World v0.7 transition: " + e);
        }
    }

    public void LoadMap(
        int mapNumber,
        int spawnX,
        int spawnY,
        bool suppressArrivalExit)
    {
        if (loading)
            return;

        loading = true;
        long phaseStart = System.Diagnostics.Stopwatch.GetTimestamp();
        MapLoadMetrics metrics = new MapLoadMetrics { map = mapNumber };

        try
        {
            TextAsset text = Resources.Load<TextAsset>(
                MapResourceRoot + mapNumber);

            if (text == null)
                throw new Exception(
                    "No existe map_" + mapNumber +
                    ".json en Resources.");

            WorldMapData data =
                JsonUtility.FromJson<WorldMapData>(text.text);

            if (data == null ||
                data.cells == null ||
                data.sprites == null)
            {
                throw new Exception(
                    "Datos inválidos para mapa " + mapNumber + ".");
            }

            if (player == null)
                player =
                    UnityEngine.Object.FindFirstObjectByType<AOTestPlayer>();

            if (player == null)
                throw new Exception("No encontré AO Test Player.");

            if (followCamera == null)
                followCamera =
                    UnityEngine.Object.FindFirstObjectByType<AOCameraFollow>();

            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            metrics.jsonMs = ElapsedMs(phaseStart, now);
            phaseStart = now;
            AOAudioV190.SetWeather(false);
            PrepareMapRoot(data);
            now = System.Diagnostics.Stopwatch.GetTimestamp();
            metrics.clearMs = ElapsedMs(phaseStart, now);
            phaseStart = now;
            BuildGrid(data);
            now = System.Diagnostics.Stopwatch.GetTimestamp();
            metrics.gridMs = ElapsedMs(phaseStart, now);
            phaseStart = now;
            BuildMapLighting(data);
            now = System.Diagnostics.Stopwatch.GetTimestamp();
            metrics.lightingMs = ElapsedMs(phaseStart, now);
            phaseStart = now;
            ResetRoofGroups(data);
            ResetTreeVisuals();
            BuildVisuals(data);
            now = System.Diagnostics.Stopwatch.GetTimestamp();
            metrics.visualsMs = ElapsedMs(phaseStart, now);
            phaseStart = now;
            BuildCollisionOverlay();
            now = System.Diagnostics.Stopwatch.GetTimestamp();
            metrics.overlayMs = ElapsedMs(phaseStart, now);
            phaseStart = now;
            BuildPopulation(data);
            now = System.Diagnostics.Stopwatch.GetTimestamp();
            metrics.populationMs = ElapsedMs(phaseStart, now);
            phaseStart = now;
            BuildMapParticles(data);
            now = System.Diagnostics.Stopwatch.GetTimestamp();
            metrics.particlesMs = ElapsedMs(phaseStart, now);
            phaseStart = now;
            BuildMapWeather();
            now = System.Diagnostics.Stopwatch.GetTimestamp();
            metrics.weatherMs = ElapsedMs(phaseStart, now);
            phaseStart = now;

            currentMap = data;
            currentMapNumber = mapNumber;
            BuildExitLookup(data);

            int targetX = Mathf.Clamp(
                spawnX, grid.XMin, grid.XMax);
            int targetY = Mathf.Clamp(
                spawnY, grid.YMin, grid.YMax);

            player.Initialize(grid, targetX, targetY);
            UpdateRoofVisibility();
            AOAudioV190.SetMapMusic(mapNumber);

            if (followCamera != null)
                followCamera.Initialize(
                    player.transform, grid, true);

            suppressExit = suppressArrivalExit;
            suppressX = targetX;
            suppressY = targetY;

            ShowStatus(
                "Mapa " + mapNumber + ": " +
                data.mapName + " (" +
                data.npcs.Length + " NPC / " +
                data.objects.Length + " objetos)",
                4f);

            Debug.Log(
                "[AO v0.7] Mapa " + mapNumber +
                " cargado: " + data.mapName +
                " @ " + targetX + "," + targetY);
            now = System.Diagnostics.Stopwatch.GetTimestamp();
            metrics.finishMs = ElapsedMs(phaseStart, now);
            metrics.totalMs = metrics.jsonMs + metrics.clearMs +
                metrics.gridMs + metrics.lightingMs + metrics.visualsMs +
                metrics.overlayMs + metrics.populationMs +
                metrics.particlesMs + metrics.weatherMs + metrics.finishMs;
            LastLoadMetrics = metrics;
            Debug.Log("AO_MAP_LOAD_PROFILE " + JsonUtility.ToJson(metrics));
        }
        finally
        {
            loading = false;
        }
    }

    void PrepareMapRoot(WorldMapData data)
    {
        if (mapRoot == null)
        {
            AOGridMap existingGrid =
                UnityEngine.Object.FindFirstObjectByType<AOGridMap>();

            if (existingGrid != null)
                mapRoot = existingGrid.gameObject;
        }

        if (mapRoot == null)
        {
            foreach (
                GameObject go in
                Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.scene.IsValid() &&
                    go.name.StartsWith(
                        "AO_MAP_",
                        StringComparison.Ordinal))
                {
                    mapRoot = go;
                    break;
                }
            }
        }

        if (mapRoot == null)
            mapRoot = new GameObject("AO_MAP_Runtime");

        mapRoot.name =
            "AO_MAP_" +
            SafeName(data.mapName) +
            "_M" + data.mapNumber;

        if (grid == null)
            grid = mapRoot.GetComponent<AOGridMap>();

        if (grid == null)
            grid = mapRoot.AddComponent<AOGridMap>();

        for (int layer = 1; layer <= 4; layer++)
        {
            Transform t =
                mapRoot.transform.Find("Layer_" + layer);

            if (t == null)
            {
                GameObject go =
                    new GameObject("Layer_" + layer);
                go.transform.SetParent(
                    mapRoot.transform, false);
                t = go.transform;
            }

            if (mapRendererPoolReady)
            {
                foreach (SpriteRenderer renderer in mapRendererPool[layer])
                    if (renderer != null)
                        renderer.gameObject.SetActive(false);
            }
            else
            {
                ClearChildren(t);
                mapRendererPool[layer].Clear();
                mapAnimationPool[layer].Clear();
            }
            mapRendererUse[layer] = 0;
            t.gameObject.SetActive(true);
        }

        Transform oldDebug =
            mapRoot.transform.Find("AO Collision Debug Runtime");
        if (oldDebug != null)
        {
            oldDebug.gameObject.SetActive(false);
            Destroy(oldDebug.gameObject);
        }

        DestroyPopulationRoot(ref npcRoot, "AO NPCs v0.7");
        DestroyPopulationRoot(ref objectRoot, "AO Objects v0.7");
        DestroyPopulationRoot(ref particleRoot, "AO Particles v0.7");
        DestroyPopulationRoot(ref weatherRoot, "AO Weather v0.7");
        weather = null;

        // Remove legacy v0.6 population roots when entering v0.7 Play.
        DestroySceneObjectNamed("AO NPCs v0.6");
        DestroySceneObjectNamed("AO Objects v0.6");
        DestroySceneObjectNamed("AO Loot v0.9");

        AOInteractionRegistry.Clear();
    }

    void BuildGrid(WorldMapData data)
    {
        grid.Initialize(
            data.xmin, data.xmax,
            data.ymin, data.ymax);

        if (data.blocks != null)
        {
            foreach (Block block in data.blocks)
                grid.OrFlags(
                    block.x, block.y, block.flags);
        }

        if (data.cells != null)
        {
            foreach (Cell cell in data.cells)
            {
                if (cell.layer == 1)
                {
                    if (IsWaterGrh(cell.grh))
                        grid.OrFlags(
                            cell.x, cell.y,
                            AOGridMap.FLAG_WATER);
                    else if (IsLavaGrh(cell.grh))
                        grid.OrFlags(
                            cell.x, cell.y,
                            AOGridMap.FLAG_LAVA);
                }
                else if (cell.layer == 2)
                {
                    grid.OrFlags(
                        cell.x, cell.y,
                        AOGridMap.FLAG_COAST);
                }
                else if (
                    cell.layer == 3 &&
                    TreeGrh.Contains(cell.grh))
                {
                    grid.OrFlags(
                        cell.x, cell.y,
                        AOGridMap.FLAG_TREE);
                }
            }
        }

        if (data.triggers != null)
        {
            foreach (Trigger trigger in data.triggers)
            {
                grid.SetTrigger(
                    trigger.x,
                    trigger.y,
                    trigger.trigger);

                if (
                    trigger.trigger ==
                        AOGridMap.TRIGGER_WATER_DETAIL ||
                    trigger.trigger ==
                        AOGridMap.TRIGGER_SWIM_VALID ||
                    trigger.trigger ==
                        AOGridMap.TRIGGER_SWIM_COMBINED ||
                    trigger.trigger ==
                        AOGridMap.TRIGGER_SWIM_ROOF)
                {
                    grid.ClearFlags(
                        trigger.x,
                        trigger.y,
                        AOGridMap.FLAG_COAST);
                }
            }
        }
    }

    void BuildMapLighting(WorldMapData data)
    {
        if (environmentLibrary == null)
        {
            TextAsset source = Resources.Load<TextAsset>(
                "AOMigrator/WorldV07/map_environment");
            if (source != null)
                environmentLibrary =
                    JsonUtility.FromJson<AOMapEnvironmentLibrary>(source.text);
        }

        int baseLight = 0;
        currentEnvironment = null;
        if (environmentLibrary != null && environmentLibrary.maps != null)
        {
            foreach (AOMapEnvironment environment in environmentLibrary.maps)
            {
                if (environment.mapNumber == data.mapNumber)
                {
                    baseLight = environment.baseLight;
                    currentEnvironment = environment;
                    break;
                }
            }
        }

        currentBaseLight = baseLight;
        mapLighting = AOMapLighting.Build(data, baseLight, worldHour);
        mapLightingXMin = data.xmin;
        mapLightingYMin = data.ymin;
        mapLightingWidth = data.xmax - data.xmin + 1;
    }

    void BuildVisuals(WorldMapData data)
    {
        Dictionary<int, Sprite> mapSprites =
            new Dictionary<int, Sprite>();
        Dictionary<int, Sprite[]> mapAnimations =
            new Dictionary<int, Sprite[]>();
        Dictionary<int, float> animationFps =
            new Dictionary<int, float>();

        foreach (SpriteDef def in data.sprites)
        {
            if (def.frames != null && def.frames.Length > 1)
            {
                Sprite[] frames = GetSprites(def.frames);
                if (frames.Length > 1)
                {
                    mapAnimations[def.id] = frames;
                    animationFps[def.id] = def.fps > 0f ? def.fps : 8f;
                    mapSprites[def.id] = frames[0];
                    continue;
                }
            }

            mapSprites[def.id] =
                GetSprite(
                    def.fileNum,
                    def.sx,
                    def.sy,
                    def.width,
                    def.height,
                    def.key);
        }

        Transform[] layerRoot = new Transform[5];
        for (int layer = 1; layer <= 4; layer++)
            layerRoot[layer] =
                mapRoot.transform.Find("Layer_" + layer);

        foreach (Cell cell in data.cells)
        {
            if (!mapSprites.TryGetValue(
                    cell.sprite, out Sprite sprite))
                continue;

            int poolIndex;
            bool reused;
            SpriteRenderer sr = GetMapRenderer(
                cell.layer, layerRoot[cell.layer],
                out poolIndex, out reused);
            GameObject go = sr.gameObject;
            go.name = "L" + cell.layer +
                "_" + cell.x + "_" + cell.y + "_G" + cell.grh;

            go.transform.localPosition =
                new Vector3(
                    cell.x - 0.5f,
                    -cell.y,
                    0f);

            sr.sprite = sprite;
            ApplyMapLight(sr, cell);

            AOAnimatedSprite animation =
                mapAnimationPool[cell.layer][poolIndex];
            if (mapAnimations.TryGetValue(
                    cell.sprite, out Sprite[] frames))
            {
                if (animation == null)
                {
                    animation = go.AddComponent<AOAnimatedSprite>();
                    mapAnimationPool[cell.layer][poolIndex] = animation;
                }
                animation.enabled = true;
                animation.Configure(
                    frames,
                    animationFps[cell.sprite]);
            }
            else if (animation != null)
                animation.enabled = false;

            if (cell.layer == 1)
                sr.sortingOrder = -30000;
            else if (cell.layer == 2)
                sr.sortingOrder = -20000 + cell.y;
            else if (cell.layer == 3)
                sr.sortingOrder = AORenderOrderV210.Layer3(cell.y);
            else
                sr.sortingOrder = 20000 + cell.y;
            if (reused)
                go.SetActive(true);
            if (cell.layer == 4)
                RegisterRoofCell(cell, sr);
            else if (cell.layer == 3 && TreeGrh.Contains(cell.grh))
                RegisterTreeVisual(cell.x, cell.y, sr);
        }
        mapRendererPoolReady = true;
    }

    void RefreshMapLighting()
    {
        if (currentMap == null || currentMap.cells == null) return;
        int[] next = new int[5];
        foreach (Cell cell in currentMap.cells)
        {
            if (cell.layer < 1 || cell.layer > 4) continue;
            int index = next[cell.layer];
            if (index >= mapRendererUse[cell.layer]) continue;
            SpriteRenderer renderer = mapRendererPool[cell.layer][index];
            if (renderer == null || renderer.sprite == null) continue;
            next[cell.layer]++;
            ApplyMapLight(renderer, cell);
        }
    }

    // Lights of the loaded map (Arte: AOLighting2DV283 reads them without parsing the JSON again).
    public AOMapLightPlacement[] CurrentMapLights => currentMap == null ? null : currentMap.lights;

    void OnLightingModeChanged() => RefreshMapLighting();

    void ApplyMapLight(SpriteRenderer renderer, Cell cell)
    {
        renderer.color = Color.white;
        // "Mejorada" (Arte): the floor takes the URP 2D lit material and the Light2D do the lighting.
        if (AOLighting2DV283.Enhanced)
        {
            renderer.sharedMaterial = AOLighting2DV283.LitMaterial;
            renderer.SetPropertyBlock(null);
            return;
        }
        if (mapLighting == null || mapLightingWidth <= 0) return;
        int index = ((cell.y - mapLightingYMin) * mapLightingWidth +
                     cell.x - mapLightingXMin) * 4;
        if (index < 0 || index + 3 >= mapLighting.Length) return;

        if (mapVertexMaterial == null)
        {
            Shader shader = Resources.Load<Shader>(
                "AOMigrator/WorldV07/AOMapVertexLit");
            if (shader != null) mapVertexMaterial = new Material(shader);
        }
        if (mapVertexMaterial == null)
        {
            renderer.color = Color32.Lerp(
                Color32.Lerp(mapLighting[index], mapLighting[index + 1], 0.5f),
                Color32.Lerp(mapLighting[index + 2], mapLighting[index + 3], 0.5f),
                0.5f);
            return;
        }

        renderer.sharedMaterial = mapVertexMaterial;
        if (mapLightProperties == null)
            mapLightProperties = new MaterialPropertyBlock();
        mapLightProperties.Clear();
        mapLightProperties.SetColor(AOColor0, mapLighting[index]);
        mapLightProperties.SetColor(AOColor1, mapLighting[index + 1]);
        mapLightProperties.SetColor(AOColor2, mapLighting[index + 2]);
        mapLightProperties.SetColor(AOColor3, mapLighting[index + 3]);
        Bounds bounds = renderer.sprite.bounds;
        mapLightProperties.SetVector(AOBounds, new Vector4(
            bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y));
        renderer.SetPropertyBlock(mapLightProperties);
    }

    SpriteRenderer GetMapRenderer(int layer, Transform parent,
                                  out int index, out bool reused)
    {
        index = mapRendererUse[layer]++;
        List<SpriteRenderer> pool = mapRendererPool[layer];
        if (index < pool.Count && pool[index] != null)
        {
            reused = true;
            return pool[index];
        }
        reused = false;
        GameObject go = new GameObject("AO Map Sprite",
                                       typeof(SpriteRenderer));
        go.transform.SetParent(parent, false);
        SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
        if (index < pool.Count)
        {
            pool[index] = renderer;
            mapAnimationPool[layer][index] = null;
        }
        else
        {
            pool.Add(renderer);
            mapAnimationPool[layer].Add(null);
        }
        return renderer;
    }

    void BuildCollisionOverlay()
    {
        int width = grid.Width;
        int height = grid.Height;

        Texture2D tex =
            new Texture2D(
                width, height,
                TextureFormat.RGBA32,
                false);

        tex.name = "AO Runtime Collision Overlay";
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels =
            new Color32[width * height];

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(0,0,0,0);

        for (int y = grid.YMin;
             y <= grid.YMax; y++)
        {
            for (int x = grid.XMin;
                 x <= grid.XMax; x++)
            {
                int flags = grid.GetFlags(x, y);
                int trigger = grid.GetTrigger(x, y);
                Color32 color =
                    new Color32(0,0,0,0);

                if (
                    trigger ==
                        AOGridMap.TRIGGER_WORKER_ONLY ||
                    trigger ==
                        AOGridMap.TRIGGER_BLOCK_15)
                {
                    color =
                        new Color32(255,200,0,120);
                }
                else if (
                    grid.IsDeepWater(x, y) &&
                    trigger !=
                        AOGridMap.TRIGGER_BRIDGE_VALID)
                {
                    color =
                        new Color32(40,140,255,105);
                }
                else if (
                    (flags &
                     AOGridMap.FLAG_ALL_SIDES) != 0)
                {
                    color =
                        new Color32(255,45,35,115);
                }

                int px = x - grid.XMin;
                int py = grid.YMax - y;
                pixels[py * width + px] = color;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, false);

        Sprite sprite =
            Sprite.Create(
                tex,
                new Rect(0,0,width,height),
                new Vector2(0f,0f),
                1f, 0,
                SpriteMeshType.FullRect);

        GameObject overlay =
            new GameObject(
                "AO Collision Debug Runtime",
                typeof(SpriteRenderer));

        overlay.transform.SetParent(
            mapRoot.transform, false);

        overlay.transform.localPosition =
            new Vector3(
                grid.XMin - 1f,
                -grid.YMax,
                0f);

        SpriteRenderer sr =
            overlay.GetComponent<SpriteRenderer>();

        sr.sprite = sprite;
        sr.sortingOrder = 30000;

        runtimeDebugOverlay = overlay;
        grid.SetDebugOverlay(overlay);
    }

    void BuildMapParticles(WorldMapData data)
    {
        if (data.particles == null || data.particles.Length == 0)
            return;

        if (particleLibrary == null)
        {
            TextAsset source = Resources.Load<TextAsset>(
                "AOMigrator/WorldV07/particle_defs");
            if (source == null)
            {
                Debug.LogWarning("Faltan definiciones de partículas de mapa.");
                return;
            }
            particleLibrary =
                JsonUtility.FromJson<AOMapParticleLibrary>(source.text);
        }
        if (particleLibrary == null || particleLibrary.definitions == null)
            return;

        Dictionary<int, AOMapParticleDefinition> definitions =
            new Dictionary<int, AOMapParticleDefinition>();
        foreach (AOMapParticleDefinition definition in particleLibrary.definitions)
            definitions[definition.id] = definition;

        Dictionary<int, Sprite[][]> spritesByType =
            new Dictionary<int, Sprite[][]>();
        Camera mapCamera = followCamera != null
            ? followCamera.GetComponent<Camera>() : Camera.main;
        particleRoot = new GameObject("AO Particles v0.7");
        int created = 0;

        foreach (AOMapParticlePlacement placement in data.particles)
        {
            if (!definitions.TryGetValue(placement.particle,
                                         out AOMapParticleDefinition definition) ||
                definition.sprites == null || definition.sprites.Length == 0)
                continue;

            if (!spritesByType.TryGetValue(placement.particle,
                                          out Sprite[][] options))
            {
                options = new Sprite[definition.sprites.Length][];
                for (int i = 0; i < options.Length; i++)
                    options[i] = GetSprites(definition.sprites[i].frames);
                spritesByType[placement.particle] = options;
            }

            GameObject groupObject =
                new GameObject("Particle_" + placement.particle + "_" +
                               placement.x + "_" + placement.y);
            groupObject.transform.SetParent(particleRoot.transform, false);
            AOMapParticleGroup group =
                groupObject.AddComponent<AOMapParticleGroup>();
            group.Configure(definition, options, mapCamera,
                            placement.x, placement.y);
            created++;
        }
        Debug.Log("[AO v0.7] Partículas de mapa: " + created);
    }

    void BuildMapWeather()
    {
        if (currentEnvironment == null ||
            (!currentEnvironment.rain && !currentEnvironment.snow &&
             !currentEnvironment.fog))
            return;

        if (particleLibrary == null)
        {
            TextAsset source = Resources.Load<TextAsset>(
                "AOMigrator/WorldV07/particle_defs");
            if (source != null)
                particleLibrary =
                    JsonUtility.FromJson<AOMapParticleLibrary>(source.text);
        }
        if (particleLibrary == null || particleLibrary.definitions == null)
            return;

        AOMapParticleDefinition rain = null;
        AOMapParticleDefinition snow = null;
        foreach (AOMapParticleDefinition definition in particleLibrary.definitions)
        {
            if (definition.id == 58)
                rain = definition;
            else if (definition.id == 57)
                snow = definition;
        }
        Sprite rainSprite = WeatherSprite(rain);
        Sprite snowSprite = WeatherSprite(snow);
        Sprite[] fogSprites = new Sprite[2];
        if (particleLibrary.fogSprites != null)
        {
            for (int i = 0; i < Mathf.Min(2, particleLibrary.fogSprites.Length); i++)
            {
                Sprite[] sprites = GetSprites(particleLibrary.fogSprites[i].frames);
                if (sprites.Length > 0)
                    fogSprites[i] = sprites[0];
            }
        }

        Camera mapCamera = followCamera != null
            ? followCamera.GetComponent<Camera>() : Camera.main;
        weatherRoot = new GameObject("AO Weather v0.7");
        weather = weatherRoot.AddComponent<AOMapWeather>();
        weather.Configure(mapCamera, currentEnvironment, rain, snow,
                          rainSprite, snowSprite, fogSprites);
        weather.SetState(requestedPrecipitation, requestedFogAlpha);
        Debug.Log("AO_MAP_WEATHER_READY map=" + currentEnvironment.mapNumber +
                  " rain=" + currentEnvironment.rain +
                  " snow=" + currentEnvironment.snow +
                  " fog=" + currentEnvironment.fog);
    }

    Sprite WeatherSprite(AOMapParticleDefinition definition)
    {
        if (definition == null || definition.sprites == null ||
            definition.sprites.Length == 0)
            return null;
        Sprite[] sprites = GetSprites(definition.sprites[0].frames);
        return sprites.Length > 0 ? sprites[0] : null;
    }

    void BuildPopulation(WorldMapData data)
    {
        npcRoot = new GameObject("AO NPCs v0.7");
        objectRoot = new GameObject("AO Objects v0.7");

        if (data.npcs != null)
        {
            for (int i = 0; i < data.npcs.Length; i++)
                CreateNPC(data.npcs[i], i + 1);
        }

        if (data.objects != null)
        {
            foreach (ObjectEntry obj in data.objects)
                CreateObject(obj);
        }
    }

    void CreateNPC(NPCEntry npc, int networkId)
    {
        GameObject go =
            new GameObject(
                "NPC_" + npc.npcIndex +
                "_" + SafeName(npc.name));

        go.transform.SetParent(
            npcRoot.transform, false);

        go.transform.position =
            grid.TileToWorld(npc.x, npc.y);

        AONPCMetadata meta =
            go.AddComponent<AONPCMetadata>();

        meta.ConfigureNPC(
            npc.npcIndex,
            npc.x, npc.y,
            npc.name,
            npc.description,
            npc.npcType,
            npc.movement,
            npc.heading,
            npc.body,
            npc.head,
            npc.helmet,
            npc.weapon,
            npc.shield);

        GameObject visualGo =
            new GameObject("Visual");
        visualGo.transform.SetParent(
            go.transform, false);

        AOCharacterRenderer visual =
            visualGo.AddComponent<AOCharacterRenderer>();

        AOCharacterRenderer.DirectionVisual[] dirs =
            new AOCharacterRenderer.DirectionVisual[4];

        for (int h = 1; h <= 4; h++)
        {
            DirectionSpec source =
                FindDirection(npc.directions, h);

            dirs[h - 1] =
                new AOCharacterRenderer.DirectionVisual {
                    heading = h,
                    body =
                        source == null
                        ? new Sprite[0]
                        : GetSprites(source.body),
                    head =
                        source == null
                        ? new Sprite[0]
                        : GetSprites(source.head),
                    helmet =
                        source == null
                        ? new Sprite[0]
                        : GetSprites(source.helmet),
                    weapon =
                        source == null
                        ? new Sprite[0]
                        : GetSprites(source.weapon),
                    shield =
                        source == null
                        ? new Sprite[0]
                        : GetSprites(source.shield)
                };
        }

        visual.Configure(
            dirs,
            npc.walkFps <= 0f
                ? 18f
                : npc.walkFps,
            npc.headOffsetX / 32f,
            -npc.headOffsetY / 32f,
            npc.bodyShiftX / 32f);

        visual.SetHeading(npc.heading);
        visual.SetWalking(false);

        int baseOrder = AORenderOrderV210.Character(npc.y);
        visual.UpdateSorting(baseOrder);

        AOPlayerCombatV09 playerCombat =
            player == null
            ? null
            : player.GetComponent<AOPlayerCombatV09>();

        AONPCCombatV09 npcCombat =
            go.AddComponent<AONPCCombatV09>();
        go.GetComponent<AONPCCombatV09>().NetworkId = networkId;

        AONPCMovementV08 ai =
            go.AddComponent<AONPCMovementV08>();

        AONPCMovementV08.WalkPoint[] route =
            npc.walkRoute == null
            ? new AONPCMovementV08.WalkPoint[0]
            : Array.ConvertAll(
                npc.walkRoute,
                p => new AONPCMovementV08.WalkPoint {
                    offsetX = p.offsetX,
                    offsetY = p.offsetY,
                    waitMs = p.waitMs
                });

        AONPCCombatV09.DropSpec[] combatDrops =
            npc.drops == null
            ? new AONPCCombatV09.DropSpec[0]
            : Array.ConvertAll(
                npc.drops,
                d => new AONPCCombatV09.DropSpec {
                    itemIndex = d.itemIndex,
                    name = d.name,
                    chanceDenominator =
                        d.chanceDenominator,
                    minAmount = d.minAmount,
                    maxAmount = d.maxAmount,
                    source = d.source
                });

        npcCombat.Configure(
            meta,
            ai,
            visual,
            playerCombat,
            npc.x,
            npc.y,
            npc.maxHp,
            npc.minHit,
            npc.maxHit,
            npc.defense,
            npc.attackPower,
            npc.evasionPower,
            npc.attackable,
            npc.attackIntervalMs,
            npc.respawnMinSeconds,
            npc.respawnMaxSeconds,
            npc.giveExp,
            npc.giveGold,
            combatDrops);

        ai.Configure(
            this,
            grid,
            player,
            meta,
            visual,
            npc.x,
            npc.y,
            npc.movement,
            npc.hostile,
            npc.attackRange,
            npc.preferredRange,
            npc.visionRange,
            npc.moveIntervalMs,
            npc.waterValid,
            npc.landInvalid,
            npc.lavaValid,
            route);

        ai.SetVisionAxes(
            npc.visionRangeX,
            npc.visionRangeY);

        if (showNPCNames && npc.showName)
            CreateNameLabel(
                go.transform,
                npc.name,
                baseOrder + 20);
    }

    void CreateObject(ObjectEntry obj)
    {
        GameObject go =
            new GameObject(
                "OBJ_" + obj.objIndex +
                "_" + SafeName(obj.name));

        go.transform.SetParent(
            objectRoot.transform, false);

        go.transform.position =
            grid.TileToWorld(obj.x, obj.y);

        SpriteRenderer renderer =
            go.AddComponent<SpriteRenderer>();

        Sprite[] frames =
            GetSprites(obj.frames);

        if (frames.Length > 0)
            renderer.sprite = frames[0];

        renderer.sortingOrder = obj.objType == 4 || obj.objType == 6 ||
            obj.objType == 8 || obj.objType == 27 || obj.objType == 28
            ? AORenderOrderV210.LargeObject(obj.y)
            : 9000 + obj.y;
        if (obj.objType == 4)
            RegisterTreeVisual(obj.x, obj.y, renderer);

        if (frames.Length > 1)
        {
            AOAnimatedSprite anim =
                go.AddComponent<AOAnimatedSprite>();

            anim.Configure(
                frames,
                obj.fps <= 0f ? 8f : obj.fps);
        }

        AOWorldObjectMetadata meta =
            go.AddComponent<AOWorldObjectMetadata>();

        meta.ConfigureObject(
            obj.objIndex,
            obj.x, obj.y,
            obj.name,
            obj.description,
            obj.objType,
            obj.amount,
            obj.grhIndex);

        if (obj.objType == 6)
        {
            AODoorCatalogV210.DoorDef doorDef =
                AODoorCatalogV210.Get(obj.objIndex);
            if (doorDef != null)
            {
                Sprite openSprite = null;
                if (!doorDef.locked && doorDef.openFrame != null)
                {
                    FrameSpec frame = doorDef.openFrame;
                    openSprite = GetSprite(frame.fileNum, frame.sx,
                        frame.sy, frame.width, frame.height, frame.key);
                }
                AODoorV210 door = go.AddComponent<AODoorV210>();
                door.Configure(grid, renderer, openSprite, obj.x, obj.y,
                               doorDef.locked);
            }
        }
    }

    void CreateNameLabel(
        Transform parent,
        string text,
        int sortingOrder)
    {
        GameObject label =
            new GameObject("Name");

        label.transform.SetParent(
            parent, false);

        label.transform.localPosition =
            new Vector3(0f, 1.15f, 0f);

        TextMesh tm =
            label.AddComponent<TextMesh>();

        tm.text = text ?? "";
        tm.anchor = TextAnchor.LowerCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontSize = 48;
        tm.characterSize = 0.025f;
        tm.richText = false;

        MeshRenderer renderer =
            label.GetComponent<MeshRenderer>();

        if (renderer != null)
            renderer.sortingOrder =
                sortingOrder;
    }

    DirectionSpec FindDirection(
        DirectionSpec[] list,
        int heading)
    {
        if (list == null)
            return null;

        foreach (DirectionSpec item in list)
        {
            if (item != null &&
                item.heading == heading)
                return item;
        }

        return null;
    }

    Sprite[] GetSprites(FrameSpec[] frames)
    {
        if (frames == null ||
            frames.Length == 0)
            return new Sprite[0];

        Sprite[] result =
            new Sprite[frames.Length];

        for (int i = 0;
             i < frames.Length; i++)
        {
            FrameSpec f = frames[i];
            result[i] =
                GetSprite(
                    f.fileNum,
                    f.sx,
                    f.sy,
                    f.width,
                    f.height,
                    f.key);
        }

        return result;
    }

    Sprite GetSprite(
        int fileNum,
        int sx,
        int sy,
        int width,
        int height,
        string key)
    {
        if (string.IsNullOrEmpty(key))
            key =
                "f" + fileNum +
                "_" + sx +
                "_" + sy +
                "_" + width +
                "_" + height;

        if (spriteCache.TryGetValue(
                key, out Sprite cached))
            return cached;

        Texture2D texture =
            LoadTexture(fileNum);

        int scale =
            textureScale.TryGetValue(
                fileNum, out int s)
            ? s
            : 1;

        sx *= scale;
        sy *= scale;
        width *= scale;
        height *= scale;

        int unityY =
            texture.height - sy - height;

        if (
            sx < 0 ||
            unityY < 0 ||
            width <= 0 ||
            height <= 0 ||
            sx + width > texture.width ||
            unityY + height > texture.height)
        {
            throw new Exception(
                "Recorte fuera de textura: " +
                key + " tex=" +
                texture.width + "x" +
                texture.height);
        }

        Sprite sprite =
            Sprite.Create(
                texture,
                new Rect(
                    sx, unityY,
                    width, height),
                new Vector2(0.5f, 0f),
                32f * scale,
                0,
                SpriteMeshType.FullRect);

        sprite.name = key;
        spriteCache[key] = sprite;
        return sprite;
    }

    Texture2D LoadTexture(int fileNum)
    {
        if (textureCache.TryGetValue(
                fileNum, out Texture2D cached))
            return cached;

        Texture2D texture =
            UseHDTextures
            ? Resources.Load<Texture2D>(
                HDTextureResourceRoot + fileNum)
            : null;

        textureScale[fileNum] =
            texture != null
            ? HDScale
            : 1;

        if (texture == null)
            texture =
                Resources.Load<Texture2D>(
                    TextureResourceRoot + fileNum);

        if (texture == null)
            throw new Exception(
                "Falta tex_" + fileNum +
                ".png en WorldV07/Textures.");

        textureCache[fileNum] = texture;
        return texture;
    }

    void BuildExitLookup(WorldMapData data)
    {
        exitByTile.Clear();

        if (data.exits == null)
            return;

        foreach (Exit tileExit in data.exits)
        {
            exitByTile[
                TileKey(
                    tileExit.x,
                    tileExit.y)] =
                tileExit;
        }
    }

    bool MapExists(int mapNumber)
    {
        if (mapAvailability.TryGetValue(
                mapNumber,
                out bool available))
            return available;

        TextAsset asset =
            Resources.Load<TextAsset>(
                MapResourceRoot + mapNumber);

        available = asset != null;
        mapAvailability[mapNumber] =
            available;
        return available;
    }

    static long TileKey(int x, int y)
    {
        return ((long)x << 32) |
               (uint)y;
    }

    void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1;
             i >= 0; i--)
        {
            GameObject child =
                parent.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    void DestroyPopulationRoot(
        ref GameObject root,
        string expectedName)
    {
        if (root == null)
            root = GameObject.Find(expectedName);

        if (root != null)
        {
            root.SetActive(false);
            Destroy(root);
            root = null;
        }
    }

    void DestroySceneObjectNamed(
        string objectName)
    {
        GameObject go =
            GameObject.Find(objectName);

        if (go != null)
        {
            go.SetActive(false);
            Destroy(go);
        }
    }

    static string SafeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "SinNombre";

        return value
            .Replace("/", "-")
            .Replace("\\", "-")
            .Replace("\n", " ");
    }

    void ShowStatus(
        string message,
        float duration = 4f)
    {
        status = message;
        statusUntil =
            duration <= 0f
            ? float.PositiveInfinity
            : Time.time + duration;

        Debug.Log("[AO v0.7] " + message);
    }

    static bool IsWaterGrh(int grh)
    {
        return
            (grh >= 1505 && grh <= 1520) ||
            (grh >= 124 && grh <= 139) ||
            (grh >= 24223 && grh <= 24238) ||
            (grh >= 24303 && grh <= 24318) ||
            (grh >= 468 && grh <= 483) ||
            (grh >= 44668 && grh <= 44683) ||
            (grh >= 24143 && grh <= 24158) ||
            (grh >= 12628 && grh <= 12643) ||
            (grh >= 2948 && grh <= 2963);
    }

    static bool IsLavaGrh(int grh)
    {
        return
            (grh >= 57400 && grh <= 57415) ||
            (grh >= 16101 && grh <= 16116) ||
            (grh >= 26767 && grh <= 26782);
    }

#if UNITY_EDITOR // debug only: hidden in player builds
    void OnGUI()
    {
        if (AOOnlineClientV240.InputBlocked) return;
        if (AOInterfaceV0101.Active)
            return;
        if (!showHud)
            return;

        string name =
            currentMap == null
            ? "sin mapa"
            : currentMap.mapName;

        int exits =
            currentMap == null ||
            currentMap.exits == null
            ? 0
            : currentMap.exits.Length;

        string text =
            "AO v0.9 - combate local / World Manager\n" +
            "Mapa: " + currentMapNumber +
            " | " + name + "\n" +
            "Salidas: " + exits +
            " | NPC IA: " +
            (AONPCMovementV08.GlobalPaused ? "PAUSA" : "ON") +
            " (" + AONPCMovementV08.ActiveControllers + ")";

        GUI.Box(
            new Rect(
                Mathf.Max(12, Screen.width - 430),
                12,
                418,
                76),
            text);

        if (!string.IsNullOrEmpty(status) &&
            Time.time <= statusUntil)
        {
            GUIStyle style =
                new GUIStyle(GUI.skin.box);
            style.wordWrap = true;
            style.alignment =
                TextAnchor.UpperLeft;

            GUI.Box(
                new Rect(
                    Mathf.Max(12, Screen.width - 430),
                    96,
                    418,
                    80),
                status,
                style);
        }
    }
#endif
}
