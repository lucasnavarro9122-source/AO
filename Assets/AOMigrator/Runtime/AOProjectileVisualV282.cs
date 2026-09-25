using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Proyectiles en vuelo (flechas, hachas arrojadizas, red y bala de cañón), fieles a InitializeProjectile
// del cliente AO20: ProjectileDef.dat (GRH, velocidad y giro) + ProjectileType de obj.dat y npcs.dat.
// Solo visual: el golpe lo resuelve el combate (cliente o servidor).
[DisallowMultipleComponent]
public class AOProjectileVisualV282 : MonoBehaviour
{
    [Serializable] class TypeDef
    {
        public int type;
        public int grh;
        public int grhRight;
        public float speedPxPerMs;
        public float rotationSpeedDegPerMs;
        public float offsetRotation;
    }

    [Serializable] class IdType
    {
        public int id;
        public int type;
    }

    [Serializable] class Data
    {
        public float yOffsetTiles = 0.6f;
        public int sortingOrder = 20030;
        public float speedMultiplier = 1f;
        public TypeDef[] types;
        public IdType[] objetos;
        public IdType[] npcs;
    }

    const float PixelsPerTile = 32f;

    static Data data;
    static bool loaded;
    static readonly Dictionary<int, Sprite> sprites = new Dictionary<int, Sprite>();

    Vector3 target;
    float unitsPerSecond;
    float spinDegreesPerSecond;

    static string RootPath =>
        Path.Combine(Application.streamingAssetsPath, "AOMigrator", "ProjectilesV282");

    // Tipo de proyectil de un objeto (flecha, hacha arrojadiza...) o de un NPC; 0 si no tiene.
    public static int TypeForObject(int objIndex) => Find(Load()?.objetos, objIndex);
    public static int TypeForNpc(int npcIndex) => Find(Load()?.npcs, npcIndex);

    // Dibuja el proyectil entre dos posiciones de mundo (los pies del tirador y los del objetivo).
    public static void Spawn(Vector3 from, Vector3 to, int projectileType)
    {
        Data d = Load();
        TypeDef def = FindType(d, projectileType);
        if (def == null) return;

        // Como el original: hacia la derecha usa el GRH derecho (si existe) y gira al revés.
        bool right = def.grhRight > 0 && to.x > from.x;
        Sprite sprite = SpriteFor(right ? def.grhRight : def.grh);
        if (sprite == null) return;

        Vector3 lift = new Vector3(0f, d.yOffsetTiles, 0f);
        Vector3 start = from + lift;
        Vector3 end = to + lift;
        end.z = start.z;

        var go = new GameObject("AO Projectile " + projectileType);
        go.transform.position = start;
        Vector3 direction = end - start;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + def.offsetRotation;
        go.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = d.sortingOrder;

        var projectile = go.AddComponent<AOProjectileVisualV282>();
        projectile.target = end;
        projectile.unitsPerSecond = Mathf.Max(0.1f, def.speedPxPerMs * 1000f / PixelsPerTile * Mathf.Max(0.01f, d.speedMultiplier));
        // El original gira en pantalla con Y hacia abajo; en Unity el ángulo positivo es antihorario.
        float spin = right ? -def.rotationSpeedDegPerMs : def.rotationSpeedDegPerMs;
        projectile.spinDegreesPerSecond = -spin * 1000f;
    }

    void Update()
    {
        Vector3 position = transform.position;
        Vector3 delta = target - position;
        float step = unitsPerSecond * Time.deltaTime;
        if (delta.magnitude <= step)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = position + delta.normalized * step;
        if (spinDegreesPerSecond != 0f)
            transform.Rotate(0f, 0f, spinDegreesPerSecond * Time.deltaTime);
    }

    static int Find(IdType[] list, int id)
    {
        if (list == null) return 0;
        foreach (IdType entry in list)
            if (entry != null && entry.id == id)
                return entry.type;
        return 0;
    }

    static TypeDef FindType(Data d, int type)
    {
        if (d?.types == null) return null;
        foreach (TypeDef def in d.types)
            if (def != null && def.type == type)
                return def;
        return null;
    }

    static Sprite SpriteFor(int grh)
    {
        if (grh <= 0) return null;
        if (sprites.TryGetValue(grh, out Sprite cached)) return cached;

        Sprite sprite = null;
        try
        {
            string path = Path.Combine(RootPath, "grh_" + grh + ".png");
            if (File.Exists(path))
            {
                var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (texture.LoadImage(File.ReadAllBytes(path), false))
                {
                    texture.name = "Projectile_" + grh;
                    texture.filterMode = FilterMode.Point;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f), PixelsPerTile, 0, SpriteMeshType.FullRect);
                    sprite.name = texture.name;
                }
                else
                {
                    UnityEngine.Object.Destroy(texture);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[AO Projectile v282] No se pudo cargar GRH " + grh + ": " + ex.Message);
        }

        sprites[grh] = sprite;
        return sprite;
    }

    static Data Load()
    {
        if (loaded) return data;
        loaded = true;
        try
        {
            string path = Path.Combine(RootPath, "projectiles.json");
            if (!File.Exists(path))
            {
                Debug.LogWarning("[AO Projectile v282] Falta projectiles.json");
                return null;
            }
            data = JsonUtility.FromJson<Data>(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            Debug.LogError("[AO Projectile v282] Error cargando projectiles.json: " + ex.Message);
        }
        return data;
    }
}
