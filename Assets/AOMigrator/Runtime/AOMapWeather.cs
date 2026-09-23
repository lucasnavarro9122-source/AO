using UnityEngine;

[DisallowMultipleComponent]
public class AOMapWeather : MonoBehaviour
{
    public enum Precipitation { None, Rain, Snow }

    Camera mapCamera;
    AOMapEnvironment environment;
    AOMapParticleDefinition rainDefinition;
    AOMapParticleDefinition snowDefinition;
    Sprite rainSprite;
    Sprite snowSprite;
    Sprite[] fogSprites;
    SpriteRenderer[] drops;
    Vector2[] dropPositions;
    float[] dropSpeeds;
    float[] dropDrifts;
    SpriteRenderer[][] fogTiles;
    static Material additiveMaterial;
    Precipitation requested;
    Precipitation active;
    byte fogAlpha;
    float visibleFogAlpha;

    public bool RainAllowed => environment != null && environment.rain;
    public bool SnowAllowed => environment != null && environment.snow;
    public bool FogAllowed => environment != null && environment.fog;
    public bool PrecipitationVisible => active != Precipitation.None;
    public bool FogVisible => FogAllowed && visibleFogAlpha > 0f;

    public void Configure(Camera camera, AOMapEnvironment mapEnvironment,
                          AOMapParticleDefinition rain,
                          AOMapParticleDefinition snow,
                          Sprite rainImage, Sprite snowImage,
                          Sprite[] fogImages)
    {
        mapCamera = camera;
        environment = mapEnvironment;
        rainDefinition = rain;
        snowDefinition = snow;
        rainSprite = rainImage;
        snowSprite = snowImage;
        fogSprites = fogImages;
    }

    public void SetState(Precipitation precipitation, byte newFogAlpha)
    {
        requested = precipitation;
        fogAlpha = newFogAlpha;
        visibleFogAlpha = newFogAlpha;
    }

    // Original client changes fog alpha by 1 every 100 ms.
    public void SetTarget(Precipitation precipitation, byte newFogAlpha)
    {
        requested = precipitation;
        fogAlpha = newFogAlpha;
    }

    void Update()
    {
        if (mapCamera == null || !mapCamera.orthographic)
            return;

        Vector3 center = mapCamera.transform.position;
        transform.position = new Vector3(center.x, center.y, 0f);
        float halfHeight = mapCamera.orthographicSize;
        float halfWidth = halfHeight * mapCamera.aspect;
        visibleFogAlpha = Mathf.MoveTowards(visibleFogAlpha, fogAlpha,
                                           Time.deltaTime * 10f);

        Precipitation next = Precipitation.None;
        if (requested == Precipitation.Rain && RainAllowed &&
            rainSprite != null)
            next = Precipitation.Rain;
        else if (requested == Precipitation.Snow && SnowAllowed &&
                 snowSprite != null)
            next = Precipitation.Snow;

        if (next != active)
        {
            active = next;
            AOAudioV190.SetWeather(active != Precipitation.None);
            if (active != Precipitation.None)
                RestartDrops(halfWidth, halfHeight);
            Debug.Log("AO_MAP_WEATHER_ACTIVE mode=" + active +
                      " drops=" + (drops == null ? 0 : drops.Length));
        }
        UpdateDrops(halfWidth, halfHeight);
        UpdateFog(halfWidth, halfHeight);
    }

    void RestartDrops(float halfWidth, float halfHeight)
    {
        AOMapParticleDefinition definition = active == Precipitation.Rain
            ? rainDefinition : snowDefinition;
        int count = definition == null ? 0 : Mathf.Clamp(definition.count, 0, 200);
        EnsureDrops(count);
        Sprite sprite = active == Precipitation.Rain ? rainSprite : snowSprite;
        for (int i = 0; i < count; i++)
        {
            drops[i].sprite = sprite;
            drops[i].enabled = true;
            Respawn(i, halfWidth, halfHeight, false);
        }
        for (int i = count; i < drops.Length; i++)
            drops[i].enabled = false;
    }

    void EnsureDrops(int count)
    {
        int oldCount = drops == null ? 0 : drops.Length;
        if (oldCount >= count)
            return;

        System.Array.Resize(ref drops, count);
        System.Array.Resize(ref dropPositions, count);
        System.Array.Resize(ref dropSpeeds, count);
        System.Array.Resize(ref dropDrifts, count);
        if (additiveMaterial == null)
        {
            Shader shader = Resources.Load<Shader>(
                "AOMigrator/WorldV07/AOParticleAdditive");
            if (shader != null)
                additiveMaterial = new Material(shader);
        }
        for (int i = oldCount; i < count; i++)
        {
            GameObject child = new GameObject("WeatherDrop_" + i,
                                                typeof(SpriteRenderer));
            child.transform.SetParent(transform, false);
            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            renderer.sortingOrder = 32000;
            if (additiveMaterial != null)
                renderer.sharedMaterial = additiveMaterial;
            drops[i] = renderer;
        }
    }

    void Respawn(int i, float halfWidth, float halfHeight, bool top)
    {
        dropPositions[i] = new Vector2(
            Random.Range(-halfWidth - 0.5f, halfWidth + 0.5f),
            top ? halfHeight + Random.Range(0f, 2f)
                : Random.Range(-halfHeight, halfHeight));
        bool rain = active == Precipitation.Rain;
        dropSpeeds[i] = rain ? Random.Range(9f, 15f)
                             : Random.Range(1.1f, 2.4f);
        dropDrifts[i] = rain ? Random.Range(-0.6f, -0.2f)
                             : Random.Range(-0.8f, 0.8f);
        drops[i].transform.localPosition = dropPositions[i];
    }

    void UpdateDrops(float halfWidth, float halfHeight)
    {
        if (drops == null)
            return;
        if (active == Precipitation.None)
        {
            foreach (SpriteRenderer renderer in drops)
                renderer.enabled = false;
            return;
        }

        int count = active == Precipitation.Rain
            ? Mathf.Clamp(rainDefinition.count, 0, 200)
            : Mathf.Clamp(snowDefinition.count, 0, 200);
        float delta = Mathf.Min(Time.deltaTime, 0.1f);
        for (int i = 0; i < count; i++)
        {
            Vector2 position = dropPositions[i];
            position.x += dropDrifts[i] * delta;
            position.y -= dropSpeeds[i] * delta;
            if (position.y < -halfHeight - 1f ||
                Mathf.Abs(position.x) > halfWidth + 2f)
            {
                Respawn(i, halfWidth, halfHeight, true);
                continue;
            }
            dropPositions[i] = position;
            drops[i].transform.localPosition = position;
        }
    }

    void UpdateFog(float halfWidth, float halfHeight)
    {
        if (!FogVisible || fogSprites == null || fogSprites.Length < 2 ||
            fogSprites[0] == null || fogSprites[1] == null)
        {
            if (fogTiles != null)
                foreach (SpriteRenderer[] layer in fogTiles)
                    foreach (SpriteRenderer tile in layer)
                        tile.enabled = false;
            return;
        }

        int columns = Mathf.Min(12, Mathf.CeilToInt(halfWidth * 2f / 16f) + 3);
        int rows = Mathf.Min(8, Mathf.CeilToInt(halfHeight * 2f / 16f) + 3);
        int count = columns * rows;
        EnsureFogTiles(count);
        Color color = new Color(1f, 1f, 1f, visibleFogAlpha / 255f);
        for (int layer = 0; layer < 2; layer++)
        {
            float xOffset = Mathf.Repeat(Time.time * (layer == 0 ? 0.28f : -0.43f), 16f);
            float yOffset = Mathf.Repeat(Time.time * (layer == 0 ? 0.20f : -0.25f), 16f);
            for (int i = 0; i < fogTiles[layer].Length; i++)
            {
                SpriteRenderer tile = fogTiles[layer][i];
                bool visible = i < count;
                tile.enabled = visible;
                if (!visible)
                    continue;
                int x = i % columns;
                int y = i / columns;
                tile.color = color;
                tile.transform.localPosition = new Vector3(
                    -halfWidth - 16f + x * 16f + xOffset,
                    -halfHeight - 16f + y * 16f + yOffset, 0f);
            }
        }
    }

    void EnsureFogTiles(int count)
    {
        if (fogTiles == null)
            fogTiles = new SpriteRenderer[2][];
        for (int layer = 0; layer < 2; layer++)
        {
            int oldCount = fogTiles[layer] == null ? 0 : fogTiles[layer].Length;
            if (oldCount >= count)
                continue;
            System.Array.Resize(ref fogTiles[layer], count);
            for (int i = oldCount; i < count; i++)
            {
                GameObject child = new GameObject("Fog_" + layer + "_" + i,
                                                    typeof(SpriteRenderer));
                child.transform.SetParent(transform, false);
                SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
                renderer.sprite = fogSprites[layer];
                renderer.sortingOrder = 31000 + layer;
                fogTiles[layer][i] = renderer;
            }
        }
    }
}
