using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class AOSkillShotConfigV267
{
    [Serializable] class ConfigFile { public string version; public Entry[] entries; }

    [Serializable] public class Entry
    {
        public int spellId;
        public float speed = 9f;
        public float range = 10f;
        public float hitRadius = .38f;
        public float visualScale = 1f;
    }

    static readonly int[] BuiltInIds =
    {
        1,2,3,6,43,55,201,202,204,205,209,211,267,291
    };

    static readonly Dictionary<int,Entry> entries = new Dictionary<int,Entry>();
    static bool loaded;
    static string ConfigPath => Path.Combine(Application.streamingAssetsPath,"AOMigrator","SpellOverrides","skillshot_tuning.json");

    public static bool IsSkillShot(int spellId)
    {
        for(int i=0;i<BuiltInIds.Length;i++) if(BuiltInIds[i]==spellId) return true;
        return false;
    }

    public static Entry Get(int spellId)
    {
        Ensure();
        if(entries.TryGetValue(spellId,out Entry e)) return e;
        return new Entry{spellId=spellId};
    }

    static void Ensure()
    {
        if(loaded) return;
        loaded=true;
        entries.Clear();
        try
        {
            if(!File.Exists(ConfigPath)) return;
            ConfigFile f=JsonUtility.FromJson<ConfigFile>(File.ReadAllText(ConfigPath));
            if(f==null||f.entries==null) return;
            foreach(Entry e in f.entries)
                if(e!=null&&e.spellId>0) entries[e.spellId]=e;
        }
        catch(Exception ex)
        {
            Debug.LogWarning("[AO SkillShots v0.26.7] No pude leer skillshot_tuning.json: "+ex.Message);
        }
    }
}
