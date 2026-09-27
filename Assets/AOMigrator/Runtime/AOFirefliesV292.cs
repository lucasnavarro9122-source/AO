using UnityEngine;

// AOFirefliesV292 (nube, 27/09, pedido de Lucas): luciérnagas con mente colmena y momentos de individualidad.
// - Enjambre amplio (22) que RECORRE lo oscuro: la colmena elige un rumbo (el próximo rincón oscuro, a 4-9 unidades)
//   cada 5-9 s y todas lo siguen alineando su vuelo; se reparten en un área grande (separación amplia, cohesión
//   suave), como agua que fluye junta.
// - Reacción casi instantánea: la que ve a un personaje o una luz cerca dispara una alarma y TODO el enjambre se abre y
//   se escurre alrededor al mismo tiempo (mente colmena); después se vuelven a juntar.
// - Individualidad: cada tanto una hace una escapada corta (1-2,5 s) por su cuenta y vuelve; cada 8-20 s una sale a
//   explorar más lejos.
// - Ciudad: sin enjambre; cada tanto aparece alguna suelta que deambula y se pierde.
// - Parpadeo que se sincroniza entre vecinas; resplandor verdoso tenue en el piso donde está el enjambre.
// 24 sprites fijos, sin memoria por cuadro (24x24 comparaciones). Solo de noche al aire libre, sin lluvia, calidad
// Alta y Ultra.
public partial class AOLivingLightV292
{
    const int FireflyCount = 24;
    const int SwarmSize = 22;             // los 2 últimos lugares son para sueltas
    const float CruiseSpeed = 1.5f;
    const float FleeSpeed = 3.4f;
    const float ThreatRadius = 2.3f;

    enum Role : byte { Off, Swarm, Explorer, Solo, Stray }

    SpriteRenderer[] fireflies;
    SpriteRenderer swarmGlow;
    Vector2[] flyPos, flyVel, flyHeading;
    float[] flyPhase, flySeed, flyTimer, flyFade;
    Role[] flyRole;
    Vector2 swarmHome, swarmTarget, alarmFrom;
    bool swarmPlaced;
    float nextExplore, nextStray, nextWaypoint, alarmUntil;

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

        if (!city && (!swarmPlaced || Mathf.Abs(swarmHome.x - center.x) > halfWidth + 6f ||
                      Mathf.Abs(swarmHome.y - center.y) > halfHeight + 6f))
        {
            swarmHome = DarkestSpot(center, halfWidth, halfHeight, Vector2.zero, 0f);
            swarmTarget = swarmHome;
            for (int i = 0; i < SwarmSize; i++)
                Spawn(i, swarmHome + Random.insideUnitCircle * 3f, Role.Swarm, 0f);
            swarmPlaced = true;
            nextExplore = time + Random.Range(6f, 14f);
            nextWaypoint = time;
        }
        if (city && swarmPlaced)
        {
            for (int i = 0; i < SwarmSize; i++) if (flyRole[i] != Role.Off) { flyRole[i] = Role.Stray; flyTimer[i] = 0f; }
            swarmPlaced = false;
        }

        // La colmena recorre lo oscuro: próximo rincón oscuro a 4-9 unidades, y el "hogar" viaja hacia él.
        if (swarmPlaced && time >= nextWaypoint)
        {
            nextWaypoint = time + Random.Range(5f, 9f);
            swarmTarget = DarkestSpot(center, halfWidth, halfHeight, swarmHome, 6.5f);
        }
        if (swarmPlaced)
            swarmHome = Vector2.MoveTowards(swarmHome, swarmTarget, 0.9f * deltaTime) + AwayFromLight(swarmHome) * 0.5f * deltaTime;

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
        if (time >= nextStray)
        {
            nextStray = time + (city ? Random.Range(25f, 60f) : Random.Range(15f, 40f));
            for (int i = SwarmSize; i < FireflyCount; i++)
                if (flyRole[i] == Role.Off)
                {
                    Spawn(i, DarkestSpot(center, halfWidth, halfHeight, Vector2.zero, 0f), Role.Stray, Random.Range(12f, 25f));
                    break;
                }
        }

        // Mente colmena: centro y rumbo común; alarma compartida si alguna tiene un personaje encima.
        Vector2 centroid = Vector2.zero, heading = Vector2.zero;
        int members = 0;
        for (int i = 0; i < SwarmSize; i++)
        {
            if (flyRole[i] != Role.Swarm) continue;
            centroid += flyPos[i];
            heading += flyVel[i];
            members++;
            Vector2 threat;
            if (NearestThreat(flyPos[i], out threat) < ThreatRadius)
            {
                alarmUntil = time + 0.6f;
                alarmFrom = threat;
            }
        }
        if (members > 0) { centroid /= members; heading /= members; }
        bool alarm = time < alarmUntil;

        int lit = 0;
        float glowSum = 0f;
        float response = 1f - Mathf.Exp(-9f * deltaTime);        // casi instantáneo
        for (int i = 0; i < FireflyCount; i++)
        {
            if (flyRole[i] == Role.Off) continue;
            Vector2 p = flyPos[i];
            float seed = flySeed[i];
            Vector2 desired = new Vector2(Mathf.PerlinNoise(time * 0.45f, seed) - 0.5f,
                                          Mathf.PerlinNoise(seed, time * 0.45f) - 0.5f) * 2.4f;
            float max = CruiseSpeed;
            switch (flyRole[i])
            {
                case Role.Swarm:
                    desired += (centroid - p) * 0.18f + (swarmHome - p) * 0.22f + heading * 0.8f;
                    // Escapada individual breve: una sola, por su cuenta, y vuelve.
                    if (Random.value < 0.05f * deltaTime)
                    {
                        flyRole[i] = Role.Solo;
                        flyTimer[i] = Random.Range(1f, 2.5f);
                        flyHeading[i] = Random.insideUnitCircle.normalized;
                    }
                    break;
                case Role.Solo:
                    desired += flyHeading[i] * 2.2f;
                    max = CruiseSpeed * 1.6f;
                    flyTimer[i] -= deltaTime;
                    if (flyTimer[i] <= 0f) flyRole[i] = Role.Swarm;
                    break;
                case Role.Explorer:
                    desired += flyHeading[i] * 1.8f;
                    max = CruiseSpeed * 1.4f;
                    flyTimer[i] -= deltaTime;
                    if (flyTimer[i] <= 0f) flyRole[i] = Role.Swarm;
                    break;
                case Role.Stray:
                    desired *= 1.4f;
                    flyTimer[i] -= deltaTime;
                    break;
            }
            // Separación amplia: ocupan mucho espacio sin encimarse.
            for (int j = 0; j < FireflyCount; j++)
            {
                if (j == i || flyRole[j] == Role.Off) continue;
                Vector2 d = p - flyPos[j];
                float m2 = d.sqrMagnitude;
                if (m2 < 0.9f && m2 > 0.0001f) desired += d / m2 * 0.25f;
            }
            // Se escurren al instante alrededor de personajes y luces; la alarma mueve a todo el enjambre junto.
            Vector2 threat;
            float threatDistance = NearestThreat(p, out threat);
            if (threatDistance < ThreatRadius)
            {
                Vector2 away = threatDistance > 0.01f ? (p - threat) / threatDistance : Random.insideUnitCircle.normalized;
                desired += away * (1f - threatDistance / ThreatRadius) * 9f;
                max = FleeSpeed;
            }
            else if (alarm && flyRole[i] == Role.Swarm)
            {
                Vector2 d = p - alarmFrom;
                float m = d.magnitude;
                if (m > 0.01f) desired += d / m * 4f / (1f + m * 0.25f);
                max = FleeSpeed * 0.8f;
            }
            desired += AwayFromLight(p) * 3f;

            if (desired.sqrMagnitude > max * max) desired = desired.normalized * max;
            Vector2 v = Vector2.Lerp(flyVel[i], desired, response);
            flyVel[i] = v;
            p += v * deltaTime;
            bool outside = Mathf.Abs(p.x - center.x) > halfWidth + 2f || Mathf.Abs(p.y - center.y) > halfHeight + 2f;
            if (outside && flyRole[i] != Role.Stray)
                p = swarmHome + Random.insideUnitCircle * 2.5f;
            flyPos[i] = p;

            // Parpadeo que se sincroniza con las vecinas; las que andan solas parpadean más rápido.
            float coupling = 0f;
            int near = 0;
            for (int j = 0; j < FireflyCount; j++)
            {
                if (j == i || flyRole[j] == Role.Off || (flyPos[j] - p).sqrMagnitude > 9f) continue;
                coupling += Mathf.Sin(flyPhase[j] - flyPhase[i]);
                near++;
            }
            float rate = flyRole[i] == Role.Solo || flyRole[i] == Role.Explorer ? 5f : 3.4f;
            flyPhase[i] += deltaTime * (rate + (near > 0 ? 0.9f * coupling / near : 0f));
            float blink = Mathf.Max(0f, Mathf.Sin(flyPhase[i]));
            blink = blink * blink * blink;

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

        if (swarmGlow == null)
            swarmGlow = MakeRenderer("FireflyGlow", -18890);
        bool glow = members > 2 && glowSum > 0.2f;
        if (swarmGlow.enabled != glow) swarmGlow.enabled = glow;
        if (glow)
        {
            swarmGlow.transform.position = new Vector3(centroid.x, centroid.y, 0f);
            swarmGlow.transform.localScale = new Vector3(6.5f, 6.5f, 1f);
            swarmGlow.color = new Color(0.55f, 0.85f, 0.35f, Mathf.Min(0.12f, glowSum * 0.008f));
        }
    }

    // Distancia al personaje más cercano (a la altura del cuerpo) y su posición.
    float NearestThreat(Vector2 p, out Vector2 at)
    {
        float best = float.MaxValue;
        at = p;
        for (int c = 0; c < AOCharacterShadowsV291.VisibleCount; c++)
        {
            Vector2 body = AOCharacterShadowsV291.VisibleFeet[c] + new Vector2(0f, 0.8f);
            float d = (p - body).magnitude;
            if (d < best) { best = d; at = body; }
        }
        return best;
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

    // El rincón más oscuro a la vista (lejos de luces y personajes, sin techo). Con "from" y "distance", prefiere
    // lugares a esa distancia de "from" (para que la colmena recorra en vez de quedarse quieta).
    Vector2 DarkestSpot(Vector2 center, float halfWidth, float halfHeight, Vector2 from, float distance)
    {
        Vector2 best = center;
        float bestScore = float.MinValue;
        for (int k = 0; k < 14; k++)
        {
            var q = new Vector2(center.x + Random.Range(-halfWidth, halfWidth) * 0.85f,
                                center.y + Random.Range(-halfHeight, halfHeight) * 0.85f);
            if (world.IsRoofAt(q)) continue;
            float score = -AwayFromLight(q).magnitude * 3f;
            for (int c = 0; c < AOCharacterShadowsV291.VisibleCount; c++)
                score += Mathf.Min(4f, (AOCharacterShadowsV291.VisibleFeet[c] - q).magnitude) * 0.3f;
            if (distance > 0f)
                score -= Mathf.Abs((q - from).magnitude - distance) * 0.35f;
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
