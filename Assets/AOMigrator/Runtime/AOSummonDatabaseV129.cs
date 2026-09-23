using System;
using System.Collections.Generic;
using UnityEngine;

public static class AOSummonDatabaseV129
{
    const string Root="AOMigrator/MagicV129/";
    [Serializable] public class FrameSpec { public int fileNum,sx,sy,width,height; public string key; }
    [Serializable] public class DirectionSpec { public int heading; public FrameSpec[] body,head,helmet,weapon,shield; }
    [Serializable] public class SummonDef {
        public int npcIndex; public string name,description; public int body,head,heading,maxHp,minHit,maxHit,defense,attackPower,evasionPower,magicResistance,magicDef;
        public float walkFps; public int headOffsetX,headOffsetY,bodyShiftX; public DirectionSpec[] directions;
    }
    [Serializable] class Database { public string version; public SummonDef[] summons; }
    static Database data; static readonly Dictionary<int,SummonDef> byId=new Dictionary<int,SummonDef>();
    public static SummonDef Get(int id){Ensure();return byId.TryGetValue(id,out SummonDef d)?d:null;}
    public static AOCharacterRenderer.DirectionVisual[] BuildVisuals(SummonDef def){
        if(def==null)return null;var result=new AOCharacterRenderer.DirectionVisual[4];
        for(int h=1;h<=4;h++){DirectionSpec s=null;if(def.directions!=null)foreach(var d in def.directions)if(d!=null&&d.heading==h){s=d;break;}
            result[h-1]=new AOCharacterRenderer.DirectionVisual{heading=h,body=Frames(s==null?null:s.body),head=Frames(s==null?null:s.head),helmet=Frames(s==null?null:s.helmet),weapon=Frames(s==null?null:s.weapon),shield=Frames(s==null?null:s.shield)};
        }return result;
    }
    static Sprite[] Frames(FrameSpec[] fs){if(fs==null)return new Sprite[0];Sprite[] a=new Sprite[fs.Length];for(int i=0;i<a.Length;i++)a[i]=SpriteFor(fs[i]);return a;}
    static readonly Dictionary<int,Texture2D> tex=new Dictionary<int,Texture2D>();static readonly Dictionary<string,Sprite> spr=new Dictionary<string,Sprite>();
    static Sprite SpriteFor(FrameSpec f){if(f==null)return null;string k=f.key;if(spr.TryGetValue(k,out Sprite c))return c;if(!tex.TryGetValue(f.fileNum,out Texture2D t)){t=Resources.Load<Texture2D>(Root+"Textures/tex_"+f.fileNum);if(t==null)return null;tex[f.fileNum]=t;}int uy=t.height-f.sy-f.height;if(uy<0||f.sx<0||f.sx+f.width>t.width)return null;Sprite s=Sprite.Create(t,new Rect(f.sx,uy,f.width,f.height),new Vector2(.5f,0f),32f,0,SpriteMeshType.FullRect);s.name=k;spr[k]=s;return s;}
    static void Ensure(){if(data!=null)return;TextAsset a=Resources.Load<TextAsset>(Root+"summons");if(a==null)return;data=JsonUtility.FromJson<Database>(a.text);if(data!=null&&data.summons!=null)foreach(var d in data.summons)if(d!=null)byId[d.npcIndex]=d;}
}
