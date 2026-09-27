using UnityEngine;

// AOMapWeatherDropsV295 (nube, 27/09, pedido de Lucas): gotas de lluvia de mejor calidad.
// Referencias: Garg y Nayar, "Photorealistic Rendering of Rain Streaks" (la gota oscila al caer: su trazo no es una
// línea pareja sino varios brillos corridos y puntitos, y su brillo depende de la luz: a contraluz se ven mucho, en la
// oscuridad casi nada) y Tatarchuk, "Artist-Directable Real-Time Rain Rendering" (lluvia por capas, brillo alrededor de
// las luces, salpicaduras según la superficie).
// - 4 trazos distintos (mismo atlas: se siguen agrupando en 1 draw call) con 2-3 brillos de oscilación y puntitos.
// - Luz: de noche la lluvia se apaga salvo cerca de faroles y antorchas, donde brilla con el color de la llama y
//   parpadea con ella; con el relámpago, todas las gotas se encienden blanco frío un instante.
// - Cortinas: franjas anchas de lluvia más densa que se desplazan despacio (misma cantidad de gotas).
// - Salpicaduras: en el piso, una corona con puntitas y un destello de impacto; en el agua de un charco, ondas finas
//   concéntricas.
public partial class AOMapWeather
{
    static Sprite[] rainStreaks;
    static Sprite crownSprite, rippleSprite;

    // Color y opacidad de una gota (o salpicadura) según la luz que la toca.
    Color LitDrop(Vector2 world, float alpha, float time)
    {
        // Ambiente: de día gris claro; de noche la lluvia casi no se ve (sin luz que la atraviese).
        float ambient = AOSkyV291.Outdoor ? Mathf.Lerp(0.42f, 1f, AOSkyV291.Daylight) : 0.7f;
        Color color = new Color(0.86f * ambient, 0.9f * ambient, ambient);
        // Faroles y antorchas: la lluvia a contraluz de una llama brilla mucho, con su color y su parpadeo.
        float best = 0f;
        Color lamp = Color.white;
        int lamps = AOLivingLightV292.LampCount;
        for (int l = 0; l < lamps; l++)
        {
            Vector2 d = world - AOLivingLightV292.LampPosition(l);
            float radius = AOLivingLightV292.LampRadius(l) + 1f;
            float m2 = d.sqrMagnitude;
            if (m2 >= radius * radius) continue;
            float f = 1f - Mathf.Sqrt(m2) / radius;          // alcance amplio: el cono de luz se llena de lluvia
            if (f > best) { best = f; lamp = AOLivingLightV292.LampLight(l); }
        }
        float lit = best * (0.35f + AOLivingLightV292.Darkness * 1.6f);
        if (lit > 0.01f)
        {
            color = Color.Lerp(color, lamp, Mathf.Clamp01(lit));
            alpha *= 1f + 2.5f * lit;
        }
        // Relámpago: todas se encienden un instante.
        float flash = AOLivingLightV292.FlashStrength;
        if (flash > 0.01f)
        {
            color = Color.Lerp(color, new Color(0.85f, 0.9f, 1f), flash);
            alpha *= 1f + 2f * flash;
        }
        // Cortinas de lluvia: franjas anchas que se desplazan despacio.
        float curtain = Mathf.PerlinNoise(world.x * 0.07f + time * 0.3f, world.y * 0.05f - time * 0.1f);
        float contrast = intensity > 1f ? 0.5f : 0.3f;
        alpha *= 1f - contrast + 2f * contrast * curtain;
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    // 4 trazos de lluvia en un mismo atlas (32x64 px; 8x64 cada uno, 64 px = 1 unidad): perfil fino, punta más
    // brillante, cola que se apaga, y 2-3 brillos corridos por la oscilación de la gota, con algún puntito.
    static Sprite RainStreak(int variant)
    {
        if (rainStreaks == null)
        {
            const int w = 8, h = 64, count = 4;
            var texture = new Texture2D(w * count, h, TextureFormat.RGBA32, false) { name = "AO Rain Streaks", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[w * count * h];
            var random = new System.Random(295);
            rainStreaks = new Sprite[count];
            for (int v = 0; v < count; v++)
            {
                float[] bands = { (float)random.NextDouble() * 0.3f + 0.1f, (float)random.NextDouble() * 0.3f + 0.4f, (float)random.NextDouble() * 0.25f + 0.7f };
                for (int y = 0; y < h; y++)
                {
                    float along = (float)y / (h - 1);                    // 0 = punta (abajo), 1 = cola (arriba)
                    float body = Mathf.Pow(1f - along, 0.8f) * Mathf.Clamp01(along * 10f + 0.35f);
                    float highlights = 0.45f;
                    for (int b = 0; b < bands.Length; b++)
                        highlights += 0.55f * Mathf.Exp(-Mathf.Pow((along - bands[b]) / 0.05f, 2f)) * (b == 0 ? 1f : 0.7f);
                    float speckle = random.NextDouble() < 0.06 ? 0.4f : 0f;
                    for (int x = 0; x < w; x++)
                    {
                        float across = 1f - Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                        float a = Mathf.Pow(across, 2.2f) * body * Mathf.Min(1.3f, highlights + speckle);
                        pixels[y * w * count + v * w + x] = new Color32(235, 242, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                    }
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            for (int v = 0; v < count; v++)
                rainStreaks[v] = Sprite.Create(texture, new Rect(v * w, 0, w, h), new Vector2(0.5f, 0.1f), 64f);
        }
        return rainStreaks[((variant % rainStreaks.Length) + rainStreaks.Length) % rainStreaks.Length];
    }

    // Corona de salpicadura en el piso: anillo achatado con puntitas hacia arriba y centro brillante (32x20 px).
    static Sprite CrownSprite()
    {
        if (crownSprite != null) return crownSprite;
        const int w = 32, h = 20;
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "AO Rain Crown", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f) / w * 2f - 1f, dy = ((y + 0.5f) / h * 2f - 1f) * 1.25f + 0.2f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.62f) * 7f);
                // Puntitas de la corona: arriba del anillo, en 6 direcciones.
                float angle = Mathf.Atan2(dy, dx);
                float spikes = dy > 0f ? Mathf.Clamp01(Mathf.Cos(angle * 6f) * 1.4f - 0.4f) * Mathf.Clamp01(1f - Mathf.Abs(r - 0.82f) * 6f) : 0f;
                float core = Mathf.Clamp01(0.3f - r) * 2f;
                float a = Mathf.Clamp01(ring + spikes * 0.8f + core);
                pixels[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        crownSprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.3f), 32f);
        return crownSprite;
    }

    // Onda en el agua de un charco: dos anillos finos concéntricos, achatados (32x16 px).
    static Sprite RippleSprite()
    {
        if (rippleSprite != null) return rippleSprite;
        const int w = 32, h = 16;
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "AO Rain Ripple", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f) / w * 2f - 1f, dy = (y + 0.5f) / h * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.85f) * 9f) + 0.6f * Mathf.Clamp01(1f - Mathf.Abs(r - 0.5f) * 10f);
                pixels[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        rippleSprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
        return rippleSprite;
    }
}
