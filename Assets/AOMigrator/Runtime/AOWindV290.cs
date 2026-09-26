using UnityEngine;

// AOWindV290: viento global y suave para el clima y los efectos ambientales (nube, 26/09).
// Una sola fuente para lluvia, nieve, niebla, humo y polvo: dirección, fuerza, ráfagas y turbulencia con ruido
// coherente (Perlin en el tiempo), sin saltos al azar cuadro a cuadro. Se calcula una vez por cuadro.
// Angle: grados desde la vertical (negativo = hacia la izquierda). Strength: 0 (calma) a 1 (ventisca).
public static class AOWindV290
{
    // Viento de base del mapa o del perfil de clima; el clima lo lleva de a poco a su objetivo.
    public static float BaseStrength = 0.3f;
    public static float BaseAngle = -14f;

    // Depuración (ventana del Editor): valores >= 0 reemplazan fuerza y ángulo.
    public static float DebugStrength = -1f;
    public static float DebugAngle = float.NaN;

    static int sampledFrame = -1;
    static float angle, strength, gust;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        BaseStrength = 0.3f;
        BaseAngle = -14f;
        DebugStrength = -1f;
        DebugAngle = float.NaN;
        sampledFrame = -1;
    }

    public static float Angle { get { Sample(); return angle; } }
    public static float Strength { get { Sample(); return strength; } }
    public static float Gust { get { Sample(); return gust; } }

    // Dirección de caída (hacia abajo en pantalla) inclinada por el viento, en unidades de mundo.
    public static Vector2 FallDirection
    {
        get
        {
            float radians = Angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(radians), -Mathf.Cos(radians));
        }
    }

    // Deriva horizontal (unidades por segundo) para humo, niebla y partículas livianas.
    public static float Drift => Mathf.Sin(Angle * Mathf.Deg2Rad) * (0.4f + 1.6f * Strength);

    static void Sample()
    {
        int frame = Time.frameCount;
        if (frame == sampledFrame) return;
        sampledFrame = frame;
        float t = Time.unscaledTime;
        // Ráfagas: picos suaves y poco frecuentes (curva ^3 del ruido lento).
        float g = Mathf.PerlinNoise(t * 0.21f, 7.3f);
        gust = g * g * g;
        float slow = Mathf.PerlinNoise(t * 0.045f, 1.7f) - 0.5f;
        float s = BaseStrength * (0.75f + 0.5f * slow) + gust * 0.45f * (0.3f + BaseStrength);
        strength = DebugStrength >= 0f ? DebugStrength : Mathf.Clamp01(s);
        // Turbulencia leve del ángulo, más fuerte con viento fuerte.
        float wobble = (Mathf.PerlinNoise(t * 0.33f, 4.1f) - 0.5f) * (4f + 10f * strength);
        float a = BaseAngle * (0.35f + 1.1f * strength) + wobble;
        angle = !float.IsNaN(DebugAngle) ? DebugAngle : Mathf.Clamp(a, -55f, 55f);
    }
}
