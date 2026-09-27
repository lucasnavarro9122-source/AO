using UnityEngine;

// AOFirefliesV292 (nube, 27/09, pedido de Lucas): luciérnagas con carácter, como un enjambre que se comporta como el
// agua: se juntan entre ellas, buscan lo oscuro para iluminarlo, y se abren y se escurren alrededor de lo que no son
// ellas (personajes, jugadores, faroles y antorchas), para volver a juntarse del otro lado.
// - Campo (y todo mapa al aire libre que no sea ciudad): un enjambre de 14 que se instala en el rincón más oscuro a la
//   vista y se mueve despacio hacia donde hay menos luz. Cada tanto una sale a explorar y después vuelve.
// - Ciudad: casi nunca; cada tanto aparece alguna suelta, deambula y se pierde (se apaga sola).
// - Parpadeo: cada una a su ritmo, pero las que están cerca se van sincronizando de a poco (como las de verdad).
// - Debajo del enjambre, un resplandor verdoso muy tenue en el piso: iluminan donde están.
// Reglas de bandada (cohesión, separación, evitar, deambular) con ruido coherente; 16 sprites fijos, sin memoria por
// cuadro. Solo de noche al aire libre, sin lluvia, calidad Alta y Ultra.
public partial class AOLivingLightV292
{
    const int FireflyCount = 16;
    const int SwarmSize = 14;             // los 2 últimos lugares son para sueltas
    const float MaxSpeed = 1.1f;

    enum Role : byte { Off, Swarm, Explorer, Stray }

    SpriteRenderer[] fireflies;
    SpriteRenderer swarmGlow;
    Vector2[] flyPos, flyVel, flyHeading;
    float[] flyPhase, flySeed, flyTimer, flyFade;
    Role[] flyRole;
    Vector2 swarmHome;
    bool swarmPlaced;
    float nextExplore, nextStray;

    public int ActiveFireflies { get; private set; }

    void UpdateFireflies(Vector2 center, float halfWidth, float halfHeight, float deltaTime)
    {
        AOMapWeather weather = world.CurrentWeather;
        bool raining = weather != null && weather.ActivePrecipitation != AOMapWeather.Precipitation.None;
        bool on = AOEffectsQualityV290.Fireflies && AOSkyV291.Outdoor && AOSkyV291.Night && !raining &&
                  AOSkyV291.Coverage < 0.8f;
        float night = Mathf.Clamp01((1f - AOSkyV291.Daylight - 0.5f) * 2f);
        if (!on || night <= 0f)
        {
            HideFireflies();
            swarmPlaced = false;
            return;
        }
        EnsureFireflies();
        bool city = string.Equals(world.CurrentMapZone, "CIUDAD", System.StringComparison.OrdinalIgnoreCase);
        float time = Time.time;

        // Enjambre: se instala en lo más oscuro a la vista; si queda lejos de la cámara, se muda.
        if (!city && (!swarmPlaced || Mathf.Abs(swarmHome.x - center.x) > halfWidth + 6f ||
                      Mathf.Abs(swarmHome.y - center.y) > halfHeight + 6f))
        {
            swarmHome = DarkestSpot(center, halfWidth, halfHeight);
            for (int i = 0; i < SwarmSize; i++)
                Spawn(i, swarmHome + Random.insideUnitCircle * 1.8f, Role.Swarm, 0f);
            swarmPlaced = true;
            nextExplore = time + Random.Range(6f, 14f);
        }
        if (city && swarmPlaced)
        {
            for (int i = 0; i < SwarmSize; i++) if (flyRole[i] != Role.Off) { flyRole[i] = Role.Stray; flyTimer[i] = 0f; }
            swarmPlaced = false;
        }

        // Cada tanto una sale a explorar (y después vuelve con las demás).
        if (!city && time >= nextExplore)
        {
            nextExplore = time + Random.Range(8f, 20f);
            int pick = Random.Range(0, SwarmSize);
            if (flyRole[pick] == Role.Swarm)
            {
                flyRole[pick] = Role.Explorer;
                flyTimer[pick] = Random.Range(4f, 8f);
                flyHeading[pick] = Random.insideUnitCircle.normalized;
            }
        }
        // Sueltas: en la ciudad son las únicas y aparecen poco; en el campo, alguna de vez en cuando.
        if (time >= nextStray)
        {
            nextStray = time + (city ? Random.Range(25f, 60f) : Random.Range(15f, 40f));
            for (int i = SwarmSize; i < FireflyCount; i++)
                if (flyRole[i] == Role.Off)
                {
                    Spawn(i, DarkestSpot(center, halfWidth, halfHeight), Role.Stray, Random.Range(12f, 25f));
                    break;
                }
        }

        // Centro del enjambre; el hogar se corre despacio hacia lo más oscuro.
        Vector2 centroid = Vector2.zero;
        int members = 0;
        for (int i = 0; i < SwarmSize; i++)
            if (flyRole[i] == Role.Swarm) { centroid += flyPos[i]; members++; }
        if (members > 0)
        {
            centroid /= members;
            swarmHome += AwayFromLight(swarmHome) * 0.25f * deltaTime;
        }

        int lit = 0;
        float glowSum = 0f;
        for (int i = 0; i < FireflyCount; i++)
        {
            if (flyRole[i] == Role.Off) continue;
            Vector2 p = flyPos[i];
            float seed = flySeed[i];
            // Deambular: ruido coherente (sin saltos).
            Vector2 steer = new Vector2(Mathf.PerlinNoise(time * 0.3f, seed) - 0.5f,
                                        Mathf.PerlinNoise(seed, time * 0.3f) - 0.5f) * 1.6f;
            switch (flyRole[i])
            {
                case Role.Swarm:
                    if (members > 1) steer += (centroid - p) * 0.35f;         // juntas, como el agua
                    steer += (swarmHome - p) * 0.12f;
                    break;
                case Role.Explorer:
                    steer += flyHeading[i] * 0.9f;
                    flyTimer[i] -= deltaTime;
                    if (flyTimer[i] <= 0f) flyRole[i] = Role.Swarm;          // vuelve con las demás
                    break;
                case Role.Stray:
                    steer *= 1.5f;                                           // sin rumbo: se pierde
                    flyTimer[i] -= deltaTime;
                    break;
            }
            // Separación: juntas pero sin encimarse.
            for (int j = 0; j < FireflyCount; j++)
            {
                if (j == i || flyRole[j] == Role.Off) continue;
                Vector2 d = p - flyPos[j];
                float m2 = d.sqrMagnitude;
                if (m2 < 0.2f && m2 > 0.0001f) steer += d / m2 * 0.06f;
            }
            // Se escurren alrededor de personajes y de otras luces.
            steer += AwayFromLight(p) * 1.6f;
            for (int c = 0; c < AOCharacterShadowsV291.VisibleCount; c++)
            {
                Vector2 d = p - (AOCharacterShadowsV291.VisibleFeet[c] + new Vector2(0f, 0.8f));  // el cuerpo, no los pies
                float m = d.magnitude;
                if (m < 1.8f && m > 0.01f) steer += d / m * (1f - m / 1.8f) * 3f;
            }

            Vector2 v = flyVel[i] * (1f - Mathf.Min(1f, 1.5f * deltaTime)) + steer * deltaTime;
            float max = flyRole[i] == Role.Explorer ? MaxSpeed * 1.5f : MaxSpeed;
            if (v.sqrMagnitude > max * max) v = v.normalized * max;
            flyVel[i] = v;
            p += v * deltaTime;
            // Si se va de la vista: las del enjambre vuelven a su lugar; las sueltas se pierden.
            bool outside = Mathf.Abs(p.x - center.x) > halfWidth + 2f || Mathf.Abs(p.y - center.y) > halfHeight + 2f;
            if (outside && flyRole[i] != Role.Stray)
                p = swarmHome + Random.insideUnitCircle * 1.5f;
            flyPos[i] = p;

            // Parpadeo que se sincroniza con las vecinas (acople suave de fases).
            float coupling = 0f;
            int near = 0;
            for (int j = 0; j < FireflyCount; j++)
            {
                if (j == i || flyRole[j] == Role.Off || (flyPos[j] - p).sqrMagnitude > 9f) continue;
                coupling += Mathf.Sin(flyPhase[j] - flyPhase[i]);
                near++;
            }
            flyPhase[i] += deltaTime * (3.4f + (near > 0 ? 0.9f * coupling / near : 0f));
            float blink = Mathf.Max(0f, Mathf.Sin(flyPhase[i]));
            blink = blink * blink * blink;

            // Fundidos: aparecer, y apagarse cuando una suelta ya se perdió.
            if (flyRole[i] == Role.Stray && (flyTimer[i] <= 0f || outside))
                flyFade[i] = Mathf.MoveTowards(flyFade[i], 0f, deltaTime * 0.5f);
            else
                flyFade[i] = Mathf.MoveTowards(flyFade[i], 1f, deltaTime * 0.7f);
            if (flyRole[i] == Role.Stray && flyFade[i] <= 0f)
            {
                flyRole[i] = Role.Off;
                if (fireflies[i].enabled) fireflies[i].enabled = false;
                continue;
            }

            float alpha = (0.15f + 0.85f * blink) * flyFade[i] * night;
            SpriteRenderer f = fireflies[i];
            bool visible = alpha > 0.02f;
            if (f.enabled != visible) f.enabled = visible;
            if (!visible) continue;
            f.transform.position = new Vector3(p.x, p.y, 0f);
            float size = 0.24f + 0.12f * blink;
            f.transform.localScale = new Vector3(size, size, 1f);
            f.color = new Color(0.8f, 1f, 0.45f, alpha);
            if (flyRole[i] == Role.Swarm) glowSum += alpha;
            lit++;
        }
        ActiveFireflies = lit;

        // Resplandor del enjambre en el piso: tenue y verdoso, sube cuando se prenden juntas.
        if (swarmGlow == null)
            swarmGlow = MakeRenderer("FireflyGlow", -18890);
        bool glow = members > 2 && glowSum > 0.2f;
        if (swarmGlow.enabled != glow) swarmGlow.enabled = glow;
        if (glow)
        {
            swarmGlow.transform.position = new Vector3(centroid.x, centroid.y, 0f);
            swarmGlow.transform.localScale = new Vector3(4.5f, 4.5f, 1f);
            swarmGlow.color = new Color(0.55f, 0.85f, 0.35f, Mathf.Min(0.12f, glowSum * 0.012f));
        }
    }

    // Empuje para alejarse de faroles y antorchas (0 lejos de toda luz).
    Vector2 AwayFromLight(Vector2 p)
    {
        Vector2 push = Vector2.zero;
        for (int l = 0; l < lampCount; l++)
        {
            Vector2 d = p - lampPos[l];
            float reach = lampRadius[l] + 1.5f;
            float m = d.magnitude;
            if (m < reach && m > 0.01f) push += d / m * (1f - m / reach);
        }
        return push;
    }

    // El lugar más oscuro a la vista: lejos de las luces y de los personajes, y sin techo encima.
    Vector2 DarkestSpot(Vector2 center, float halfWidth, float halfHeight)
    {
        Vector2 best = center;
        float bestScore = float.MinValue;
        for (int k = 0; k < 12; k++)
        {
            var q = new Vector2(center.x + Random.Range(-halfWidth, halfWidth) * 0.85f,
                                center.y + Random.Range(-halfHeight, halfHeight) * 0.85f);
            if (world.IsRoofAt(q)) continue;
            float score = -AwayFromLight(q).magnitude * 3f;
            for (int c = 0; c < AOCharacterShadowsV291.VisibleCount; c++)
                score += Mathf.Min(4f, (AOCharacterShadowsV291.VisibleFeet[c] - q).magnitude) * 0.3f;
            if (score > bestScore) { bestScore = score; best = q; }
        }
        return best;
    }

    void Spawn(int i, Vector2 position, Role role, float timer)
    {
        flyPos[i] = position;
        flyVel[i] = Vector2.zero;
        flyRole[i] = role;
        flyTimer[i] = timer;
        flyFade[i] = 0f;
        flyPhase[i] = Random.Range(0f, 6.2832f);
    }

    void EnsureFireflies()
    {
        if (fireflies != null) return;
        fireflies = new SpriteRenderer[FireflyCount];
        flyPos = new Vector2[FireflyCount];
        flyVel = new Vector2[FireflyCount];
        flyHeading = new Vector2[FireflyCount];
        flyPhase = new float[FireflyCount];
        flySeed = new float[FireflyCount];
        flyTimer = new float[FireflyCount];
        flyFade = new float[FireflyCount];
        flyRole = new Role[FireflyCount];
        for (int i = 0; i < FireflyCount; i++)
        {
            fireflies[i] = MakeRenderer("Firefly_" + i, 30430);
            flySeed[i] = Random.Range(0f, 100f);
        }
        nextStray = Time.time + Random.Range(10f, 30f);
    }

    void HideFireflies()
    {
        if (fireflies != null)
            for (int i = 0; i < fireflies.Length; i++)
            {
                flyRole[i] = Role.Off;
                if (fireflies[i].enabled) fireflies[i].enabled = false;
            }
        if (swarmGlow != null && swarmGlow.enabled) swarmGlow.enabled = false;
        ActiveFireflies = 0;
    }
}
