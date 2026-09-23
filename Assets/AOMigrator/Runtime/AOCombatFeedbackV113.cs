using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AOCombatFeedbackV113 : MonoBehaviour
{
    class FloatingText
    {
        public Vector3 world;
        public string text;
        public float start;
        public float duration;
        public bool miss;
    }

    class VisualBase
    {
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
    }

    static AOCombatFeedbackV113 instance;

    readonly List<FloatingText> floating =
        new List<FloatingText>();

    readonly Dictionary<int, VisualBase> bases =
        new Dictionary<int, VisualBase>();

    readonly Dictionary<int, Coroutine> attackCoroutines =
        new Dictionary<int, Coroutine>();

    AudioSource audioSource;

    AudioClip hit;
    AudioClip deathMale;
    AudioClip deathFemale;
    AudioClip equipArmor;
    AudioClip equipWeapon;
    AudioClip shieldClash;

    GUIStyle damageStyle;
    GUIStyle missStyle;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        Ensure();
    }

    static AOCombatFeedbackV113 Ensure()
    {
        if (instance != null)
            return instance;

        instance =
            UnityEngine.Object
                .FindFirstObjectByType
                    <AOCombatFeedbackV113>();

        if (instance != null)
            return instance;

        GameObject go =
            new GameObject(
                "AO Combat Feedback v0.11.3");

        instance =
            go.AddComponent
                <AOCombatFeedbackV113>();

        return instance;
    }

    void Awake()
    {
        if (instance != null &&
            instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        audioSource =
            gameObject.AddComponent
                <AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        hit =
            Resources.Load<AudioClip>(
                "AOMigrator/CombatV113/Audio/hit_10");

        deathMale =
            Resources.Load<AudioClip>(
                "AOMigrator/CombatV113/Audio/death_male_11");

        deathFemale =
            Resources.Load<AudioClip>(
                "AOMigrator/CombatV113/Audio/death_female_74");

        equipArmor =
            Resources.Load<AudioClip>(
                "AOMigrator/CombatV113/Audio/equip_armor_203");

        equipWeapon =
            Resources.Load<AudioClip>(
                "AOMigrator/CombatV113/Audio/equip_weapon_208");

        shieldClash =
            Resources.Load<AudioClip>(
                "AOMigrator/CombatV113/Audio/shield_clash_136");
    }

    public static void PlayAttack(
        AOCharacterRenderer visual,
        int heading,
        int weaponType)
    {
        if (visual == null)
            return;

        AOCombatFeedbackV113 fx =
            Ensure();

        visual.PlayCombatBurst(
            weaponType == 3 ||
            weaponType == 11
            ? 0.16f
            : 0.22f);

        fx.StartAttackMotion(
            visual.transform,
            heading,
            weaponType);
    }

    public static void PlayHit(
        AOCharacterRenderer visual,
        int damage)
    {
        if (visual == null)
            return;

        AOCombatFeedbackV113 fx =
            Ensure();

        fx.StartCoroutine(
            fx.HitFlashRoutine(
                visual));

        fx.SpawnFloating(
            visual.transform.position +
                new Vector3(
                    0f,
                    0.85f,
                    0f),
            "-" +
            Mathf.Max(
                0,
                damage),
            false);

        fx.PlayOneShot(
            fx.hit,
            0.75f);
    }

    public static void PlayShieldBlock(
        AOCharacterRenderer visual)
    {
        if (visual == null)
            return;

        AOCombatFeedbackV113 fx =
            Ensure();

        fx.SpawnFloating(
            visual.transform.position +
                new Vector3(
                    0f,
                    0.85f,
                    0f),
            "BLOCK",
            true);

        fx.PlayOneShot(
            fx.shieldClash,
            0.7f);
    }

    public static void PlayMiss(
        Vector3 world)
    {
        AOAudioV190.PlayEffect(2, 0.7f);
        Ensure()
            .SpawnFloating(
                world +
                new Vector3(
                    0f,
                    0.7f,
                    0f),
                "MISS",
                true);
    }

    public static void PlayDeath(
        AOCharacterRenderer visual,
        bool female)
    {
        if (visual == null)
            return;

        AOCombatFeedbackV113 fx =
            Ensure();

        fx.StartCoroutine(
            fx.DeathRoutine(
                visual));

        fx.PlayOneShot(
            female
            ? fx.deathFemale
            : fx.deathMale,
            0.8f);
    }

    public static void ResetVisual(
        AOCharacterRenderer visual)
    {
        if (visual == null)
            return;

        AOCombatFeedbackV113 fx =
            Ensure();

        fx.RestoreBase(
            visual.transform);

        foreach (
            SpriteRenderer sr in
            visual.GetComponentsInChildren
                <SpriteRenderer>(true))
        {
            if (sr != null)
                sr.color =
                    Color.white;
        }

        visual.StopCombatBurst();
        visual.ForceRefreshVisuals();
    }

    public static void PlayEquip(
        AOCharacterRenderer visual,
        int objType,
        bool playSound = true)
    {
        if (visual == null)
            return;

        AOCombatFeedbackV113 fx =
            Ensure();

        fx.StartCoroutine(
            fx.EquipPulseRoutine(
                visual.transform));

        if (!playSound)
            return;

        if (objType == 2)
        {
            fx.PlayOneShot(
                fx.equipWeapon,
                0.7f);
        }
        else if (
            objType == 3 ||
            objType == 16 ||
            objType == 17)
        {
            fx.PlayOneShot(
                fx.equipArmor,
                0.7f);
        }
    }

    void StartAttackMotion(
        Transform visual,
        int heading,
        int weaponType)
    {
        if (visual == null)
            return;

        RememberBase(
            visual);

        int id =
            visual.GetInstanceID();

        if (attackCoroutines.TryGetValue(
                id,
                out Coroutine running) &&
            running != null)
        {
            StopCoroutine(
                running);

            RestoreBase(
                visual);
        }

        attackCoroutines[id] =
            StartCoroutine(
                AttackMotionRoutine(
                    visual,
                    heading,
                    weaponType));
    }

    IEnumerator AttackMotionRoutine(
        Transform visual,
        int heading,
        int weaponType)
    {
        VisualBase b =
            GetBase(
                visual);

        float duration =
            weaponType == 3 ||
            weaponType == 11
            ? 0.16f
            : 0.22f;

        float distance =
            weaponType == 3 ||
            weaponType == 11
            ? 0.045f
            : 0.13f;

        Vector3 dir =
            HeadingVector(
                heading);

        float elapsed = 0f;

        while (elapsed <
               duration)
        {
            if (visual == null)
                yield break;

            elapsed +=
                Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    duration);

            float arc =
                Mathf.Sin(
                    t *
                    Mathf.PI);

            visual.localPosition =
                b.localPosition +
                dir *
                distance *
                arc;

            float angle =
                weaponType == 3 ||
                weaponType == 11
                ? -3f *
                  arc
                : (heading ==
                   AOGridMap.WEST
                    ? 8f
                    : -8f) *
                  arc;

            visual.localRotation =
                b.localRotation *
                Quaternion.Euler(
                    0f,
                    0f,
                    angle);

            yield return null;
        }

        RestoreBase(
            visual);

        attackCoroutines.Remove(
            visual.GetInstanceID());
    }

    IEnumerator HitFlashRoutine(
        AOCharacterRenderer visual)
    {
        SpriteRenderer[] renderers =
            visual.GetComponentsInChildren
                <SpriteRenderer>(true);

        Color[] original =
            new Color[
                renderers.Length];

        for (int i = 0;
             i < renderers.Length;
             i++)
        {
            if (renderers[i] == null)
                continue;

            original[i] =
                renderers[i].color;

            renderers[i].color =
                new Color(
                    1f,
                    0.45f,
                    0.45f,
                    original[i].a);
        }

        yield return new WaitForSeconds(
            0.08f);

        for (int i = 0;
             i < renderers.Length;
             i++)
        {
            if (renderers[i] != null)
                renderers[i].color =
                    original[i];
        }
    }

    IEnumerator DeathRoutine(
        AOCharacterRenderer visual)
    {
        Transform tr =
            visual.transform;

        RememberBase(
            tr);

        VisualBase b =
            GetBase(
                tr);

        SpriteRenderer[] renderers =
            visual.GetComponentsInChildren
                <SpriteRenderer>(true);

        Color[] original =
            new Color[
                renderers.Length];

        for (int i = 0;
             i < renderers.Length;
             i++)
        {
            if (renderers[i] != null)
                original[i] =
                    renderers[i].color;
        }

        float duration = 0.32f;
        float elapsed = 0f;

        while (elapsed <
               duration)
        {
            elapsed +=
                Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    duration);

            tr.localRotation =
                b.localRotation *
                Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Lerp(
                        0f,
                        -70f,
                        t));

            tr.localScale =
                Vector3.Lerp(
                    b.localScale,
                    b.localScale *
                    0.82f,
                    t);

            for (int i = 0;
                 i < renderers.Length;
                 i++)
            {
                if (renderers[i] == null)
                    continue;

                Color c =
                    original[i];

                c.a =
                    Mathf.Lerp(
                        original[i].a,
                        0.18f,
                        t);

                renderers[i].color = c;
            }

            yield return null;
        }
    }

    IEnumerator EquipPulseRoutine(
        Transform tr)
    {
        if (tr == null)
            yield break;

        RememberBase(
            tr);

        VisualBase b =
            GetBase(
                tr);

        float duration = 0.16f;
        float elapsed = 0f;

        while (elapsed <
               duration)
        {
            elapsed +=
                Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    duration);

            float pulse =
                Mathf.Sin(
                    t *
                    Mathf.PI);

            tr.localScale =
                b.localScale *
                (1f +
                 pulse *
                 0.06f);

            yield return null;
        }

        RestoreBase(
            tr);
    }

    void SpawnFloating(
        Vector3 world,
        string text,
        bool miss)
    {
        floating.Add(
            new FloatingText {
                world = world,
                text = text,
                start = Time.time,
                duration = 0.9f,
                miss = miss
            });
    }

    void OnGUI()
    {
        if (floating.Count == 0)
            return;

        Camera cam =
            Camera.main;

        if (cam == null)
        {
            AOCameraFollow follow =
                UnityEngine.Object
                    .FindFirstObjectByType
                        <AOCameraFollow>();

            if (follow != null)
                cam =
                    follow.GetComponent<Camera>();
        }

        if (cam == null)
            return;

        EnsureStyles();

        for (int i =
                 floating.Count - 1;
             i >= 0;
             i--)
        {
            FloatingText f =
                floating[i];

            float age =
                Time.time -
                f.start;

            if (age >=
                f.duration)
            {
                floating.RemoveAt(i);
                continue;
            }

            float t =
                age /
                f.duration;

            Vector3 screen =
                cam.WorldToScreenPoint(
                    f.world +
                    Vector3.up *
                    (t * 0.5f));

            if (screen.z < 0f)
                continue;

            float alpha =
                1f -
                Mathf.Clamp01(
                    (t - 0.6f) /
                    0.4f);

            GUIStyle style =
                f.miss
                ? missStyle
                : damageStyle;

            Color old =
                GUI.color;

            GUI.color =
                new Color(
                    1f,
                    1f,
                    1f,
                    alpha);

            GUI.Label(
                new Rect(
                    screen.x - 45f,
                    Screen.height -
                        screen.y -
                        14f,
                    90f,
                    28f),
                f.text,
                style);

            GUI.color = old;
        }
    }

    void EnsureStyles()
    {
        if (damageStyle != null)
            return;

        damageStyle =
            new GUIStyle(
                GUI.skin.label);

        damageStyle.alignment =
            TextAnchor.MiddleCenter;

        damageStyle.fontStyle =
            FontStyle.Bold;

        damageStyle.fontSize = 18;

        damageStyle.normal.textColor =
            new Color(
                1f,
                0.35f,
                0.25f);

        missStyle =
            new GUIStyle(
                damageStyle);

        missStyle.fontSize = 14;

        missStyle.normal.textColor =
            new Color(
                0.9f,
                0.9f,
                0.9f);
    }

    void PlayOneShot(
        AudioClip clip,
        float volume)
    {
        if (clip == null ||
            audioSource == null)
            return;

        audioSource.PlayOneShot(
            clip,
            Mathf.Clamp01(
                volume));
    }

    void RememberBase(
        Transform tr)
    {
        if (tr == null)
            return;

        int id =
            tr.GetInstanceID();

        if (bases.ContainsKey(id))
            return;

        bases[id] =
            new VisualBase {
                localPosition =
                    tr.localPosition,
                localRotation =
                    tr.localRotation,
                localScale =
                    tr.localScale
            };
    }

    VisualBase GetBase(
        Transform tr)
    {
        RememberBase(
            tr);

        return bases[
            tr.GetInstanceID()];
    }

    void RestoreBase(
        Transform tr)
    {
        if (tr == null)
            return;

        VisualBase b =
            GetBase(
                tr);

        tr.localPosition =
            b.localPosition;

        tr.localRotation =
            b.localRotation;

        tr.localScale =
            b.localScale;
    }

    static Vector3 HeadingVector(
        int heading)
    {
        switch (heading)
        {
            case AOGridMap.NORTH:
                return Vector3.up;

            case AOGridMap.EAST:
                return Vector3.right;

            case AOGridMap.SOUTH:
                return Vector3.down;

            case AOGridMap.WEST:
                return Vector3.left;
        }

        return Vector3.down;
    }
}
