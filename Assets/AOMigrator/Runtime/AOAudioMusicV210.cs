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
        Ensure().PlayMapMusic(MusicForMap(mapNumber));
    }

    void PlayMapMusic(int musicId)
    {
        if (playingMusicId == musicId)
            return;
        StopMapMusic();
        if (musicId <= 0 || musicId > 999)
            return;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        string path = Path.Combine(Application.streamingAssetsPath,
            "AOMigrator", "Music", "track_" + musicId + ".mid");
        if (!File.Exists(path))
            return;

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

    void Update()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
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
