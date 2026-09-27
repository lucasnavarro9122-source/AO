using System;
using UnityEngine;

// AOMapWeatherRoofsV290 (nube, 26/09): agua de lluvia en los techos.
// Las gotas que caen sobre un techo forman un hilo que escurre siguiendo la pendiente (a dos aguas: se aleja de
// la cumbrera y baja; al frente: baja derecho). Al llegar al alero cae unas dos casillas y salpica en el piso.
// Datos: Resources/AOMigrator/WorldV07/roof_flow.json (Tools/roof_flow.py). Sin datos del mapa, la lluvia
// salpica en el piso como siempre. El techo que el jugador tiene encima (transparente) no recibe agua.
// Costo: un pool fijo de AOEffectsQualityV290.RoofPool sprites (0 en Low), mismo material aditivo.
public partial class AOMapWeather
{
    [Serializable] class RoofFlowFile { public int version; public int cell; public RoofFlowMap[] maps; }
    [Serializable] class RoofFlowMap { public int map; public int width; public int height; public int[] runs; public RoofFlowRoof[] roofs; }
    [Serializable] class RoofFlowRoof { public int trigger; public int mode; public float ridge; }

    const float RivuletSpeedMin = 1.2f, RivuletSpeedMax = 1.9f;
    const float EaveGravity = 16f;

    static RoofFlowFile roofFile;
    static bool roofFileLoaded;

    ushort[] roofLabels;
    RoofFlowRoof[] roofInfo;
    int roofWidth, roofHeight;
    float roofCellsPerUnit = 4f;
    int playerRoof;

    SpriteRenderer[] rivulets;
    Transform[] rivuletTransforms;
    Vector2[] rivuletPos, rivuletVel;
    float[] rivuletAge, rivuletLife, rivuletFloor;
    byte[] rivuletState;      // 0 libre, 1 escurre por el techo, 2 cae del alero
    int[] rivuletRoof;
    int rivuletNext;

    public int ActiveRoofWater { get; private set; }
    public bool HasRoofData => roofLabels != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRoofFile()
    {
        roofFile = null;
        roofFileLoaded = false;
    }

    void ConfigureRoofs(int mapNumber)
    {
        roofLabels = null;
        roofInfo = null;
        if (!roofFileLoaded)
        {
            roofFileLoaded = true;
            TextAsset source = Resources.Load<TextAsset>("AOMigrator/WorldV07/roof_flow");
            if (source != null)
                roofFile = JsonUtility.FromJson<RoofFlowFile>(source.text);
        }
        if (roofFile == null || roofFile.maps == null || roofFile.cell <= 0)
            return;
        foreach (RoofFlowMap entry in roofFile.maps)
        {
            if (entry == null || entry.map != mapNumber || entry.runs == null || entry.roofs == null)
                continue;
            int total = entry.width * entry.height;
            var labels = new ushort[total];
            int index = 0;
            for (int r = 0; r + 1 < entry.runs.Length && index < total; r += 2)
            {
                ushort value = (ushort)Mathf.Clamp(entry.runs[r], 0, entry.roofs.Length);
                int end = Mathf.Min(total, index + entry.runs[r + 1]);
                for (; index < end; index++)
                    labels[index] = value;
            }
            roofLabels = labels;
            roofInfo = entry.roofs;
            roofWidth = entry.width;
            roofHeight = entry.height;
            roofCellsPerUnit = 32f / roofFile.cell;
            return;
        }
    }

    // Techo (1..n) en un punto del mundo; 0 = ninguno.
    int RoofAt(Vector2 position)
    {
        if (roofLabels == null)
            return 0;
        int gx = Mathf.FloorToInt(position.x * roofCellsPerUnit);
        int gy = Mathf.FloorToInt(-position.y * roofCellsPerUnit);
        if (gx < 0 || gy < 0 || gx >= roofWidth || gy >= roofHeight)
            return 0;
        return roofLabels[gy * roofWidth + gx];
    }

    bool IsPlayerRoof(int roof) =>
        playerRoof != 0 && roof > 0 && roofInfo[roof - 1].trigger == playerRoof;

    // ¿El punto está bajo el techo que el jugador tiene encima (y que por eso se ve transparente)?
    bool IsUnderPlayerRoof(Vector2 position)
    {
        if (playerRoof == 0)
            return false;
        int roof = RoofAt(position);
        if (roof > 0)
            return IsPlayerRoof(roof);
        return world != null && world.TriggerAtWorld(position) == playerRoof;
    }

    // Una gota de la capa del medio aterrizó en "position". Devuelve true si cayó sobre un techo.
    bool TryRoofWater(Vector2 position)
    {
        int roof = RoofAt(position);
        if (roof <= 0)
            return false;
        if (IsPlayerRoof(roof))
            return true;
        int size = AOEffectsQualityV290.RoofPool;
        if (size > 0 && UnityEngine.Random.value < 0.5f && SpawnRivulet(position, roof, size))
            return true;
        Splash(position, 0.55f);
        return true;
    }

    Vector2 FlowOf(int roof, float x)
    {
        RoofFlowRoof info = roofInfo[roof - 1];
        if (info.mode == 1)
            return new Vector2(0f, -1f);
        float side = x < info.ridge ? -1f : 1f;
        return new Vector2(side * 0.86f, -0.5f);
    }

    bool SpawnRivulet(Vector2 position, int roof, int size)
    {
        EnsureRivulets(size);
        int i = -1;
        for (int n = 0; n < size; n++)
        {
            int candidate = (rivuletNext + n) % size;
            if (rivuletState[candidate] == 0) { i = candidate; break; }
        }
        if (i < 0)
            return false;
        rivuletNext = (i + 1) % size;
        Vector2 flow = FlowOf(roof, position.x);
        rivuletState[i] = 1;
        rivuletRoof[i] = roof;
        rivuletPos[i] = position;
        rivuletVel[i] = flow * UnityEngine.Random.Range(RivuletSpeedMin, RivuletSpeedMax);
        rivuletAge[i] = 0f;
        rivuletLife[i] = UnityEngine.Random.Range(0.7f, 1.6f);
        Transform t = rivuletTransforms[i];
        t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(flow.x, -flow.y) * Mathf.Rad2Deg);
        t.localScale = new Vector3(0.75f, 0.32f, 1f);
        rivulets[i].enabled = true;
        return true;
    }

    // Gota que cae sobre una copa: se desliza entre las hojas hasta "bottom" (borde de abajo de la copa).
    void SpawnLeafDrop(Vector2 position, float bottom, int size)
    {
        EnsureRivulets(size);
        int i = -1;
        for (int n = 0; n < size; n++)
        {
            int candidate = (rivuletNext + n) % size;
            if (rivuletState[candidate] == 0) { i = candidate; break; }
        }
        if (i < 0) return;
        rivuletNext = (i + 1) % size;
        rivuletState[i] = 1;
        rivuletRoof[i] = -1;                      // negativo = árbol
        rivuletPos[i] = position;
        rivuletVel[i] = Vector2.zero;
        rivuletAge[i] = 0f;
        rivuletLife[i] = UnityEngine.Random.Range(1.5f, 3f);
        rivuletFloor[i] = bottom;
        Transform t = rivuletTransforms[i];
        t.localRotation = Quaternion.identity;
        t.localScale = new Vector3(0.6f, 0.2f, 1f);
        rivulets[i].enabled = true;
    }

    void EnsureRivulets(int size)
    {
        if (rivulets != null && rivulets.Length >= size)
            return;
        int old = rivulets == null ? 0 : rivulets.Length;
        Array.Resize(ref rivulets, size);
        Array.Resize(ref rivuletTransforms, size);
        Array.Resize(ref rivuletPos, size);
        Array.Resize(ref rivuletVel, size);
        Array.Resize(ref rivuletAge, size);
        Array.Resize(ref rivuletLife, size);
        Array.Resize(ref rivuletFloor, size);
        Array.Resize(ref rivuletState, size);
        Array.Resize(ref rivuletRoof, size);
        Material material = Additive();
        for (int i = old; i < size; i++)
        {
            GameObject child = new GameObject("RoofWater_" + i, typeof(SpriteRenderer));
            child.transform.SetParent(transform, false);
            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            renderer.sprite = Streak();
            renderer.sortingOrder = 30500;
            if (material != null)
                renderer.sharedMaterial = material;
            renderer.enabled = false;
            rivulets[i] = renderer;
            rivuletTransforms[i] = child.transform;
        }
    }

    void FreeRivulet(int i)
    {
        rivuletState[i] = 0;
        rivulets[i].enabled = false;
    }

    void UpdateRoofWater(float deltaTime)
    {
        ActiveRoofWater = 0;
        if (rivulets == null)
            return;
        float alphaScale = AlphaScale();
        for (int i = 0; i < rivulets.Length; i++)
        {
            if (rivuletState[i] == 0)
                continue;
            Vector2 p = rivuletPos[i];
            float alpha;
            if (rivuletState[i] == 1 && rivuletRoof[i] < 0)
            {
                // Gota entre las hojas: baja con un vaivén y gotea desde el borde de abajo de la copa.
                rivuletAge[i] += deltaTime;
                p += new Vector2(Mathf.Sin(rivuletAge[i] * 9f + i) * 0.35f, -0.9f) * deltaTime;
                if (p.y <= rivuletFloor[i])
                {
                    rivuletState[i] = 2;
                    rivuletVel[i] = new Vector2(0f, -0.4f);
                    rivuletFloor[i] = p.y - UnityEngine.Random.Range(0.4f, 1.3f);
                    rivuletTransforms[i].localRotation = Quaternion.identity;
                    alpha = 0.6f;
                }
                else if (rivuletAge[i] >= rivuletLife[i])
                {
                    FreeRivulet(i);
                    continue;
                }
                else
                    alpha = 0.55f * Mathf.Clamp01(rivuletAge[i] * 8f) *
                            (0.6f + 0.4f * Mathf.Abs(Mathf.Sin(rivuletAge[i] * 14f + i)));   // brillo en las hojas
            }
            else if (rivuletState[i] == 1)
            {
                int roof = rivuletRoof[i];
                if (roofInfo == null || roof <= 0 || roof > roofInfo.Length || IsPlayerRoof(roof))
                {
                    FreeRivulet(i);
                    continue;
                }
                rivuletAge[i] += deltaTime;
                p += rivuletVel[i] * deltaTime;
                if (RoofAt(p) != roof)
                {
                    // Alero: la gota cae más o menos la altura de la pared y salpica.
                    rivuletState[i] = 2;
                    rivuletVel[i] = new Vector2(rivuletVel[i].x * 0.2f, -0.5f);
                    rivuletFloor[i] = p.y - UnityEngine.Random.Range(1.7f, 2.3f);
                    rivuletTransforms[i].localRotation = Quaternion.identity;
                    alpha = 0.6f;
                }
                else if (rivuletAge[i] >= rivuletLife[i])
                {
                    FreeRivulet(i);
                    continue;
                }
                else
                {
                    // Aparece y se seca de a poco (no se corta de golpe).
                    float left = rivuletLife[i] - rivuletAge[i];
                    alpha = 0.5f * Mathf.Clamp01(rivuletAge[i] * 6f) * Mathf.Clamp01(left * 3f);
                }
            }
            else
            {
                Vector2 v = rivuletVel[i];
                v.y -= EaveGravity * deltaTime;
                rivuletVel[i] = v;
                p += v * deltaTime;
                if (p.y <= rivuletFloor[i])
                {
                    Splash(new Vector2(p.x, rivuletFloor[i]), 0.9f);
                    FreeRivulet(i);
                    continue;
                }
                rivuletTransforms[i].localScale = new Vector3(0.8f, Mathf.Clamp(0.2f - v.y * 0.035f, 0.2f, 0.55f), 1f);
                alpha = 0.6f;
            }
            rivuletPos[i] = p;
            rivuletTransforms[i].position = new Vector3(p.x, p.y, 0f);
            rivulets[i].color = new Color(0.85f, 0.92f, 1f, alpha * alphaScale);
            ActiveRoofWater++;
        }
    }
}
