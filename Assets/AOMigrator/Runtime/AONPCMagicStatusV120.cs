using UnityEngine;
[DisallowMultipleComponent]
public class AONPCMagicStatusV120 : MonoBehaviour
{
    AONPCCombatV09 combat;AONPCMovementV08 movement;float paralyzedUntil,immobilizedUntil,poisonUntil,nextPoisonTick,incinerateUntil,nextFireTick;AOPlayerCombatV09 caster;
    public bool IsParalyzed=>Time.time<paralyzedUntil; public bool IsImmobilized=>Time.time<immobilizedUntil; public bool CanAttack=>!IsParalyzed;
    void Awake(){combat=GetComponent<AONPCCombatV09>();movement=GetComponent<AONPCMovementV08>();}
    void Update(){
        if (AOOnlineClientV240.Requested) return;
        if(combat==null)combat=GetComponent<AONPCCombatV09>();if(movement==null)movement=GetComponent<AONPCMovementV08>();
        bool locked=IsParalyzed||IsImmobilized;if(movement!=null){if(locked&&movement.enabled)movement.enabled=false;else if(!locked&&!movement.enabled&&combat!=null&&combat.IsAlive)movement.enabled=true;}
        if(Time.time<poisonUntil&&Time.time>=nextPoisonTick&&combat!=null&&combat.IsAlive){nextPoisonTick=Time.time+3.64f;int pct=UnityEngine.Random.Range(3,6);combat.TakeMagicDamage(1+pct*combat.MaxHP/100,caster);}
        if(Time.time<incinerateUntil&&Time.time>=nextFireTick&&combat!=null&&combat.IsAlive){nextFireTick=Time.time+.75f;combat.TakeMagicDamage(UnityEngine.Random.Range(20,31),caster);}
    }
    public void ApplyParalysis(float s){paralyzedUntil=Mathf.Max(paralyzedUntil,Time.time+Mathf.Max(.5f,s));}
    public void ApplyImmobilize(float s){immobilizedUntil=Mathf.Max(immobilizedUntil,Time.time+Mathf.Max(.5f,s));}
    public void RemoveParalysis(){paralyzedUntil=immobilizedUntil=0f;}
    public void ApplyPoison(int level,float s,AOPlayerCombatV09 c){caster=c;poisonUntil=Mathf.Max(poisonUntil,Time.time+Mathf.Max(6f,s));nextPoisonTick=Time.time+3.64f;}
    public void CurePoison(){poisonUntil=nextPoisonTick=0f;caster=null;}
    public void ApplyIncinerate(float s,AOPlayerCombatV09 c){caster=c;incinerateUntil=Mathf.Max(incinerateUntil,Time.time+Mathf.Max(3f,s));nextFireTick=Time.time+.05f;}
    public void RemoveDebuffs(){paralyzedUntil=immobilizedUntil=poisonUntil=incinerateUntil=0f;AOMagicEffectRuntimeV129 e=GetComponent<AOMagicEffectRuntimeV129>();if(e!=null)e.RemoveDebuffs();}
}
