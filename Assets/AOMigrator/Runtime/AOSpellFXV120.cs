using System.Collections;
using UnityEngine;

public class AOSpellFXV120 : MonoBehaviour
{
    static AOSpellFXV120 instance; AudioSource source;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatic(){instance=null;}
    static AOSpellFXV120 Ensure(){if(instance!=null)return instance;instance=Object.FindFirstObjectByType<AOSpellFXV120>();if(instance!=null)return instance;GameObject g=new GameObject("AO Spell FX v0.13.2");instance=g.AddComponent<AOSpellFXV120>();return instance;}
    void Awake(){if(instance!=null&&instance!=this){Destroy(gameObject);return;}instance=this;source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0f;}
    public static void Play(AOSpellDatabaseV120.SpellDef spell,Vector3 world){Play(spell,world,world);}
    // Hechizo del jugador (o de quien no tenga animación de casteo): no busca NPC en la escena.
    public static void Play(AOSpellDatabaseV120.SpellDef spell,Vector3 from,Vector3 to){if(spell==null)return;AOSpellFXV120 fx=Ensure();fx.PlaySound(spell.wav);fx.StartCoroutine(fx.Sequence(spell,from,to));}
    // Hechizo de un NPC: anima su casteo (V268) y después el FX. Evita recorrer todos los NPC por hechizo.
    public static void PlayFromNpc(GameObject npc,AOSpellDatabaseV120.SpellDef spell,Vector3 from,Vector3 to){if(spell==null)return;if(npc!=null)AOCastAnimationRuntimeV268.PlayNpc(npc,spell);Play(spell,from,to);}
    public static void PlayCastSound(AOSpellDatabaseV120.SpellDef spell){if(spell==null)return;Ensure().PlaySound(spell.wav);}
    public static void PlayImpact(AOSpellDatabaseV120.SpellDef spell,Vector3 world,bool withSound=false){if(spell==null)return;AOSpellFXV120 fx=Ensure();if(withSound)fx.PlaySound(spell.wav);fx.StartCoroutine(fx.ImpactOnly(spell,world));}

    IEnumerator Sequence(AOSpellDatabaseV120.SpellDef spell,Vector3 from,Vector3 to)
    {
        var tune=AOSpellVisualOverridesV130.Tuning(spell.id);
        Sprite icon=AOSpellDatabaseV120.Icon(spell.id);Sprite[] dbFrames=AOSpellDatabaseV120.FXFrames(spell.id);
        Sprite[] travel=AOSpellVisualOverridesV130.ProjectileFrames(spell.id);Sprite[] impact=ResolveImpact(spell,dbFrames,icon);
        bool customTravel=travel!=null&&travel.Length>0;bool shouldTravel=(spell.particleTravel>0||customTravel)&&Vector3.Distance(from,to)>.2f;
        if(shouldTravel)
        {
            Sprite fallback=customTravel?travel[0]:(impact!=null&&impact.Length>0?impact[0]:icon);
            if(fallback!=null)
            {
                GameObject p=new GameObject("Spell Projectile "+spell.name);SpriteRenderer sr=p.AddComponent<SpriteRenderer>();sr.sortingOrder=16010;p.transform.localScale=Vector3.one*Mathf.Max(.1f,tune.projectileScale);
                float distance=Vector3.Distance(from,to),speed=Mathf.Max(1f,tune.projectileSpeed),dur=Mathf.Clamp(distance/speed,.12f,1.2f),t=0f;
                while(t<dur){t+=Time.deltaTime;float n=Mathf.Clamp01(t/dur);p.transform.position=Vector3.Lerp(from+Vector3.up*tune.projectileYOffset,to+Vector3.up*tune.projectileYOffset,n);if(customTravel){int f=Mathf.Clamp(Mathf.FloorToInt(n*Mathf.Max(1,travel.Length)),0,travel.Length-1);sr.sprite=travel[f];}else sr.sprite=fallback;yield return null;}Destroy(p);
            }
        }
        yield return ImpactRoutine(spell,to,impact,tune);
    }

    IEnumerator ImpactOnly(AOSpellDatabaseV120.SpellDef spell,Vector3 world)
    {
        var tune=AOSpellVisualOverridesV130.Tuning(spell.id);
        Sprite icon=AOSpellDatabaseV120.Icon(spell.id);Sprite[] dbFrames=AOSpellDatabaseV120.FXFrames(spell.id);
        Sprite[] impact=ResolveImpact(spell,dbFrames,icon);
        yield return ImpactRoutine(spell,world,impact,tune);
    }

    Sprite[] ResolveImpact(AOSpellDatabaseV120.SpellDef spell,Sprite[] dbFrames,Sprite icon)
    {
        Sprite[] impact=AOSpellVisualOverridesV130.ImpactFrames(spell.id);
        if((impact==null||impact.Length==0)&&dbFrames!=null&&dbFrames.Length>0)impact=dbFrames;
        if((impact==null||impact.Length==0)&&icon!=null)impact=new Sprite[]{icon};
        return impact;
    }

    IEnumerator ImpactRoutine(AOSpellDatabaseV120.SpellDef spell,Vector3 to,Sprite[] impact,AOSpellVisualOverridesV130.VisualTuning tune)
    {
        if(impact==null||impact.Length==0)yield break;
        GameObject g=new GameObject("Spell FX "+spell.id+" "+spell.name);g.transform.position=to+Vector3.up*tune.impactYOffset;g.transform.localScale=Vector3.one*Mathf.Max(.1f,tune.impactScale);SpriteRenderer r=g.AddComponent<SpriteRenderer>();r.sortingOrder=16000;
        float d=tune.impactDuration>0f?tune.impactDuration:(impact.Length<=1?.35f:Mathf.Clamp(impact.Length/14f,.28f,1.2f)),e=0f;
        while(e<d){e+=Time.deltaTime;int f=Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(e/d)*impact.Length),0,impact.Length-1);r.sprite=impact[f];yield return null;}Destroy(g);
    }

    void PlaySound(int wav){if(wav<=0||source==null)return;AudioClip c=Resources.Load<AudioClip>("AOMigrator/MagicV129/Audio/wav_"+wav);if(c!=null)source.PlayOneShot(c,.78f);}
}
