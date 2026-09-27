using UnityEditor;
using UnityEngine;

// AOWeatherDebugV290 (nube, 26/09): ventana del Editor para probar el clima en Play sin servidor.
// 27/09: también el cielo (hora, nubes, sol y luna) y las sombras de los personajes (V291).
// Menú "AO Migrator/Clima (depuración)". Solo Editor: el jugador no la ve y no hay teclas nuevas.
// Nada queda guardado: la calidad y los valores de depuración se reinician al salir de Play (no toca PlayerPrefs).
public class AOWeatherDebugV290 : EditorWindow
{
    bool smooth = true;
    bool overrideIntensity;
    float intensity = 1f;
    bool overrideWind;
    float windStrength = 0.3f;
    bool overrideAngle;
    float windAngle = -14f;
    int fogAlpha = 90;
    float fps;
    double lastRepaint;
    float hour = 13f;
    bool overrideCoverage;
    float coverage = 0.35f;
    Vector2 scroll;

    static readonly string[] QualityNames = { "Del jugador", "Low", "Medium", "High", "Ultra" };

    [MenuItem("AO Migrator/Clima (depuración)")]
    static void Open() => GetWindow<AOWeatherDebugV290>("Clima");

    void OnEnable() => EditorApplication.update += Tick;
    void OnDisable() => EditorApplication.update -= Tick;

    void Tick()
    {
        if (!EditorApplication.isPlaying)
            return;
        float dt = Time.unscaledDeltaTime;
        if (dt > 0f)
            fps = Mathf.Lerp(fps, 1f / dt, 0.1f);
        if (EditorApplication.timeSinceStartup - lastRepaint > 0.25)
        {
            lastRepaint = EditorApplication.timeSinceStartup;
            Repaint();
        }
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("Entrá en Play (con el candado de Unity) para probar el clima.\n" +
                                    "El clima en el juego sigue dormido: esta ventana es la forma de verlo.",
                                    MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        AOWorldManagerV07 world = UnityEngine.Object.FindFirstObjectByType<AOWorldManagerV07>();
        if (world != null)
            DrawSky(world);
        AOMapWeather weather = world != null ? world.CurrentWeather : null;
        if (weather == null)
        {
            EditorGUILayout.HelpBox("Este mapa no tiene clima (map_environment sin lluvia, nieve ni niebla). " +
                                    "Probá en Ullathorpe (mapa 1).", MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        EditorGUILayout.LabelField("Clima rápido", EditorStyles.boldLabel);
        smooth = EditorGUILayout.Toggle(new GUIContent("Transición suave", "Apagado: cambia al instante"), smooth);
        AOMapWeather.DebugAllowAll = EditorGUILayout.Toggle(
            new GUIContent("Ignorar permisos del mapa", "Permite nieve o niebla en mapas que no la tienen"),
            AOMapWeather.DebugAllowAll);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Despejado")) Preset(world, weather, AOMapWeather.Precipitation.None, -1f, 0, -1f, float.NaN);
        if (GUILayout.Button("Llovizna")) Preset(world, weather, AOMapWeather.Precipitation.Rain, 0.4f, 0, 0.12f, float.NaN);
        if (GUILayout.Button("Lluvia")) Preset(world, weather, AOMapWeather.Precipitation.Rain, 1f, 0, -1f, float.NaN);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Lluvia fuerte")) Preset(world, weather, AOMapWeather.Precipitation.Rain, 1.3f, 0, 0.55f, float.NaN);
        if (GUILayout.Button(new GUIContent("Tormenta", "Lluvia fuerte y viento fuerte (rayos y truenos: próximo paquete)")))
            Preset(world, weather, AOMapWeather.Precipitation.Rain, 1.5f, 40, 0.85f, -32f);
        if (GUILayout.Button("Niebla")) Preset(world, weather, AOMapWeather.Precipitation.None, -1f, 110, 0.2f, float.NaN);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Nieve")) Preset(world, weather, AOMapWeather.Precipitation.Snow, 1f, 0, -1f, float.NaN);
        if (GUILayout.Button("Ventisca")) Preset(world, weather, AOMapWeather.Precipitation.Snow, 1.5f, 60, 0.9f, -42f);
        if (GUILayout.Button("Solo viento")) { overrideWind = true; windStrength = 0.8f; }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Ajuste fino", EditorStyles.boldLabel);
        overrideIntensity = EditorGUILayout.ToggleLeft("Intensidad (1 = la del AO)", overrideIntensity);
        using (new EditorGUI.DisabledScope(!overrideIntensity))
            intensity = EditorGUILayout.Slider(intensity, 0f, 1.5f);
        AOMapWeather.DebugIntensity = overrideIntensity ? intensity : -1f;

        overrideWind = EditorGUILayout.ToggleLeft("Fuerza del viento", overrideWind);
        using (new EditorGUI.DisabledScope(!overrideWind))
            windStrength = EditorGUILayout.Slider(windStrength, 0f, 1f);
        AOWindV290.DebugStrength = overrideWind ? windStrength : -1f;

        overrideAngle = EditorGUILayout.ToggleLeft("Ángulo del viento (nieve y niebla; la lluvia cae vertical)", overrideAngle);
        using (new EditorGUI.DisabledScope(!overrideAngle))
            windAngle = EditorGUILayout.Slider(windAngle, -55f, 55f);
        AOWindV290.DebugAngle = overrideAngle ? windAngle : float.NaN;

        EditorGUI.BeginChangeCheck();
        fogAlpha = EditorGUILayout.IntSlider("Niebla (alfa del AO)", fogAlpha, 0, 255);
        if (EditorGUI.EndChangeCheck())
            weather.SetTarget(weather.RequestedPrecipitation, (byte)fogAlpha);

        int quality = AOEffectsQualityV290.DebugLevel + 1;
        int nextQuality = EditorGUILayout.Popup("Calidad de efectos", quality, QualityNames);
        if (nextQuality != quality)
            AOEffectsQualityV290.DebugLevel = nextQuality - 1;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Estado", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Clima", weather.ActivePrecipitation + "  (pedido: " + weather.RequestedPrecipitation +
                                            ", intensidad " + weather.Intensity.ToString("0.00") + ")");
        EditorGUILayout.LabelField("Calidad", AOEffectsQualityV290.Level.ToString());
        EditorGUILayout.LabelField("Gotas / copos", weather.ActiveDrops.ToString());
        EditorGUILayout.LabelField("Salpicaduras", weather.ActiveSplashes.ToString());
        EditorGUILayout.LabelField("Agua en techos", weather.ActiveRoofWater +
                                   (weather.HasRoofData ? "" : "  (este mapa no tiene datos de techos)"));
        EditorGUILayout.LabelField("Rebotes en el jugador", weather.ActiveBounces.ToString());
        EditorGUILayout.LabelField("Niebla (mosaicos)", weather.ActiveFogTiles.ToString());
        EditorGUILayout.LabelField("Cielo abierto", weather.OpenSky.ToString("0.00") +
                                   (world.PlayerUnderRoof ? "  (bajo techo)" : ""));
        EditorGUILayout.LabelField("Viento", "fuerza " + AOWindV290.Strength.ToString("0.00") +
                                   ", ángulo " + AOWindV290.Angle.ToString("0") + "°, ráfaga " + AOWindV290.Gust.ToString("0.00"));
        EditorGUILayout.LabelField("Cuadro", fps.ToString("0") + " FPS  (" + (fps > 0f ? 1000f / fps : 0f).ToString("0.0") + " ms)");
        EditorGUILayout.EndScrollView();
    }

    // Cielo y sombras (V291): hora del mundo, nubes y contadores.
    void DrawSky(AOWorldManagerV07 world)
    {
        EditorGUILayout.LabelField("Cielo y sombras", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Amanecer 7 h")) SetHour(world, 7f);
        if (GUILayout.Button("Mediodía 13 h")) SetHour(world, 13f);
        if (GUILayout.Button("Atardecer 17:30")) SetHour(world, 17.5f);
        if (GUILayout.Button("Noche 23 h")) SetHour(world, 23f);
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button(new GUIContent("Relámpago ahora", "Solo se ve al aire libre; en tormenta caen solos cada 6-16 s")))
            AOLivingLightV292.DebugStrikeNow = true;
        EditorGUILayout.BeginHorizontal();
        // Cambiar la hora re-ilumina el mapa entero: se aplica con el botón, no mientras se arrastra.
        hour = EditorGUILayout.Slider("Hora", hour, 0f, 24f);
        if (GUILayout.Button("Aplicar", GUILayout.Width(70f))) SetHour(world, hour);
        EditorGUILayout.EndHorizontal();

        overrideCoverage = EditorGUILayout.ToggleLeft("Nubes (0 = despejado, 1 = cubierto)", overrideCoverage);
        using (new EditorGUI.DisabledScope(!overrideCoverage))
            coverage = EditorGUILayout.Slider(coverage, 0f, 1f);
        AOSkyV291.DebugCoverage = overrideCoverage ? coverage : -1f;

        EditorGUILayout.LabelField("Hora del mundo", world.CurrentWorldHour.ToString("0.0") +
                                   (AOSkyV291.Outdoor ? (AOSkyV291.Night ? "  (noche, luna)" : "  (día, sol)") : "  (sin cielo: dungeon o luz fija)"));
        AOSkyV291 sky = AOSkyV291.Instance;
        EditorGUILayout.LabelField("Nubes", "cobertura " + AOSkyV291.Coverage.ToString("0.00") +
                                   (sky != null ? ", sombras " + sky.ActiveClouds + ", luna " + sky.ActiveMoonPatches : ""));
        EditorGUILayout.LabelField("Sombras", AOCharacterShadowsV291.ActiveShadows + " personajes, opacidad del sol/luna " +
                                   AOSkyV291.KeyShadowAlpha.ToString("0.00") + ", largo " + AOSkyV291.ShadowLength.ToString("0.0"));
        AOLivingLightV292 living = AOLivingLightV292.Instance;
        if (living != null)
            EditorGUILayout.LabelField("Luz viva", "oscuridad " + AOLivingLightV292.Darkness.ToString("0.00") + ", halos " +
                                       living.ActiveGlows + ", luciérnagas " + living.ActiveFireflies +
                                       ", relámpago " + AOLivingLightV292.FlashStrength.ToString("0.00"));
        EditorGUILayout.Space();
    }

    void SetHour(AOWorldManagerV07 world, float value)
    {
        hour = value;
        world.SetWorldHour(value);
    }

    void Preset(AOWorldManagerV07 world, AOMapWeather weather, AOMapWeather.Precipitation precipitation,
                float presetIntensity, int presetFog, float wind, float angle)
    {
        overrideIntensity = presetIntensity >= 0f;
        if (overrideIntensity) intensity = presetIntensity;
        overrideWind = wind >= 0f;
        if (overrideWind) windStrength = wind;
        overrideAngle = !float.IsNaN(angle);
        if (overrideAngle) windAngle = angle;
        fogAlpha = presetFog;
        // Nieve o niebla en un mapa que no las tiene (Ullathorpe solo tiene lluvia): se permiten para probar.
        if (precipitation == AOMapWeather.Precipitation.Snow && !weather.SnowAllowed ||
            precipitation == AOMapWeather.Precipitation.Rain && !weather.RainAllowed ||
            presetFog > 0 && !weather.FogAllowed)
            AOMapWeather.DebugAllowAll = true;
        AOMapWeather.DebugIntensity = overrideIntensity ? intensity : -1f;
        AOWindV290.DebugStrength = overrideWind ? windStrength : -1f;
        AOWindV290.DebugAngle = overrideAngle ? windAngle : float.NaN;
        if (smooth)
            weather.SetTarget(precipitation, (byte)presetFog);
        else
            world.SetWeather(precipitation, (byte)presetFog);
    }
}
