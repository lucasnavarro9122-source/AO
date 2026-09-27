using System.Collections.Generic;
using UnityEngine;

// AOMapWeatherNatureV293 (nube, 27/09, pedido de Lucas): la lluvia con los árboles y el suelo.
// - Árboles (objetos tipo 4 del AO): la gota que cae sobre la copa no salpica el piso; se desliza entre las hojas
//   (un brillo corto que baja con un leve vaivén) y gotea desde el borde de abajo de la copa al piso. Debajo del
//   árbol llueve menos: ahí no hay salpicaduras directas, solo el goteo de las hojas.
// - Charcos: con la lluvia el suelo se moja de a poco (~45 s) y aparecen charcos en tierra caminable sin techo
//   (siempre en los mismos lugares de cada mapa); se secan de a poco (~2 min) cuando para. Mojan el piso (más oscuro
//   y con un brillo frío de cielo), las gotas que caen adentro hacen ondas más grandes, y reflejan a los personajes
//   que están cerca (AOCharacterShadowsV291: copia dada vuelta, visible solo dentro del charco con SpriteMask).
// - Fluidez: las gotas aparecen y aterrizan con fundidos cortos, y las salpicaduras en el piso sueltan 1-2 gotitas.
// Calidad: charcos desde Medium; reflejos desde High; agua de hojas con el pool de techos (0 en Low).
public partial class AOMapWeather
{
    struct Puddle { public Vector2 center; public float width, height; }

    const float WetSeconds = 45f, DrySeconds = 120f;
    const int MaxTrees = 32, MaxPuddles = 40;

    // Mojado del suelo (0 seco ... 1 empapado): lo leen los reflejos de los personajes.
    public static float Wetness { get; private set; }
    // Depuración (ventana "Clima"): moja el suelo al instante.
    public static bool DebugSoakNow;
    static readonly List<Puddle> visiblePuddles = new List<Puddle>(MaxPuddles);

    readonly Vector2[] canopyCenter = new Vector2[MaxTrees];
    readonly float[] canopyRadius = new float[MaxTrees];
    int canopyCount;
    Puddle[] mapPuddles;
    SpriteRenderer[] puddleWet, puddleSheen;
    SpriteMask[] puddleMask;

    public int ActivePuddles { get; private set; }

    // ¿Hay un charco cerca de ese punto? (para los reflejos de los personajes)
    public static bool PuddleNear(Vector2 position, float radius)
    {
        if (Wetness < 0.1f) return false;
        for (int i = 0; i < visiblePuddles.Count; i++)
        {
            Vector2 d = position - visiblePuddles[i].center;
            if (Mathf.Abs(d.x) < visiblePuddles[i].width * 0.5f + radius &&
                Mathf.Abs(d.y) < visiblePuddles[i].height * 0.5f + radius)
                return true;
        }
        return false;
    }

    void UpdateNature(float deltaTime, Vector2 cam, float halfWidth, float halfHeight)
    {
        bool raining = active == Precipitation.Rain && intensity > 0f;
        if (DebugSoakNow) { DebugSoakNow = false; Wetness = 1f; }
        Wetness = raining ? Mathf.MoveTowards(Wetness, 1f, deltaTime * Mathf.Clamp01(intensity) / WetSeconds)
                          : Mathf.MoveTowards(Wetness, 0f, deltaTime / DrySeconds);
        CollectCanopies(cam, halfWidth, halfHeight, raining);
        UpdatePuddles(cam, halfWidth, halfHeight);
    }

    // ------------------------------------------------------------------ árboles

    void CollectCanopies(Vector2 cam, float halfWidth, float halfHeight, bool raining)
    {
        canopyCount = 0;
        if (!raining || world == null) return;
        int count = world.TreeVisualCount;
        for (int i = 0; i < count && canopyCount < MaxTrees; i++)
        {
            SpriteRenderer tree = world.TreeVisualRenderer(i);
            if (tree == null || !tree.enabled || tree.color.a < 0.6f) continue;   // transparente: el jugador está debajo
            Bounds b = tree.bounds;
            if (b.max.x < cam.x - halfWidth - 2f || b.min.x > cam.x + halfWidth + 2f ||
                b.max.y < cam.y - halfHeight - 2f || b.min.y > cam.y + halfHeight + 2f)
                continue;
            // Copa: la parte de arriba del dibujo (el tronco queda abajo).
            canopyCenter[canopyCount] = new Vector2(b.center.x, b.min.y + b.size.y * 0.62f);
            canopyRadius[canopyCount] = b.size.x * 0.38f;
            canopyCount++;
        }
    }

    // Una gota del medio cayó sobre una copa: se desliza por las hojas y gotea (o la absorben las hojas).
    bool TryTreeWater(Vector2 position)
    {
        for (int t = 0; t < canopyCount; t++)
        {
            Vector2 d = position - canopyCenter[t];
            if (d.sqrMagnitude > canopyRadius[t] * canopyRadius[t]) continue;
            int size = AOEffectsQualityV290.RoofPool;
            if (size > 0 && Random.value < 0.55f)
                SpawnLeafDrop(position, canopyCenter[t].y - canopyRadius[t] * 0.8f, size);
            return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ charcos

    void UpdatePuddles(Vector2 cam, float halfWidth, float halfHeight)
    {
        visiblePuddles.Clear();
        ActivePuddles = 0;
        bool show = Wetness > 0.02f && AOEffectsQualityV290.Details && world != null;
        if (show && mapPuddles == null)
            BuildPuddles();
        int used = 0;
        if (show && mapPuddles != null)
        {
            EnsurePuddlePool();
            float grow = Mathf.SmoothStep(0f, 1f, Wetness);
            bool reflections = AOEffectsQualityV290.Level >= AOEffectsQuality.High;
            for (int i = 0; i < mapPuddles.Length && used < MaxPuddles; i++)
            {
                Puddle p = mapPuddles[i];
                if (Mathf.Abs(p.center.x - cam.x) > halfWidth + 2f || Mathf.Abs(p.center.y - cam.y) > halfHeight + 2f)
                    continue;
                var scale = new Vector3(p.width * (0.4f + 0.6f * grow), p.height * (0.4f + 0.6f * grow), 1f);
                var position = new Vector3(p.center.x, p.center.y, 0f);
                SpriteRenderer wet = puddleWet[used], sheen = puddleSheen[used];
                if (!wet.enabled) { wet.enabled = true; sheen.enabled = true; }
                wet.transform.position = position;
                wet.transform.localScale = scale;
                wet.color = new Color(0.55f, 0.6f, 0.72f, 0.65f * grow);
                sheen.transform.position = position;
                sheen.transform.localScale = scale;
                sheen.color = new Color(0.36f, 0.44f, 0.6f, 0.14f * grow * Mathf.Lerp(0.6f, 1f, openSky));
                SpriteMask mask = puddleMask[used];
                if (mask.enabled != reflections) mask.enabled = reflections;
                mask.transform.position = position;
                mask.transform.localScale = scale;
                visiblePuddles.Add(new Puddle { center = p.center, width = scale.x, height = scale.y });
                used++;
            }
        }
        if (puddleWet != null)
            for (int i = used; i < puddleWet.Length; i++)
            {
                if (puddleWet[i].enabled) { puddleWet[i].enabled = false; puddleSheen[i].enabled = false; }
                if (puddleMask[i].enabled) puddleMask[i].enabled = false;
            }
        ActivePuddles = used;
    }

    // Charcos fijos por mapa: ~6 % de las casillas caminables sin techo, elegidas por un hash de la casilla
    // (siempre las mismas), con tamaño y corrimiento propios. Se calcula una vez por mapa.
    void BuildPuddles()
    {
        var list = new List<Puddle>();
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
                });
            }
        mapPuddles = list.ToArray();
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
        if (puddleWet != null) return;
        puddleWet = new SpriteRenderer[MaxPuddles];
        puddleSheen = new SpriteRenderer[MaxPuddles];
        puddleMask = new SpriteMask[MaxPuddles];
        Material multiply = Multiply(), additive = Additive();
        Sprite shape = PuddleSprite();
        for (int i = 0; i < MaxPuddles; i++)
        {
            puddleWet[i] = PuddlePart("PuddleWet_" + i, shape, multiply, -18960);
            puddleSheen[i] = PuddlePart("PuddleSheen_" + i, shape, additive, -18955);
            var go = new GameObject("PuddleMask_" + i);
            go.transform.SetParent(transform, false);
            SpriteMask mask = go.AddComponent<SpriteMask>();
            mask.sprite = shape;
            mask.alphaCutoff = 0.35f;
            mask.enabled = false;
            puddleMask[i] = mask;
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

    static Sprite puddleSprite;

    // Charco: mancha achatada de borde irregular y suave (64x64 px = 1 unidad; se estira con la escala).
    static Sprite PuddleSprite()
    {
        if (puddleSprite != null) return puddleSprite;
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "AO Puddle", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float angle = Mathf.Atan2(dy, dx);
                float edge = 0.78f + 0.14f * Mathf.PerlinNoise(Mathf.Cos(angle) * 1.7f + 3f, Mathf.Sin(angle) * 1.7f + 7f);
                float r = Mathf.Sqrt(dx * dx + dy * dy) / edge;
                float a = Mathf.Clamp01((1f - r) * 6f);
                // Un poco más claro arriba: el reflejo del cielo.
                float sky = Mathf.Lerp(0.8f, 1f, (float)y / (size - 1));
                pixels[y * size + x] = new Color32((byte)(255 * sky), (byte)(255 * sky), 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        puddleSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return puddleSprite;
    }
}
