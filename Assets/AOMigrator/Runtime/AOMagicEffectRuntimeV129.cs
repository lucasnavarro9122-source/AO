using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class AOMagicEffectRuntimeV129 : MonoBehaviour
{
    [Serializable] class ActiveEffect {
        public int id; public AOMagicEffectDatabaseV129.EffectDef def;
        public float expiresAt,nextTickAt; public int ticksRemaining;
        public AOPlayerCombatV09 caster;
    }

    readonly List<ActiveEffect> active=new List<ActiveEffect>();
    AOPlayerRPGV11 rpg; AOPlayerCombatV09 playerCombat; AONPCCombatV09 npcCombat;
    float protection;
    float manualSpeedMultiplier=1f;
    float manualSpeedUntil;

    public float PhysicalDamageReduction { get; private set; }
    public float MagicDamageReduction { get; private set; }
    public float PhysicalDamageBonus { get; private set; }
    public float MagicDamageBonus { get; private set; }
    public float SpeedMultiplier { get; private set; }=1f;
    public float SelfHealingBonus { get; private set; }
    public float MagicHealingBonus { get; private set; }
    public int HitBonus { get; private set; }
    public int EvasionBonus { get; private set; }
    public int PhysicalLinearBonus { get; private set; }
    public int DefenseBonus { get; private set; }
    public float Protection => protection;
    public int ActiveCount => active.Count;

    void Awake(){FindRefs();Recalculate();}
    void FindRefs(){if(rpg==null)rpg=GetComponent<AOPlayerRPGV11>();if(playerCombat==null)playerCombat=GetComponent<AOPlayerCombatV09>();if(npcCombat==null)npcCombat=GetComponent<AONPCCombatV09>();}

    void Update(){
        if (AOOnlineClientV240.InputBlocked) return;
        FindRefs(); float now=Time.time; bool changed=false;
        for(int i=active.Count-1;i>=0;i--){
            ActiveEffect a=active[i]; if(a==null||a.def==null){active.RemoveAt(i);changed=true;continue;}
            if(NeedsTicks(a.def)&&now>=a.nextTickAt&&a.ticksRemaining!=0){
                Tick(a); if(a.ticksRemaining>0)a.ticksRemaining--; a.nextTickAt=now+Mathf.Max(.04f,a.def.tickTimeMs/1000f);
            }
            if(now>=a.expiresAt || (a.ticksRemaining==0&&NeedsTicks(a.def))){
                active.RemoveAt(i);changed=true;
            }
        }
        if(manualSpeedUntil>0f&&now>=manualSpeedUntil){manualSpeedUntil=0f;manualSpeedMultiplier=1f;changed=true;}
        if(changed){if(!HasProtectionEffect())protection=0f;Recalculate();}
    }

    static bool NeedsTicks(AOMagicEffectDatabaseV129.EffectDef d){return d.type==1||d.type==9||d.type==10||d.tickManaConsumption>0||d.tickStaminaConsumption>0;}

    public bool ApplyEffect(int id,AOPlayerCombatV09 caster=null){
        var d=AOMagicEffectDatabaseV129.Get(id);if(d==null||!d.SupportedLocal)return false;
        // Single by id/shared type; overwrite when the DAT asks for it.
        for(int i=active.Count-1;i>=0;i--){
            ActiveEffect old=active[i]; if(old==null||old.def==null)continue;
            bool same=old.id==id;
            bool sameShared=d.sharedTypeId>0&&old.def.sharedTypeId==d.sharedTypeId&&(d.limit==5||old.def.limit==5);
            if((same||sameShared)&&d.overrideExisting){active.RemoveAt(i);}
            else if((same||sameShared)&&!d.overrideExisting)return false;
        }
        ActiveEffect a=new ActiveEffect{ id=id,def=d,caster=caster,ticksRemaining=d.ticks>0?d.ticks:(NeedsTicks(d)?1:-1) };
        float duration=Mathf.Max(.1f,d.DurationSeconds);
        a.expiresAt=Time.time+duration;a.nextTickAt=Time.time+Mathf.Max(.04f,d.tickTimeMs/1000f);
        active.Add(a);
        if(d.type==15){protection=Mathf.Max(protection,Mathf.Abs(Roll(d.tickPowerMin,d.tickPowerMax)));}
        if(d.type==16&&d.npcId>0&&rpg!=null){
            AOSummonDatabaseV129.SummonDef form=AOSummonDatabaseV129.Get(d.npcId);
            if(form!=null){
                AOMimicVisualV129 mimic=GetComponent<AOMimicVisualV129>();
                if(mimic==null)mimic=gameObject.AddComponent<AOMimicVisualV129>();
                mimic.ApplyDefinition(form,d.DurationSeconds);
            }
        }
        if(d.type==10){ // local party of one: aura affects its owner too.
            if(d.applyEffectId>0)ApplyEffect(d.applyEffectId,caster);
            if(d.secondaryEffectId>0&&d.secondaryEffectId!=d.applyEffectId)ApplyEffect(d.secondaryEffectId,caster);
        }
        Recalculate();
        Debug.Log("[AO v0.12.9] EOT "+id+" aplicado a "+name+".");
        return true;
    }

    void Tick(ActiveEffect a){
        var d=a.def;
        if(d.type!=10 &&
           rpg!=null &&
           (d.tickManaConsumption>0 ||
            d.tickStaminaConsumption>0))
        {
            if(!rpg.SpendMagicCost(
                    d.tickManaConsumption,
                    d.tickStaminaConsumption))
            {
                a.expiresAt=0f;
                return;
            }
        }

        if(d.type==1){
            int amount=Mathf.RoundToInt(Roll(d.tickPowerMin,d.tickPowerMax));
            if(playerCombat!=null){if(amount>=0)playerCombat.RestoreHealth(ApplyHealing(amount));else playerCombat.ReceiveMagicDamage(ModifyIncomingMagic(-amount),"Efecto mágico");}
            else if(npcCombat!=null){if(amount>=0)npcCombat.HealMagic(ApplyHealing(amount));else npcCombat.TakeMagicDamage(ModifyIncomingMagic(-amount),a.caster);}
        } else if(d.type==9 && rpg!=null){
            int amount=Mathf.RoundToInt(Roll(d.tickPowerMin,d.tickPowerMax));rpg.ModifyMana(amount);
        } else if(d.type==10 && rpg!=null){
            if(d.tickManaConsumption>0||d.tickStaminaConsumption>0){
                if(!rpg.SpendMagicCost(d.tickManaConsumption,d.tickStaminaConsumption)){a.expiresAt=0f;return;}
            }
            if(d.applyEffectId>0)ApplyEffect(d.applyEffectId,a.caster);
            if(d.secondaryEffectId>0&&d.secondaryEffectId!=d.applyEffectId)ApplyEffect(d.secondaryEffectId,a.caster);
        }
    }

    public int ModifyIncomingPhysical(int damage){
        float reduced=damage*(1f-Mathf.Clamp(PhysicalDamageReduction,-1f,.95f))-DefenseBonus;
        return Absorb(Mathf.Max(0,Mathf.RoundToInt(reduced)));
    }
    public int ModifyIncomingMagic(int damage){
        float reduced=damage*(1f-Mathf.Clamp(MagicDamageReduction,-1f,.95f));
        return Absorb(Mathf.Max(0,Mathf.RoundToInt(reduced)));
    }
    public int ModifyOutgoingPhysical(int damage){return Mathf.Max(0,Mathf.RoundToInt(damage*(1f+PhysicalDamageBonus))+PhysicalLinearBonus);}
    public int ModifyOutgoingMagic(int damage){return Mathf.Max(0,Mathf.RoundToInt(damage*(1f+MagicDamageBonus)));}
    public int ApplyHealing(int amount){return Mathf.Max(0,Mathf.RoundToInt(amount*(1f+SelfHealingBonus+MagicHealingBonus)));}
    public int ApplyIncomingHealing(int amount){return Mathf.Max(0,Mathf.RoundToInt(amount*(1f+SelfHealingBonus)));}
    public int ApplyOutgoingMagicHealing(int amount){return Mathf.Max(0,Mathf.RoundToInt(amount*(1f+MagicHealingBonus)));}

    int Absorb(int damage){
        if(damage<=0||protection<=0f)return damage;
        int absorbed=Mathf.Min(damage,Mathf.CeilToInt(protection)); protection=Mathf.Max(0f,protection-absorbed); return damage-absorbed;
    }

    public void NotifyPhysicalHit(AONPCCombatV09 target,AOPlayerCombatV09 caster){
        if(target==null)return;
        AOMagicEffectRuntimeV129 dst=target.GetComponent<AOMagicEffectRuntimeV129>();
        if(dst==null)dst=target.gameObject.AddComponent<AOMagicEffectRuntimeV129>();
        foreach(ActiveEffect a in active.ToArray()){
            if(a!=null&&a.def!=null&&a.def.type==8&&a.def.applyEffectId>0)dst.ApplyEffect(a.def.applyEffectId,caster);
        }
    }

    public void ApplyTemporarySpeed(float multiplier,float seconds){
        manualSpeedMultiplier=Mathf.Clamp(multiplier,.25f,3f);manualSpeedUntil=Time.time+Mathf.Max(.5f,seconds);Recalculate();
    }

    public bool RemoveOneDebuff(){
        for(int i=0;i<active.Count;i++){ActiveEffect a=active[i];if(a!=null&&a.def!=null&&(a.def.buffType==2||a.def.buffType==4)){active.RemoveAt(i);Recalculate();return true;}}
        return false;
    }

    public bool StealOneBuffTo(AOMagicEffectRuntimeV129 receiver,AOPlayerCombatV09 caster){
        if(receiver==null)return false;
        for(int i=0;i<active.Count;i++){ActiveEffect a=active[i];if(a!=null&&a.def!=null&&(a.def.buffType==1||a.def.buffType==5)){int id=a.id;active.RemoveAt(i);Recalculate();return receiver.ApplyEffect(id,caster);}}
        return false;
    }

    public void RemoveDebuffs(){
        for(int i=active.Count-1;i>=0;i--)if(active[i]!=null&&active[i].def!=null&&(active[i].def.buffType==2||active[i].def.buffType==4))active.RemoveAt(i);
        Recalculate();
    }

    public void ClearAll(){active.Clear();protection=0f;Recalculate();}

    bool HasProtectionEffect(){foreach(var a in active)if(a!=null&&a.def!=null&&a.def.type==15)return true;return false;}

    void Recalculate(){
        PhysicalDamageReduction=MagicDamageReduction=PhysicalDamageBonus=MagicDamageBonus=0f;
        SelfHealingBonus=MagicHealingBonus=0f;HitBonus=EvasionBonus=PhysicalLinearBonus=DefenseBonus=0;SpeedMultiplier=1f;
        foreach(ActiveEffect a in active){
            if(a==null||a.def==null)continue;var d=a.def;
            if(d.type==2||d.type==16||d.type==17){
                PhysicalDamageReduction+=d.physicalDamageReduction;MagicDamageReduction+=d.magicDamageReduction;
                PhysicalDamageBonus+=d.physicalDamageDone;MagicDamageBonus+=d.magicDamageDone;SpeedMultiplier+=d.speedModifier;
                HitBonus+=d.hitModifier;EvasionBonus+=d.evasionModifier;PhysicalLinearBonus+=d.physicalLinearBonus;DefenseBonus+=d.defenseBonus;
                SelfHealingBonus+=d.selfHealingBonus;MagicHealingBonus+=d.magicHealingBonus;
            }
        }
        SpeedMultiplier=Mathf.Clamp(SpeedMultiplier*manualSpeedMultiplier,.25f,3f);
    }

    static float Roll(float min,float max){float lo=Mathf.Min(min,max),hi=Mathf.Max(min,max);return UnityEngine.Random.Range(lo,hi);}
}
