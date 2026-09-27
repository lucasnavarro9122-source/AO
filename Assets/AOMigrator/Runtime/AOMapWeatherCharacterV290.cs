using UnityEngine;

// AOMapWeatherCharacterV290 (nube, 27/09, pedido de Lucas): la lluvia rebota en la armadura del jugador.
// Las gotas que le pegan en la cabeza (casco) y los hombros hacen un brillo corto y saltan 3 o 4 gotitas hacia
// arriba y afuera (trazos cortos orientados según su movimiento), que caen con gravedad y se apagan.
// Todo proporcional al personaje: gotitas de 2 px de ancho y 4-5 px de largo en un personaje de 52 px.
// No pasa bajo techo, con nieve ni en calidad Baja. Pool fijo (AOEffectsQualityV290.BouncePool), mismo material
// aditivo que la lluvia: sin Instantiate/Destroy ni GetComponent por cuadro.
public partial class AOMapWeather
{
    const float HitsPerSecond = 12f;        // con la lluvia del AO (intensidad 1); la tormenta (1,5) pega más
    const float BounceGravity = 11f;        // unidades/s²: las gotitas suben ~0,15 y caen
    const float GlintSeconds = 0.09f;

    SpriteRenderer[] bounces;
    Transform[] bounceTransforms;
    Vector2[] bouncePos, bounceVel;
    float[] bounceAge, bounceLife, bounceSize;
    bool[] bounceGlint;
    int bounceNext;
    float hitDebt;

    Transform playerTransform;
    AOCharacterRenderer playerVisual;
    float visualRetryAt;

    public int ActiveBounces { get; private set; }

    void UpdateCharacterRain(float deltaTime)
    {
        UpdateBounces(deltaTime);
        int size = AOEffectsQualityV290.BouncePool;
        if (active != Precipitation.Rain || size <= 0 || intensity <= 0f || playerRoof != 0 || openSky < 0.5f ||
            !FindPlayerVisual())
        {
            hitDebt = 0f;
            return;
        }
        hitDebt += HitsPerSecond * AOEffectsQualityV290.Density * intensity * openSky * deltaTime;
        int guard = 4;
        while (hitDebt >= 1f && guard-- > 0)
        {
            hitDebt -= 1f;
            HitArmor(size);
        }
    }

    // El personaje puede cambiar (carga, muerte, transformación): se busca solo cuando cambia o, si todavía no
    // tiene su visual, una vez por segundo.
    bool FindPlayerVisual()
    {
        Transform current = world != null ? world.PlayerTransform : null;
        if (current != playerTransform)
        {
            playerTransform = current;
            playerVisual = null;
            visualRetryAt = 0f;
        }
        if (playerTransform == null)
            return false;
        if (playerVisual == null && Time.unscaledTime >= visualRetryAt)
        {
            playerVisual = playerTransform.GetComponentInChildren<AOCharacterRenderer>(true);
            visualRetryAt = Time.unscaledTime + 1f;
        }
        return playerVisual != null && playerVisual.isActiveAndEnabled;
    }

    void HitArmor(int size)
    {
        Vector3 feet = playerVisual.transform.position;
        // Arriba de la cabeza (o del casco): el mismo punto que usa el texto hablado, menos su margen.
        float top = playerVisual.SpeechAnchor.y - 0.15f;
        float height = top - feet.y;
        if (height < 0.8f || height > 3f)
        {
            height = CharacterHeight;
            top = feet.y + height;
        }
        float side = Random.value < 0.5f ? -1f : 1f;
        Vector2 hit;
        if (Random.value < 0.45f)
            hit = new Vector2(feet.x + Random.Range(-0.16f, 0.16f), top - Random.Range(0.02f, 0.14f));       // cabeza
        else
            hit = new Vector2(feet.x + side * Random.Range(0.12f, 0.3f), feet.y + height * Random.Range(0.6f, 0.7f)); // hombro

        // Brillo del golpe en el metal y 2-3 gotitas que saltan hacia afuera (del lado del golpe) y hacia arriba.
        SpawnBounce(size, hit, Vector2.zero, GlintSeconds, 1.2f, true);
        int droplets = Random.value < 0.4f ? 4 : 3;
        for (int d = 0; d < droplets; d++)
        {
            float outward = (d == 0 ? side : Random.value < 0.5f ? -side : side) * Random.Range(0.5f, 1.6f);
            SpawnBounce(size, hit, new Vector2(outward, Random.Range(1.2f, 2.3f)), Random.Range(0.3f, 0.45f),
                        Random.Range(0.4f, 0.55f), false);
        }
    }

    void SpawnBounce(int size, Vector2 position, Vector2 velocity, float life, float scale, bool glint)
    {
        EnsureBounces(size);
        int i = bounceNext % size;
        bounceNext = (i + 1) % size;
        bouncePos[i] = position;
        bounceVel[i] = velocity;
        bounceAge[i] = 0f;
        bounceLife[i] = life;
        bounceSize[i] = scale;
        bounceGlint[i] = glint;
        // Destello: punto redondo. Gotita: trazo corto (el de la lluvia) que se orienta según su velocidad.
        bounces[i].sprite = glint ? Droplet() : Streak();
        bounceTransforms[i].localScale = glint ? new Vector3(scale, scale, 1f) : new Vector3(scale, 0.14f, 1f);
        bounceTransforms[i].localRotation = Quaternion.identity;
        bounceTransforms[i].position = new Vector3(position.x, position.y, 0f);
        bounces[i].color = new Color(0.9f, 0.95f, 1f, 0f);
        bounces[i].enabled = true;
    }

    void EnsureBounces(int size)
    {
        if (bounces != null && bounces.Length >= size)
            return;
        int old = bounces == null ? 0 : bounces.Length;
        System.Array.Resize(ref bounces, size);
        System.Array.Resize(ref bounceTransforms, size);
        System.Array.Resize(ref bouncePos, size);
        System.Array.Resize(ref bounceVel, size);
        System.Array.Resize(ref bounceAge, size);
        System.Array.Resize(ref bounceLife, size);
        System.Array.Resize(ref bounceSize, size);
        System.Array.Resize(ref bounceGlint, size);
        Material material = Additive();
        for (int i = old; i < size; i++)
        {
            GameObject child = new GameObject("RainBounce_" + i, typeof(SpriteRenderer));
            child.transform.SetParent(transform, false);
            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            renderer.sprite = Droplet();
            renderer.sortingOrder = 32000;
            if (material != null)
                renderer.sharedMaterial = material;
            renderer.enabled = false;
            bounces[i] = renderer;
            bounceTransforms[i] = child.transform;
        }
    }

    void UpdateBounces(float deltaTime)
    {
        ActiveBounces = 0;
        if (bounces == null)
            return;
        for (int i = 0; i < bounces.Length; i++)
        {
            if (!bounces[i].enabled)
                continue;
            bounceAge[i] += deltaTime;
            float k = bounceAge[i] / bounceLife[i];
            if (k >= 1f)
            {
                bounces[i].enabled = false;
                continue;
            }
            ActiveBounces++;
            float alpha;
            if (bounceGlint[i])
            {
                // Destello: aparece de golpe y se achica.
                float s = bounceSize[i] * (1f - 0.5f * k);
                bounceTransforms[i].localScale = new Vector3(s, s, 1f);
                alpha = 0.85f * (1f - k);
            }
            else
            {
                Vector2 v = bounceVel[i];
                v.y -= BounceGravity * deltaTime;
                bounceVel[i] = v;
                bouncePos[i] += v * deltaTime;
                // La punta del trazo va hacia donde se mueve (sube, frena y cae).
                bounceTransforms[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(v.x, -v.y) * Mathf.Rad2Deg);
                alpha = 0.9f * (1f - k * k);
            }
            // El pool cuelga de la cámara: se reubica en el mundo cada cuadro.
            bounceTransforms[i].position = new Vector3(bouncePos[i].x, bouncePos[i].y, 0f);
            bounces[i].color = new Color(0.9f, 0.95f, 1f, alpha);
        }
    }

    static Sprite dropletSprite;

    // Gotita redonda y suave (8x8 px, 64 px = 1 unidad: a escala 0,4 mide 0,05 unidades = 1-2 px del AO).
    static Sprite Droplet()
    {
        if (dropletSprite != null) return dropletSprite;
        const int size = 8;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "AO Rain Droplet", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                pixels[y * size + x] = new Color32(235, 242, 255, (byte)Mathf.RoundToInt(a * a * 255f));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        dropletSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
        return dropletSprite;
    }
}
