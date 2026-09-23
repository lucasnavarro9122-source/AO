using System;
using UnityEngine;

public class AOCharacterRenderer : MonoBehaviour
{
    [Serializable]
    public class DirectionVisual
    {
        public int heading;
        public Sprite[] body;
        public Sprite[] head;
        public Sprite[] helmet;
        public Sprite[] weapon;
        public Sprite[] shield;
    }

    [SerializeField] DirectionVisual[] directions;
    [SerializeField] int heading = AOGridMap.SOUTH;
    [SerializeField] bool walking;
    [SerializeField] float walkFps = 18f;
    [SerializeField] float headOffsetX;
    [SerializeField] float headOffsetY;
    [SerializeField] float bodyShiftX;

    DirectionVisual[] equipmentDirections;
    bool equipmentBodyOverride;
    float equipmentHeadOffsetX;
    float equipmentHeadOffsetY;
    float equipmentBodyShiftX;

    SpriteRenderer bodyRenderer;
    SpriteRenderer headRenderer;
    SpriteRenderer helmetRenderer;
    SpriteRenderer weaponRenderer;
    SpriteRenderer shieldRenderer;

    // Tiempo continuo de la caminata.
    // No se reinicia al cambiar de dirección/equipamiento mientras camina.
    float walkTime;

    bool combatAnimating;
    float combatTime;
    float combatDuration = 0.22f;

    public int Heading => heading;
    public bool Walking => walking;
    public bool CombatAnimating => combatAnimating;

    public int CurrentBodyFrameCount =>
        CountFrames(
            EffectiveBodyFrames());

    public int CurrentWeaponFrameCount =>
        CountFrames(
            EffectiveWeaponFrames());

    public int CurrentShieldFrameCount =>
        CountFrames(
            EffectiveShieldFrames());

    public int CurrentHelmetFrameCount =>
        CountFrames(
            EffectiveHelmetFrames());

    public void Configure(
        DirectionVisual[] newDirections,
        float fps,
        float headX,
        float headY,
        float bodyX)
    {
        directions = newDirections;
        walkFps = Mathf.Max(1f, fps);
        headOffsetX = headX;
        headOffsetY = headY;
        bodyShiftX = bodyX;

        EnsureRenderers();
        ApplyOffsets();
        RefreshSprites();
    }

    public void ConfigureEquipment(
        DirectionVisual[] overrides,
        bool bodyOverride,
        float overrideHeadX,
        float overrideHeadY,
        float overrideBodyX)
    {
        equipmentDirections = overrides;
        equipmentBodyOverride = bodyOverride;
        equipmentHeadOffsetX = overrideHeadX;
        equipmentHeadOffsetY = overrideHeadY;
        equipmentBodyShiftX = overrideBodyX;

        // Importante: no reinicia walkTime.
        // Cambiar arma/escudo/ropa mientras caminamos conserva la fase.
        EnsureRenderers();
        ApplyOffsets();
        RefreshSprites();
    }

    public void ForceRefreshVisuals()
    {
        EnsureRenderers();
        ApplyOffsets();
        RefreshSprites();
    }

    public void PlayCombatBurst(
        float duration = 0.22f)
    {
        combatAnimating = true;
        combatTime = 0f;
        combatDuration =
            Mathf.Max(
                0.08f,
                duration);

        RefreshSprites();
    }

    public void StopCombatBurst()
    {
        combatAnimating = false;
        combatTime = 0f;
        RefreshSprites();
    }

    void Awake()
    {
        EnsureRenderers();
        ApplyOffsets();
        RefreshSprites();
    }

    void Update()
    {
        if (walking)
        {
            walkTime +=
                Time.deltaTime;

            if (walkTime > 10000f)
                walkTime =
                    Mathf.Repeat(
                        walkTime,
                        100f);
        }

        if (combatAnimating)
        {
            combatTime +=
                Time.deltaTime;

            if (combatTime >=
                combatDuration)
            {
                combatAnimating = false;
                combatTime = 0f;
            }
        }

        RefreshSprites();
    }

    public void SetHeading(
        int newHeading)
    {
        if (newHeading < AOGridMap.NORTH ||
            newHeading > AOGridMap.WEST)
            return;

        if (heading == newHeading)
            return;

        heading = newHeading;

        // El cliente original preserva la fase al girar si ya está caminando.
        if (!walking)
            walkTime = 0f;

        RefreshSprites();
    }

    public void SetWalking(
        bool value)
    {
        if (walking == value)
            return;

        walking = value;

        // Sólo reseteamos al detenerse de verdad.
        // AOTestPlayer v0.11.2 evita llamar esto entre tiles consecutivos.
        if (!walking)
            walkTime = 0f;

        RefreshSprites();
    }

    public void UpdateSorting(
        int baseOrder)
    {
        EnsureRenderers();

        switch (heading)
        {
            case AOGridMap.EAST:
                SetOrders(
                    baseOrder,
                    0, 1, 2, 3, -2);
                break;

            case AOGridMap.NORTH:
                SetOrders(
                    baseOrder,
                    0, 1, 2, -2, -3);
                break;

            case AOGridMap.WEST:
                SetOrders(
                    baseOrder,
                    0, 1, 2, -3, 3);
                break;

            default:
                SetOrders(
                    baseOrder,
                    0, 1, 2, 4, 3);
                break;
        }
    }

    void SetOrders(
        int baseOrder,
        int body,
        int head,
        int helmet,
        int weapon,
        int shield)
    {
        bodyRenderer.sortingOrder =
            baseOrder + body;

        headRenderer.sortingOrder =
            baseOrder + head;

        helmetRenderer.sortingOrder =
            baseOrder + helmet;

        weaponRenderer.sortingOrder =
            baseOrder + weapon;

        shieldRenderer.sortingOrder =
            baseOrder + shield;
    }

    void EnsureRenderers()
    {
        bodyRenderer =
            EnsureChild(
                "Body",
                bodyRenderer);

        headRenderer =
            EnsureChild(
                "Head",
                headRenderer);

        helmetRenderer =
            EnsureChild(
                "Helmet",
                helmetRenderer);

        weaponRenderer =
            EnsureChild(
                "Weapon",
                weaponRenderer);

        shieldRenderer =
            EnsureChild(
                "Shield",
                shieldRenderer);
    }

    SpriteRenderer EnsureChild(
        string childName,
        SpriteRenderer current)
    {
        if (current != null)
            return current;

        Transform child =
            transform.Find(
                childName);

        if (child == null)
        {
            GameObject go =
                new GameObject(
                    childName,
                    typeof(SpriteRenderer));

            go.transform.SetParent(
                transform,
                false);

            child = go.transform;
        }

        SpriteRenderer sr =
            child.GetComponent
                <SpriteRenderer>();

        if (sr == null)
        {
            sr =
                child.gameObject
                    .AddComponent
                        <SpriteRenderer>();
        }

        return sr;
    }

    void ApplyOffsets()
    {
        if (bodyRenderer == null)
            return;

        float activeBodyX =
            equipmentBodyOverride
            ? equipmentBodyShiftX
            : bodyShiftX;

        float activeHeadX =
            equipmentBodyOverride
            ? equipmentHeadOffsetX
            : headOffsetX;

        float activeHeadY =
            equipmentBodyOverride
            ? equipmentHeadOffsetY
            : headOffsetY;

        bodyRenderer.transform.localPosition =
            new Vector3(
                activeBodyX,
                0f,
                0f);

        headRenderer.transform.localPosition =
            new Vector3(
                activeHeadX,
                activeHeadY,
                0f);

        helmetRenderer.transform.localPosition =
            new Vector3(
                activeHeadX,
                activeHeadY,
                0f);

        weaponRenderer.transform.localPosition =
            Vector3.zero;

        shieldRenderer.transform.localPosition =
            Vector3.zero;
    }

    DirectionVisual Current()
    {
        if (directions == null)
            return null;

        foreach (DirectionVisual d in directions)
        {
            if (d != null &&
                d.heading == heading)
                return d;
        }

        return null;
    }

    DirectionVisual CurrentEquipment()
    {
        if (equipmentDirections == null)
            return null;

        foreach (
            DirectionVisual d in
            equipmentDirections)
        {
            if (d != null &&
                d.heading == heading)
                return d;
        }

        return null;
    }

    Sprite[] EffectiveBodyFrames()
    {
        DirectionVisual d =
            Current();

        DirectionVisual e =
            CurrentEquipment();

        if (e != null &&
            e.body != null &&
            e.body.Length > 0)
            return e.body;

        return d == null
            ? null
            : d.body;
    }

    Sprite[] EffectiveWeaponFrames()
    {
        DirectionVisual d =
            Current();

        DirectionVisual e =
            CurrentEquipment();

        if (e != null &&
            e.weapon != null &&
            e.weapon.Length > 0)
            return e.weapon;

        return d == null
            ? null
            : d.weapon;
    }

    Sprite[] EffectiveShieldFrames()
    {
        DirectionVisual d =
            Current();

        DirectionVisual e =
            CurrentEquipment();

        if (e != null &&
            e.shield != null &&
            e.shield.Length > 0)
            return e.shield;

        return d == null
            ? null
            : d.shield;
    }

    Sprite[] EffectiveHelmetFrames()
    {
        DirectionVisual d =
            Current();

        DirectionVisual e =
            CurrentEquipment();

        if (e != null &&
            e.helmet != null &&
            e.helmet.Length > 0)
            return e.helmet;

        return d == null
            ? null
            : d.helmet;
    }

    int ReferenceFrameCount()
    {
        int body =
            CountFrames(
                EffectiveBodyFrames());

        if (body > 1)
            return body;

        int weapon =
            CountFrames(
                EffectiveWeaponFrames());

        if (weapon > 1)
            return weapon;

        int shield =
            CountFrames(
                EffectiveShieldFrames());

        if (shield > 1)
            return shield;

        return 1;
    }

    static int CountFrames(
        Sprite[] frames)
    {
        return frames == null
            ? 0
            : frames.Length;
    }

    int FrameIndex(
        int targetLength)
    {
        if (targetLength <= 1)
            return 0;

        if (combatAnimating)
        {
            float attackPhase =
                Mathf.Clamp01(
                    combatTime /
                    Mathf.Max(
                        0.01f,
                        combatDuration));

            return Mathf.Clamp(
                Mathf.FloorToInt(
                    attackPhase *
                    targetLength),
                0,
                targetLength - 1);
        }

        if (!walking)
            return 0;

        int referenceLength =
            Mathf.Max(
                1,
                ReferenceFrameCount());

        // walkFps indica frames/segundo del cuerpo de referencia.
        // La fase normalizada mantiene Body/Weapon/Shield sincronizados,
        // incluso si un recurso tiene 5 frames y otro 6.
        float phase =
            Mathf.Repeat(
                walkTime *
                walkFps /
                referenceLength,
                1f);

        return Mathf.Clamp(
            Mathf.FloorToInt(
                phase *
                targetLength),
            0,
            targetLength - 1);
    }

    Sprite Pick(
        Sprite[] frames)
    {
        if (frames == null ||
            frames.Length == 0)
            return null;

        return frames[
            FrameIndex(
                frames.Length)];
    }

    void RefreshSprites()
    {
        EnsureRenderers();

        DirectionVisual d =
            Current();

        if (d == null)
            return;

        DirectionVisual e =
            CurrentEquipment();

        bodyRenderer.sprite =
            Pick(
                e != null &&
                e.body != null &&
                e.body.Length > 0
                ? e.body
                : d.body);

        headRenderer.sprite =
            Pick(d.head);

        helmetRenderer.sprite =
            Pick(
                e != null &&
                e.helmet != null &&
                e.helmet.Length > 0
                ? e.helmet
                : d.helmet);

        weaponRenderer.sprite =
            Pick(
                e != null &&
                e.weapon != null &&
                e.weapon.Length > 0
                ? e.weapon
                : d.weapon);

        shieldRenderer.sprite =
            Pick(
                e != null &&
                e.shield != null &&
                e.shield.Length > 0
                ? e.shield
                : d.shield);
    }
}
