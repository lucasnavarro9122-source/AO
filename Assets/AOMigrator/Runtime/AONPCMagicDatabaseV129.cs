using System;
using System.Collections.Generic;
using UnityEngine;

public static class AONPCMagicDatabaseV129
{
    const string Path="AOMigrator/MagicV129/npc_magic";
    [Serializable] public class Entry { public int npcIndex,magicResistance,magicDef; public bool immuneToSpells; }
    [Serializable] class Database { public string version; public Entry[] npcs; }
    static Database data; static readonly Dictionary<int,Entry> byId=new Dictionary<int,Entry>();
    public static Entry Get(int id){Ensure();return byId.TryGetValue(id,out Entry e)?e:null;}
    static void Ensure(){if(data!=null)return;TextAsset a=Resources.Load<TextAsset>(Path);if(a==null)return;data=JsonUtility.FromJson<Database>(a.text);if(data!=null&&data.npcs!=null)foreach(var e in data.npcs)if(e!=null&&e.npcIndex>0)byId[e.npcIndex]=e;}
}
