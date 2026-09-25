using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Luz "Mejorada" (URP 2D). "Original" es la fiel al AO20 (AOMapLighting) y es la de siempre: no crea nada.
// Mejorada:
// - luz global que sigue la hora del juego (DayColor / baseLight), fría de noche;
// - una Light2D por cada luz del mapa (las blancas del original se leen cálidas) con parpadeo leve;
// - luz mínima alrededor del jugador, para que siempre se lea;
// - posproceso: bloom, viñeta, saturación y contraste.
// Parámetros de la vista aprobada: Tools/hd_remake/preview_luces.py (light_propuesta y finish).
// Para que el piso reciba estas luces, AOWorldManagerV07.ApplyMapLight usa LitMaterial cuando Enhanced.
[DisallowMultipleComponent]
public class AOLighting2DV283 : MonoBehaviour
{
    [Serializable] class MapLights
    {
        public AOMapLightPlacement[] lights;
    }

    struct Flicker
    {
        public Light2D light;
        public float baseIntensity;
        public float seed;
    }

    static readonly Color NightAmbient = new Color(0.24f, 0.30f, 0.50f);
    static readonly Color NoonAmbient = new Color(1f, 0.98f, 0.94f);
    static readonly Color WarmLight = new Color(1f, 0.74f, 0.46f);
    const float NightDayColor = 120f / 255f;

    static AOLighting2DV283 instance;
    static bool enhanced;
    static Material litMaterial;
    static Dictionary<int, int> baseLights;

    public static event Action ModeChanged;
    public static bool Enhanced => enhanced;

    // Posproceso URP (bloom, viñeta, color). Apagado: con la cámara recortada de la interfaz clásica
    // (pixelRect 736x608) el posproceso del Renderer 2D duplica la imagen (captura de QA, lighting_v287).
    // En su lugar va una viñeta propia (sprite que sigue a la cámara). Se reactiva cuando la cámara renderice a textura.
    public static bool PostProcessing = false;

    // Material iluminado por defecto del Renderer 2D (Sprite-Lit-Default).
    public static Material LitMaterial
    {
        get
        {
            if (litMaterial == null)
            {
                var probe = new GameObject("AO Lit Material Probe");
                litMaterial = probe.AddComponent<SpriteRenderer>().sharedMaterial;
                Destroy(probe);
            }
            return litMaterial;
        }
    }

    AOWorldManagerV07 world;
    Transform lightsRoot;
    Light2D globalLight;
    Light2D playerLight;
    Volume volume;
    SpriteRenderer vignette;
    static Sprite vignetteSprite;
    readonly List<Light2D> mapLights = new List<Light2D>();
    readonly List<Flicker> flickers = new List<Flicker>();
    int builtMap = -1;
    float builtHour = -1f;
    bool cameraPostBefore;
    Camera postCamera;

    // Lo llama Interfaz desde la opción "Luz: Original / Mejorada".
    public static void SetEnhanced(bool on)
    {
        if (enhanced == on) return;
        enhanced = on;
        Ensure().Apply();
        ModeChanged?.Invoke();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        Ensure();
    }

    static AOLighting2DV283 Ensure()
    {
        if (instance != null) return instance;
        var go = new GameObject("AO Lighting 2D v283");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<AOLighting2DV283>();
        return instance;
    }

    void Update()
    {
        if (!enhanced) return;
        if (world == null) world = UnityEngine.Object.FindFirstObjectByType<AOWorldManagerV07>();
        if (world == null || world.IsLoading) return;

        int map = world.CurrentMapNumber;
        if (map != builtMap) BuildMapLights(map);

        float hour = world.CurrentWorldHour;
        if (Mathf.Abs(Mathf.DeltaAngle(hour * 15f, builtHour * 15f)) > 1.5f) UpdateGlobal(hour);

        float t = Time.time;
        foreach (Flicker f in flickers)
            if (f.light != null)
                f.light.intensity = f.baseIntensity * (1f + 0.14f * (Mathf.PerlinNoise(t * 6f, f.seed) - 0.5f));

        Camera cam = AOActionBarV260.GameCamera;
        if (cam != null && playerLight != null)
        {
            Vector3 p = cam.transform.position;
            playerLight.transform.position = new Vector3(p.x, p.y, 0f);
        }
        if (PostProcessing) EnsureCameraPost(cam);
        UpdateVignette(cam);
    }

    void Apply()
    {
        if (enhanced)
        {
            builtMap = -1;
            builtHour = -1f;
            EnsureRoot();
            if (PostProcessing) EnsureVolume();
            return;
        }

        // Original: nada de Light2D (URP 2D usa su luz blanca por defecto) ni posproceso.
        if (lightsRoot != null) Destroy(lightsRoot.gameObject);
        lightsRoot = null;
        globalLight = null;
        playerLight = null;
        mapLights.Clear();
        flickers.Clear();
        if (volume != null) Destroy(volume.gameObject);
        volume = null;
        if (vignette != null) Destroy(vignette.gameObject);
        vignette = null;
        RestoreCameraPost();
        builtMap = -1;
    }

    void EnsureRoot()
    {
        if (lightsRoot != null) return;
        lightsRoot = new GameObject("Lights").transform;
        lightsRoot.SetParent(transform, false);

        globalLight = NewLight("Global", Light2D.LightType.Global, lightsRoot);
        globalLight.intensity = 1f;

        playerLight = NewLight("Jugador", Light2D.LightType.Point, lightsRoot);
        playerLight.color = new Color(1f, 0.95f, 0.9f);
        playerLight.intensity = 0.3f;
        playerLight.pointLightInnerRadius = 0f;
        playerLight.pointLightOuterRadius = 3.5f;
        playerLight.falloffIntensity = 0.7f;
    }

    static Light2D NewLight(string name, Light2D.LightType type, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var light = go.AddComponent<Light2D>();
        light.lightType = type;
        light.shadowsEnabled = false;
        return light;
    }

    void BuildMapLights(int map)
    {
        builtMap = map;
        EnsureRoot();
        foreach (Light2D old in mapLights)
            if (old != null) Destroy(old.gameObject);
        mapLights.Clear();
        flickers.Clear();

        UpdateGlobal(world.CurrentWorldHour);

        // Las luces del mapa ya cargado (AOWorldManagerV07); si no están, se leen del JSON del mapa.
        AOMapLightPlacement[] lights = world.CurrentMapLights ?? LoadLights(map);
        if (lights == null) return;

        int index = 0;
        foreach (AOMapLightPlacement placement in lights)
        {
            if (placement == null) continue;
            int range = placement.range >= 100 ? placement.range - 99 : placement.range;
            if (range <= 0 || range > 128) continue;
            // Mismo radio que la vista aprobada, un poco más largo para cubrir su luz de relleno lejana.
            float radius = (placement.range >= 100 ? Mathf.Max(2.5f, range * 1.25f) : Mathf.Max(3.5f, range * 1.6f)) * 1.3f;

            Color color = Decode(placement.color);
            float min = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
            bool warm = min > 0.85f;
            if (warm) color = WarmLight;
            // Fría y casi blanca (la luz de luna de los haces del dungeon, nube 25/09): relleno suave, no foco.
            // Solo si el azul supera al rojo: las grises de los mapas originales y las cálidas de la arena no cambian.
            bool moon = !warm && color.b - color.r >= 0.1f && color.maxColorComponent - min < 0.25f;

            Light2D light = NewLight("Luz " + placement.x + "," + placement.y, Light2D.LightType.Point, lightsRoot);
            light.transform.position = new Vector3(placement.x - 0.5f, -placement.y + 0.5f, 0f);
            light.color = color;
            light.intensity = moon ? 0.55f : 1.05f;
            light.pointLightInnerRadius = 0f;
            light.pointLightOuterRadius = radius;
            light.falloffIntensity = 0.55f;
            mapLights.Add(light);
            if (warm)   // solo faroles y antorchas parpadean
                flickers.Add(new Flicker { light = light, baseIntensity = light.intensity, seed = index * 7.31f + 1f });
            index++;
        }
    }

    static AOMapLightPlacement[] LoadLights(int map)
    {
        TextAsset json = Resources.Load<TextAsset>("AOMigrator/WorldV07/Maps/map_" + map);
        if (json == null) return null;
        MapLights data = null;
        try { data = JsonUtility.FromJson<MapLights>(json.text); }
        catch (Exception ex) { Debug.LogWarning("[AO Lighting 2D v283] Luces del mapa " + map + ": " + ex.Message); }
        Resources.UnloadAsset(json);
        return data?.lights;
    }

    void UpdateGlobal(float hour)
    {
        builtHour = hour;
        if (globalLight == null) return;
        int baseLight = BaseLight(builtMap);
        if (baseLight == 0)
        {
            Color day = AOMapLighting.DayColor(hour);
            float t = Mathf.InverseLerp(NightDayColor, 1f, day.maxColorComponent);
            globalLight.color = Color.Lerp(NightAmbient, NoonAmbient, t);
        }
        else
        {
            Color dungeon = Decode(baseLight);
            globalLight.color = new Color(dungeon.r * 0.85f, dungeon.g * 0.9f, Mathf.Min(1f, dungeon.b * 1.1f));
        }
    }

    // Viñeta de la vista aprobada (1 - 0,42·r^2,4, tope 0,35) como sprite negro con alfa en los bordes.
    void UpdateVignette(Camera cam)
    {
        if (cam == null || !cam.orthographic) return;
        if (vignette == null)
        {
            var go = new GameObject("Viñeta");
            go.transform.SetParent(transform, false);
            vignette = go.AddComponent<SpriteRenderer>();
            vignette.sprite = VignetteSprite();
            vignette.sortingOrder = 32000;
        }
        float height = cam.orthographicSize * 2f;
        float width = height * cam.aspect;
        Vector3 p = cam.transform.position;
        vignette.transform.position = new Vector3(p.x, p.y, 0f);
        vignette.transform.localScale = new Vector3(width, height, 1f);
    }

    static Sprite VignetteSprite()
    {
        if (vignetteSprite != null) return vignetteSprite;
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "AO Vignette", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
        };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float dark = 1f - Mathf.Clamp(1f - 0.42f * Mathf.Pow(u * u + v * v, 1.2f), 0.35f, 1f);
                pixels[y * size + x] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(dark * 255f));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        // 1 unidad de mundo = todo el sprite: se escala con el tamaño de la vista.
        vignetteSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return vignetteSprite;
    }

    void EnsureVolume()
    {
        if (volume != null) return;
        var go = new GameObject("Posproceso");
        go.transform.SetParent(transform, false);
        volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        Bloom bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.8f);
        bloom.intensity.Override(0.6f);
        bloom.scatter.Override(0.7f);
        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.32f);
        vignette.smoothness.Override(0.45f);
        ColorAdjustments adjustments = profile.Add<ColorAdjustments>(true);
        adjustments.saturation.Override(15f);
        adjustments.contrast.Override(6f);
        volume.sharedProfile = profile;
    }

    void EnsureCameraPost(Camera cam)
    {
        if (cam == null || cam == postCamera) return;
        RestoreCameraPost();
        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        if (data == null) return;
        postCamera = cam;
        cameraPostBefore = data.renderPostProcessing;
        data.renderPostProcessing = true;
    }

    void RestoreCameraPost()
    {
        if (postCamera == null) return;
        UniversalAdditionalCameraData data = postCamera.GetUniversalAdditionalCameraData();
        if (data != null) data.renderPostProcessing = cameraPostBefore;
        postCamera = null;
    }

    // Luz ambiente de la luz Original (AOMapLighting): baseLight del mapa o el color del día.
    // Sirve para teñir sprites agregados en tiempo de juego (obstáculos del ring) como el resto del mapa.
    public static Color OriginalAmbient(int map, float hour)
    {
        int baseLight = BaseLight(map);
        return baseLight == 0 ? (Color)AOMapLighting.DayColor(hour) : Decode(baseLight);
    }

    static int BaseLight(int map)
    {
        if (baseLights == null)
        {
            baseLights = new Dictionary<int, int>();
            TextAsset json = Resources.Load<TextAsset>("AOMigrator/WorldV07/map_environment");
            if (json != null)
            {
                try
                {
                    AOMapEnvironmentLibrary library = JsonUtility.FromJson<AOMapEnvironmentLibrary>(json.text);
                    if (library?.maps != null)
                        foreach (AOMapEnvironment entry in library.maps)
                            if (entry != null) baseLights[entry.mapNumber] = entry.baseLight;
                }
                catch (Exception ex) { Debug.LogWarning("[AO Lighting 2D v283] map_environment: " + ex.Message); }
            }
        }
        return baseLights.TryGetValue(map, out int value) ? value : 0;
    }

    // Igual que AOMapLighting.Decode: R en los bits 16-23, G en 8-15 y B en 0-7.
    static Color Decode(int packed) => new Color32(
        (byte)((packed >> 16) & 255), (byte)((packed >> 8) & 255), (byte)(packed & 255), 255);
}
