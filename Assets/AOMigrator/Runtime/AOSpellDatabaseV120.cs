using System;
using System.Collections.Generic;
using UnityEngine;

public static class AOSpellDatabaseV120
{
    const string Root="AOMigrator/MagicV129/";
    [Serializable] public class FrameSpec { public int fileNum,sx,sy,width,height; public string key; }
    [Serializable] public class SpellDef {
        public int id; public string name,description,magicWords,casterMessage,targetMessage,selfMessage;
        public int type,skillType,target,targetEffectType,autoCast,minSkill,manaRequired,staminaRequired,requiredHp,cooldown,maxLevelCastable;
        public float staminaPercentRequired,speed;
        public int needStaff,staffAffected,magicPowerNeeded,requiredObject,requiredObject2,requireWeaponType,requirementMask,eotId,antiRm;
        public int wav,fxGrh,loops,iconIndex,particle,particleTravel,particleTime,duration,areaAffects,areaRadius;
        public int raiseHp,minHp,maxHp,raiseMana,minMana,maxMana,raiseStamina,minStamina,maxStamina,raiseHunger,minHunger,maxHunger,raiseThirst,minThirst,maxThirst;
        public int raiseAgility,minAgility,maxAgility,raiseStrength,minStrength,maxStrength,raiseCharisma,minCharisma,maxCharisma;
        public int poison,curePoison,invisibility,removeInvisibility,paralyze,immobilize,removeParalysis,incinerate,blindness,dumb,removeDumb,curse,removeCurse,resurrect,mimic,removeDebuff,stealBuff;
        public int summonMode,summonNpc,summonCount,materializeObject,materializeCount,teleport,teleportMap,teleportX,teleportY;
        public bool supportedLocal; public FrameSpec icon; public FrameSpec[] fxFrames;
        public string TargetLabel { get { switch(target){case 1:return "Usuario";case 2:return "NPC";case 3:return "Usuario / NPC";case 4:return "Terreno";case 5:return "Mascotas";default:return "Especial";} } }
        public bool IsPhysicalSkill => type==9 || type==10;
        public bool RequiresShield => (requirementMask & 2)!=0;
        public bool RequiresLand => (requirementMask & 512)!=0;
        public bool WorksOnDead => (requirementMask & 2048)!=0;
    }
    [Serializable] class Database { public string version; public int declaredSlots,definedSpells; public SpellDef[] spells; }
    [Serializable] public class Config {
        public string version; public int maxUserSpells,castIntervalMs,meditationStartupMs,meditationTickMs;
        public float manaRecoveryBasePercent,manaRecoveryBase,manaRecoverySkillMultiplier;
    }
    static Database data; static Config config;
    static readonly Dictionary<int,SpellDef> byId=new Dictionary<int,SpellDef>();
    static readonly List<SpellDef> ordered=new List<SpellDef>();
    static readonly Dictionary<int,Texture2D> textures=new Dictionary<int,Texture2D>();
    static readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
    public static int Count { get { Ensure(); return ordered.Count; } }
    public static int DeclaredSlots { get { Ensure(); return data==null?0:data.declaredSlots; } }
    public static int MaxUserSpells { get { Ensure(); return config==null?40:config.maxUserSpells; } }
    public static Config Settings { get { Ensure(); return config; } }
    public static SpellDef Get(int id){Ensure();return byId.TryGetValue(id,out SpellDef s)?s:null;}
    public static SpellDef GetAt(int i){Ensure();return i>=0&&i<ordered.Count?ordered[i]:null;}
    public static Sprite Icon(int id){var s=Get(id);return s==null||s.icon==null?null:SpriteFor(s.icon);}
    public static Sprite[] FXFrames(int id){var s=Get(id);if(s==null||s.fxFrames==null)return new Sprite[0];var a=new Sprite[s.fxFrames.Length];for(int i=0;i<a.Length;i++)a[i]=SpriteFor(s.fxFrames[i]);return a;}
    static Sprite SpriteFor(FrameSpec f){
        if(f==null)return null; string key=string.IsNullOrEmpty(f.key)?"f"+f.fileNum+"_"+f.sx+"_"+f.sy+"_"+f.width+"_"+f.height:f.key;
        if(sprites.TryGetValue(key,out Sprite c))return c; Texture2D t=Texture(f.fileNum);if(t==null)return null;
        int uy=t.height-f.sy-f.height;if(f.sx<0||uy<0||f.width<=0||f.height<=0||f.sx+f.width>t.width||uy+f.height>t.height)return null;
        Sprite s=Sprite.Create(t,new Rect(f.sx,uy,f.width,f.height),new Vector2(.5f,0f),32f,0,SpriteMeshType.FullRect);s.name=key;sprites[key]=s;return s;
    }
    static Texture2D Texture(int n){if(textures.TryGetValue(n,out Texture2D c))return c;var t=Resources.Load<Texture2D>(Root+"Textures/tex_"+n);if(t!=null)textures[n]=t;return t;}
    static void Ensure(){
        if(data!=null)return;
        TextAsset a=Resources.Load<TextAsset>(Root+"spells"); if(a==null){Debug.LogError("[AO v0.12.9] Falta spells.json");return;}
        data=JsonUtility.FromJson<Database>(a.text); TextAsset ca=Resources.Load<TextAsset>(Root+"magic_config"); if(ca!=null)config=JsonUtility.FromJson<Config>(ca.text);
        byId.Clear();ordered.Clear();if(data!=null&&data.spells!=null)foreach(var s in data.spells){if(s==null||s.id<=0)continue;byId[s.id]=s;ordered.Add(s);}
        ordered.Sort((x,y)=>x.id.CompareTo(y.id));Debug.Log("[AO v0.12.9] Hechizos: "+ordered.Count+"/"+DeclaredSlots);
    }
}
