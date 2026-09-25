using System;
using System.Collections.Generic;
using UnityEngine;

// Dibuja los obstáculos del ring que genera AOArenaGen (Programación publica el layout en AODuelClient).
// Solo visual: el bloqueo y el agua los marca la superposición de AOGridMap.
// Paleta: StreamingAssets/AOMigrator/ArenaGen/arena_palette.json (claves = enum Kind; "frames" = recorte de cada GRH).
// - Pillar / Tree: un sprite por celda.   - Rock: un sprite centrado sobre el bloque 2x2.
// - Wall (recta o L): un tile por celda.   - Pool: set de agua 4x4 sin costa.   - Deco: capa 2, caminable.
[DisallowMultipleComponent]
public class AOArenaVisualV285 : MonoBehaviour
{
    [Serializable] class KindDef
    {
        public int[] grh;
        public int @base;
    }

    [Serializable] class Kinds
    {
        public KindDef Pillar, Rock, Wall, Tree, Pool, Deco;
    }

    [Serializable] class Theme
    {
        public Kinds kinds;
    }

    [Serializable] class Themes
    {
        public Theme Bosque, Desierto, Nieve, Mazmorra, Pantano, Ciudad;
    }

    [Serializable] class Frame
    {
        public int grh, fileNum, sx, sy, w, h;
    }

    [Serializable] class Palette
    {
        public Themes temas;
        public Frame[] frames;
    }

    const int PoolOrder = -29990;          // encima del piso del ring (capa 1 = -30000)
    const int DecoOrderBase = -20000;      // capa 2 del mapa

    static AOArenaVisualV285 instance;
    static Palette palette;
    static bool paletteLoaded;
    static readonly Dictionary<int, Frame> frames = new Dictionary<int, Frame>();
    static readonly Dictionary<int, Sprite> sprites = new Dictionary<int, Sprite>();
    static readonly Dictionary<int, Texture2D> textures = new Dictionary<int, Texture2D>();
    static readonly Dictionary<int, bool> hdTexture = new Dictionary<int, bool>();

    readonly Dictionary<int, GameObject> rings = new Dictionary<int, GameObject>();
    readonly Dictionary<int, int> ringMaps = new Dictionary<int, int>();   // mapa en el que se dibujó cada ring
    readonly List<int> stale = new List<int>();
    readonly List<SpriteRenderer> tinted = new List<SpriteRenderer>();
    AOWorldManagerV07 world;
    float nextTint;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        AODuelClient.LayoutApplied -= OnApplied;
        AODuelClient.LayoutApplied += OnApplied;
        AODuelClient.LayoutCleared -= OnCleared;
        AODuelClient.LayoutCleared += OnCleared;
        AOLighting2DV283.ModeChanged -= OnLightingChanged;
        AOLighting2DV283.ModeChanged += OnLightingChanged;
        // Si ya había retos en curso (entrar tarde como espectador), se dibujan igual.
        foreach (AODuelClient.ActiveArena arena in AODuelClient.Arenas)
            if (arena?.Layout != null) OnApplied(arena.Ring, arena.OriginX, arena.OriginY, arena.Layout);
    }

    static AOArenaVisualV285 Ensure()
    {
        if (instance != null) return instance;
        var go = new GameObject("AO Arena Visual v285");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<AOArenaVisualV285>();
        return instance;
    }

    static void OnApplied(int ring, int originX, int originY, AOArenaLayout layout) => Ensure().Draw(ring, originX, originY, layout);
    static void OnCleared(int ring) { if (instance != null) instance.Clear(ring); }
    static void OnLightingChanged() { if (instance != null) instance.nextTint = 0f; }

    void Draw(int ring, int originX, int originY, AOArenaLayout layout)
    {
        Clear(ring);
        if (layout == null) return;
        Kinds kinds = KindsFor(layout.Theme);
        if (kinds == null) return;

        var root = new GameObject("Ring " + ring);
        root.transform.SetParent(transform, false);
        rings[ring] = root;
        if (world == null) world = UnityEngine.Object.FindFirstObjectByType<AOWorldManagerV07>();
        ringMaps[ring] = world != null ? world.CurrentMapNumber : 0;

        for (int cy = 0; cy < AOArenaGen.H; cy++)
        {
            for (int cx = 0; cx < AOArenaGen.W; cx++)
            {
                int i = AOArenaLayout.Index(cx, cy);
                var kind = (AOArenaGen.Kind)layout.Kinds[i];
                if (kind == AOArenaGen.Kind.None) continue;
                int x = originX + cx, y = originY + cy;
                int variant = layout.Variants[i];
                switch (kind)
                {
                    case AOArenaGen.Kind.Pool:
                        if (kinds.Pool != null)
                            Place(root, kinds.Pool.@base + (y % 4) * 4 + (x % 4), x, y, 0f, PoolOrder);
                        break;
                    case AOArenaGen.Kind.Deco:
                        Place(root, Pick(kinds.Deco, variant), x, y, 0f, DecoOrderBase + y);
                        break;
                    case AOArenaGen.Kind.Rock:
                        // Un solo sprite por estampa 2x2: se dibuja desde su celda de arriba a la izquierda.
                        if (IsKind(layout, cx - 1, cy, kind) || IsKind(layout, cx, cy - 1, kind)) break;
                        Place(root, Pick(kinds.Rock, variant), x, y + 1, 0.5f, AORenderOrderV210.Layer3(y + 1));
                        break;
                    case AOArenaGen.Kind.Pillar:
                        Place(root, Pick(kinds.Pillar, variant), x, y, 0f, AORenderOrderV210.Layer3(y));
                        break;
                    case AOArenaGen.Kind.Tree:
                        Place(root, Pick(kinds.Tree, variant), x, y, 0f, AORenderOrderV210.Layer3(y));
                        break;
                    case AOArenaGen.Kind.Wall:
                        Place(root, Pick(kinds.Wall, variant), x, y, 0f, AORenderOrderV210.Layer3(y));
                        break;
                }
            }
        }
        nextTint = 0f;
    }

    void Clear(int ring)
    {
        if (!rings.TryGetValue(ring, out GameObject root)) return;
        rings.Remove(ring);
        ringMaps.Remove(ring);
        if (root == null)
        {
            tinted.RemoveAll(r => r == null);
            return;
        }
        tinted.RemoveAll(r => r == null || r.transform.IsChildOf(root.transform));
        Destroy(root);
    }

    void Place(GameObject root, int grh, int tileX, int tileY, float offsetX, int order)
    {
        Sprite sprite = SpriteFor(grh);
        if (sprite == null) return;
        var go = new GameObject("g" + grh);
        go.transform.SetParent(root.transform, false);
        // Mismo anclaje que AOGridMap.TileToWorld: abajo al centro de la casilla.
        go.transform.position = new Vector3(tileX - 0.5f + offsetX, -tileY, 0f);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        tinted.Add(renderer);
    }

    void Update()
    {
        if (rings.Count == 0) return;
        if (world == null) world = UnityEngine.Object.FindFirstObjectByType<AOWorldManagerV07>();

        // LayoutCleared no llega si el cliente se desconecta o cambia de mapa: se limpia al salir del mapa del ring.
        if (world != null && !world.IsLoading)
        {
            stale.Clear();
            foreach (KeyValuePair<int, int> entry in ringMaps)
                if (entry.Value != 0 && entry.Value != world.CurrentMapNumber) stale.Add(entry.Key);
            foreach (int ring in stale) Clear(ring);
            if (rings.Count == 0) return;
        }

        if (Time.time < nextTint) return;
        nextTint = Time.time + 1f;
        // Luz Original: el mismo tono ambiente que el mapa (AOMapLighting). Mejorada: blanco, lo iluminan las Light2D.
        Color tint = Color.white;
        if (!AOLighting2DV283.Enhanced)
        {
            if (world != null) tint = AOLighting2DV283.OriginalAmbient(world.CurrentMapNumber, world.CurrentWorldHour);
        }
        foreach (SpriteRenderer renderer in tinted)
            if (renderer != null) renderer.color = tint;
    }

    static bool IsKind(AOArenaLayout layout, int cx, int cy, AOArenaGen.Kind kind) =>
        cx >= 0 && cy >= 0 && cx < AOArenaGen.W && cy < AOArenaGen.H &&
        (AOArenaGen.Kind)layout.Kinds[AOArenaLayout.Index(cx, cy)] == kind;

    static int Pick(KindDef def, int variant) =>
        def?.grh == null || def.grh.Length == 0 ? 0 : def.grh[variant % def.grh.Length];

    static Kinds KindsFor(AOArenaGen.Theme theme)
    {
        Palette p = LoadPalette();
        if (p?.temas == null) return null;
        switch (theme)
        {
            case AOArenaGen.Theme.Bosque: return p.temas.Bosque?.kinds;
            case AOArenaGen.Theme.Desierto: return p.temas.Desierto?.kinds;
            case AOArenaGen.Theme.Nieve: return p.temas.Nieve?.kinds;
            case AOArenaGen.Theme.Mazmorra: return p.temas.Mazmorra?.kinds;
            case AOArenaGen.Theme.Pantano: return p.temas.Pantano?.kinds;
            case AOArenaGen.Theme.Ciudad: return p.temas.Ciudad?.kinds;
        }
        return null;
    }

    static Palette LoadPalette()
    {
        if (paletteLoaded) return palette;
        paletteLoaded = true;
        try
        {
            string path = System.IO.Path.Combine(Application.streamingAssetsPath, "AOMigrator", "ArenaGen", "arena_palette.json");
            if (!System.IO.File.Exists(path))
            {
                Debug.LogWarning("[AO Arena Visual v285] Falta arena_palette.json");
                return null;
            }
            palette = JsonUtility.FromJson<Palette>(System.IO.File.ReadAllText(path));
            if (palette?.frames != null)
                foreach (Frame f in palette.frames)
                    if (f != null) frames[f.grh] = f;
        }
        catch (Exception ex)
        {
            Debug.LogError("[AO Arena Visual v285] Error cargando la paleta: " + ex.Message);
        }
        return palette;
    }

    static Sprite SpriteFor(int grh)
    {
        if (grh <= 0) return null;
        if (sprites.TryGetValue(grh, out Sprite cached)) return cached;
        Sprite sprite = null;
        if (frames.TryGetValue(grh, out Frame f))
        {
            Texture2D texture = TextureFor(f.fileNum, out bool hd);
            if (texture != null)
            {
                // Las texturas HD (AOMigratorHD) son 4x: mismo recorte y pixelsPerUnit por 4, mismo tamaño en el mundo.
                int scale = hd ? 4 : 1;
                int unityY = texture.height - (f.sy + f.h) * scale;
                var rect = new Rect(f.sx * scale, unityY, f.w * scale, f.h * scale);
                if (rect.xMin >= 0 && rect.yMin >= 0 && rect.xMax <= texture.width && rect.yMax <= texture.height)
                {
                    sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0f), 32f * scale, 0, SpriteMeshType.FullRect);
                    sprite.name = "arena_g" + grh;
                }
            }
        }
        sprites[grh] = sprite;
        return sprite;
    }

    static Texture2D TextureFor(int fileNum, out bool hd)
    {
        if (textures.TryGetValue(fileNum, out Texture2D cached))
        {
            hd = hdTexture[fileNum];
            return cached;
        }
        Texture2D texture = Resources.Load<Texture2D>("AOMigratorHD/WorldV07/Textures/tex_" + fileNum);
        hd = texture != null;
        if (texture == null) texture = Resources.Load<Texture2D>("AOMigrator/WorldV07/Textures/tex_" + fileNum);
        textures[fileNum] = texture;
        hdTexture[fileNum] = hd;
        return texture;
    }
}
