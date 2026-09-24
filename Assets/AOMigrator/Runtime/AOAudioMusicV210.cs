using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using System.Runtime.InteropServices;
using System.Text;
#endif

public partial class AOAudioV190
{
    [Serializable]
    class MapMusic
    {
        public int mapNumber;
        public int musicId;
    }

    [Serializable]
    class MusicCatalog
    {
        public MapMusic[] maps;
    }

    static Dictionary<int, int> musicByMap;
    int playingMusicId;
    int requestedMapNumber;
    float nextMusicCheck;
    public static int CurrentMapMusicId => instance == null ? 0 :
        instance.playingMusicId;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    const string MidiAlias = "ao_unity_map_music";

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    static extern int mciSendString(string command, StringBuilder result,
                                    int resultLength, IntPtr callback);

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    static extern bool mciGetErrorString(int error, StringBuilder text,
                                         int length);
#endif

    static int MusicForMap(int mapNumber)
    {
        if (musicByMap == null)
        {
            musicByMap = new Dictionary<int, int>();
            TextAsset source = Resources.Load<TextAsset>(
                "AOMigrator/AudioV190/map_music");
            if (source != null)
            {
                MusicCatalog catalog = JsonUtility.FromJson<MusicCatalog>(source.text);
                if (catalog != null && catalog.maps != null)
                    foreach (MapMusic entry in catalog.maps)
                        musicByMap[entry.mapNumber] = entry.musicId;
            }
        }
        return musicByMap.TryGetValue(mapNumber, out int id) ? id : 0;
    }

    public static void SetMapMusic(int mapNumber)
    {
        AOAudioV190 audio = Ensure();
        audio.requestedMapNumber = mapNumber;
        audio.PlayMapMusic(AOMainMenuV140.SessionActive ? MusicForMap(mapNumber) : 2);
    }

    public static void RefreshMusicPreference()
    {
        if (instance == null) return;
        if (!AOPlayerSettingsV230.Music)
            instance.StopMapMusic();
        else
            instance.PlayMapMusic(AOMainMenuV140.SessionActive ? MusicForMap(instance.requestedMapNumber) : 2);
    }

    void PlayMapMusic(int musicId)
    {
        if (playingMusicId == musicId)
            return;
        StopMapMusic();
        if (!AOPlayerSettingsV230.Music)
            return;
        if (musicId <= 0 || musicId > 999)
            return;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        string path;
        try { path = PrepareMidiPath(musicId); }
        catch (IOException error) { Debug.LogWarning("AO music: " + error.Message); return; }
        catch (UnauthorizedAccessException error) { Debug.LogWarning("AO music: " + error.Message); return; }
        if (string.IsNullOrEmpty(path)) return;
        // A previous domain reload may leave an MCI alias open.
        mciSendString("close " + MidiAlias, null, 0, IntPtr.Zero);

        int open = mciSendString("open \"" + path +
            "\" type sequencer alias " + MidiAlias, null, 0, IntPtr.Zero);
        if (open != 0)
        {
            Debug.LogWarning("AO music: " + MidiError(open));
            return;
        }
        int play = mciSendString("play " + MidiAlias,
                                 null, 0, IntPtr.Zero);
        if (play != 0)
        {
            Debug.LogWarning("AO music: " + MidiError(play));
            mciSendString("close " + MidiAlias, null, 0, IntPtr.Zero);
            return;
        }
        playingMusicId = musicId;
        nextMusicCheck = Time.unscaledTime + 2f;
#endif
    }

    // Windows MCI fails on some long install paths. Keep a short local MIDI cache.
    public static string PrepareMidiPath(int musicId)
    {
        if (musicId <= 0 || musicId > 999) return null;
        string source = Path.Combine(Application.streamingAssetsPath, "AOMigrator", "Music", "track_" + musicId + ".mid");
        if (!File.Exists(source)) return null;
        string cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AoDuels", "Music");
        Directory.CreateDirectory(cache);
        string target = Path.Combine(cache, "track_" + musicId + ".mid");
        if (!File.Exists(target) || new FileInfo(source).Length != new FileInfo(target).Length || File.GetLastWriteTimeUtc(source) != File.GetLastWriteTimeUtc(target))
            File.Copy(source, target, true);
        return target;
    }

    void Update()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        int desired = AOMainMenuV140.SessionActive ? MusicForMap(requestedMapNumber) : 2;
        if (AOPlayerSettingsV230.Music && desired != playingMusicId && Time.unscaledTime >= nextMusicCheck)
        { nextMusicCheck = Time.unscaledTime + 5f; PlayMapMusic(desired); }
        if (playingMusicId == 0 || Time.unscaledTime < nextMusicCheck)
            return;
        nextMusicCheck = Time.unscaledTime + 2f;
        StringBuilder mode = new StringBuilder(32);
        if (mciSendString("status " + MidiAlias + " mode", mode,
                          mode.Capacity, IntPtr.Zero) != 0 ||
            mode.ToString() != "stopped")
            return;
        mciSendString("seek " + MidiAlias + " to start", null, 0,
                      IntPtr.Zero);
        mciSendString("play " + MidiAlias, null, 0, IntPtr.Zero);
#endif
    }

    void StopMapMusic()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (playingMusicId != 0)
            mciSendString("close " + MidiAlias, null, 0, IntPtr.Zero);
#endif
        playingMusicId = 0;
    }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    static string MidiError(int code)
    {
        StringBuilder message = new StringBuilder(256);
        mciGetErrorString(code, message, message.Capacity);
        return "MIDI " + code + ": " + message;
    }
#endif
}
