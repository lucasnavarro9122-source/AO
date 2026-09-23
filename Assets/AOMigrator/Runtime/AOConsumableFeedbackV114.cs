using UnityEngine;

public class AOConsumableFeedbackV114 : MonoBehaviour
{
    static AOConsumableFeedbackV114 instance;

    AudioSource source;
    AudioClip food;
    AudioClip drink;
    AudioClip alternateDrink;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        instance = null;
    }

    static AOConsumableFeedbackV114 Ensure()
    {
        if (instance != null)
            return instance;

        instance =
            Object.FindFirstObjectByType
                <AOConsumableFeedbackV114>();

        if (instance != null)
            return instance;

        GameObject go =
            new GameObject(
                "AO Consumable Feedback v0.11.4");

        instance =
            go.AddComponent
                <AOConsumableFeedbackV114>();

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

        source =
            gameObject.AddComponent
                <AudioSource>();

        source.playOnAwake = false;
        source.spatialBlend = 0f;

        food =
            Resources.Load<AudioClip>(
                "AOMigrator/ConsumablesV114/Audio/7");

        alternateDrink =
            Resources.Load<AudioClip>(
                "AOMigrator/ConsumablesV114/Audio/46");

        drink =
            Resources.Load<AudioClip>(
                "AOMigrator/ConsumablesV114/Audio/135");
    }

    public static void PlayFood()
    {
        AOConsumableFeedbackV114 fx =
            Ensure();

        fx.Play(
            fx.food);
    }

    public static void PlayDrink(
        int soundId)
    {
        AOConsumableFeedbackV114 fx =
            Ensure();

        if (soundId == 46)
            fx.Play(
                fx.alternateDrink);
        else
            fx.Play(
                fx.drink);
    }

    static void Pulse(
        AOCharacterRenderer visual)
    {
        if (visual != null)
        {
            AOCombatFeedbackV113.PlayEquip(
                visual,
                3);
        }
    }

    public static void Used(
        AOCharacterRenderer visual,
        bool isFood,
        int soundId)
    {
        if (isFood)
            PlayFood();
        else
            PlayDrink(soundId);

        Pulse(
            visual);
    }

    void Play(
        AudioClip clip)
    {
        if (clip == null ||
            source == null)
            return;

        source.PlayOneShot(
            clip,
            0.72f);
    }
}
