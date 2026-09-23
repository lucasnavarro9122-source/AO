// AO Terrain HD v0.2.9
using UnityEngine;

namespace AOMigrator.TerrainHD
{
    public sealed class AOTerrainHDStyleManager : MonoBehaviour
    {
        const string PrefKey =
            "AO_TERRAIN_HD_STYLE_V02";

        const string RegistryResourcePath =
            "AOMigratorTerrainHD/AOTerrainHDRegistry";

        static AOTerrainHDStyleManager instance;

        AOTerrainHDRegistry registry;
        AOTerrainHDVisualStyle style;
        float nextScan;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            EnsureInstance();
        }

        static void EnsureInstance()
        {
            if (instance != null)
                return;

            instance =
                Object.FindFirstObjectByType<
                    AOTerrainHDStyleManager>();

            if (instance != null)
                return;

            GameObject go =
                new GameObject(
                    "AO Terrain HD Style Manager");

            instance =
                go.AddComponent<AOTerrainHDStyleManager>();

            DontDestroyOnLoad(go);
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            registry =
                Resources.Load<AOTerrainHDRegistry>(
                    RegistryResourcePath);

            style =
                (AOTerrainHDVisualStyle)
                Mathf.Clamp(
                    PlayerPrefs.GetInt(
                        PrefKey,
                        (int)AOTerrainHDVisualStyle.Classic),
                    0,
                    1);

            if (registry != null)
                registry.RebuildLookup();
        }

        void Update()
        {
            if (registry == null)
            {
                registry =
                    Resources.Load<AOTerrainHDRegistry>(
                        RegistryResourcePath);

                if (registry != null)
                    registry.RebuildLookup();
            }

            if (registry == null ||
                Time.unscaledTime < nextScan)
                return;

            nextScan = Time.unscaledTime + 1f;

            SpriteRenderer[] renderers =
                Object.FindObjectsByType<SpriteRenderer>(
                    FindObjectsSortMode.None);

            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer == null ||
                    renderer.sprite == null)
                    continue;

                Sprite resolved =
                    registry.Resolve(
                        renderer.sprite,
                        style);

                Sprite reverse =
                    registry.Resolve(
                        renderer.sprite,
                        style ==
                        AOTerrainHDVisualStyle.HD
                            ? AOTerrainHDVisualStyle.Classic
                            : AOTerrainHDVisualStyle.HD);

                if (resolved == renderer.sprite &&
                    reverse == renderer.sprite)
                    continue;

                AOTerrainHDRendererBridge bridge =
                    renderer.GetComponent<
                        AOTerrainHDRendererBridge>();

                if (bridge == null)
                {
                    bridge =
                        renderer.gameObject
                            .AddComponent<
                                AOTerrainHDRendererBridge>();
                }

                bridge.Bind(renderer);
            }
        }

        public static Sprite Resolve(Sprite current)
        {
            EnsureInstance();

            if (instance == null ||
                instance.registry == null ||
                current == null)
                return current;

            return instance.registry.Resolve(
                current,
                instance.style);
        }

        public static void SetStyle(
            AOTerrainHDVisualStyle value)
        {
            EnsureInstance();

            if (instance == null)
                return;

            instance.style = value;

            PlayerPrefs.SetInt(
                PrefKey,
                (int)value);

            PlayerPrefs.Save();

            if (instance.registry != null)
                instance.registry.RebuildLookup();

            AOTerrainHDRendererBridge[] bridges =
                Object.FindObjectsByType<
                    AOTerrainHDRendererBridge>(
                    FindObjectsSortMode.None);

            foreach (AOTerrainHDRendererBridge bridge
                     in bridges)
            {
                if (bridge != null)
                    bridge.ApplyNow();
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class AOTerrainHDRendererBridge :
        MonoBehaviour
    {
        SpriteRenderer target;

        public void Bind(SpriteRenderer renderer)
        {
            target = renderer;
            ApplyNow();
        }

        void Awake()
        {
            if (target == null)
                target = GetComponent<SpriteRenderer>();
        }

        void LateUpdate()
        {
            ApplyNow();
        }

        public void ApplyNow()
        {
            if (target == null)
                target = GetComponent<SpriteRenderer>();

            if (target == null ||
                target.sprite == null)
                return;

            Sprite resolved =
                AOTerrainHDStyleManager.Resolve(
                    target.sprite);

            if (resolved != null &&
                resolved != target.sprite)
                target.sprite = resolved;
        }
    }
}
