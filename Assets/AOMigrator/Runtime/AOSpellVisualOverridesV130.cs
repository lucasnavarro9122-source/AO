using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class AOSpellVisualOverridesV130
{
    [Serializable] class ManifestFile { public int spell_id; public string name; public FieldInfo fields; public ResourceInfo[] resources; }
    [Serializable] class FieldInfo { public string eotid; }
    [Serializable] class ResourceInfo { public int spell_id; public string kind; public int source_id; public int top_grh; public string detail; public FrameInfo[] frames; }
    [Serializable] class FrameInfo { public int frame_number; public int grh; public string central_image; public string spell_copy; }
    [Serializable] class TuningFile { public string version; public VisualTuning[] entries; }

    [Serializable] public class VisualTuning
    {
        public int spellId;
        public float projectileScale=1f,impactScale=1f,persistentScale=1f;
        public float projectileYOffset=.35f,impactYOffset=.25f,persistentYOffset=.25f;
        public float projectileSpeed=18f,impactDuration=.35f,persistentFps=8f,transientFps=14f;
    }

    class SpellVisual
    {
        public Sprite icon;
        public readonly List<Sprite> travel=new List<Sprite>();
        public readonly List<Sprite> impact=new List<Sprite>();
        public readonly List<Sprite> particles=new List<Sprite>();
    }

    class EffectVisual
    {
        public int sourceSpellId;
        public readonly List<Sprite> client=new List<Sprite>();
        public readonly List<Sprite> tick=new List<Sprite>();
        public readonly List<Sprite> onHit=new List<Sprite>();
        public readonly List<Sprite> aura=new List<Sprite>();
    }

    static readonly Dictionary<int,SpellVisual> bySpell=new Dictionary<int,SpellVisual>();
    static readonly Dictionary<int,EffectVisual> byEffect=new Dictionary<int,EffectVisual>();
    static readonly Dictionary<int,VisualTuning> tuning=new Dictionary<int,VisualTuning>();
    static bool loaded;
    static string RootPath=>Path.Combine(Application.streamingAssetsPath,"AOMigrator","SpellOverrides","02_Por_Hechizo");
    static string TuningPath=>Path.Combine(Application.streamingAssetsPath,"AOMigrator","SpellOverrides","spell_visual_tuning.json");

    public static Sprite[] ProjectileFrames(int spellId){Ensure();return bySpell.TryGetValue(spellId,out SpellVisual v)&&v.travel.Count>0?v.travel.ToArray():Array.Empty<Sprite>();}
    public static Sprite[] ImpactFrames(int spellId){Ensure();if(!bySpell.TryGetValue(spellId,out SpellVisual v))return Array.Empty<Sprite>();if(v.impact.Count>0)return v.impact.ToArray();return v.particles.Count>0?v.particles.ToArray():Array.Empty<Sprite>();}
    public static Sprite[] BuffFrames(int spellId){Ensure();return bySpell.TryGetValue(spellId,out SpellVisual v)&&v.particles.Count>0?v.particles.ToArray():Array.Empty<Sprite>();}
    public static bool HasBuffVisual(int spellId){Ensure();return bySpell.TryGetValue(spellId,out SpellVisual v)&&v.particles.Count>0;}
    public static Sprite Icon(int spellId){Ensure();return bySpell.TryGetValue(spellId,out SpellVisual v)?v.icon:null;}

    public static Sprite[] EffectClientFrames(int effectId){Ensure();return byEffect.TryGetValue(effectId,out EffectVisual v)&&v.client.Count>0?v.client.ToArray():Array.Empty<Sprite>();}
    public static Sprite[] EffectTickFrames(int effectId){Ensure();return byEffect.TryGetValue(effectId,out EffectVisual v)&&v.tick.Count>0?v.tick.ToArray():Array.Empty<Sprite>();}
    public static Sprite[] EffectOnHitFrames(int effectId){Ensure();return byEffect.TryGetValue(effectId,out EffectVisual v)&&v.onHit.Count>0?v.onHit.ToArray():Array.Empty<Sprite>();}
    public static Sprite[] EffectAuraFrames(int effectId){Ensure();return byEffect.TryGetValue(effectId,out EffectVisual v)&&v.aura.Count>0?v.aura.ToArray():Array.Empty<Sprite>();}
    public static int SourceSpellForEffect(int effectId){Ensure();return byEffect.TryGetValue(effectId,out EffectVisual v)?v.sourceSpellId:0;}

    public static VisualTuning Tuning(int spellId)
    {
        Ensure();
        if(spellId>0&&tuning.TryGetValue(spellId,out VisualTuning t))return t;
        return new VisualTuning{spellId=spellId};
    }

    static void Ensure()
    {
        if(loaded)return;
        loaded=true;bySpell.Clear();byEffect.Clear();tuning.Clear();
        LoadTuning();
        try
        {
            if(!Directory.Exists(RootPath)){Debug.Log("[AO Spell Visuals v0.13.1] Carpeta no encontrada: "+RootPath);return;}
            int manifests=0;
            foreach(string manifestPath in Directory.GetFiles(RootPath,"manifest.json",SearchOption.AllDirectories))
            {
                ManifestFile manifest=JsonUtility.FromJson<ManifestFile>(File.ReadAllText(manifestPath));
                if(manifest==null||manifest.spell_id<=0)continue;
                manifests++;
                SpellVisual visual=GetSpell(manifest.spell_id);
                int eotId=0;
                if(manifest.fields!=null&&!string.IsNullOrEmpty(manifest.fields.eotid))int.TryParse(manifest.fields.eotid,out eotId);
                EffectVisual effect=eotId>0?GetEffect(eotId,manifest.spell_id):null;
                string spellDir=Path.GetDirectoryName(manifestPath);
                if(manifest.resources==null)continue;
                foreach(ResourceInfo resource in manifest.resources)
                {
                    if(resource==null||resource.frames==null||resource.frames.Length==0)continue;
                    List<FrameInfo> ordered=new List<FrameInfo>(resource.frames);ordered.Sort((a,b)=>a.frame_number.CompareTo(b.frame_number));
                    if(resource.kind=="icon") { if(visual.icon==null)visual.icon=LoadSprite(spellDir,ordered[0]); continue; }
                    if(resource.kind=="particleviaje") { AddUniqueFrames(visual.travel,spellDir,ordered); continue; }
                    if(resource.kind=="spell_fx"||resource.kind=="spell_fx_direct_grh_fallback") { AddUniqueFrames(visual.impact,spellDir,ordered); continue; }
                    if(resource.kind=="particle") { AddUniqueFrames(visual.particles,spellDir,ordered); continue; }
                    if(effect==null)continue;
                    if(resource.kind=="eot_client_effect") { if(effect.client.Count==0)AddUniqueFrames(effect.client,spellDir,ordered); continue; }
                    if(resource.kind=="eot_tickfx") { if(effect.tick.Count==0)AddUniqueFrames(effect.tick,spellDir,ordered); continue; }
                    if(resource.kind=="eot_onhitfx") { if(effect.onHit.Count==0)AddUniqueFrames(effect.onHit,spellDir,ordered); continue; }
                    if(resource.kind=="eot_aura") { if(effect.aura.Count==0)AddUniqueFrames(effect.aura,spellDir,ordered); continue; }
                }
            }
            Debug.Log("[AO Spell Visuals v0.13.1] Hechizos: "+bySpell.Count+" | EOT visuales: "+byEffect.Count+" | manifests: "+manifests);
        }
        catch(Exception ex){Debug.LogError("[AO Spell Visuals v0.13.1] Error cargando overrides: "+ex.Message);}
    }

    static void LoadTuning()
    {
        try
        {
            if(!File.Exists(TuningPath))return;
            TuningFile f=JsonUtility.FromJson<TuningFile>(File.ReadAllText(TuningPath));
            if(f==null||f.entries==null)return;
            foreach(VisualTuning t in f.entries)if(t!=null&&t.spellId>0)tuning[t.spellId]=t;
        }
        catch(Exception ex){Debug.LogWarning("[AO Spell Visuals v0.13.1] No pude leer tuning: "+ex.Message);}
    }

    static SpellVisual GetSpell(int id){if(!bySpell.TryGetValue(id,out SpellVisual v)){v=new SpellVisual();bySpell[id]=v;}return v;}
    static EffectVisual GetEffect(int id,int sourceSpell){if(!byEffect.TryGetValue(id,out EffectVisual v)){v=new EffectVisual{sourceSpellId=sourceSpell};byEffect[id]=v;}else if(v.sourceSpellId<=0)v.sourceSpellId=sourceSpell;return v;}

    static void AddUniqueFrames(List<Sprite> target,string spellDir,List<FrameInfo> frames)
    {
        foreach(FrameInfo frame in frames)
        {
            Sprite s=LoadSprite(spellDir,frame);if(s==null)continue;
            bool exists=false;foreach(Sprite old in target)if(old!=null&&old.name==s.name){exists=true;break;}
            if(!exists)target.Add(s);
        }
    }

    static Sprite LoadSprite(string spellDir,FrameInfo frame)
    {
        try
        {
            string filename=!string.IsNullOrEmpty(frame.spell_copy)?Path.GetFileName(frame.spell_copy):null;
            if(string.IsNullOrEmpty(filename))return null;
            string fullPath=Path.Combine(spellDir,filename);if(!File.Exists(fullPath))return null;
            byte[] data=File.ReadAllBytes(fullPath);Texture2D tex=new Texture2D(2,2,TextureFormat.ARGB32,false);
            if(!tex.LoadImage(data,false))return null;
            tex.name=Path.GetFileNameWithoutExtension(filename);tex.filterMode=FilterMode.Point;tex.wrapMode=TextureWrapMode.Clamp;
            Sprite s=Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),new Vector2(.5f,0f),32f,0,SpriteMeshType.FullRect);s.name=tex.name;return s;
        }
        catch{return null;}
    }
}
