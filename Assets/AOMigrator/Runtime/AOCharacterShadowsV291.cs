using System.Collections.Generic;
using UnityEngine;

// AOCharacterShadowsV291 (nube, 27/09, pedido de Lucas): cada personaje (jugador, NPC, compañeros, mascotas) tiene
// sombra del lado opuesto a la luz que más lo ilumina:
// - al aire libre, el sol o la luna (AOSkyV291): larga al amanecer y al atardecer, corta al mediodía, tenue con
//   luna y casi nada bajo una nube o con el cielo cubierto;
// - de noche o en dungeons, la antorcha o luz del mapa más fuerte que tenga cerca (las mismas luces del AO):
//   la sombra se aleja de la llama y se alarga cuanto más lejos está el personaje.
// Técnica clásica del 2D isométrico: la silueta del personaje (cuerpo, cabeza y casco) copiada en negro, girada
// hacia donde cae la sombra y estirada según el largo, apoyada en los pies; más una mancha suave de contacto.
// Orden de dibujo: justo debajo del personaje en su fila (como pinta el AO, fila por fila): la sombra cae sobre lo
// que está detrás (pasto, arbustos, la pared de atrás) y queda debajo de lo que está adelante y del personaje.
// No toca AOCharacterRenderer: busca los personajes cada 1,5 s. Las sombras NO son hijas del personaje (así no
// cambian el área de clic de los NPC ni las recorren invisibilidad, mímesis o muerte): cuelgan de este objeto y
// siguen los pies cada cuadro; se borran cuando el personaje desaparece.
// Calidad: Low solo el jugador; Medium 16, High 32 y Ultra 48 personajes a la vista.
[DefaultExecutionOrder(1002)]
public class AOCharacterShadowsV291 : MonoBehaviour
{
    class Entry
    {
        public AOCharacterRenderer visual;
        public Transform root, litRoot, reflectRoot;
        public SpriteRenderer reflectBody, reflectHead, reflectHelmet;   // reflejo en los charcos (V293)
        public SpriteRenderer body, head, helmet, contact;
        public SpriteRenderer litBody, litHead, litHelmet;     // luz del farol o del relámpago sobre el personaje
        public SpriteRenderer sourceBody, sourceHead, sourceHelmet;
        public float angle, length = 0.5f, alpha;
        public bool shown, hasSources;
    }

    const float ScanSeconds = 1.5f;
    const float ContactAlpha = 0.26f;
    const int MaxLights = 48;

    readonly List<Entry> entries = new List<Entry>();
    readonly HashSet<AOCharacterRenderer> known = new HashSet<AOCharacterRenderer>();
    float scanAt, worldRetryAt;
    AOWorldManagerV07 world;
    Transform playerTransform;

    readonly Vector2[] lightPos = new Vector2[MaxLights];
    readonly float[] lightRadius = new float[MaxLights];
    readonly float[] lightPower = new float[MaxLights];
    readonly int[] lightSeed = new int[MaxLights];
    readonly Color[] lightColor = new Color[MaxLights];
    int lightCount;

    public static int ActiveShadows { get; private set; }

    // Pies de los personajes a la vista (las luciérnagas se apartan de ellos). Se llena cada cuadro.
    public static readonly Vector2[] VisibleFeet = new Vector2[64];
    public static int VisibleCount { get; private set; }

    void LateUpdate()
    {
        if (world == null)
        {
            if (Time.unscaledTime < worldRetryAt) return;
            worldRetryAt = Time.unscaledTime + 1f;
            world = UnityEngine.Object.FindFirstObjectByType<AOWorldManagerV07>();
            if (world == null) return;
        }
        if (Time.unscaledTime >= scanAt)
        {
            scanAt = Time.unscaledTime + ScanSeconds;
            Scan();
        }
        Camera cam = AOActionBarV260.GameCamera;
        if (cam == null || !cam.orthographic || world.IsLoading)
        {
            HideAll();
            return;
        }

        Vector2 center = cam.transform.position;
        float halfHeight = cam.orthographicSize + 2f;
        float halfWidth = cam.orthographicSize * cam.aspect + 2f;
        CollectLights(center, halfWidth + 10f, halfHeight + 10f);
        playerTransform = world.PlayerTransform;

        float ambient = AmbientBrightness();
        float deltaTime = Mathf.Min(Time.deltaTime, 0.1f);
        float blend = 1f - Mathf.Exp(-8f * deltaTime);
        int budget = AOEffectsQualityV290.CharacterShadows;
        int used = 0;
        VisibleCount = 0;

        // Primero el jugador (siempre tiene sombra), después el resto hasta el tope de la calidad.
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                if (e.visual == null)
                {
                    // Destruido entre dos búsquedas (mascota, invocación): la sombra no queda flotando.
                    if (pass == 0) Show(e, false);
                    continue;
                }
                bool isPlayer = playerTransform != null && e.visual.transform.IsChildOf(playerTransform);
                if (isPlayer != (pass == 0)) continue;
                Vector2 feet = e.visual.transform.position;
                bool inView = Mathf.Abs(feet.x - center.x) < halfWidth && Mathf.Abs(feet.y - center.y) < halfHeight &&
                              e.visual.isActiveAndEnabled;
                if (inView && VisibleCount < VisibleFeet.Length)
                    VisibleFeet[VisibleCount++] = feet;
                if (!inView || used >= budget)
                {
                    Show(e, false);
                    continue;
                }
                used++;
                UpdateEntry(e, feet, ambient, blend);
            }
        }
        ActiveShadows = used;
    }

    // ------------------------------------------------------------------ luz dominante

    void UpdateEntry(Entry e, Vector2 feet, float ambient, float blend)
    {
        if (!e.hasSources && !FindSources(e))
        {
            Show(e, false);
            return;
        }

        // Sol o luna.
        Vector2 direction = AOSkyV291.ShadowDirection;
        float length = AOSkyV291.ShadowLength;
        float alpha = AOSkyV291.Outdoor ? AOSkyV291.KeyShadowAlpha * (1f - 0.85f * AOSkyV291.CloudAt(feet)) : 0f;

        // Luces del mapa: pesan más cuanto más oscuro está el ambiente.
        float bestWeight = 0f;
        int bestLight = -1;
        Vector2 bestPos = Vector2.zero;
        float bestDistance = 0f;
        for (int l = 0; l < lightCount; l++)
        {
            Vector2 offset = feet - lightPos[l];
            float distance = offset.magnitude;
            if (distance >= lightRadius[l]) continue;
            float falloff = 1f - distance / lightRadius[l];
            float weight = lightPower[l] * falloff * falloff * Mathf.Clamp01(1.15f - ambient);
            if (weight > bestWeight) { bestWeight = weight; bestPos = lightPos[l]; bestDistance = distance; bestLight = l; }
        }
        float pointAlpha = Mathf.Clamp01(bestWeight * 1.3f) * 0.5f;
        if (pointAlpha > alpha && bestDistance > 0.2f)
        {
            direction = (feet - bestPos) / bestDistance;
            length = Mathf.Clamp(0.45f + bestDistance * 0.2f, 0.45f, 1.1f);
            alpha = pointAlpha;
        }

        // Relámpago: sombra corta y dura desde el rayo mientras dura el destello (al aire libre, sin techo encima).
        float flash = AOSkyV291.Outdoor && !IsUnderRoof(e) ? AOLivingLightV292.FlashStrength : 0f;
        if (flash * 0.7f > alpha)
        {
            direction = AOLivingLightV292.FlashShadowDirection;
            length = 0.8f;
            alpha = flash * 0.7f;
        }

        // Personaje transparente (fantasma, invisibilidad): sombra en proporción.
        alpha *= e.sourceBody != null ? e.sourceBody.color.a : 1f;

        float targetAngle = Mathf.Atan2(-direction.x, direction.y) * Mathf.Rad2Deg;
        e.angle = e.shown ? Mathf.LerpAngle(e.angle, targetAngle, blend) : targetAngle;
        e.length = e.shown ? Mathf.Lerp(e.length, length, blend) : length;
        e.alpha = e.shown ? Mathf.Lerp(e.alpha, alpha, blend) : alpha;
        Show(e, true);

        var feetPosition = new Vector3(feet.x, feet.y, 0f);
        e.root.position = feetPosition;
        e.contact.transform.position = feetPosition;
        e.litRoot.position = feetPosition;
        // Pixel art: el giro va en pasos de 5° (sin temblor de píxeles) y la sombra es un poco más angosta que el
        // cuerpo (es su proyección en el piso).
        e.root.rotation = Quaternion.Euler(0f, 0f, Mathf.Round(e.angle / 5f) * 5f);
        e.root.localScale = new Vector3(0.9f, e.length, 1f);
        var shade = new Color(0f, 0f, 0f, e.alpha);
        Mirror(e.body, e.sourceBody, shade);
        Mirror(e.head, e.sourceHead, shade);
        Mirror(e.helmet, e.sourceHelmet, shade);
        e.contact.color = new Color(0f, 0f, 0f, ContactAlpha * (e.sourceBody != null ? e.sourceBody.color.a : 1f));

        // Luz sobre el personaje: el color del farol que lo ilumina, parpadeando con él (más de noche), o el
        // blanco frío del relámpago.
        Color wash = new Color(0f, 0f, 0f, 0f);
        if (AOEffectsQualityV290.CharacterLight)
        {
            if (bestLight >= 0)
            {
                float amount = Mathf.Clamp01(bestWeight * 1.6f) * 0.32f * AOLivingLightV292.Flicker(lightSeed[bestLight]);
                Color c = lightColor[bestLight];
                wash = new Color(c.r, c.g, c.b, amount);
            }
            if (flash * 0.55f > wash.a)
                wash = new Color(0.78f, 0.86f, 1f, flash * 0.55f);
            wash.a *= e.sourceBody != null ? e.sourceBody.color.a : 1f;
        }
        Mirror(e.litBody, e.sourceBody, wash);
        Mirror(e.litHead, e.sourceHead, wash);
        Mirror(e.litHelmet, e.sourceHelmet, wash);

        // Reflejo en los charcos: el personaje dado vuelta bajo sus pies, visible solo dentro del charco (máscara).
        // Si el charco está agitado (alguien lo pisó), el reflejo se ondula y se desarma hasta que el agua se calma.
        float disturb = AOEffectsQualityV290.Level >= AOEffectsQuality.High ? AOMapWeather.DisturbanceAt(feet, 1.2f) : -1f;
        bool reflect = disturb >= 0f;
        var mirrorColor = new Color(0.62f, 0.68f, 0.8f, reflect ? 0.55f * AOMapWeather.Wetness * (1f - 0.55f * disturb) : 0f);
        if (e.reflectRoot.gameObject.activeSelf != reflect) e.reflectRoot.gameObject.SetActive(reflect);
        if (reflect)
        {
            float t = Time.time;
            e.reflectRoot.position = feetPosition + new Vector3(Mathf.Sin(t * 16f) * 0.06f * disturb, 0f, 0f);
            e.reflectRoot.localScale = new Vector3(1f + 0.08f * disturb * Mathf.Sin(t * 11f),
                                                   1f - 0.2f * disturb * Mathf.Abs(Mathf.Sin(t * 7f)), 1f);
            Reflect(e.reflectBody, e.sourceBody, mirrorColor);
            Reflect(e.reflectHead, e.sourceHead, mirrorColor);
            Reflect(e.reflectHelmet, e.sourceHelmet, mirrorColor);
        }
        // Mancha de contacto del ancho real del cuerpo (enanos, gigantes, monturas).
        if (e.sourceBody.sprite != null)
        {
            float width = Mathf.Clamp(e.sourceBody.sprite.bounds.size.x * 0.85f, 0.4f, 2.5f);
            if (Mathf.Abs(e.contact.transform.localScale.x - width) > 0.01f)
                e.contact.transform.localScale = new Vector3(width, width, 1f);
        }

        // Una por debajo de la parte más baja del personaje (el orden cambia al moverse de fila).
        int order = e.sourceBody.sortingOrder;
        if (e.sourceHead != null) order = Mathf.Min(order, e.sourceHead.sortingOrder);
        if (e.sourceHelmet != null) order = Mathf.Min(order, e.sourceHelmet.sortingOrder);
        order -= 1;
        if (e.body.sortingOrder != order || e.body.sortingLayerID != e.sourceBody.sortingLayerID)
        {
            int layer = e.sourceBody.sortingLayerID;
            e.body.sortingLayerID = layer;
            e.head.sortingLayerID = layer;
            e.helmet.sortingLayerID = layer;
            e.contact.sortingLayerID = layer;
            e.body.sortingOrder = order;
            e.head.sortingOrder = order;
            e.helmet.sortingOrder = order;
            e.contact.sortingOrder = order;
            // La luz va encima de la parte más alta del personaje (cuerpo, cabeza y casco).
            int top = Mathf.Max(e.sourceBody.sortingOrder,
                                Mathf.Max(e.sourceHead != null ? e.sourceHead.sortingOrder : 0,
                                          e.sourceHelmet != null ? e.sourceHelmet.sortingOrder : 0)) + 1;
            e.litBody.sortingLayerID = layer;
            e.litHead.sortingLayerID = layer;
            e.litHelmet.sortingLayerID = layer;
            e.litBody.sortingOrder = top;
            e.litHead.sortingOrder = top;
            e.litHelmet.sortingOrder = top;
        }
    }

    static void Reflect(SpriteRenderer reflection, SpriteRenderer source, Color color)
    {
        bool on = source != null && source.enabled && source.sprite != null && source.gameObject.activeInHierarchy;
        if (reflection.enabled != on) reflection.enabled = on;
        if (!on) return;
        if (reflection.sprite != source.sprite) reflection.sprite = source.sprite;
        reflection.flipX = source.flipX;
        reflection.flipY = !source.flipY;                       // dado vuelta desde los pies
        Vector3 local = source.transform.localPosition;
        reflection.transform.localPosition = new Vector3(local.x, -local.y, 0f);
        reflection.color = new Color(color.r, color.g, color.b, color.a * source.color.a);
    }

    bool IsUnderRoof(Entry e)
    {
        if (playerTransform != null && e.visual.transform.IsChildOf(playerTransform))
            return world.PlayerUnderRoof;
        return world.IsRoofAt(e.visual.transform.position);
    }

    static void Mirror(SpriteRenderer shadow, SpriteRenderer source, Color shade)
    {
        bool on = source != null && source.enabled && source.sprite != null && source.gameObject.activeInHierarchy &&
                  shade.a > 0.01f;
        if (shadow.enabled != on) shadow.enabled = on;
        if (!on) return;
        if (shadow.sprite != source.sprite) shadow.sprite = source.sprite;
        shadow.flipX = source.flipX;
        shadow.transform.localPosition = source.transform.localPosition;
        shadow.color = shade;
    }

    float AmbientBrightness()
    {
        if (AOSkyV291.Outdoor)
            return Mathf.Lerp(0.47f, 1f, AOSkyV291.Daylight);       // noche del AO: 120/255
        int packed = world.CurrentBaseLight;
        if (packed == 0)
            return ((Color)AOMapLighting.DayColor(world.CurrentWorldHour)).maxColorComponent;
        return Mathf.Max((packed >> 16) & 255, (packed >> 8) & 255, packed & 255) / 255f;
    }

    void CollectLights(Vector2 center, float halfWidth, float halfHeight)
    {
        lightCount = 0;
        AOMapLightPlacement[] lights = world.CurrentMapLights;
        if (lights == null) return;
        for (int i = 0; i < lights.Length && lightCount < MaxLights; i++)
        {
            AOMapLightPlacement light = lights[i];
            // Mismo lugar que AOLighting2DV283 y AOMapLighting: centro de la casilla.
            var position = new Vector2(light.x - 0.5f, -light.y + 0.5f);
            if (Mathf.Abs(position.x - center.x) > halfWidth || Mathf.Abs(position.y - center.y) > halfHeight)
                continue;
            int range = light.range >= 100 ? light.range - 99 : light.range;
            if (range <= 0) continue;
            int c = light.color & 0xFFFFFF;
            float luminance = (0.3f * ((c >> 16) & 255) + 0.59f * ((c >> 8) & 255) + 0.11f * (c & 255)) / 255f;
            lightPos[lightCount] = position;
            lightRadius[lightCount] = range + 0.5f;
            lightPower[lightCount] = Mathf.Clamp01(luminance * 1.2f);
            lightSeed[lightCount] = AOLivingLightV292.Seed(light);
            lightColor[lightCount] = AOLivingLightV292.LightColor(light);
            lightCount++;
        }
    }

    // ------------------------------------------------------------------ alta y baja de personajes

    void Scan()
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i].visual != null) continue;
            if (entries[i].root != null) Destroy(entries[i].root.gameObject);
            if (entries[i].contact != null) Destroy(entries[i].contact.gameObject);
            if (entries[i].litRoot != null) Destroy(entries[i].litRoot.gameObject);
            if (entries[i].reflectRoot != null) Destroy(entries[i].reflectRoot.gameObject);
            entries.RemoveAt(i);
        }
        known.RemoveWhere(v => v == null);
        AOCharacterRenderer[] found = UnityEngine.Object.FindObjectsByType<AOCharacterRenderer>(FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] == null || known.Contains(found[i])) continue;
            known.Add(found[i]);
            entries.Add(Create(found[i]));
        }
    }

    Entry Create(AOCharacterRenderer visual)
    {
        var e = new Entry { visual = visual };
        var root = new GameObject("Sombra (luz) " + visual.name);
        root.transform.SetParent(transform, false);
        e.root = root.transform;
        e.body = Part(e.root, "Cuerpo");
        e.head = Part(e.root, "Cabeza");
        e.helmet = Part(e.root, "Casco");
        var mirror = new GameObject("Reflejo de " + visual.name);
        mirror.transform.SetParent(e.root.parent, false);
        mirror.SetActive(false);
        e.reflectRoot = mirror.transform;
        e.reflectBody = Part(mirror.transform, "Cuerpo");
        e.reflectHead = Part(mirror.transform, "Cabeza");
        e.reflectHelmet = Part(mirror.transform, "Casco");
        foreach (SpriteRenderer part in new[] { e.reflectBody, e.reflectHead, e.reflectHelmet })
        {
            part.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            part.sortingOrder = -18940;                          // sobre el charco, bajo todo lo demás
        }
        var light = new GameObject("Luz sobre " + visual.name);
        light.transform.SetParent(e.root.parent, false);
        light.SetActive(false);
        e.litRoot = light.transform;
        e.litBody = Part(light.transform, "Cuerpo");
        e.litHead = Part(light.transform, "Cabeza");
        e.litHelmet = Part(light.transform, "Casco");
        Material glow = AOLivingLightV292.Additive();
        if (glow != null)
        {
            e.litBody.sharedMaterial = glow;
            e.litHead.sharedMaterial = glow;
            e.litHelmet.sharedMaterial = glow;
        }
        var contact = new GameObject("Sombra (contacto) " + visual.name);
        contact.transform.SetParent(transform, false);
        contact.transform.localScale = new Vector3(0.72f, 0.72f, 1f);
        e.contact = contact.AddComponent<SpriteRenderer>();
        e.contact.sprite = ContactSprite();
        contact.SetActive(false);
        root.SetActive(false);
        FindSources(e);
        return e;
    }

    static SpriteRenderer Part(Transform root, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.enabled = false;
        return renderer;
    }

    // AOCharacterRenderer crea sus partes como hijos "Body", "Head" y "Helmet" (EnsureRenderers).
    static bool FindSources(Entry e)
    {
        Transform t = e.visual.transform;
        e.sourceBody = Source(t, "Body");
        e.sourceHead = Source(t, "Head");
        e.sourceHelmet = Source(t, "Helmet");
        e.hasSources = e.sourceBody != null;
        if (e.hasSources)
        {
            // Mismo material que el personaje (Original o Mejorada): la silueta negra se ve igual en los dos.
            Material material = e.sourceBody.sharedMaterial;
            if (material != null)
            {
                e.body.sharedMaterial = material;
                e.head.sharedMaterial = material;
                e.helmet.sharedMaterial = material;
            }
        }
        return e.hasSources;
    }

    static SpriteRenderer Source(Transform visual, string name)
    {
        Transform child = visual.Find(name);
        return child != null ? child.GetComponent<SpriteRenderer>() : null;
    }

    static void Show(Entry e, bool on)
    {
        if (e.shown == on) return;
        e.shown = on;
        if (e.root != null) e.root.gameObject.SetActive(on);
        if (e.contact != null) e.contact.gameObject.SetActive(on);
        if (e.litRoot != null) e.litRoot.gameObject.SetActive(on);
        if (!on && e.reflectRoot != null) e.reflectRoot.gameObject.SetActive(false);
    }

    void HideAll()
    {
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].visual != null) Show(entries[i], false);
        ActiveShadows = 0;
    }

    static Sprite contactSprite;

    // Mancha de contacto bajo los pies: elipse suave (32x16 px = 1 x 0,5 unidades).
    static Sprite ContactSprite()
    {
        if (contactSprite != null) return contactSprite;
        const int w = 32, h = 16;
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "AO Contact Shadow", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f) / w * 2f - 1f, dy = (y + 0.5f) / h * 2f - 1f;
                float f = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                pixels[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(f * f * 255f));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        contactSprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
        return contactSprite;
    }
}
