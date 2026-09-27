using UnityEngine;

// AOMapWeather: motor de clima del mapa (lluvia, nieve, niebla). Uno por mapa (AOWorldManagerV07.BuildMapWeather).
// V290 (nube, 26/09): mismo contrato público (Configure, SetState, SetTarget, *Allowed, *Visible) y mismos datos del
// AO (particle_defs 57/58, fogSprites, map_environment), con:
// - gotas y copos en el MUNDO (no pegados a la pantalla), en 3 capas de profundidad con parallax, repartiendo la
//   misma cantidad del AO (200 gotas / 90 copos) x calidad;
// - trazos de lluvia verticales y proporcionales al personaje (27/09: como mucho 1/3 de su altura), con velocidad
//   y tamaño por capa; la nieve y la niebla sí las lleva el viento global (AOWindV290);
// - gotas que rebotan en la cabeza y los hombros del jugador (AOMapWeatherCharacterV290);
// - salpicaduras donde "aterriza" cada gota de la capa del medio (pool), y agua que escurre por los techos
//   (AOMapWeatherRoofsV290);
// - las dos capas de niebla del AO ancladas al mundo, más niebla de suelo en bancos (debajo de objetos y techos);
// - tinte del día de lluvia/nieve que multiplica (el negro del vacío sigue negro);
// - transiciones suaves de intensidad; nada de lluvia dentro del techo que el jugador tiene encima;
// - sigue a la cámara después de AOCameraFollow (sin cuadro de atraso) y no recorre nada cuando está apagado.
// Orden de dibujo: piso (capas 1-2) < niebla de suelo -19000 < objetos y personajes 10000+ < techos 20000+y < agua de techos 30500 < salpicaduras 30600
//                  < tinte 30990 < niebla del AO 31000 < gotas 32000.
// Costo (High): <= 200 sprites de precipitación + <= 26 salpicaduras + <= 24 de techos + niebla (2 capas de
// mosaicos de 16 unidades como el original + ~30 manchas de suelo) + 1 quad. Sin Instantiate/Destroy en régimen.
[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public partial class AOMapWeather : MonoBehaviour
{
    public enum Precipitation { None, Rain, Snow }

    struct Layer
    {
        // scale: ancho (lluvia) o tamaño (nieve); length: largo del trazo en unidades de mundo (solo lluvia).
        public float share, scale, length, alpha, speedMin, speedMax, parallax, landMin, landMax, sway;
        public bool lands;
    }

    // Tamaños proporcionales al personaje (Lucas, 27/09): un personaje del AO mide ~52 px = 1,6 casillas.
    // El trazo más largo (capa de adelante) mide ~1/3 del personaje y 2 px de ancho; la lluvia cae vertical.
    public const float CharacterHeight = 1.6f;

    // Capas: fondo (chico, lento, tenue), medio (normal; aterriza y salpica), frente (más grande y rápida, pocas).
    static readonly Layer[] RainLayers =
    {
        new Layer { share = .45f, scale = .5f, length = CharacterHeight * .13f, alpha = .3f, speedMin = 7f, speedMax = 9f, parallax = .8f, landMin = 1.5f, landMax = 5f },
        new Layer { share = .40f, scale = .75f, length = CharacterHeight * .22f, alpha = .55f, speedMin = 11f, speedMax = 14f, parallax = 1f, landMin = 1.2f, landMax = 6f, lands = true },
        new Layer { share = .15f, scale = 1f, length = CharacterHeight * .32f, alpha = .42f, speedMin = 16f, speedMax = 20f, parallax = 1.2f, landMin = 99f, landMax = 99f },
    };
    // Copo del AO (16 px): como mucho su tamaño original en la capa de adelante.
    static readonly Layer[] SnowLayers =
    {
        new Layer { share = .45f, scale = .45f, length = 1f, alpha = .5f, speedMin = .7f, speedMax = 1.1f, parallax = .8f, landMin = 3f, landMax = 7f, sway = .25f },
        new Layer { share = .40f, scale = .7f, length = 1f, alpha = .8f, speedMin = 1.2f, speedMax = 1.9f, parallax = 1f, landMin = 3f, landMax = 8f, sway = .4f, lands = true },
        new Layer { share = .15f, scale = 1f, length = 1f, alpha = .6f, speedMin = 2.1f, speedMax = 2.9f, parallax = 1.15f, landMin = 99f, landMax = 99f, sway = .6f },
    };

    const float TransitionSeconds = 4f;
    const float Margin = 1.5f;
    const int MaxDrops = 300;               // 200 del AO x 1,5 (tope de Ultra y de la depuración)
    const float SplashSeconds = 0.28f;
    const float GroundFogSpacing = 6f;      // unidades entre manchas de niebla de suelo
    const float GroundFogSize = 9f;         // tamaño de cada mancha (se superponen: sin costuras)

    Camera mapCamera;
    AOMapEnvironment environment;
    AOWorldManagerV07 world;
    AOMapParticleDefinition rainDefinition;
    AOMapParticleDefinition snowDefinition;
    Sprite rainSprite;
    Sprite snowSprite;
    Sprite[] fogSprites;
    static Material additiveMaterial;
    static Material multiplyMaterial;
    static bool multiplyMissing;
    Precipitation requested;
    Precipitation active;
    byte fogAlpha;
    float visibleFogAlpha;
    float intensity;
    float intensityTarget;
    bool instantChange;

    // Pool de precipitación
    SpriteRenderer[] drops;
    Transform[] dropTransforms;
    Vector2[] dropPos;      // en el espacio de su capa (mundo con parallax)
    Vector2[] dropVel;
    float[] dropAge, dropLife, dropPhase, dropAlpha;
    byte[] dropLayer;
    int poolCount;          // gotas creadas
    int activeCount;        // gotas en uso (según intensidad y calidad)
    int layerMask = -1;     // capas habilitadas por calidad (bits 0..2); -1 = recalcular

    // Salpicaduras (pool circular, ancladas al mundo)
    SpriteRenderer[] splashes;
    Transform[] splashTransforms;
    Vector2[] splashPos;
    float[] splashAge, splashStrength;
    int splashNext;

    // Niebla: 0 y 1 = las dos capas del AO (mosaicos); 2 = niebla de suelo (manchas suaves)
    SpriteRenderer[][] fogTiles = new SpriteRenderer[3][];
    readonly Vector2[] fogOffset = new Vector2[3];
    SpriteRenderer tint;
    float tintAlpha = -1f;

    // Techo del jugador (fundido suave: 1 = cielo abierto)
    float openSky = 1f;

    public bool RainAllowed => environment != null && environment.rain;
    public bool SnowAllowed => environment != null && environment.snow;
    public bool FogAllowed => environment != null && environment.fog;
    // "Pedida y activa": la transición de intensidad no demora el aviso (lo usa AOMapLoadProfile a los 2 cuadros).
    public bool PrecipitationVisible => active != Precipitation.None && (intensity > 0f || intensityTarget > 0f);
    public bool FogVisible => (FogAllowed || DebugAllowAll) && visibleFogAlpha > 0f;

    // Depuración (ventana "Clima" del Editor; nada de esto se ve ni se toca en el juego).
    // DebugIntensity >= 0 reemplaza la intensidad (1 = la del AO). DebugAllowAll ignora los permisos del mapa.
    public static float DebugIntensity = -1f;
    public static bool DebugAllowAll;
    public Precipitation RequestedPrecipitation => requested;
    public byte FogTarget => fogAlpha;
    public Precipitation ActivePrecipitation => active;
    public float Intensity => intensity;
    public float OpenSky => openSky;
    public int ActiveDrops => activeCount;
    public int ActiveSplashes { get; private set; }
    public int ActiveFogTiles { get; private set; }

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
        world = UnityEngine.Object.FindFirstObjectByType<AOWorldManagerV07>();
        ConfigureRoofs(mapEnvironment != null ? mapEnvironment.mapNumber : 0);
    }

    public void SetState(Precipitation precipitation, byte newFogAlpha)
    {
        requested = precipitation;
        fogAlpha = newFogAlpha;
        visibleFogAlpha = newFogAlpha;
        instantChange = true;
    }

    // Original client changes fog alpha by 1 every 100 ms.
    public void SetTarget(Precipitation precipitation, byte newFogAlpha)
    {
        requested = precipitation;
        fogAlpha = newFogAlpha;
    }

    void OnEnable() => AOPlayerSettingsV230.EffectsQualityChanged += OnQualityChanged;
    void OnDisable() => AOPlayerSettingsV230.EffectsQualityChanged -= OnQualityChanged;
    void OnQualityChanged() => layerMask = -1;

    void LateUpdate()
    {
        if (mapCamera == null || !mapCamera.orthographic)
            return;

        Vector3 center = mapCamera.transform.position;
        Vector2 cam = new Vector2(center.x, center.y);
        transform.position = new Vector3(center.x, center.y, 0f);
        float halfHeight = mapCamera.orthographicSize;
        float halfWidth = halfHeight * mapCamera.aspect;
        float deltaTime = Mathf.Min(AOMainMenuV140.ModalOpen
            ? Time.unscaledDeltaTime
            : Time.deltaTime, 0.1f);
        visibleFogAlpha = Mathf.MoveTowards(visibleFogAlpha, fogAlpha, deltaTime * 10f);

        UpdateOpenSky(deltaTime);
        UpdatePrecipitationState(deltaTime, cam, halfWidth, halfHeight);
        if (activeCount > 0)
            UpdateDrops(deltaTime, cam, halfWidth, halfHeight);
        UpdateSplashes(deltaTime);
        UpdateRoofWater(deltaTime);
        UpdateCharacterRain(deltaTime);
        UpdateNature(deltaTime, cam, halfWidth, halfHeight);
        UpdateFog(deltaTime, cam, halfWidth, halfHeight);
        UpdateTint(halfWidth, halfHeight);
    }

    // ------------------------------------------------------------------ precipitación

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetDebug()
    {
        DebugIntensity = -1f;
        DebugAllowAll = false;
    }

    Precipitation Resolve()
    {
        if (requested == Precipitation.Rain && (RainAllowed || DebugAllowAll) && rainSprite != null)
            return Precipitation.Rain;
        if (requested == Precipitation.Snow && (SnowAllowed || DebugAllowAll) && snowSprite != null)
            return Precipitation.Snow;
        return Precipitation.None;
    }

    void UpdatePrecipitationState(float deltaTime, Vector2 cam, float halfWidth, float halfHeight)
    {
        Precipitation want = Resolve();
        if (want != active && (active == Precipitation.None || intensity <= 0f || instantChange))
        {
            // Cambio de tipo: solo cuando el anterior ya se desvaneció (o al cargar el mapa).
            SetActiveCount(0, cam, halfWidth, halfHeight);
            active = want;
            intensity = 0f;
            layerMask = -1;
            AOAudioV190.SetWeather(active == Precipitation.Rain);
            Debug.Log("AO_MAP_WEATHER_ACTIVE mode=" + active + " drops=" + BaseCount());
        }
        intensityTarget = want == active && active != Precipitation.None
            ? (DebugIntensity >= 0f ? DebugIntensity : 1f) : 0f;
        intensity = instantChange ? intensityTarget
            : Mathf.MoveTowards(intensity, intensityTarget, deltaTime / TransitionSeconds);
        instantChange = false;
        if (active != Precipitation.None && intensity <= 0f && intensityTarget <= 0f)
        {
            SetActiveCount(0, cam, halfWidth, halfHeight);
            active = Precipitation.None;
            AOAudioV190.SetWeather(false);
            Debug.Log("AO_MAP_WEATHER_ACTIVE mode=None drops=0");
        }
        if (active == Precipitation.None)
            return;

        AOAudioV190.SetWeatherLevel(Mathf.Clamp01(intensity) * Mathf.Lerp(0.45f, 1f, openSky));
        if (layerMask < 0)
            RebuildLayers(cam, halfWidth, halfHeight);
        // Hasta 1 = la cantidad del AO; por encima (tormenta de la depuración) suma gotas hasta el tope del pool.
        int wanted = Mathf.RoundToInt(BaseCount() * AOEffectsQualityV290.Density * intensity);
        SetActiveCount(wanted, cam, halfWidth, halfHeight);
    }

    int BaseCount()
    {
        AOMapParticleDefinition definition = active == Precipitation.Rain ? rainDefinition
            : active == Precipitation.Snow ? snowDefinition : null;
        return definition == null ? 0 : Mathf.Clamp(definition.count, 0, 200);
    }

    Layer[] Layers => active == Precipitation.Snow ? SnowLayers : RainLayers;

    // Reparte los lugares del pool entre las capas habilitadas, intercalados (cualquier prefijo del pool tiene la
    // mezcla proporcional): al subir o bajar la intensidad cambian todas las capas a la vez.
    void RebuildLayers(Vector2 cam, float halfWidth, float halfHeight)
    {
        int layers = AOEffectsQualityV290.DepthLayers;
        layerMask = layers == 1 ? 0b010 : layers == 2 ? 0b011 : 0b111;
        // Pool según calidad y tipo (lluvia 200, nieve 90 del AO), con lugar para la tormenta de la depuración (x1,5).
        EnsurePool(Mathf.Min(MaxDrops, Mathf.CeilToInt(BaseCount() * AOEffectsQualityV290.Density * 1.5f)));
        Layer[] specs = Layers;
        float total = 0f;
        for (int l = 0; l < 3; l++)
            if ((layerMask & (1 << l)) != 0) total += specs[l].share;
        float acc0 = 0f, acc1 = 0f, acc2 = 0f;
        for (int i = 0; i < poolCount; i++)
        {
            int pick = 1;
            float best = float.MinValue;
            for (int l = 0; l < 3; l++)
            {
                if ((layerMask & (1 << l)) == 0) continue;
                float used = l == 0 ? acc0 : l == 1 ? acc1 : acc2;
                float deficit = specs[l].share / total * (i + 1) - used;
                if (deficit > best) { best = deficit; pick = l; }
            }
            if (pick == 0) acc0 += 1f; else if (pick == 1) acc1 += 1f; else acc2 += 1f;
            dropLayer[i] = (byte)pick;
        }
        Sprite sprite = active == Precipitation.Snow ? snowSprite : Streak();
        for (int i = 0; i < poolCount; i++)
        {
            drops[i].sprite = sprite;
            if (i < activeCount)
                Respawn(i, cam, halfWidth, halfHeight, true);
        }
    }

    void EnsurePool(int count)
    {
        if (poolCount >= count)
            return;
        System.Array.Resize(ref drops, count);
        System.Array.Resize(ref dropTransforms, count);
        System.Array.Resize(ref dropPos, count);
        System.Array.Resize(ref dropVel, count);
        System.Array.Resize(ref dropAge, count);
        System.Array.Resize(ref dropLife, count);
        System.Array.Resize(ref dropPhase, count);
        System.Array.Resize(ref dropAlpha, count);
        System.Array.Resize(ref dropLayer, count);
        Material material = Additive();
        for (int i = poolCount; i < count; i++)
        {
            GameObject child = new GameObject("WeatherDrop_" + i, typeof(SpriteRenderer));
            child.transform.SetParent(transform, false);
            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            renderer.sortingOrder = 32000;
            if (material != null)
                renderer.sharedMaterial = material;
            renderer.enabled = false;
            drops[i] = renderer;
            dropTransforms[i] = child.transform;
            dropLayer[i] = 1;
        }
        poolCount = count;
    }

    void SetActiveCount(int wanted, Vector2 cam, float halfWidth, float halfHeight)
    {
        wanted = Mathf.Clamp(wanted, 0, poolCount);
        if (wanted == activeCount)
            return;
        for (int i = activeCount; i < wanted; i++)
        {
            drops[i].enabled = true;
            Respawn(i, cam, halfWidth, halfHeight, true);
        }
        for (int i = wanted; i < activeCount; i++)
            drops[i].enabled = false;
        activeCount = wanted;
    }

    // anywhere: en cualquier altura de la pantalla (al empezar); si no, las que no aterrizan nacen arriba.
    void Respawn(int i, Vector2 cam, float halfWidth, float halfHeight, bool anywhere)
    {
        Layer spec = Layers[dropLayer[i]];
        Vector2 origin = cam * spec.parallax;
        float x = Random.Range(-halfWidth - Margin, halfWidth + Margin);
        // Las que aterrizan nacen en cualquier altura: así los impactos se reparten parejo por toda la pantalla.
        float y = anywhere || spec.lands
            ? Random.Range(-halfHeight, halfHeight + Margin)
            : halfHeight + Random.Range(0f, Margin);
        dropPos[i] = origin + new Vector2(x, y);
        bool snow = active == Precipitation.Snow;
        float speed = Random.Range(spec.speedMin, spec.speedMax);
        if (snow)
        {
            // La nieve sí la lleva el viento.
            float radians = AOWindV290.Angle * 1.6f * (0.85f + 0.3f * spec.parallax) * Mathf.Deg2Rad;
            dropVel[i] = new Vector2(Mathf.Sin(radians), -Mathf.Cos(radians)) * speed;
        }
        else
            dropVel[i] = new Vector2(0f, -speed);      // lluvia y tormenta: de arriba hacia abajo (pedido de Lucas)
        dropAge[i] = 0f;
        dropLife[i] = Random.Range(spec.landMin, spec.landMax) / speed;
        dropPhase[i] = Random.Range(0f, 6.2832f);
        dropAlpha[i] = spec.alpha * Random.Range(0.75f, 1.1f);
        Transform t = dropTransforms[i];
        float size = spec.scale * Random.Range(0.85f, 1.15f);
        // Trazo: 64 px = 1 unidad de largo; el largo sale de la capa (proporcional al personaje).
        t.localScale = snow ? new Vector3(size, size, 1f)
                            : new Vector3(size, spec.length * Random.Range(0.85f, 1.15f), 1f);
        t.localRotation = Quaternion.identity;
    }

    // Menos visible bajo techo (se ve por la ventana) y al empezar o terminar de llover.
    float AlphaScale() => Mathf.Lerp(0.2f, 1f, openSky) * Mathf.Clamp01(intensity * 3f);

    void UpdateDrops(float deltaTime, Vector2 cam, float halfWidth, float halfHeight)
    {
        Layer[] specs = Layers;
        bool snow = active == Precipitation.Snow;
        float alphaScale = AlphaScale();
        float time = Time.time;
        bool hideUnderRoof = playerRoof != 0;
        float span = 2f * (halfWidth + Margin);
        for (int i = 0; i < activeCount; i++)
        {
            Layer spec = specs[dropLayer[i]];
            Vector2 origin = cam * spec.parallax;
            dropAge[i] += deltaTime;
            Vector2 p = dropPos[i] + dropVel[i] * deltaTime;
            Vector2 local = p - origin;
            if (dropAge[i] >= dropLife[i] || local.y < -halfHeight - Margin)
            {
                if (spec.lands && dropAge[i] >= dropLife[i])
                    Land(cam + local, snow);
                Respawn(i, cam, halfWidth, halfHeight, false);
                continue;
            }
            // Envoltura: la cámara se mueve y la lluvia queda en el mundo.
            if (local.x < -halfWidth - Margin) { p.x += span; local.x += span; }
            else if (local.x > halfWidth + Margin) { p.x -= span; local.x -= span; }
            if (local.y > halfHeight + Margin * 2f)
            {
                float jump = 2f * halfHeight + Margin;
                p.y -= jump;
                local.y -= jump;
            }
            dropPos[i] = p;
            if (snow)
                local.x += Mathf.Sin(time * (0.8f + spec.sway) + dropPhase[i]) * spec.sway;
            dropTransforms[i].localPosition = local;

            // Aparecen sin "saltar" y los copos se apagan al posarse (vista cenital) en vez de cortarse.
            // Fluidez: aparecen en 0,06 s y las que aterrizan se funden en los últimos 0,04 s (sin cortes).
            float alpha = dropAlpha[i] * alphaScale * Mathf.Clamp01(dropAge[i] * (snow ? 3f : 16f));
            if (!snow && spec.lands)
                alpha *= Mathf.Clamp01((dropLife[i] - dropAge[i]) * 25f);
            if (snow)
                alpha *= Mathf.Clamp01((dropLife[i] - dropAge[i]) * 2f);
            if (hideUnderRoof && IsUnderPlayerRoof(cam + local))
                alpha = 0f;
            drops[i].color = new Color(1f, 1f, 1f, alpha);
        }
    }

    // Donde aterriza una gota del medio: sobre un techo escurre (AOMapWeatherRoofsV290); en el piso salpica.
    void Land(Vector2 worldPosition, bool snow)
    {
        if (snow || IsUnderPlayerRoof(worldPosition))
            return;
        if (TryRoofWater(worldPosition))
            return;
        if (TryTreeWater(worldPosition))
            return;
        if (InPuddle(worldPosition))
        {
            Splash(worldPosition, 1.5f);                 // onda más grande en el agua del charco
            return;
        }
        Splash(worldPosition, 1f);
        // Salpicadura con 1-2 gotitas que saltan (calidad Alta y Ultra).
        int size = AOEffectsQualityV290.BouncePool;
        if (size >= 24 && Random.value < 0.35f)
            for (int d = Random.value < 0.5f ? 1 : 2; d > 0; d--)
                SpawnBounce(size, worldPosition, new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(0.5f, 1f)),
                            Random.Range(0.18f, 0.28f), 0.35f, false);
    }

    // ------------------------------------------------------------------ salpicaduras

    void Splash(Vector2 worldPosition, float strength)
    {
        int size = AOEffectsQualityV290.SplashPool;
        if (size <= 0)
            return;
        EnsureSplashes(size);
        int i = splashNext % size;
        // El más viejo todavía se ve: el pool está lleno y esta salpicadura se omite (no se corta ninguna).
        if (splashes[i].enabled && splashAge[i] < SplashSeconds)
            return;
        splashNext = (i + 1) % size;
        splashAge[i] = 0f;
        splashStrength[i] = strength;
        splashPos[i] = worldPosition;
        splashTransforms[i].localScale = new Vector3(0.12f, 0.12f, 1f);
        splashTransforms[i].position = new Vector3(worldPosition.x, worldPosition.y, 0f);
        splashes[i].color = new Color(0.85f, 0.9f, 1f, 0f);
        splashes[i].enabled = true;
    }

    void EnsureSplashes(int size)
    {
        if (splashes != null && splashes.Length >= size)
            return;
        int old = splashes == null ? 0 : splashes.Length;
        System.Array.Resize(ref splashes, size);
        System.Array.Resize(ref splashTransforms, size);
        System.Array.Resize(ref splashPos, size);
        System.Array.Resize(ref splashAge, size);
        System.Array.Resize(ref splashStrength, size);
        Material material = Additive();
        for (int i = old; i < size; i++)
        {
            GameObject child = new GameObject("WeatherSplash_" + i, typeof(SpriteRenderer));
            child.transform.SetParent(transform, false);
            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            renderer.sprite = Ring();
            renderer.sortingOrder = 30600;
            if (material != null)
                renderer.sharedMaterial = material;
            renderer.enabled = false;
            splashes[i] = renderer;
            splashTransforms[i] = child.transform;
            splashAge[i] = SplashSeconds;
        }
    }

    void UpdateSplashes(float deltaTime)
    {
        ActiveSplashes = 0;
        if (splashes == null)
            return;
        float alphaScale = AlphaScale();
        for (int i = 0; i < splashes.Length; i++)
        {
            if (!splashes[i].enabled)
                continue;
            splashAge[i] += deltaTime;
            float k = splashAge[i] / SplashSeconds;
            if (k >= 1f)
            {
                splashes[i].enabled = false;
                continue;
            }
            ActiveSplashes++;
            // Corona chica: de 0,12 a 0,4 unidades de ancho (un cuarto del personaje como mucho).
            float s = (0.12f + k * 0.28f) * (0.7f + 0.3f * splashStrength[i]);
            splashTransforms[i].localScale = new Vector3(s, s, 1f);
            // El pool cuelga de la cámara: se reubica en el mundo cada cuadro.
            splashTransforms[i].position = new Vector3(splashPos[i].x, splashPos[i].y, 0f);
            splashes[i].color = new Color(0.85f, 0.9f, 1f, 0.38f * splashStrength[i] * (1f - k) * alphaScale);
        }
    }

    // ------------------------------------------------------------------ niebla

    void UpdateFog(float deltaTime, Vector2 cam, float halfWidth, float halfHeight)
    {
        bool show = FogVisible && fogSprites != null && fogSprites.Length >= 2 &&
                    fogSprites[0] != null && fogSprites[1] != null;
        if (!show)
        {
            if (ActiveFogTiles > 0)
                for (int layer = 0; layer < 3; layer++)
                    SetFogLayer(layer, 0);
            ActiveFogTiles = 0;
            return;
        }

        float density = visibleFogAlpha / 255f * Mathf.Lerp(0.45f, 1f, openSky);
        float drift = AOWindV290.Drift;
        int used = 0;
        // Capas del AO: mismo dibujo, alfa y sentido de movimiento que el original, pero en el mundo
        // (no se arrastran con el jugador) y empujadas por el viento. Alfa pareja: los mosaicos no marcan costuras.
        for (int layer = 0; layer < 2; layer++)
        {
            bool enabledLayer = layer < AOEffectsQualityV290.FogLayers;
            fogOffset[layer] += new Vector2((layer == 0 ? 0.28f : -0.43f) + drift * (layer == 0 ? 0.5f : 0.7f),
                                            layer == 0 ? 0.20f : -0.25f) * deltaTime;
            used += enabledLayer ? LayoutFogLayer(layer, fogSprites[layer], cam, halfWidth, halfHeight, density) : 0;
            if (!enabledLayer)
                SetFogLayer(layer, 0);
        }
        // Niebla de suelo: manchas suaves que se superponen, con bancos y claros por ruido en el mundo.
        if (AOEffectsQualityV290.Details)
        {
            fogOffset[2] += new Vector2(0.06f + drift * 0.25f, 0.015f) * deltaTime;
            used += LayoutGroundFog(cam, halfWidth, halfHeight, density);
        }
        else
            SetFogLayer(2, 0);
        ActiveFogTiles = used;
    }

    int LayoutFogLayer(int layer, Sprite sprite, Vector2 cam, float halfWidth, float halfHeight, float density)
    {
        float tile = sprite.bounds.size.x > 0.01f ? sprite.bounds.size.x : 16f;
        Vector2 offset = fogOffset[layer];
        offset.x = Mathf.Repeat(offset.x, tile);
        offset.y = Mathf.Repeat(offset.y, tile);
        fogOffset[layer] = offset;
        float startX = Mathf.Floor((cam.x - halfWidth - offset.x) / tile) * tile + offset.x;
        float startY = Mathf.Floor((cam.y - halfHeight - offset.y) / tile) * tile + offset.y;
        int columns = Mathf.FloorToInt((cam.x + halfWidth - startX) / tile) + 1;
        int rows = Mathf.FloorToInt((cam.y + halfHeight - startY) / tile) + 1;
        int count = columns * rows;
        SpriteRenderer[] tiles = EnsureFogTiles(layer, count, sprite, 31000 + layer, 1f);
        Color color = new Color(1f, 1f, 1f, Mathf.Clamp01(density));
        Vector3 pivot = sprite.bounds.center;      // los sprites del mapa tienen el pivote abajo al centro
        for (int i = 0; i < count; i++)
        {
            SpriteRenderer renderer = tiles[i];
            if (!renderer.enabled) renderer.enabled = true;
            renderer.transform.position = new Vector3(startX + (i % columns + 0.5f) * tile - pivot.x,
                                                      startY + (i / columns + 0.5f) * tile - pivot.y, 0f);
            renderer.color = color;
        }
        for (int i = count; i < tiles.Length; i++)
            if (tiles[i].enabled) tiles[i].enabled = false;
        return count;
    }

    int LayoutGroundFog(Vector2 cam, float halfWidth, float halfHeight, float density)
    {
        Vector2 offset = fogOffset[2];
        float reach = GroundFogSize * 0.5f;
        int x0 = Mathf.FloorToInt((cam.x - halfWidth - reach - offset.x) / GroundFogSpacing);
        int x1 = Mathf.CeilToInt((cam.x + halfWidth + reach - offset.x) / GroundFogSpacing);
        int y0 = Mathf.FloorToInt((cam.y - halfHeight - reach - offset.y) / GroundFogSpacing);
        int y1 = Mathf.CeilToInt((cam.y + halfHeight + reach - offset.y) / GroundFogSpacing);
        int columns = x1 - x0 + 1;
        int count = columns * (y1 - y0 + 1);
        float scale = GroundFogSize / 8f;        // la mancha mide 8 unidades a escala 1
        SpriteRenderer[] tiles = EnsureFogTiles(2, count, FogPuff(), -19000, scale);
        float time = Time.time;
        int used = 0;
        for (int i = 0; i < count; i++)
        {
            SpriteRenderer renderer = tiles[i];
            int gx = x0 + i % columns;
            int gy = y0 + i / columns;
            // Cada mancha conserva su identidad al moverse (el ruido va por su índice, no por su posición).
            float jx = Mathf.PerlinNoise(gx * 0.61f, gy * 0.37f) - 0.5f;
            float jy = Mathf.PerlinNoise(gx * 0.43f + 5.1f, gy * 0.59f) - 0.5f;
            float n = Mathf.PerlinNoise(gx * 0.19f + time * 0.01f, gy * 0.19f - time * 0.008f);
            float a = density * 0.6f * Mathf.SmoothStep(0f, 1f, (n - 0.3f) / 0.45f);
            bool visible = a > 0.01f;
            if (renderer.enabled != visible) renderer.enabled = visible;
            if (!visible) continue;
            renderer.transform.position = new Vector3(gx * GroundFogSpacing + offset.x + jx * 3f,
                                                      gy * GroundFogSpacing + offset.y + jy * 3f, 0f);
            renderer.color = new Color(0.86f, 0.9f, 0.95f, a);
            used++;
        }
        for (int i = count; i < tiles.Length; i++)
            if (tiles[i].enabled) tiles[i].enabled = false;
        return used;
    }

    SpriteRenderer[] EnsureFogTiles(int layer, int count, Sprite sprite, int sortingOrder, float scale)
    {
        int oldCount = fogTiles[layer] == null ? 0 : fogTiles[layer].Length;
        if (oldCount < count)
        {
            System.Array.Resize(ref fogTiles[layer], count);
            for (int i = oldCount; i < count; i++)
            {
                GameObject child = new GameObject("Fog_" + layer + "_" + i, typeof(SpriteRenderer));
                child.transform.SetParent(transform, false);
                child.transform.localScale = new Vector3(scale, scale, 1f);
                SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                // Capas del AO arriba de todo, como el original; la de suelo, sobre el piso y bajo objetos y techos.
                renderer.sortingOrder = sortingOrder;
                renderer.enabled = false;
                fogTiles[layer][i] = renderer;
            }
        }
        return fogTiles[layer];
    }

    void SetFogLayer(int layer, int visible)
    {
        SpriteRenderer[] tiles = fogTiles[layer];
        if (tiles == null)
            return;
        for (int i = visible; i < tiles.Length; i++)
            if (tiles[i].enabled) tiles[i].enabled = false;
    }

    // ------------------------------------------------------------------ tinte del clima

    // Día de lluvia: más frío y apagado; nieve: apenas más frío. Multiplica (el negro sigue negro).
    void UpdateTint(float halfWidth, float halfHeight)
    {
        float strength = active == Precipitation.Rain ? 0.2f : active == Precipitation.Snow ? 0.12f : 0f;
        float a = strength * Mathf.Clamp01(intensity) * Mathf.Lerp(0.6f, 1f, openSky);
        if (a <= 0.001f)
        {
            if (tint != null && tint.enabled) tint.enabled = false;
            tintAlpha = 0f;
            return;
        }
        if (tint == null)
        {
            GameObject child = new GameObject("WeatherTint", typeof(SpriteRenderer));
            child.transform.SetParent(transform, false);
            tint = child.GetComponent<SpriteRenderer>();
            tint.sprite = WhitePixel();
            tint.sortingOrder = 30990;
            Material material = Multiply();
            if (material != null)
                tint.sharedMaterial = material;
        }
        if (!tint.enabled) tint.enabled = true;
        tint.transform.localScale = new Vector3(halfWidth * 2f + 1f, halfHeight * 2f + 1f, 1f);
        if (Mathf.Abs(a - tintAlpha) > 0.002f)
        {
            tintAlpha = a;
            Color color = active == Precipitation.Snow
                ? new Color(0.84f, 0.9f, 1f, a)
                : new Color(0.42f, 0.48f, 0.6f, a);
            // Sin el shader de multiplicar: solo oscurece un poco (negro con alfa), el negro sigue negro.
            tint.color = multiplyMissing ? new Color(0f, 0f, 0f, a * 0.5f) : color;
        }
    }

    // ------------------------------------------------------------------ techo del jugador

    void UpdateOpenSky(float deltaTime)
    {
        playerRoof = world != null ? world.PlayerRoofTrigger : 0;
        openSky = Mathf.MoveTowards(openSky, playerRoof != 0 ? 0f : 1f, deltaTime * 2f);
    }

    // ------------------------------------------------------------------ materiales y texturas propias (una vez)

    static Sprite streakSprite, ringSprite, whiteSprite, puffSprite;

    static Material Additive()
    {
        if (additiveMaterial == null)
        {
            Shader shader = Resources.Load<Shader>("AOMigrator/WorldV07/AOParticleAdditive");
            if (shader != null)
                additiveMaterial = new Material(shader);
        }
        return additiveMaterial;
    }

    static Material Multiply()
    {
        if (multiplyMaterial == null && !multiplyMissing)
        {
            Shader shader = Resources.Load<Shader>("AOMigrator/WorldV07/AOWeatherMultiply");
            if (shader != null && shader.isSupported)
                multiplyMaterial = new Material(shader);
            else
                multiplyMissing = true;
        }
        return multiplyMaterial;
    }

    // Trazo de lluvia: fino, más brillante en la punta de abajo, cola que se apaga (8x64 px, 64 px = 1 unidad).
    static Sprite Streak()
    {
        if (streakSprite != null) return streakSprite;
        const int w = 8, h = 64;
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "AO Rain Streak", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float across = 1f - Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                float along = (float)y / (h - 1);                       // 0 = abajo (punta), 1 = arriba (cola)
                float a = Mathf.Pow(across, 1.6f) * Mathf.Pow(1f - along, 0.7f) * Mathf.Clamp01(along * 8f + 0.3f);
                pixels[y * w + x] = new Color32(220, 232, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        streakSprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.1f), 64f);
        return streakSprite;
    }

    // Anillo de salpicadura, achatado (vista cenital con leve perspectiva): 32x16 px.
    static Sprite Ring()
    {
        if (ringSprite != null) return ringSprite;
        const int w = 32, h = 16;
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "AO Rain Splash", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f) / w * 2f - 1f, dy = (y + 0.5f) / h * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.75f) * 5f) + Mathf.Clamp01(0.35f - r) * 1.2f;
                pixels[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        ringSprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
        return ringSprite;
    }

    // Mancha de niebla de suelo: borde muy suave con algo de textura (64x64 px = 8 unidades a escala 1).
    static Sprite FogPuff()
    {
        if (puffSprite != null) return puffSprite;
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "AO Ground Fog", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float falloff = Mathf.Clamp01(1f - r);
                falloff = falloff * falloff * (3f - 2f * falloff);
                float wisps = 0.65f + 0.35f * Mathf.PerlinNoise(x * 0.09f, y * 0.13f) +
                              0.2f * (Mathf.PerlinNoise(x * 0.23f + 7f, y * 0.21f) - 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(falloff * wisps) * 255f));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        puffSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 8f);
        return puffSprite;
    }

    static Sprite WhitePixel()
    {
        if (whiteSprite != null) return whiteSprite;
        var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "AO Weather Tint", filterMode = FilterMode.Point };
        var pixels = new Color32[16];
        for (int i = 0; i < 16; i++) pixels[i] = new Color32(255, 255, 255, 255);
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        whiteSprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        return whiteSprite;
    }
}
