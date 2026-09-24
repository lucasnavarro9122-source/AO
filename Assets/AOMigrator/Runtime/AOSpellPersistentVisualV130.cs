using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class AOSpellPersistentVisualV130 : MonoBehaviour
{
    class LoopVisual
    {
        public string key; public GameObject go; public SpriteRenderer renderer; public Sprite[] frames; public float expiresAt; public float fps; public float startedAt;
    }
    readonly Dictionary<string,LoopVisual> loops=new Dictionary<string,LoopVisual>();

    public static void ApplyEffect(GameObject target,int effectId,float duration)
    {
        if(target==null||effectId<=0)return;var c=Ensure(target);c.ApplyEffectLocal(effectId,Mathf.Max(.1f,duration));
    }
    public static void StopEffect(GameObject target,int effectId){if(target==null)return;var c=target.GetComponent<AOSpellPersistentVisualV130>();if(c!=null)c.StopPrefix("eot:"+effectId+":");}
    public static void PlayTick(GameObject target,int effectId){if(target==null||effectId<=0)return;var c=Ensure(target);c.PlayTransient(AOSpellVisualOverridesV130.EffectTickFrames(effectId),effectId,16025);}
    public static void PlayOnHit(GameObject target,int effectId){if(target==null||effectId<=0)return;var c=Ensure(target);c.PlayTransient(AOSpellVisualOverridesV130.EffectOnHitFrames(effectId),effectId,16030);}
    public static void ApplySpellBuff(GameObject target,int spellId,float duration)
    {
        if(target==null||spellId<=0||duration<=0f)return;Sprite[] frames=AOSpellVisualOverridesV130.BuffFrames(spellId);if(frames==null||frames.Length==0)return;var c=Ensure(target);var tune=AOSpellVisualOverridesV130.Tuning(spellId);c.AddLoop("spell:"+spellId+":buff",frames,duration,tune.persistentFps,tune.persistentScale,tune.persistentYOffset,16005);
    }
    public static void StopSpellBuff(GameObject target,int spellId){if(target==null)return;var c=target.GetComponent<AOSpellPersistentVisualV130>();if(c!=null)c.StopPrefix("spell:"+spellId+":");}
    public static void ClearAll(GameObject target){if(target==null)return;var c=target.GetComponent<AOSpellPersistentVisualV130>();if(c!=null)c.ClearLocal();}

    static AOSpellPersistentVisualV130 Ensure(GameObject target){var c=target.GetComponent<AOSpellPersistentVisualV130>();if(c==null)c=target.AddComponent<AOSpellPersistentVisualV130>();return c;}

    void ApplyEffectLocal(int effectId,float duration)
    {
        int spellId=AOSpellVisualOverridesV130.SourceSpellForEffect(effectId);var tune=AOSpellVisualOverridesV130.Tuning(spellId);
        Sprite[] client=AOSpellVisualOverridesV130.EffectClientFrames(effectId);if(client!=null&&client.Length>0)AddLoop("eot:"+effectId+":client",client,duration,tune.persistentFps,tune.persistentScale,tune.persistentYOffset,16006);
        Sprite[] aura=AOSpellVisualOverridesV130.EffectAuraFrames(effectId);if(aura!=null&&aura.Length>0)AddLoop("eot:"+effectId+":aura",aura,duration,tune.persistentFps,tune.persistentScale,tune.persistentYOffset-.06f,15998);
    }

    void AddLoop(string key,Sprite[] frames,float duration,float fps,float scale,float y,int sorting)
    {
        if(frames==null||frames.Length==0)return;
        if(loops.TryGetValue(key,out LoopVisual old)&&old!=null&&old.go!=null){old.frames=frames;old.expiresAt=Time.time+duration;old.fps=Mathf.Max(1f,fps);old.go.transform.localScale=Vector3.one*Mathf.Max(.1f,scale);old.go.transform.localPosition=new Vector3(0f,y,0f);return;}
        GameObject g=new GameObject("AO Persistent "+key);g.transform.SetParent(transform,false);g.transform.localPosition=new Vector3(0f,y,0f);g.transform.localScale=Vector3.one*Mathf.Max(.1f,scale);SpriteRenderer r=g.AddComponent<SpriteRenderer>();r.sortingOrder=sorting;r.sprite=frames[0];
        loops[key]=new LoopVisual{key=key,go=g,renderer=r,frames=frames,expiresAt=Time.time+duration,fps=Mathf.Max(1f,fps),startedAt=Time.time};
    }

    void Update()
    {
        if(loops.Count==0)return;float now=Time.time;List<string> remove=null;
        foreach(var kv in loops){LoopVisual v=kv.Value;if(v==null||v.go==null||now>=v.expiresAt){if(remove==null)remove=new List<string>();remove.Add(kv.Key);continue;}if(v.frames!=null&&v.frames.Length>0&&v.renderer!=null){int f=Mathf.FloorToInt((now-v.startedAt)*v.fps)%v.frames.Length;v.renderer.sprite=v.frames[Mathf.Clamp(f,0,v.frames.Length-1)];}}
        if(remove!=null)foreach(string key in remove)Remove(key);
    }

    void PlayTransient(Sprite[] frames,int effectId,int sorting)
    {
        if(frames==null||frames.Length==0)return;int spellId=AOSpellVisualOverridesV130.SourceSpellForEffect(effectId);var tune=AOSpellVisualOverridesV130.Tuning(spellId);StartCoroutine(Transient(frames,tune.transientFps,tune.persistentScale,tune.persistentYOffset,sorting));
    }

    IEnumerator Transient(Sprite[] frames,float fps,float scale,float y,int sorting)
    {
        GameObject g=new GameObject("AO EOT transient");g.transform.SetParent(transform,false);g.transform.localPosition=new Vector3(0f,y,0f);g.transform.localScale=Vector3.one*Mathf.Max(.1f,scale);SpriteRenderer r=g.AddComponent<SpriteRenderer>();r.sortingOrder=sorting;
        float step=1f/Mathf.Max(1f,fps);for(int i=0;i<frames.Length;i++){r.sprite=frames[i];yield return new WaitForSeconds(step);}Destroy(g);
    }

    void StopPrefix(string prefix){List<string> keys=new List<string>();foreach(string key in loops.Keys)if(key.StartsWith(prefix))keys.Add(key);foreach(string key in keys)Remove(key);}
    void Remove(string key){if(!loops.TryGetValue(key,out LoopVisual v))return;if(v!=null&&v.go!=null)Destroy(v.go);loops.Remove(key);}
    void ClearLocal(){foreach(var kv in loops)if(kv.Value!=null&&kv.Value.go!=null)Destroy(kv.Value.go);loops.Clear();StopAllCoroutines();}
    void OnDestroy(){ClearLocal();}
}
