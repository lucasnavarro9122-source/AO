using System.Collections;
using UnityEngine;

public class AOSpellFXV120 : MonoBehaviour
{
    static AOSpellFXV120 instance; AudioSource source;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatic(){instance=null;}
    static AOSpellFXV120 Ensure(){if(instance!=null)return instance;instance=Object.FindFirstObjectByType<AOSpellFXV120>();if(instance!=null)return instance;GameObject g=new GameObject("AO Spell FX v0.12.9");instance=g.AddComponent<AOSpellFXV120>();return instance;}
    void Awake(){if(instance!=null&&instance!=this){Destroy(gameObject);return;}instance=this;source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0f;}
    public static void Play(AOSpellDatabaseV120.SpellDef spell,Vector3 world){Play(spell,world,world);}
    public static void Play(AOSpellDatabaseV120.SpellDef spell,Vector3 from,Vector3 to){if(spell==null)return;AOSpellFXV120 fx=Ensure();fx.PlaySound(spell.wav);fx.StartCoroutine(fx.Sequence(spell,from,to));}
    IEnumerator Sequence(AOSpellDatabaseV120.SpellDef spell,Vector3 from,Vector3 to){
        Sprite[] frames=AOSpellDatabaseV120.FXFrames(spell.id);Sprite icon=AOSpellDatabaseV120.Icon(spell.id);
        if(spell.particleTravel>0 && Vector3.Distance(from,to)>.2f){
            GameObject p=new GameObject("Spell Projectile "+spell.name);SpriteRenderer sr=p.AddComponent<SpriteRenderer>();sr.sortingOrder=16000;sr.sprite=icon!=null?icon:(frames.Length>0?frames[0]:null);
            float dur=Mathf.Clamp(Vector3.Distance(from,to)*.055f,.18f,.65f),t=0f;while(t<dur){t+=Time.deltaTime;p.transform.position=Vector3.Lerp(from+Vector3.up*.35f,to+Vector3.up*.35f,Mathf.Clamp01(t/dur));yield return null;}Destroy(p);
        }
        if(frames.Length==0 && icon!=null)frames=new Sprite[]{icon};if(frames.Length==0)yield break;
        GameObject g=new GameObject("Spell FX "+spell.id+" "+spell.name);g.transform.position=to+Vector3.up*.25f;SpriteRenderer r=g.AddComponent<SpriteRenderer>();r.sortingOrder=16000;
        float d=frames.Length<=1?.35f:Mathf.Clamp(frames.Length/14f,.28f,1.2f),e=0f;while(e<d){e+=Time.deltaTime;int f=Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(e/d)*frames.Length),0,frames.Length-1);r.sprite=frames[f];yield return null;}Destroy(g);
    }
    void PlaySound(int wav){if(wav<=0||source==null)return;AudioClip c=Resources.Load<AudioClip>("AOMigrator/MagicV129/Audio/wav_"+wav);if(c!=null)source.PlayOneShot(c,.78f);}
}
