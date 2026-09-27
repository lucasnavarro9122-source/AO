using UnityEngine;

// AOMapWeatherNatureV293 (nube, 27/09, pedido de Lucas): la lluvia con los árboles y el suelo.
// - Árboles (objetos tipo 4 del AO): la gota que cae sobre la copa no salpica el piso; se desliza entre las hojas
//   (un brillo corto que baja con un leve vaivén) y gotea desde el borde de abajo de la copa al piso. Debajo del
//   árbol llueve menos: ahí no hay salpicaduras directas, solo el goteo de las hojas.
// - Suelo mojado: con la lluvia se moja de a poco (~45 s) y se seca de a poco (~2 min) cuando para; los charcos
//   están en AOMapWeatherPuddlesV294.
// - Fluidez: las gotas aparecen y aterrizan con fundidos cortos, y las salpicaduras en el piso sueltan 1-2 gotitas.
// Calidad: charcos desde Medium; reflejos desde High; agua de hojas con el pool de techos (0 en Low).
public partial class AOMapWeather
{
    const float WetSeconds = 45f, DrySeconds = 120f;
    const int MaxTrees = 32;

    // Mojado del suelo (0 seco ... 1 empapado): lo leen los reflejos de los personajes.
    public static float Wetness { get; private set; }
    // Depuración (ventana "Clima"): moja el suelo al instante.
    public static bool DebugSoakNow;

    readonly Vector2[] canopyCenter = new Vector2[MaxTrees];
    readonly float[] canopyRadius = new float[MaxTrees];
    int canopyCount;

    void UpdateNature(float deltaTime, Vector2 cam, float halfWidth, float halfHeight)
    {
        bool raining = active == Precipitation.Rain && intensity > 0f;
        if (DebugSoakNow) { DebugSoakNow = false; Wetness = 1f; }
        Wetness = raining ? Mathf.MoveTowards(Wetness, 1f, deltaTime * Mathf.Clamp01(intensity) / WetSeconds)
                          : Mathf.MoveTowards(Wetness, 0f, deltaTime / DrySeconds);
        CollectCanopies(cam, halfWidth, halfHeight, raining);
        UpdatePuddles(cam, halfWidth, halfHeight);
    }

    // ------------------------------------------------------------------ árboles

    void CollectCanopies(Vector2 cam, float halfWidth, float halfHeight, bool raining)
    {
        canopyCount = 0;
        if (!raining || world == null) return;
        int count = world.TreeVisualCount;
        for (int i = 0; i < count && canopyCount < MaxTrees; i++)
        {
            SpriteRenderer tree = world.TreeVisualRenderer(i);
            if (tree == null || !tree.enabled || tree.color.a < 0.6f) continue;   // transparente: el jugador está debajo
            Bounds b = tree.bounds;
            if (b.max.x < cam.x - halfWidth - 2f || b.min.x > cam.x + halfWidth + 2f ||
                b.max.y < cam.y - halfHeight - 2f || b.min.y > cam.y + halfHeight + 2f)
                continue;
            // Copa: la parte de arriba del dibujo (el tronco queda abajo).
            canopyCenter[canopyCount] = new Vector2(b.center.x, b.min.y + b.size.y * 0.62f);
            canopyRadius[canopyCount] = b.size.x * 0.38f;
            canopyCount++;
        }
    }

    // Una gota del medio cayó sobre una copa: se desliza por las hojas y gotea (o la absorben las hojas).
    bool TryTreeWater(Vector2 position)
    {
        for (int t = 0; t < canopyCount; t++)
        {
            Vector2 d = position - canopyCenter[t];
            if (d.sqrMagnitude > canopyRadius[t] * canopyRadius[t]) continue;
            int size = AOEffectsQualityV290.RoofPool;
            if (size > 0 && Random.value < 0.55f)
                SpawnLeafDrop(position, canopyCenter[t].y - canopyRadius[t] * 0.8f, size);
            return true;
        }
        return false;
    }
}
