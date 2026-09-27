using UnityEngine;

// AOSkyV291 (nube, 27/09, pedido de Lucas): sol, luna y nubes para los mapas al aire libre.
// - El sol y la luna recorren el cielo según la hora del mundo (AOWorldManagerV07.CurrentWorldHour): salen por el
//   este, pasan por el sur (del lado de la cámara) y se ponen por el oeste. De ahí salen la dirección y el largo
//   de las sombras de los personajes (AOCharacterShadowsV291): largas al amanecer y al atardecer, cortas al
//   mediodía, siempre del lado opuesto a la luz.
// - Nubes: un campo de ruido anclado al mundo que se mueve con el viento (AOWindV290). Se interponen entre el sol
//   o la luna y el piso: su sombra pasa por el suelo, los techos y los personajes, y apaga la sombra de los
//   personajes que quedan debajo. Con lluvia o niebla el cielo se cubre y la luz se vuelve pareja.
// - Luna (efecto Purkinje, como el cine "noche americana"): de noche los rojos se apagan y el aire se tiñe de
//   azul, más lejos (arriba de la pantalla, hacia el cielo) que cerca. La luz de luna es tenue y solo toca el piso
//   donde se abren las nubes: manchas frías que se mueven con ellas.
// - Rayos entre nubes (calidad Alta y Ultra): haces suaves en los claros; cálidos de día y fríos de noche.
// No toca la luz del AO (AOMapLighting) ni la Mejorada (AOLighting2DV283): se dibuja encima, multiplicando (el
// negro sigue negro) o sumando muy poco. Solo al aire libre (mapa que sigue la hora y no es dungeon).
// Orden de dibujo: techos 20000+y < tinte de noche 30390 < sombras de nubes 30400 < luna 30410 < rayos 30420
//                  < agua de techos 30500 < ... (clima).
// Costo: 1 quad + ~40 manchas de nube + ~40 de luna (solo de noche, solo las visibles) + 3 rayos. Pools fijos.
[DefaultExecutionOrder(1001)]
public class AOSkyV291 : MonoBehaviour
{
    public static AOSkyV291 Instance { get; private set; }

    // Estado que leen las sombras de los personajes.
    public static bool Outdoor { get; private set; }
    public static bool Night { get; private set; }
    public static Vector2 ShadowDirection { get; private set; } = Vector2.up;   // hacia dónde cae la sombra
    public static float ShadowLength { get; private set; } = 0.5f;              // largo / alto del personaje
    public static float KeyShadowAlpha { get; private set; }                    // opacidad de la sombra del sol o la luna
    public static float Daylight { get; private set; } = 1f;                    // 0 noche ... 1 día
    public static float Coverage { get; private set; } = 0.35f;                 // 0 despejado ... 1 cubierto

    // Depuración (ventana "Clima"): >= 0 reemplaza la cobertura de nubes.
    public static float DebugCoverage = -1f;

    const float CloudSpacing = 7f;      // unidades entre manchas de nube
    const float CloudSize = 13f;        // tamaño de cada mancha (se superponen: sin costuras)
    const float SunShadowAlpha = 0.45f;
    const float MoonShadowAlpha = 0.25f;
    const int RayCount = 3;

    AOWorldManagerV07 world;
    float worldRetryAt;
    Vector2 cloudOffset;
    float sunStrength, moonStrength;

    SpriteRenderer grade;
    SpriteRenderer[] clouds;
    SpriteRenderer[] moonPatches;
    SpriteRenderer[] rays;
    float[] rayAlpha;
    Vector2[] rayPos;
    float rayPickAt;

    public int ActiveClouds { get; private set; }
    public int ActiveMoonPatches { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        Outdoor = false;
        Night = false;
        ShadowDirection = Vector2.up;
        ShadowLength = 0.5f;
        KeyShadowAlpha = 0f;
        Daylight = 1f;
        Coverage = 0.35f;
        DebugCoverage = -1f;
        multiplyMaterial = null;
        additiveMaterial = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (Instance != null)
            return;
        var go = new GameObject("AO Sky v291");
        DontDestroyOnLoad(go);
        go.AddComponent<AOSkyV291>();
        go.AddComponent<AOCharacterShadowsV291>();
        go.AddComponent<AOLivingLightV292>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // Nubosidad (0..1) sobre un punto del mundo; 0 si no hay cielo.
    public static float CloudAt(Vector2 position)
    {
        if (Instance == null || !Outdoor)
            return 0f;
        return Instance.Field((position - Instance.cloudOffset) / CloudSpacing);
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
        float deltaTime = Mathf.Min(Time.deltaTime, 0.1f);
        Outdoor = world.CurrentMapOutdoor;
        UpdateSkyLight(world.CurrentWorldHour, deltaTime);
        if (!Outdoor || cam == null || !cam.orthographic)
        {
            HideAll();
            return;
        }

        Vector2 center = cam.transform.position;
        transform.position = new Vector3(center.x, center.y, 0f);
        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;
        cloudOffset += new Vector2(0.35f + AOWindV290.Drift * 1.2f, 0.12f) * deltaTime;

        UpdateGrade(halfWidth, halfHeight);
        if (AOEffectsQualityV290.CloudShadows)
            LayoutClouds(center, halfWidth, halfHeight);
        else
            HideClouds();
        if (AOEffectsQualityV290.SkyRays)
            UpdateRays(center, halfWidth, halfHeight, deltaTime);
        else
            HideRays();
    }

    // ------------------------------------------------------------------ sol y luna

    void UpdateSkyLight(float hour, float deltaTime)
    {
        hour = Mathf.Repeat(hour, 24f);
        sunStrength = Smooth(Mathf.InverseLerp(5.5f, 7.5f, hour)) * (1f - Smooth(Mathf.InverseLerp(16.5f, 18.5f, hour)));
        moonStrength = 1f - sunStrength;
        Daylight = sunStrength;
        Night = sunStrength < 0.5f;

        // Recorrido: este (x+) -> sur (y-, del lado de la cámara) -> oeste (x-). De día el sol, de noche la luna.
        bool day = hour >= 6f && hour < 18f;
        float t = day ? (hour - 6f) / 12f : Mathf.Repeat(hour - 18f, 24f) / 12f;
        float phi = t * Mathf.PI;
        Vector2 toLight = new Vector2(Mathf.Cos(phi), -0.6f * Mathf.Sin(phi)).normalized;
        float elevation = Mathf.Lerp(6f, day ? 64f : 48f, Mathf.Sin(phi));
        ShadowDirection = -toLight;
        // Proporcional al cuerpo: de 0,5 (mediodía) a 1,3 veces la altura del personaje (amanecer y atardecer).
        ShadowLength = Mathf.Clamp(1f / Mathf.Tan(elevation * Mathf.Deg2Rad), 0.5f, 1.3f);

        // Cobertura: despejado con nubes sueltas; con lluvia, nieve o niebla se cubre (de a poco).
        float target = 0.35f;
        AOMapWeather weather = world != null ? world.CurrentWeather : null;
        if (weather != null)
        {
            if (weather.ActivePrecipitation != AOMapWeather.Precipitation.None)
                target = Mathf.Lerp(0.35f, 0.92f, Mathf.Clamp01(weather.Intensity));
            else if (weather.FogVisible)
                target = 0.7f;
        }
        if (DebugCoverage >= 0f)
            Coverage = DebugCoverage;
        else
            Coverage = Mathf.MoveTowards(Coverage, target, deltaTime * 0.05f);

        // Con el cielo cubierto la luz es difusa: casi no hay sombras marcadas.
        float direct = 1f - Smooth(Mathf.InverseLerp(0.6f, 0.92f, Coverage));
        KeyShadowAlpha = Outdoor ? Mathf.Max(sunStrength * SunShadowAlpha, moonStrength * MoonShadowAlpha) * direct : 0f;
    }

    static float Smooth(float x) => x * x * (3f - 2f * x);

    // ------------------------------------------------------------------ nubes

    // Campo de nubes en "espacio nube" (1 = una mancha). Dos octavas de Perlin, umbral según la cobertura.
    float Field(Vector2 q)
    {
        float n = 0.65f * Mathf.PerlinNoise(q.x * 0.23f + 11.3f, q.y * 0.23f + 5.7f) +
                  0.35f * Mathf.PerlinNoise(q.x * 0.61f + 2.1f, q.y * 0.61f + 9.4f);
        float threshold = 1f - Coverage;
        return Smooth(Mathf.Clamp01((n - threshold + 0.16f) / 0.32f));
    }

    void LayoutClouds(Vector2 cam, float halfWidth, float halfHeight)
    {
        float reach = CloudSize * 0.5f;
        int x0 = Mathf.FloorToInt((cam.x - halfWidth - reach - cloudOffset.x) / CloudSpacing);
        int x1 = Mathf.CeilToInt((cam.x + halfWidth + reach - cloudOffset.x) / CloudSpacing);
        int y0 = Mathf.FloorToInt((cam.y - halfHeight - reach - cloudOffset.y) / CloudSpacing);
        int y1 = Mathf.CeilToInt((cam.y + halfHeight + reach - cloudOffset.y) / CloudSpacing);
        int columns = x1 - x0 + 1;
        int count = columns * (y1 - y0 + 1);
        clouds = EnsurePool(clouds, count, "CloudShadow_", Multiply(), 30400, CloudSize / 8f);
        bool moonOn = moonStrength > 0.05f;
        if (moonOn)
            moonPatches = EnsurePool(moonPatches, count, "MoonLight_", Additive(), 30410, CloudSize / 8f);

        // De día la nube tapa al sol (sombra marcada); de noche tapa a la luna (más suave). Con el cielo cubierto
        // la sombra se vuelve pareja y la pone el tinte del clima, no las manchas.
        float partly = 1f - Smooth(Mathf.InverseLerp(0.62f, 0.92f, Coverage));
        float cloudShade = (sunStrength * 0.5f + moonStrength * 0.3f) * partly;
        Color day = new Color(0.6f, 0.64f, 0.76f);
        Color night = new Color(0.7f, 0.72f, 0.86f);
        Color shadeColor = Color.Lerp(night, day, sunStrength);
        float moonLight = moonStrength * 0.1f * (1f - 0.8f * Smooth(Mathf.InverseLerp(0.5f, 0.92f, Coverage)));
        int used = 0, lit = 0;
        for (int i = 0; i < count; i++)
        {
            int gx = x0 + i % columns;
            int gy = y0 + i / columns;
            // Cada mancha conserva su identidad al moverse: el ruido va por su índice.
            float cloud = Field(new Vector2(gx, gy));
            float jx = (Mathf.PerlinNoise(gx * 0.71f, gy * 0.37f) - 0.5f) * 2.5f;
            float jy = (Mathf.PerlinNoise(gx * 0.43f + 5.1f, gy * 0.59f) - 0.5f) * 2.5f;
            var position = new Vector3(gx * CloudSpacing + cloudOffset.x + jx, gy * CloudSpacing + cloudOffset.y + jy, 0f);

            float a = cloud * cloudShade;
            SpriteRenderer c = clouds[i];
            bool visible = a > 0.01f;
            if (c.enabled != visible) c.enabled = visible;
            if (visible)
            {
                c.transform.position = position;
                c.color = new Color(shadeColor.r, shadeColor.g, shadeColor.b, a);
                used++;
            }

            if (!moonOn) continue;
            // Luz de luna: solo donde se abre el cielo, tenue y fría.
            float gap = 1f - cloud;
            float m = gap * gap * moonLight;
            SpriteRenderer p = moonPatches[i];
            bool litPatch = m > 0.004f;
            if (p.enabled != litPatch) p.enabled = litPatch;
            if (litPatch)
            {
                p.transform.position = position;
                p.color = new Color(0.55f, 0.66f, 0.95f, m);
                lit++;
            }
        }
        Disable(clouds, count);
        if (moonPatches != null)
            Disable(moonPatches, moonOn ? count : 0);
        ActiveClouds = used;
        ActiveMoonPatches = lit;
    }

    // ------------------------------------------------------------------ noche

    // Noche americana: multiplica con un degradé (más frío arriba, lejos, como cielo). El negro sigue negro.
    void UpdateGrade(float halfWidth, float halfHeight)
    {
        // La Mejorada ya trae su ambiente azul de noche: ahí el tinte va a la mitad.
        float strength = moonStrength * (AOLighting2DV283.Enhanced ? 0.22f : 0.42f);
        if (strength <= 0.005f)
        {
            if (grade != null && grade.enabled) grade.enabled = false;
            return;
        }
        if (grade == null)
        {
            var go = new GameObject("NightGrade");
            go.transform.SetParent(transform, false);
            grade = go.AddComponent<SpriteRenderer>();
            grade.sprite = GradeSprite();
            grade.sortingOrder = 30390;
            Material material = Multiply();
            if (material != null) grade.sharedMaterial = material;
        }
        if (!grade.enabled) grade.enabled = true;
        grade.transform.localScale = new Vector3(halfWidth * 2f + 1f, halfHeight * 2f + 1f, 1f);
        grade.color = multiplyMissing ? new Color(0f, 0f, 0f, strength * 0.35f)
                                      : new Color(0.6f, 0.68f, 0.95f, strength);
    }

    // ------------------------------------------------------------------ rayos entre nubes

    void UpdateRays(Vector2 cam, float halfWidth, float halfHeight, float deltaTime)
    {
        if (rays == null)
        {
            rays = new SpriteRenderer[RayCount];
            rayAlpha = new float[RayCount];
            rayPos = new Vector2[RayCount];
            for (int i = 0; i < RayCount; i++)
            {
                var go = new GameObject("SkyRay_" + i);
                go.transform.SetParent(transform, false);
                rays[i] = go.AddComponent<SpriteRenderer>();
                rays[i].sprite = RaySprite();
                rays[i].sortingOrder = 30420;
                Material material = Additive();
                if (material != null) rays[i].sharedMaterial = material;
                rays[i].enabled = false;
            }
        }
        // Cada 3 s se buscan claros cerca del borde de una nube dentro de la vista (ahí se ven los haces).
        if (Time.time >= rayPickAt)
        {
            rayPickAt = Time.time + 3f;
            for (int i = 0; i < RayCount; i++)
            {
                Vector2 best = rayPos[i];
                float bestScore = -1f;
                for (int tries = 0; tries < 6; tries++)
                {
                    var candidate = new Vector2(cam.x + Random.Range(-halfWidth, halfWidth) * 0.8f,
                                                cam.y + Random.Range(-halfHeight, halfHeight) * 0.8f);
                    float here = CloudAt(candidate);
                    float around = CloudAt(candidate + new Vector2(2.5f, 1.5f));
                    float score = (1f - here) * around;
                    if (score > bestScore) { bestScore = score; best = candidate; }
                }
                if (bestScore > 0.25f) rayPos[i] = best;
            }
        }
        float partly = Smooth(Mathf.InverseLerp(0.15f, 0.45f, Coverage)) * (1f - Smooth(Mathf.InverseLerp(0.7f, 0.95f, Coverage)));
        float haze = 1f;
        AOMapWeather weather = world != null ? world.CurrentWeather : null;
        if (weather != null && (weather.FogVisible || weather.ActivePrecipitation != AOMapWeather.Precipitation.None))
            haze = 1.5f;                         // con aire húmedo los haces se notan más
        float strength = (sunStrength * 0.055f + moonStrength * 0.03f) * partly * haze;
        Color color = Color.Lerp(new Color(0.6f, 0.7f, 1f), new Color(1f, 0.9f, 0.7f), sunStrength);
        // Inclinados hacia el sol o la luna (de este a oeste según la hora).
        float tilt = ShadowDirection.x * 28f;
        for (int i = 0; i < RayCount; i++)
        {
            float target = strength * Mathf.Clamp01((1f - CloudAt(rayPos[i])) * 1.5f);
            rayAlpha[i] = Mathf.MoveTowards(rayAlpha[i], target, deltaTime * 0.03f);
            bool visible = rayAlpha[i] > 0.002f;
            if (rays[i].enabled != visible) rays[i].enabled = visible;
            if (!visible) continue;
            Transform t = rays[i].transform;
            t.position = new Vector3(rayPos[i].x, rayPos[i].y, 0f);
            t.localRotation = Quaternion.Euler(0f, 0f, tilt);
            t.localScale = new Vector3(1.6f, 1.1f, 1f);          // 1,6 x 8,8 unidades
            rays[i].color = new Color(color.r, color.g, color.b, rayAlpha[i]);
        }
    }

    // ------------------------------------------------------------------ utilidades

    void HideAll()
    {
        if (grade != null && grade.enabled) grade.enabled = false;
        HideClouds();
        HideRays();
    }

    void HideClouds()
    {
        Disable(clouds, 0);
        Disable(moonPatches, 0);
        ActiveClouds = 0;
        ActiveMoonPatches = 0;
    }

    void HideRays()
    {
        if (rays == null) return;
        for (int i = 0; i < rays.Length; i++)
        {
            rayAlpha[i] = 0f;
            if (rays[i].enabled) rays[i].enabled = false;
        }
    }

    static void Disable(SpriteRenderer[] pool, int from)
    {
        if (pool == null) return;
        for (int i = from; i < pool.Length; i++)
            if (pool[i].enabled) pool[i].enabled = false;
    }

    SpriteRenderer[] EnsurePool(SpriteRenderer[] pool, int count, string name, Material material, int order, float scale)
    {
        int old = pool == null ? 0 : pool.Length;
        if (old >= count) return pool;
        System.Array.Resize(ref pool, count);
        for (int i = old; i < count; i++)
        {
            var go = new GameObject(name + i);
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = CloudSprite();
            renderer.sortingOrder = order;
            if (material != null) renderer.sharedMaterial = material;
            renderer.enabled = false;
            pool[i] = renderer;
        }
        return pool;
    }

    static Material multiplyMaterial, additiveMaterial;
    static bool multiplyMissing;
    static Sprite cloudSprite, gradeSprite, raySprite;

    static Material Multiply()
    {
        if (multiplyMaterial == null && !multiplyMissing)
        {
            Shader shader = Resources.Load<Shader>("AOMigrator/WorldV07/AOWeatherMultiply");
            if (shader != null && shader.isSupported) multiplyMaterial = new Material(shader);
            else multiplyMissing = true;
        }
        return multiplyMaterial;
    }

    static Material Additive()
    {
        if (additiveMaterial == null)
        {
            Shader shader = Resources.Load<Shader>("AOMigrator/WorldV07/AOParticleAdditive");
            if (shader != null) additiveMaterial = new Material(shader);
        }
        return additiveMaterial;
    }

    // Mancha de nube: borde muy suave (64x64 px = 8 unidades a escala 1).
    static Sprite CloudSprite()
    {
        if (cloudSprite != null) return cloudSprite;
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "AO Cloud Shadow", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float f = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                f = f * f * (3f - 2f * f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(f * 255f));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        cloudSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 8f);
        return cloudSprite;
    }

    // Degradé vertical del tinte de noche: fuerte arriba (lejos, cielo), más suave abajo (cerca).
    static Sprite GradeSprite()
    {
        if (gradeSprite != null) return gradeSprite;
        const int h = 32;
        var texture = new Texture2D(h, h, TextureFormat.RGBA32, false) { name = "AO Night Grade", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[h * h];
        for (int y = 0; y < h; y++)
        {
            var c = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Lerp(0.55f, 1f, (float)y / (h - 1)) * 255f));
            for (int x = 0; x < h; x++)
                pixels[y * h + x] = c;
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        // 32 x 32 px con 32 px por unidad = 1 x 1 unidad; la escala del quad lo lleva al tamaño de la pantalla.
        gradeSprite = Sprite.Create(texture, new Rect(0, 0, h, h), new Vector2(0.5f, 0.5f), h);
        return gradeSprite;
    }

    // Haz de luz: angosto, suave a los costados y que se apaga en las puntas (16x128 px = 1 x 8 unidades).
    static Sprite RaySprite()
    {
        if (raySprite != null) return raySprite;
        const int w = 16, h = 128;
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "AO Sky Ray", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float across = 1f - Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                float along = (float)y / (h - 1);
                float a = across * across * Mathf.Sin(along * Mathf.PI) * Mathf.Lerp(0.6f, 1f, along);
                pixels[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        raySprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
        return raySprite;
    }
}
