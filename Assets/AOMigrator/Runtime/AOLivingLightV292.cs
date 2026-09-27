using UnityEngine;

// AOLivingLightV292 (nube, 27/09, pedido de Lucas): que la luz y los efectos sean protagonistas y le peguen a los
// personajes y al entorno.
// - Faroles y antorchas del mapa (las mismas luces del AO) con halo en el piso que respira y parpadea; cuanto más
//   oscuro (noche o dungeon), más se notan. Los personajes cerca se tiñen del color de esa luz y parpadean con ella
//   (AOCharacterShadowsV291 usa Flicker y LightColor con la misma semilla por luz).
// - Luciérnagas de noche al aire libre, sin lluvia (calidad Alta y Ultra): enjambre que busca lo oscuro
//   (AOFirefliesV292).
// - Relámpagos en tormenta (lluvia con intensidad >= 1,2): doble destello frío sobre todo el mundo; durante el
//   destello los personajes se iluminan y proyectan una sombra corta y dura desde el rayo.
// No re-ilumina el mapa (regla del clima): halos y destello son sprites aditivos con pool fijo.
// Orden de dibujo: piso < halos -18900 < objetos y personajes < ... < luciérnagas 30430 < destello 31500 < gotas.
[DefaultExecutionOrder(1003)]
public partial class AOLivingLightV292 : MonoBehaviour
{
    public static AOLivingLightV292 Instance { get; private set; }

    // 0 (día pleno) ... 1 (oscuridad total): cuánto se notan las luces del mapa.
    public static float Darkness { get; private set; }
    // Relámpago: fuerza del destello (0..1) y hacia dónde caen las sombras mientras dura.
    public static float FlashStrength { get; private set; }
    public static Vector2 FlashShadowDirection { get; private set; } = Vector2.up;

    // Depuración (ventana "Clima"): un relámpago ya.
    public static bool DebugStrikeNow;

    const int MaxGlows = 24;

    AOWorldManagerV07 world;
    float worldRetryAt;
    SpriteRenderer[] glows;
    SpriteRenderer flash;
    float nextStrike = 8f, strikeAt = -10f, strikePower;

    public int ActiveGlows { get; private set; }

    // Luces a la vista (para que las luciérnagas las eviten), llenado en UpdateGlows.
    readonly Vector2[] lampPos = new Vector2[MaxGlows];
    readonly float[] lampRadius = new float[MaxGlows];
    int lampCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        Darkness = 0f;
        FlashStrength = 0f;
        FlashShadowDirection = Vector2.up;
        DebugStrikeNow = false;
        glowSprite = null;
        additive = null;
    }

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    // Parpadeo de una luz (0,85..1,15), igual para su halo y para los personajes que ilumina.
    public static float Flicker(int seed) =>
        1f + 0.3f * (Mathf.PerlinNoise(Time.time * 5.5f, seed * 0.173f) - 0.5f) +
        0.08f * (Mathf.PerlinNoise(Time.time * 17f, seed * 0.311f + 3f) - 0.5f);

    public static int Seed(AOMapLightPlacement light) => light.x * 131 + light.y * 17;

    // Color de la luz del AO (0xRRGGBB), con el blanco puro llevado a un cálido de farol.
    public static Color LightColor(AOMapLightPlacement light)
    {
        int c = light.color & 0xFFFFFF;
        var color = new Color(((c >> 16) & 255) / 255f, ((c >> 8) & 255) / 255f, (c & 255) / 255f);
        float saturation = color.maxColorComponent - Mathf.Min(color.r, Mathf.Min(color.g, color.b));
        return saturation < 0.08f ? Color.Lerp(color, new Color(1f, 0.82f, 0.55f), 0.55f) : color;
    }

    void LateUpdate()
    {
        if (world == null)
        {
            if (Time.unscaledTime < worldRetryAt) return;
            worldRetryAt = Time.unscaledTime + 1f;
            world = UnityEngine.Object.FindFirstObjectByType<AOWorldManagerV07>();
            if (world == null) return;
        }
        Camera cam = AOActionBarV260.GameCamera;
        if (cam == null || !cam.orthographic || world.IsLoading)
        {
            HideAll();
            return;
        }
        Vector2 center = cam.transform.position;
        transform.position = new Vector3(center.x, center.y, 0f);
        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;
        float deltaTime = Mathf.Min(Time.deltaTime, 0.1f);

        Darkness = ComputeDarkness();
        UpdateGlows(center, halfWidth, halfHeight);
        UpdateLightning(halfWidth, halfHeight);
        UpdateFireflies(center, halfWidth, halfHeight, deltaTime);
    }

    float ComputeDarkness()
    {
        if (AOSkyV291.Outdoor)
            return 1f - Mathf.Lerp(0.47f, 1f, AOSkyV291.Daylight);          // noche del AO: 120/255
        int packed = world.CurrentBaseLight;
        float ambient = packed == 0
            ? ((Color)AOMapLighting.DayColor(world.CurrentWorldHour)).maxColorComponent
            : Mathf.Max((packed >> 16) & 255, (packed >> 8) & 255, packed & 255) / 255f;
        return Mathf.Clamp01(1f - ambient);
    }

    // ------------------------------------------------------------------ halos de faroles y antorchas

    void UpdateGlows(Vector2 center, float halfWidth, float halfHeight)
    {
        int used = 0;
        AOMapLightPlacement[] lights = world.CurrentMapLights;
        lampCount = 0;
        if (lights != null)
            for (int i = 0; i < lights.Length && lampCount < MaxGlows; i++)
            {
                int r = lights[i].range >= 100 ? lights[i].range - 99 : lights[i].range;
                var p = new Vector2(lights[i].x - 0.5f, -lights[i].y + 0.5f);
                if (r <= 0 || Mathf.Abs(p.x - center.x) > halfWidth + 6f || Mathf.Abs(p.y - center.y) > halfHeight + 6f) continue;
                lampPos[lampCount] = p;
                lampRadius[lampCount] = r + 0.5f;
                lampCount++;
            }
        float strength = Darkness * (AOLighting2DV283.Enhanced ? 0.45f : 1f);   // la Mejorada ya ilumina el piso
        if (AOEffectsQualityV290.LightGlows && lights != null && strength > 0.05f)
        {
            if (glows == null)
            {
                glows = new SpriteRenderer[MaxGlows];
                for (int i = 0; i < MaxGlows; i++)
                    glows[i] = MakeRenderer("LightGlow_" + i, -18900);
            }
            for (int i = 0; i < lights.Length && used < MaxGlows; i++)
            {
                AOMapLightPlacement light = lights[i];
                int range = light.range >= 100 ? light.range - 99 : light.range;
                if (range <= 0) continue;
                var position = new Vector2(light.x - 0.5f, -light.y + 0.5f);
                if (Mathf.Abs(position.x - center.x) > halfWidth + range || Mathf.Abs(position.y - center.y) > halfHeight + range)
                    continue;
                float flicker = Flicker(Seed(light));
                SpriteRenderer glow = glows[used++];
                if (!glow.enabled) glow.enabled = true;
                glow.transform.position = new Vector3(position.x, position.y, 0f);
                float size = (range + 0.5f) * 2f * (0.97f + 0.06f * flicker);        // el sprite mide 1 unidad
                glow.transform.localScale = new Vector3(size, size, 1f);
                Color color = LightColor(light);
                glow.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(0.24f * strength * flicker));
            }
        }
        if (glows != null)
            for (int i = used; i < glows.Length; i++)
                if (glows[i].enabled) glows[i].enabled = false;
        ActiveGlows = used;
    }

    // ------------------------------------------------------------------ relámpagos

    void UpdateLightning(float halfWidth, float halfHeight)
    {
        AOMapWeather weather = world.CurrentWeather;
        bool storm = AOSkyV291.Outdoor && weather != null &&
                     weather.ActivePrecipitation == AOMapWeather.Precipitation.Rain && weather.Intensity >= 1.2f;
        float now = Time.time;
        if ((storm && now >= nextStrike) || DebugStrikeNow)
        {
            DebugStrikeNow = false;
            strikeAt = now;
            strikePower = Random.Range(0.6f, 1f);
            nextStrike = now + Random.Range(6f, 16f);
            float azimuth = Random.Range(0f, Mathf.PI * 2f);
            FlashShadowDirection = new Vector2(Mathf.Cos(azimuth), Mathf.Sin(azimuth));
        }
        else if (!storm && now >= nextStrike)
            nextStrike = now + 4f;

        // Doble destello: golpe corto y un eco más débil (como un rayo real), después se apaga.
        float t = now - strikeAt;
        float envelope = 0f;
        if (t >= 0f && t < 0.9f)
        {
            envelope = Mathf.Exp(-t / 0.06f);
            if (t > 0.16f) envelope += 0.7f * Mathf.Exp(-(t - 0.16f) / 0.1f);
        }
        FlashStrength = Mathf.Clamp01(envelope * strikePower);
        if (FlashStrength <= 0.01f)
        {
            if (flash != null && flash.enabled) flash.enabled = false;
            return;
        }
        if (flash == null)
            flash = MakeRenderer("LightningFlash", 31500);
        if (!flash.enabled) flash.enabled = true;
        flash.transform.localPosition = Vector3.zero;
        flash.transform.localScale = new Vector3(halfWidth * 2f + 1f, halfHeight * 2f + 1f, 1f);
        float alpha = FlashStrength * (AOLighting2DV283.Enhanced ? 0.3f : 0.42f);
        flash.color = new Color(0.72f, 0.8f, 1f, alpha);
        flash.sprite = FlatSprite();
    }

    // ------------------------------------------------------------------ utilidades

    void HideAll()
    {
        if (glows != null) for (int i = 0; i < glows.Length; i++) if (glows[i].enabled) glows[i].enabled = false;
        HideFireflies();
        if (flash != null && flash.enabled) flash.enabled = false;
        FlashStrength = 0f;
        ActiveGlows = 0;
    }

    SpriteRenderer MakeRenderer(string name, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = GlowSprite();
        renderer.sortingOrder = order;
        Material material = Additive();
        if (material != null) renderer.sharedMaterial = material;
        renderer.enabled = false;
        return renderer;
    }

    static Material additive;
    static Sprite glowSprite, flatSprite;

    public static Material Additive()
    {
        if (additive == null)
        {
            Shader shader = Resources.Load<Shader>("AOMigrator/WorldV07/AOParticleAdditive");
            if (shader != null) additive = new Material(shader);
        }
        return additive;
    }

    // Halo: centro brillante y caída suave (64x64 px = 1 unidad).
    static Sprite GlowSprite()
    {
        if (glowSprite != null) return glowSprite;
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "AO Light Glow", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float f = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(f * f * f * 255f));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        glowSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return glowSprite;
    }

    // Destello: plano (4x4 px = 1 unidad; la escala lo lleva al tamaño de la pantalla).
    static Sprite FlatSprite()
    {
        if (flatSprite != null) return flatSprite;
        var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "AO Lightning", filterMode = FilterMode.Point };
        var pixels = new Color32[16];
        for (int i = 0; i < 16; i++) pixels[i] = new Color32(255, 255, 255, 255);
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        flatSprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        return flatSprite;
    }
}
