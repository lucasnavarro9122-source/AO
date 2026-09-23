using System;
using System.Collections.Generic;
using UnityEngine;

// Sound IDs and footstep pairs follow the original AO20 client.
[DisallowMultipleComponent]
public partial class AOAudioV190 : MonoBehaviour
{
    static AOAudioV190 instance;
    readonly Dictionary<int, AudioClip> clips = new Dictionary<int, AudioClip>();

    AudioSource effects;
    AudioSource weather;
    bool weatherActive;
    int stepIndex;
    float lastStepAt = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        Ensure();
    }

    static AOAudioV190 Ensure()
    {
        if (instance != null)
            return instance;

        instance = UnityEngine.Object.FindFirstObjectByType<AOAudioV190>();
        if (instance != null)
            return instance;

        GameObject go = new GameObject("AO Audio v0.19");
        return go.AddComponent<AOAudioV190>();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        effects = gameObject.AddComponent<AudioSource>();
        effects.playOnAwake = false;
        effects.spatialBlend = 0f;

        weather = gameObject.AddComponent<AudioSource>();
        weather.playOnAwake = false;
        weather.spatialBlend = 0f;
        weather.loop = true;
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            StopMapMusic();
            instance = null;
        }
    }

    AudioClip Clip(int id)
    {
        if (id <= 0)
            return null;
        if (clips.TryGetValue(id, out AudioClip cached))
            return cached;

        string root = "AOMigrator/AudioV190/";
        AudioClip clip = Resources.Load<AudioClip>(root + "wav_" + id);
        if (clip == null)
            clip = Resources.Load<AudioClip>(root + "ogg_" + id);
        clips[id] = clip;
        return clip;
    }

    public static void PlayEffect(int id, float volume = 0.7f)
    {
        AOAudioV190 audio = Ensure();
        AudioClip clip = audio.Clip(id);
        if (clip != null && audio.effects != null)
            audio.effects.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    public static bool Clicked(bool clicked)
    {
        if (clicked)
            PlayEffect(500, 0.3f);
        return clicked;
    }

    public static void PlayFootstep(string terrain, string zone)
    {
        AOAudioV190 audio = Ensure();
        float now = Time.unscaledTime;
        if (now - audio.lastStepAt < 0.18f)
            return;

        audio.lastStepAt = now;
        audio.stepIndex++;
        bool alternate = (audio.stepIndex & 1) == 0;
        int id;

        if (string.Equals(terrain, "NIEVE", StringComparison.OrdinalIgnoreCase))
            id = alternate ? 200 : 199;
        else if (string.Equals(terrain, "DESIERTO", StringComparison.OrdinalIgnoreCase))
            id = alternate ? 198 : 197;
        else if (string.Equals(zone, "CIUDAD", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(zone, "DUNGEON", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(terrain, "INFRAMUNDO", StringComparison.OrdinalIgnoreCase))
            id = alternate ? 24 : 23;
        else
            id = alternate ? 69 : 201;

        PlayEffect(id, 0.38f);
    }

    public static void SetWeather(bool active)
    {
        AOAudioV190 audio = Ensure();
        if (audio.weatherActive == active)
            return;

        audio.weatherActive = active;
        if (audio.weather != null)
            audio.weather.Stop();

        if (active)
        {
            AudioClip rain = audio.Clip(194);
            if (rain == null || audio.weather == null)
                return;
            audio.weather.clip = rain;
            audio.weather.volume = 0.3f;
            audio.weather.Play();
        }
        else
        {
            PlayEffect(195, 0.3f);
        }
    }
}
