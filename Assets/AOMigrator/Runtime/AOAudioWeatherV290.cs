using UnityEngine;

// AOAudioWeatherV290 (nube, 26/09): volumen de la lluvia según la intensidad del clima y el techo (más apagada
// bajo techo). No cambia qué sonido suena ni cuándo (eso sigue en SetWeather).
public partial class AOAudioV190
{
    public static void SetWeatherLevel(float level)
    {
        if (instance == null || instance.weather == null || !instance.weatherActive)
            return;
        float volume = 0.3f * AOPlayerSettingsV230.Ambient * Mathf.Clamp01(level);
        if (Mathf.Abs(instance.weather.volume - volume) > 0.005f)
            instance.weather.volume = volume;
    }
}
