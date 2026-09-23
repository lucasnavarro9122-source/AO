using UnityEngine;

public static class AOLootFeedbackV180
{
    static AudioSource source;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        source = null;
    }

    static AudioSource Source()
    {
        if (source != null)
            return source;

        GameObject go =
            GameObject.Find(
                "AO Loot Audio v0.18");

        if (go == null)
        {
            go =
                new GameObject(
                    "AO Loot Audio v0.18");
        }

        source =
            go.GetComponent<AudioSource>();

        if (source == null)
        {
            source =
                go.AddComponent<AudioSource>();
        }

        source.playOnAwake =
            false;

        source.spatialBlend =
            0f;

        return source;
    }

    public static void PlayDrop()
    {
        AudioClip clip =
            Resources.Load<AudioClip>(
                "AOMigrator/LootV180/Audio/wav_132");

        if (clip != null)
        {
            Source().PlayOneShot(
                clip,
                0.78f);
        }
    }
}
