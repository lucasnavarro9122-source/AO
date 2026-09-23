using System;
using System.Collections.Generic;
using UnityEngine;

public static class AOMagicEffectDatabaseV129
{
    const string Path="AOMigrator/MagicV129/effects";
    [Serializable] public class EffectDef {
        public int id,type,subType,sharedTypeId,limit,ticks,tickTimeMs,tickManaConsumption,tickStaminaConsumption,tickFX,onHitFX,onHitWav,buffType,area,applyEffectId,secondaryEffectId,npcId;
        public float tickPowerMin,tickPowerMax,physicalDamageReduction,magicDamageReduction,physicalDamageDone,magicDamageDone,speedModifier,selfHealingBonus,magicHealingBonus;
        public int hitModifier,evasionModifier,physicalLinearBonus,defenseBonus; public bool overrideExisting;
        public float DurationSeconds {
            get {
                if(ticks>0&&tickTimeMs>0)return ticks*tickTimeMs/1000f;
                if(tickTimeMs>0)return tickTimeMs/1000f;
                return 1f;
            }
        }
        public bool SupportedLocal => type==1||type==2||type==8||type==9||type==10||type==15||type==16||type==17;
    }
    [Serializable] class Database { public string version; public EffectDef[] effects; }
    static Database data; static readonly Dictionary<int,EffectDef> byId=new Dictionary<int,EffectDef>();
    public static EffectDef Get(int id){Ensure();return byId.TryGetValue(id,out EffectDef e)?e:null;}
    static void Ensure(){
        if(data!=null)return;TextAsset a=Resources.Load<TextAsset>(Path);if(a==null){Debug.LogError("[AO v0.12.9] Falta effects.json");return;}
        data=JsonUtility.FromJson<Database>(a.text);byId.Clear();if(data!=null&&data.effects!=null)foreach(var e in data.effects)if(e!=null&&e.id>0)byId[e.id]=e;
    }
}
