using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Meditación fiel al AO20.
// - La tabla nivel → FX sale de Meditaciones.dat (ciudadano, criminal y MeditationLevelMax),
//   igual que Protocol.HandleMeditate del servidor original.
// - Los FX compuestos se reproducen como LoadComposedFx del cliente original:
//   clip 1 = inicio una vez, clip 2 = loop, clip 3 = inicio al revés a mitad de tiempo al dejar de meditar.
// - Si un FX de la tabla todavía no tiene frames, se usa el tier provisorio por minLevel/maxLevel.
[DisallowMultipleComponent]
public class AOMeditationVisualV269 : MonoBehaviour
{
    [Serializable] class Manifest
    {
        public string version;
        public int soundId;
        public int levelMaxFx;
        public LevelRange[] levelTable;
        public Entry[] entries;
    }

    [Serializable] class LevelRange
    {
        public int minLevel;
        public int maxLevel;
        public int fx;
        public int fxCriminal;
    }

    [Serializable] class Entry
    {
        public int fx;
        public string label;
        public int minLevel;
        public int maxLevel;
        public int durationMs;
        public int pixelsPerUnit = 32;
        public int offsetX;
        public int offsetY;
        public float yOffset;
        public float scale = 1f;
        public int loopFx;
        public string[] frames;
    }

    const float OriginalPixelsPerTile = 32f;
    const int DefaultSoundId = 158;
    const float BaseVolume = 0.78f;

    static Manifest manifest;
    static bool manifestLoaded;
    static readonly Dictionary<int, Entry> entriesByFx = new Dictionary<int, Entry>();
    static readonly Dictionary<int, Sprite[]> spriteCache = new Dictionary<int, Sprite[]>();
    static readonly HashSet<int> warnedMissingFx = new HashSet<int>();

    GameObject visualObject;
    SpriteRenderer visualRenderer;
    AudioSource audioSource;
    Coroutine animationRoutine;
    Entry activeEntry;
    bool ending;
    bool positionalAudio;

    public bool IsActive => activeEntry != null && !ending;
    public int ActiveFx => IsActive ? activeEntry.fx : 0;

    static string RootPath =>
        Path.Combine(Application.streamingAssetsPath, "AOMigrator", "MeditationV269");

    void Awake()
    {
        EnsureAudioSource();
    }

    public void Begin(int level)
    {
        Begin(level, false);
    }

    public void Begin(int level, bool criminal)
    {
        int fx = FxForLevel(level, criminal);
        Entry entry = FindEntry(fx);
        if (entry == null)
        {
            entry = LegacyEntryForLevel(level);
            if (fx > 0 && entry != null && warnedMissingFx.Add(fx))
                Debug.LogWarning("[AO Meditation v269] FX " + fx + " sin frames todavía; uso provisorio FX " +
                                 entry.fx + ".");
        }

        StartEntry(entry, false);
    }

    // Como MeditateToggle del cliente original: el FX ya viene decidido (jugadores remotos).
    // El sonido se atenúa por distancia al jugador local.
    public void BeginFx(int fx)
    {
        Entry entry = FindEntry(fx);
        if (entry == null && fx > 0 && warnedMissingFx.Add(fx))
            Debug.LogWarning("[AO Meditation v269] FX " + fx + " sin frames.");

        StartEntry(entry, true);
    }

    public static int FxForLevel(int level, bool criminal)
    {
        EnsureManifest();
        level = Mathf.Max(1, level);

        if (manifest != null && manifest.levelTable != null && manifest.levelTable.Length > 0)
        {
            foreach (LevelRange range in manifest.levelTable)
            {
                if (range == null || level < range.minLevel || level > range.maxLevel) continue;
                return criminal && range.fxCriminal > 0 ? range.fxCriminal : range.fx;
            }

            // Case Else del servidor original: MeditationLevelMax para ambos bandos.
            if (manifest.levelMaxFx > 0) return manifest.levelMaxFx;
        }

        Entry legacy = LegacyEntryForLevel(level);
        return legacy == null ? 0 : legacy.fx;
    }

    void StartEntry(Entry entry, bool positional)
    {
        if (entry == null) return;

        if (!ending && activeEntry == entry && visualObject != null)
            return;

        StopImmediate();
        activeEntry = entry;
        positionalAudio = positional;

        visualObject = new GameObject("AO Meditation Aura FX " + entry.fx);
        visualObject.transform.SetParent(transform, false);

        visualRenderer = visualObject.AddComponent<SpriteRenderer>();
        visualRenderer.sortingOrder = 20020;
        ApplyPlacement(entry);

        Sprite[] frames = Frames(entry);
        if (frames.Length > 0)
            visualRenderer.sprite = frames[0];
        animationRoutine = StartCoroutine(Play(entry));

        EnsureAudioSource();
        int soundId = manifest != null && manifest.soundId > 0 ? manifest.soundId : DefaultSoundId;
        AudioClip clip = Resources.Load<AudioClip>("AOMigrator/MeditationV269/Audio/meditation_" + soundId);
        if (clip != null)
        {
            audioSource.clip = clip;
            audioSource.loop = true;
            audioSource.volume = BaseVolume;
            audioSource.panStereo = 0f;
            if (positionalAudio) ApplyPositionalAudio();
            audioSource.Play();
        }
    }

    void Update()
    {
        if (positionalAudio && audioSource != null && audioSource.isPlaying)
            ApplyPositionalAudio();
    }

    // Como ComputeCharFxVolume/ComputeCharFxPan + EstaPCarea del cliente original:
    // −1,2 dB por tile (Manhattan, tope −40 dB), paneo de 500/10000 por tile (tope 0,9) y mudo fuera de pantalla.
    void ApplyPositionalAudio()
    {
        Camera cam = AOActionBarV260.GameCamera;
        if (cam == null)
        {
            audioSource.volume = BaseVolume;
            audioSource.panStereo = 0f;
            return;
        }

        Vector3 listener = cam.transform.position;
        Vector3 source = transform.position;
        int dx = Mathf.RoundToInt(source.x - listener.x);
        int dy = Mathf.RoundToInt(source.y - listener.y);
        int distance = Mathf.Abs(dx) + Mathf.Abs(dy);

        bool inArea = distance < 20;
        if (cam.orthographic)
        {
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            inArea = Mathf.Abs(dx) < halfWidth + 1f && Mathf.Abs(dy) < halfHeight + 1f;
        }

        if (!inArea)
        {
            audioSource.volume = 0f;
            return;
        }

        float attenuationDb = distance < 20 ? Mathf.Min(40f, distance * 1.2f) : 40f;
        audioSource.volume = BaseVolume * Mathf.Pow(10f, -attenuationDb / 20f);
        audioSource.panStereo = dx == 0 ? 0f : Mathf.Sign(dx) * Mathf.Min(0.9f, distance * 0.05f);
    }

    public void End(bool immediate = false)
    {
        // El sonido se corta al toque, como StopWav en el original.
        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = null;
        }

        if (ending && !immediate) return;

        Entry entry = activeEntry;
        bool composed = entry != null && LoopEntryFor(entry) != null;
        if (immediate || !composed || visualRenderer == null || !isActiveAndEnabled)
        {
            StopImmediate();
            return;
        }

        ending = true;
        if (animationRoutine != null) StopCoroutine(animationRoutine);
        animationRoutine = StartCoroutine(Outro(entry));
    }

    void StopImmediate()
    {
        activeEntry = null;
        ending = false;

        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        if (visualObject != null)
        {
            Destroy(visualObject);
            visualObject = null;
            visualRenderer = null;
        }
    }

    IEnumerator Play(Entry entry)
    {
        Entry loop = LoopEntryFor(entry);
        if (loop != null)
        {
            Sprite[] intro = Frames(entry);
            float introStep = FrameTime(entry, intro.Length);
            for (int i = 0; i < intro.Length && visualRenderer != null; i++)
            {
                visualRenderer.sprite = intro[i];
                yield return new WaitForSeconds(introStep);
            }
        }

        Entry loopEntry = loop ?? entry;
        Sprite[] frames = Frames(loopEntry);
        if (frames.Length == 0 || visualRenderer == null) yield break;

        ApplyPlacement(loopEntry);
        float step = FrameTime(loopEntry, frames.Length);
        int frame = 0;
        while (visualRenderer != null)
        {
            visualRenderer.sprite = frames[frame];
            frame = (frame + 1) % frames.Length;
            yield return new WaitForSeconds(step);
        }
    }

    IEnumerator Outro(Entry entry)
    {
        Sprite[] intro = Frames(entry);
        ApplyPlacement(entry);
        float step = FrameTime(entry, intro.Length) * 0.5f;
        for (int i = intro.Length - 1; i >= 0 && visualRenderer != null; i--)
        {
            visualRenderer.sprite = intro[i];
            yield return new WaitForSeconds(step);
        }

        animationRoutine = null;
        StopImmediate();
    }

    void ApplyPlacement(Entry entry)
    {
        if (visualObject == null || entry == null) return;

        // OffsetX/OffsetY de FXs.ini son píxeles de pantalla del original (Y hacia abajo).
        visualObject.transform.localPosition = new Vector3(
            entry.offsetX / OriginalPixelsPerTile,
            entry.yOffset - entry.offsetY / OriginalPixelsPerTile,
            -0.01f);
        visualObject.transform.localScale = Vector3.one * Mathf.Max(0.01f, entry.scale);
    }

    static float FrameTime(Entry entry, int frameCount)
    {
        return Mathf.Max(0.025f, (entry.durationMs / 1000f) / Mathf.Max(1, frameCount));
    }

    void EnsureAudioSource()
    {
        if (audioSource != null) return;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    static Entry FindEntry(int fx)
    {
        EnsureManifest();
        return fx > 0 && entriesByFx.TryGetValue(fx, out Entry entry) ? entry : null;
    }

    static Entry LoopEntryFor(Entry entry)
    {
        if (entry == null || entry.loopFx <= 0 || entry.loopFx == entry.fx) return null;
        Entry loop = FindEntry(entry.loopFx);
        return loop != null && Frames(loop).Length > 0 ? loop : null;
    }

    static Entry LegacyEntryForLevel(int level)
    {
        EnsureManifest();
        if (manifest == null || manifest.entries == null) return null;

        level = Mathf.Max(1, level);
        Entry last = null;
        foreach (Entry entry in manifest.entries)
        {
            if (entry == null || entry.maxLevel <= 0) continue;
            if (level >= entry.minLevel && level <= entry.maxLevel) return entry;
            last = entry;
        }

        return last;
    }

    static Sprite[] Frames(Entry entry)
    {
        if (entry == null) return Array.Empty<Sprite>();
        if (spriteCache.TryGetValue(entry.fx, out Sprite[] cached)) return cached;

        var result = new List<Sprite>();
        if (entry.frames != null)
        {
            foreach (string relative in entry.frames)
            {
                try
                {
                    string full = Path.Combine(RootPath, relative.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(full)) continue;

                    byte[] bytes = File.ReadAllBytes(full);
                    Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                    if (!texture.LoadImage(bytes, false))
                    {
                        UnityEngine.Object.Destroy(texture);
                        continue;
                    }

                    texture.name = "MeditationFX_" + entry.fx + "_" +
                                   Path.GetFileNameWithoutExtension(relative);
                    texture.filterMode = FilterMode.Point;
                    texture.wrapMode = TextureWrapMode.Clamp;

                    Sprite sprite = Sprite.Create(
                        texture,
                        new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0f),
                        Mathf.Max(1, entry.pixelsPerUnit),
                        0,
                        SpriteMeshType.FullRect);
                    sprite.name = texture.name;
                    result.Add(sprite);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[AO Meditation v269] No se pudo cargar frame: " +
                                     relative + " | " + ex.Message);
                }
            }
        }

        Sprite[] frames = result.ToArray();
        spriteCache[entry.fx] = frames;
        return frames;
    }

    static void EnsureManifest()
    {
        if (manifestLoaded) return;
        manifestLoaded = true;

        try
        {
            string path = Path.Combine(RootPath, "meditation_fx.json");
            if (!File.Exists(path))
            {
                Debug.LogWarning("[AO Meditation v269] Falta meditation_fx.json");
                return;
            }

            manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
            if (manifest?.entries != null)
                foreach (Entry entry in manifest.entries)
                    if (entry != null && entry.fx > 0)
                        entriesByFx[entry.fx] = entry;

            Debug.Log("[AO Meditation v269] FX de meditación cargados: " + entriesByFx.Count +
                      " | tabla por nivel: " + (manifest?.levelTable == null ? 0 : manifest.levelTable.Length));
        }
        catch (Exception ex)
        {
            Debug.LogError("[AO Meditation v269] Error cargando manifest: " + ex.Message);
        }
    }

    void OnDisable()
    {
        End(true);
    }

    void OnDestroy()
    {
        End(true);
    }
}
