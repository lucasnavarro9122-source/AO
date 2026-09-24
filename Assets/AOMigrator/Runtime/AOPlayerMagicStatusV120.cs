using UnityEngine;

[DisallowMultipleComponent]
public class AOPlayerMagicStatusV120 : MonoBehaviour
{
    AOTestPlayer movement; AOPlayerCombatV09 combat; AOCharacterRenderer visual;
    float paralyzedUntil,immobilizedUntil,invisibleUntil,poisonUntil,nextPoisonTick,incinerateUntil,nextIncinerateTick,blindUntil,dumbUntil,curseUntil;
    int poisonLevel; bool invisibleVisualApplied;
    public bool IsParalyzed=>Time.time<paralyzedUntil; public bool IsImmobilized=>Time.time<immobilizedUntil; public bool IsPoisoned=>Time.time<poisonUntil;
    public bool IsInvisible=>Time.time<invisibleUntil; public bool IsBlind=>Time.time<blindUntil; public bool IsDumb=>Time.time<dumbUntil; public bool IsCursed=>Time.time<curseUntil;
    public bool IsIncinerated=>Time.time<incinerateUntil; public bool CanCast=>!IsParalyzed;

    void Awake(){movement=GetComponent<AOTestPlayer>();combat=GetComponent<AOPlayerCombatV09>();visual=GetComponentInChildren<AOCharacterRenderer>(true);}
    void Update(){
        if (AOOnlineClientV240.InputBlocked) return;
        if(movement==null)movement=GetComponent<AOTestPlayer>();if(combat==null)combat=GetComponent<AOPlayerCombatV09>();
        bool locked=IsParalyzed||IsImmobilized;
        if(movement!=null){if(locked&&movement.enabled)movement.enabled=false;else if(!locked&&!movement.enabled&&(combat==null||!combat.IsDead))movement.enabled=true;}
        if(IsPoisoned&&Time.time>=nextPoisonTick&&combat!=null&&!combat.IsDead){nextPoisonTick=Time.time+3.64f;int pct=UnityEngine.Random.Range(3,6);combat.ReceiveMagicDamage(1+pct*combat.MaxHP/100,"Veneno");}
        if(IsIncinerated&&Time.time>=nextIncinerateTick&&combat!=null&&!combat.IsDead){nextIncinerateTick=Time.time+.75f;combat.ReceiveMagicDamage(UnityEngine.Random.Range(20,31),"Incineración");}
        if(IsInvisible&&!invisibleVisualApplied)ApplyInvisibleVisual(true);else if(!IsInvisible&&invisibleVisualApplied)ApplyInvisibleVisual(false);
    }
    public void ApplyParalysis(float sec){paralyzedUntil=Mathf.Max(paralyzedUntil,Time.time+Mathf.Max(.5f,sec));}
    public void ApplyImmobilize(float sec){immobilizedUntil=Mathf.Max(immobilizedUntil,Time.time+Mathf.Max(.5f,sec));}
    public void RemoveParalysis(){paralyzedUntil=immobilizedUntil=0f;}
    public void ApplyPoison(int lvl,float sec){poisonLevel=Mathf.Max(poisonLevel,lvl);poisonUntil=Mathf.Max(poisonUntil,Time.time+Mathf.Max(6f,sec));nextPoisonTick=Time.time+3.64f;}
    public void CurePoison(){poisonUntil=nextPoisonTick=0f;poisonLevel=0;}
    public void ApplyInvisibility(float sec){invisibleUntil=Mathf.Max(invisibleUntil,Time.time+Mathf.Max(1f,sec));ApplyInvisibleVisual(true);}
    public void RemoveInvisibility(){invisibleUntil=0f;ApplyInvisibleVisual(false);}
    public void ApplyIncinerate(float sec){incinerateUntil=Mathf.Max(incinerateUntil,Time.time+Mathf.Max(3f,sec));nextIncinerateTick=Time.time+.05f;}
    public void ApplyBlind(float sec){blindUntil=Mathf.Max(blindUntil,Time.time+Mathf.Max(1f,sec));}
    public void ApplyDumb(float sec){dumbUntil=Mathf.Max(dumbUntil,Time.time+Mathf.Max(1f,sec));}
    public void RemoveDumb(){dumbUntil=0f;}
    public void ApplyCurse(float sec){curseUntil=Mathf.Max(curseUntil,Time.time+Mathf.Max(1f,sec));}
    public void RemoveCurse(){curseUntil=0f;}
    public void RemoveDebuffs(){paralyzedUntil=immobilizedUntil=poisonUntil=incinerateUntil=blindUntil=dumbUntil=curseUntil=0f;nextPoisonTick=nextIncinerateTick=0f;AOMagicEffectRuntimeV129 e=GetComponent<AOMagicEffectRuntimeV129>();if(e!=null)e.RemoveDebuffs();}
    public string StatusSummary {
        get {
            string s=""; if(IsParalyzed)s+="PAR "; if(IsImmobilized)s+="INM "; if(IsPoisoned)s+="VEN "; if(IsIncinerated)s+="FUEGO "; if(IsInvisible)s+="INVI "; if(IsBlind)s+="CIEGO "; if(IsDumb)s+="ESTUP "; if(IsCursed)s+="MALD ";
            return s.Trim();
        }
    }
    void ApplyInvisibleVisual(bool inv){if(visual==null)visual=GetComponentInChildren<AOCharacterRenderer>(true);if(visual==null)return;foreach(var sr in visual.GetComponentsInChildren<SpriteRenderer>(true)){if(sr==null)continue;Color c=sr.color;c.a=inv?.35f:1f;sr.color=c;}invisibleVisualApplied=inv;}
    void OnDisable(){if(invisibleVisualApplied)ApplyInvisibleVisual(false);}
}
