using UnityEngine;

// AOEffectsQualityV290: calidad de efectos visuales (nube, 26/09). Solo cambia presentación: cantidades, capas y
// extras de clima y efectos ambientales. Nunca toca gameplay (colisión, visión, daño ni tiempos).
// Se guarda en las mismas PlayerPrefs que el resto de los ajustes (AOPlayerSettingsV230). La opción en el menú
// la agrega Interfaz: AOPlayerSettingsV230.EffectsQuality = ...
public enum AOEffectsQuality { Low = 0, Medium = 1, High = 2, Ultra = 3 }

public static partial class AOPlayerSettingsV230
{
    static int effectsQualityCache = -1;

    public static event System.Action EffectsQualityChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetEffectsQuality() => effectsQualityCache = -1;

    public static AOEffectsQuality EffectsQuality
    {
        get
        {
            if (effectsQualityCache < 0)
                effectsQualityCache = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "effectsQuality",
                    (int)AOEffectsQuality.High), 0, 3);
            return (AOEffectsQuality)effectsQualityCache;
        }
        set
        {
            int next = Mathf.Clamp((int)value, 0, 3);
            if (next == effectsQualityCache) return;
            effectsQualityCache = next;
            PlayerPrefs.SetInt(Prefix + "effectsQuality", next);
            PlayerPrefs.Save();
            EffectsQualityChanged?.Invoke();
        }
    }

    public static void NotifyEffectsQualityChanged() => EffectsQualityChanged?.Invoke();
}

// Multiplicadores por nivel, en un solo lugar para todos los efectos.
public static class AOEffectsQualityV290
{
    // Depuración (ventana "Clima" del Editor): >= 0 reemplaza el nivel sin tocar las preferencias del jugador.
    static int debugLevel = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetDebug() => debugLevel = -1;

    public static int DebugLevel
    {
        get => debugLevel;
        set
        {
            int next = value < 0 ? -1 : Mathf.Clamp(value, 0, 3);
            if (next == debugLevel) return;
            debugLevel = next;
            AOPlayerSettingsV230.NotifyEffectsQualityChanged();
        }
    }

    public static AOEffectsQuality Level =>
        debugLevel >= 0 ? (AOEffectsQuality)debugLevel : AOPlayerSettingsV230.EffectsQuality;

    // Cantidad de partículas de clima (sobre la cuenta original del AO).
    public static float Density
    {
        get
        {
            switch (Level)
            {
                case AOEffectsQuality.Low: return 0.4f;
                case AOEffectsQuality.Medium: return 0.7f;
                case AOEffectsQuality.Ultra: return 1.3f;
                default: return 1f;
            }
        }
    }

    // Capas de profundidad: Low solo la del medio; Medium fondo + medio; High y Ultra las tres.
    public static int DepthLayers => Level == AOEffectsQuality.Low ? 1 : Level == AOEffectsQuality.Medium ? 2 : 3;

    // Detalles secundarios: salpicaduras, agua en los techos, niebla de suelo.
    public static bool Details => Level >= AOEffectsQuality.Medium;
    public static int SplashPool => Level == AOEffectsQuality.Low ? 0 : Level == AOEffectsQuality.Medium ? 14 : Level == AOEffectsQuality.High ? 26 : 36;
    public static int RoofPool => Level == AOEffectsQuality.Low ? 0 : Level == AOEffectsQuality.Medium ? 12 : Level == AOEffectsQuality.High ? 24 : 32;
    // Gotitas que rebotan en la armadura del jugador.
    public static int BouncePool => Level == AOEffectsQuality.Low ? 0 : Level == AOEffectsQuality.Medium ? 12 : Level == AOEffectsQuality.High ? 24 : 36;
    // Cielo (V291): sombras de nubes y manchas de luna desde Medium; rayos entre nubes desde High.
    public static bool CloudShadows => Level >= AOEffectsQuality.Medium;
    public static bool SkyRays => Level >= AOEffectsQuality.High;
    // Sombras de personajes: en Low solo el jugador; desde Medium, todos los que se ven (hasta este tope).
    public static int CharacterShadows => Level == AOEffectsQuality.Low ? 1 : Level == AOEffectsQuality.Medium ? 16 : Level == AOEffectsQuality.High ? 32 : 48;
    // Luz viva (V292): halos de faroles y luz sobre los personajes desde Medium; luciérnagas desde High.
    public static bool LightGlows => Level >= AOEffectsQuality.Medium;
    public static bool CharacterLight => Level >= AOEffectsQuality.Medium;
    public static bool Fireflies => Level >= AOEffectsQuality.High;
    public static int FogLayers => Level == AOEffectsQuality.Low ? 1 : 2;
}
