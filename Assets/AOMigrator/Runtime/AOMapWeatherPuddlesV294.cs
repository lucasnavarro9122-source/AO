using System.Collections.Generic;
using UnityEngine;

// AOMapWeatherPuddlesV294 (nube, 27/09, pedido de Lucas): charcos creíbles que viven con el entorno.
// Referencias: S. Lagarde, "Water drop" (superficies mojadas: el suelo mojado se oscurece y satura, el agua casi no
// refleja mirada desde arriba y refleja más hacia los bordes, brillos que titilan con las ondas) y shaders de agua
// 2D en pixel art (reflejos cerca de los bordes con ondulación).
// Capas de cada charco, todas a ras del piso (debajo de árboles, objetos y personajes):
//   halo de tierra húmeda (multiplica, más grande y suave) -> agua (multiplica poco: se ve la textura del piso) ->
//   borde de cielo (reflejo, más fuerte en el borde lejano: efecto Fresnel visto desde arriba) -> brillos que titilan
//   -> reflejos (personajes y faroles, solo dentro del charco con SpriteMask).
// Interacción: quien camina por encima lo agita: ondas desde los pies, el agua se corre hacia donde va el paso y
// vuelve como un resorte, y el reflejo se ondula y se desarma hasta que el agua se calma y vuelve a tomar forma.
// Charcos fijos por mapa (hash de la casilla, ~6 % de la tierra caminable sin techo). Calidad: charcos desde Medium;
// reflejos y brillos desde High. Pools fijos.
public partial class AOMapWeather
{
    struct Puddle { public Vector2 center; public float width, height; public int shape, index; }

    const int MaxPuddles = 40, MaxGlints = 24, MaxLampReflections = 12;

    static readonly List<Puddle> visiblePuddles = new List<Puddle>(MaxPuddles);
    static readonly float[] visibleDisturb = new float[MaxPuddles];

    Puddle[] mapPuddles;
    float[] puddleDisturb, puddleStepAt;
    Vector2[] puddleOffset, puddleVelocity, puddleLastFeet;
    SpriteRenderer[] puddleDamp, puddleWater, puddleRim;
    SpriteMask[] puddleMask;
    SpriteRenderer[] glints;
    SpriteRenderer[] lampReflections;

    public int ActivePuddles { get; private set; }

    // ¿Hay un charco cerca de ese punto? (reflejos de los personajes)
    public static bool PuddleNear(Vector2 position, float radius) => DisturbanceAt(position, radius) >= 0f;

    // Agitación del charco que contiene ese punto (0 calmo ... 1 recién pisado); -1 si no hay charco.
    public static float DisturbanceAt(Vector2 position, float radius = 0f)
    {
        if (Wetness < 0.1f) return -1f;
        for (int i = 0; i < visiblePuddles.Count; i++)
        {
            Vector2 d = position - visiblePuddles[i].center;
            float nx = d.x / (visiblePuddles[i].width * 0.5f + radius), ny = d.y / (visiblePuddles[i].height * 0.5f + radius);
            if (nx * nx + ny * ny < 1f) return visibleDisturb[i];
        }
        return -1f;
    }

    void UpdatePuddles(Vector2 cam, float halfWidth, float halfHeight)
    {
        visiblePuddles.Clear();
        ActivePuddles = 0;
        bool show = Wetness > 0.02f && AOEffectsQualityV290.Details && world != null;
        if (show && mapPuddles == null)
            BuildPuddles();
        int used = 0, glintsUsed = 0;
        float deltaTime = Mathf.Min(Time.deltaTime, 0.1f);
        float time = Time.time;
        bool fine = AOEffectsQualityV290.Level >= AOEffectsQuality.High;
        if (show && mapPuddles != null)
        {
            EnsurePuddlePool();
            float grow = Mathf.SmoothStep(0f, 1f, Wetness);
            for (int k = 0; k < mapPuddles.Length && used < MaxPuddles; k++)
            {
                Puddle p = mapPuddles[k];
                if (Mathf.Abs(p.center.x - cam.x) > halfWidth + 2f || Mathf.Abs(p.center.y - cam.y) > halfHeight + 2f)
                    continue;
                float w = p.width * (0.4f + 0.6f * grow), h = p.height * (0.4f + 0.6f * grow);
                Agitate(k, p.center, w, h, deltaTime, time);
                float disturb = puddleDisturb[k];
                // El agua se corre hacia donde fue el paso y vuelve (resorte), y se estira un poco al moverse.
                Vector2 offset = puddleOffset[k] * 0.14f;
                float stretch = 1f + Mathf.Min(0.2f, puddleOffset[k].magnitude * 0.15f);
                var center = new Vector3(p.center.x + offset.x, p.center.y + offset.y, 0f);
                var scale = new Vector3(w * stretch, h / Mathf.Sqrt(stretch), 1f);
                Sprite shape = PuddleShapes()[p.shape];

                SpriteRenderer damp = puddleDamp[used], water = puddleWater[used], rim = puddleRim[used];
                if (!water.enabled) { damp.enabled = true; water.enabled = true; rim.enabled = true; }
                // Tierra húmeda alrededor: más oscura y un poco más saturada, no azul.
                damp.sprite = shape;
                damp.transform.position = new Vector3(p.center.x, p.center.y, 0f);
                damp.transform.localScale = new Vector3(w * 1.45f, h * 1.5f, 1f);
                damp.color = new Color(0.78f, 0.74f, 0.7f, 0.32f * grow);
                // Agua: casi transparente vista desde arriba (se ve la textura del piso), algo más fría y oscura.
                water.sprite = shape;
                water.transform.position = center;
                water.transform.localScale = scale;
                water.color = new Color(0.68f, 0.72f, 0.8f, 0.48f * grow);
                // Borde de cielo (Fresnel desde arriba): el reflejo aparece hacia el borde lejano.
                rim.transform.position = center;
                rim.transform.localScale = scale;
                float sky = Mathf.Lerp(0.6f, 1f, openSky) * (1f - 0.5f * disturb);
                rim.color = new Color(0.55f, 0.62f, 0.78f, 0.16f * grow * sky);

                SpriteMask mask = puddleMask[used];
                if (mask.enabled != fine) mask.enabled = fine;
                if (fine)
                {
                    mask.sprite = shape;
                    mask.transform.position = center;
                    mask.transform.localScale = scale;
                }
                // Brillos que titilan en el agua (más con lluvia o recién pisado).
                if (fine && glintsUsed < MaxGlints)
                {
                    float n = Mathf.PerlinNoise(time * 2.3f + k * 1.7f, k * 0.37f);
                    float a = Mathf.Clamp01((n - 0.55f) * 3.5f) * (0.25f + 0.35f * Mathf.Max(disturb, Mathf.Clamp01(intensity))) * grow;
                    if (a > 0.02f)
                    {
                        SpriteRenderer g = glints[glintsUsed++];
                        if (!g.enabled) g.enabled = true;
                        float gx = (Mathf.PerlinNoise(k * 0.9f, time * 0.2f) - 0.5f) * w * 0.7f;
                        float gy = (Mathf.PerlinNoise(time * 0.2f, k * 0.9f) - 0.5f) * h * 0.6f;
                        g.transform.position = new Vector3(center.x + gx, center.y + gy, 0f);
                        g.color = new Color(0.9f, 0.95f, 1f, a);
                    }
                }
                visiblePuddles.Add(new Puddle { center = p.center, width = scale.x, height = scale.y, shape = p.shape, index = k });
                visibleDisturb[used] = disturb;
                used++;
            }
            UpdateLampReflections(cam, halfWidth, halfHeight, fine, grow, time);
        }
        if (puddleWater != null)
            for (int i = used; i < puddleWater.Length; i++)
            {
                if (puddleWater[i].enabled) { puddleDamp[i].enabled = false; puddleWater[i].enabled = false; puddleRim[i].enabled = false; }
                if (puddleMask[i].enabled) puddleMask[i].enabled = false;
            }
        if (glints != null)
            for (int i = glintsUsed; i < glints.Length; i++)
                if (glints[i].enabled) glints[i].enabled = false;
        if (!show && lampReflections != null)
            for (int i = 0; i < lampReflections.Length; i++)
                if (lampReflections[i].enabled) lampReflections[i].enabled = false;
        ActivePuddles = used;
    }

    // Pisadas: ondas desde los pies, empuje del agua hacia donde va el paso y calma de a poco.
    void Agitate(int k, Vector2 center, float w, float h, float deltaTime, float time)
    {
        for (int c = 0; c < AOCharacterShadowsV291.VisibleCount; c++)
        {
            Vector2 feet = AOCharacterShadowsV291.VisibleFeet[c];
            Vector2 d = feet - center;
            float nx = d.x / (w * 0.5f), ny = d.y / (h * 0.5f);
            if (nx * nx + ny * ny >= 1f) continue;
            Vector2 step = feet - puddleLastFeet[k];
            puddleLastFeet[k] = feet;
            if (step.sqrMagnitude < 0.0004f || step.sqrMagnitude > 4f) continue;        // quieto (o recién llegó)
            puddleDisturb[k] = 1f;
            puddleVelocity[k] += step.normalized * 3f;
            if (time >= puddleStepAt[k])
            {
                puddleStepAt[k] = time + 0.22f;
                Splash(feet, 1.9f);                                  // onda grande desde el pie
            }
        }
        // Resorte amortiguado: el agua vuelve a su lugar en ~1 s.
        Vector2 accel = -30f * puddleOffset[k] - 5f * puddleVelocity[k];
        puddleVelocity[k] += accel * deltaTime;
        puddleOffset[k] += puddleVelocity[k] * deltaTime;
        puddleDisturb[k] = Mathf.MoveTowards(puddleDisturb[k], 0f, deltaTime / 1.6f);
    }

    // De noche, los faroles cercanos se reflejan en los charcos (mancha cálida que parpadea, solo dentro del agua).
    void UpdateLampReflections(Vector2 cam, float halfWidth, float halfHeight, bool fine, float grow, float time)
    {
        int used = 0;
        AOMapLightPlacement[] lights = world.CurrentMapLights;
        float strength = AOLivingLightV292.Darkness * grow;
        if (fine && lights != null && strength > 0.05f)
        {
            for (int i = 0; i < lights.Length && used < MaxLampReflections; i++)
            {
                AOMapLightPlacement light = lights[i];
                var p = new Vector2(light.x - 0.5f, -light.y + 0.5f);
                if (Mathf.Abs(p.x - cam.x) > halfWidth + 3f || Mathf.Abs(p.y - cam.y) > halfHeight + 3f) continue;
                // El reflejo de una luz alta cae por debajo de su base (espejo del poste).
                Vector2 mirrored = p + new Vector2(0f, -1.1f);
                float disturb = Mathf.Max(0f, DisturbanceAt(mirrored, 1f));
                SpriteRenderer r = lampReflections[used++];
                if (!r.enabled) r.enabled = true;
                r.transform.position = new Vector3(mirrored.x + Mathf.Sin(time * 14f) * 0.08f * disturb, mirrored.y, 0f);
                r.transform.localScale = new Vector3(1.1f, 1.8f * (1f - 0.3f * disturb), 1f);
                Color c = AOLivingLightV292.LightColor(light);
                r.color = new Color(c.r, c.g, c.b, 0.5f * strength * AOLivingLightV292.Flicker(AOLivingLightV292.Seed(light)));
            }
        }
        if (lampReflections != null)
            for (int i = used; i < lampReflections.Length; i++)
                if (lampReflections[i].enabled) lampReflections[i].enabled = false;
    }

    [System.Serializable] class PuddleSpotsFile { public int version; public PuddleSpotsMap[] maps; }
    [System.Serializable] class PuddleSpotsMap { public int map; public int[] spots; }
    static PuddleSpotsFile puddleSpots;
    static bool puddleSpotsLoaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetPuddleSpots()
    {
        puddleSpots = null;
        puddleSpotsLoaded = false;
    }

    // Charcos del mapa: primero los de Tools/puddle_spots.py (en caminos de tierra y piedra, según el color real del
    // piso); si el mapa no está en el archivo, un reparto por hash en la tierra caminable.
    void BuildPuddles()
    {
        var list = new List<Puddle>();
        if (!puddleSpotsLoaded)
        {
            puddleSpotsLoaded = true;
            TextAsset source = Resources.Load<TextAsset>("AOMigrator/WorldV07/puddle_spots");
            if (source != null) puddleSpots = JsonUtility.FromJson<PuddleSpotsFile>(source.text);
        }
        if (puddleSpots != null && puddleSpots.maps != null)
            foreach (PuddleSpotsMap entry in puddleSpots.maps)
            {
                if (entry == null || entry.map != mapNumber || entry.spots == null) continue;
                for (int i = 0; i + 3 < entry.spots.Length; i += 4)
                {
                    int x = entry.spots[i], y = entry.spots[i + 1];
                    float size = entry.spots[i + 2] / 100f;
                    uint h = (uint)(x * 2654435761u) ^ (uint)(y * 40503);
                    float jx = ((h >> 8) & 255) / 255f - 0.5f, jy = ((h >> 16) & 255) / 255f - 0.5f;
                    list.Add(new Puddle
                    {
                        center = new Vector2(x - 0.5f + jx * 0.4f, -y + 0.5f + jy * 0.3f),
                        width = size * 1.3f,
                        height = size * 0.7f,
                        shape = Mathf.Clamp(entry.spots[i + 3], 0, 2),
                    });
                }
                break;
            }
        if (list.Count > 0)
        {
            FinishPuddles(list);
            return;
        }
        for (int y = 1; y <= 100; y++)
            for (int x = 1; x <= 100; x++)
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(mapNumber * 83492791);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                if (h % 100 >= 6 || !world.IsPuddleGround(x, y)) continue;
                float jx = ((h >> 8) & 255) / 255f - 0.5f, jy = ((h >> 16) & 255) / 255f - 0.5f;
                float size = 0.7f + ((h >> 24) & 255) / 255f * 0.9f;
                list.Add(new Puddle
                {
                    center = new Vector2(x - 0.5f + jx * 0.5f, -y + 0.5f + jy * 0.4f),
                    width = size * 1.3f,
                    height = size * 0.7f,
                    shape = (int)((h >> 5) % 3),
                });
            }
        FinishPuddles(list);
    }

    void FinishPuddles(List<Puddle> list)
    {
        mapPuddles = list.ToArray();
        int n = mapPuddles.Length;
        puddleDisturb = new float[n];
        puddleStepAt = new float[n];
        puddleOffset = new Vector2[n];
        puddleVelocity = new Vector2[n];
        puddleLastFeet = new Vector2[n];
    }

    int mapNumber => environment != null ? environment.mapNumber : 0;

    bool InPuddle(Vector2 position)
    {
        for (int i = 0; i < visiblePuddles.Count; i++)
        {
            Vector2 d = position - visiblePuddles[i].center;
            float nx = d.x / (visiblePuddles[i].width * 0.5f), ny = d.y / (visiblePuddles[i].height * 0.5f);
            if (nx * nx + ny * ny < 0.8f) return true;
        }
        return false;
    }

    void EnsurePuddlePool()
    {
        if (puddleWater != null) return;
        puddleDamp = new SpriteRenderer[MaxPuddles];
        puddleWater = new SpriteRenderer[MaxPuddles];
        puddleRim = new SpriteRenderer[MaxPuddles];
        puddleMask = new SpriteMask[MaxPuddles];
        Material multiply = Multiply(), additive = Additive();
        for (int i = 0; i < MaxPuddles; i++)
        {
            puddleDamp[i] = PuddlePart("PuddleDamp_" + i, PuddleShapes()[0], multiply, -18970);
            puddleWater[i] = PuddlePart("PuddleWater_" + i, PuddleShapes()[0], multiply, -18960);
            puddleRim[i] = PuddlePart("PuddleSky_" + i, RimSprite(), additive, -18955);
            var go = new GameObject("PuddleMask_" + i);
            go.transform.SetParent(transform, false);
            SpriteMask mask = go.AddComponent<SpriteMask>();
            mask.sprite = PuddleShapes()[0];
            mask.alphaCutoff = 0.35f;
            mask.enabled = false;
            puddleMask[i] = mask;
        }
        glints = new SpriteRenderer[MaxGlints];
        for (int i = 0; i < MaxGlints; i++)
        {
            glints[i] = PuddlePart("PuddleGlint_" + i, Droplet(), additive, -18950);
            glints[i].transform.localScale = new Vector3(0.7f, 0.35f, 1f);
        }
        lampReflections = new SpriteRenderer[MaxLampReflections];
        for (int i = 0; i < MaxLampReflections; i++)
        {
            lampReflections[i] = PuddlePart("PuddleLamp_" + i, PuddleShapes()[0], additive, -18945);
            lampReflections[i].maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }
    }

    SpriteRenderer PuddlePart(string name, Sprite sprite, Material material, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        if (material != null) renderer.sharedMaterial = material;
        renderer.enabled = false;
        return renderer;
    }

    static Sprite[] puddleShapes;
    static Sprite rimSprite;

    // 3 formas de charco: borde irregular con ruido fractal y caída suave de 2-3 px (64x64 px = 1 unidad).
    static Sprite[] PuddleShapes()
    {
        if (puddleShapes != null) return puddleShapes;
        puddleShapes = new Sprite[3];
        const int size = 64;
        for (int s = 0; s < 3; s++)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "AO Puddle " + s, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float angle = Mathf.Atan2(dy, dx);
                    float cx = Mathf.Cos(angle), cy = Mathf.Sin(angle);
                    float edge = 0.7f + 0.16f * Mathf.PerlinNoise(cx * 1.6f + 3f + s * 11f, cy * 1.6f + 7f) +
                                 0.08f * Mathf.PerlinNoise(cx * 4.2f + 9f + s * 5f, cy * 4.2f + 1f);
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / edge;
                    float a = Mathf.Clamp01((1f - r) * 9f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            puddleShapes[s] = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
        return puddleShapes;
    }

    // Borde de cielo: banda fina cerca del borde, más fuerte arriba (el borde lejano, donde el agua refleja más).
    static Sprite RimSprite()
    {
        if (rimSprite != null) return rimSprite;
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "AO Puddle Sky", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / 0.78f;
                float band = Mathf.Clamp01(1f - Mathf.Abs(r - 0.82f) * 6f);
                float far = Mathf.Lerp(0.25f, 1f, (float)y / (size - 1));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(band * far * 255f));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        rimSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return rimSprite;
    }
}
