using UnityEngine;

public class AOConsumableFeedbackV114 : MonoBehaviour
{
    static AOConsumableFeedbackV114 instance;

    AudioSource source;
    AudioClip food;
    AudioClip drink;

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

        drink =
            Resources.Load<AudioClip>(
                "AOMigrator/ConsumablesV114/Audio/135");
    }

    public static void PlayFood(int soundId = 0)
    {
        if (soundId > 0)
        {
            AOAudioV190.PlayEffect(soundId, 0.72f);
            return;
        }

        AOConsumableFeedbackV114 fx =
            Ensure();

        fx.Play(
            fx.food);
    }

    public static void PlayDrink(
        int soundId)
    {
        if (soundId > 0)
        {
            AOAudioV190.PlayEffect(soundId, 0.72f);
            return;
        }

        AOConsumableFeedbackV114 fx =
            Ensure();
        fx.Play(fx.drink);
    }

    static void Pulse(
        AOCharacterRenderer visual)
    {
        if (visual != null)
        {
            AOCombatFeedbackV113.PlayEquip(
                visual,
                3,
                false);
        }
    }

    public static void Used(
        AOCharacterRenderer visual,
        bool isFood,
        int soundId)
    {
        if (isFood)
            PlayFood(soundId);
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
