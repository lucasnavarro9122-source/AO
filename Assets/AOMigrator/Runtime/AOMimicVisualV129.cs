using System.Collections.Generic;
using UnityEngine;

public class AOMimicVisualV129 : MonoBehaviour
{
    class State { public SpriteRenderer renderer; public bool enabled; public Color color; }
    readonly List<State> original=new List<State>(); readonly List<SpriteRenderer> clones=new List<SpriteRenderer>();
    AOCharacterRenderer ownVisual,sourceVisual,morphRenderer; GameObject overlay; float until;

    public void Apply(AOCharacterRenderer source,float seconds){
        Clear();ownVisual=GetComponentInChildren<AOCharacterRenderer>(true);sourceVisual=source;if(ownVisual==null||sourceVisual==null)return;
        foreach(SpriteRenderer sr in ownVisual.GetComponentsInChildren<SpriteRenderer>(true)){original.Add(new State{renderer=sr,enabled=sr.enabled,color=sr.color});sr.enabled=false;}
        overlay=new GameObject("Mimetismo v0.12.9");overlay.transform.SetParent(transform,false);
        CloneNow();until=Time.time+Mathf.Max(5f,seconds<=0?30f:seconds);
    }

    public void ApplyDefinition(
        AOSummonDatabaseV129.SummonDef def,
        float seconds)
    {
        if(def==null)return;
        Clear();
        ownVisual=GetComponentInChildren<AOCharacterRenderer>(true);
        if(ownVisual==null)return;

        foreach(SpriteRenderer sr in ownVisual.GetComponentsInChildren<SpriteRenderer>(true))
        {
            original.Add(new State{renderer=sr,enabled=sr.enabled,color=sr.color});
            sr.enabled=false;
        }

        overlay=new GameObject("Forma mágica v0.12.9");
        overlay.transform.SetParent(transform,false);
        morphRenderer=overlay.AddComponent<AOCharacterRenderer>();
        morphRenderer.Configure(
            AOSummonDatabaseV129.BuildVisuals(def),
            def.walkFps<=0?18f:def.walkFps,
            def.headOffsetX/32f,
            -def.headOffsetY/32f,
            def.bodyShiftX/32f);
        morphRenderer.SetHeading(ownVisual.Heading);
        morphRenderer.SetWalking(ownVisual.Walking);
        until=Time.time+Mathf.Max(1f,seconds);
    }

    void Update(){
        if(overlay==null)return;
        if(Time.time>=until){Clear();return;}
        if(morphRenderer!=null){
            if(ownVisual!=null){
                morphRenderer.SetHeading(ownVisual.Heading);
                morphRenderer.SetWalking(ownVisual.Walking);
                morphRenderer.UpdateSorting(15000);
            }
            return;
        }
        if(sourceVisual==null){Clear();return;}
        CloneNow();
    }

    void CloneNow(){
        if(overlay==null||sourceVisual==null)return;SpriteRenderer[] src=sourceVisual.GetComponentsInChildren<SpriteRenderer>(true);
        while(clones.Count<src.Length){GameObject g=new GameObject("MimicPart");g.transform.SetParent(overlay.transform,false);clones.Add(g.AddComponent<SpriteRenderer>());}
        for(int i=0;i<clones.Count;i++){SpriteRenderer dst=clones[i];if(i>=src.Length){dst.enabled=false;continue;}SpriteRenderer s=src[i];dst.enabled=s.enabled;dst.sprite=s.sprite;dst.color=s.color;dst.flipX=s.flipX;dst.flipY=s.flipY;dst.sortingLayerID=s.sortingLayerID;dst.sortingOrder=s.sortingOrder;dst.transform.localPosition=s.transform.localPosition;dst.transform.localRotation=s.transform.localRotation;dst.transform.localScale=s.transform.localScale;}
    }

    public void Clear(){
        foreach(State s in original)if(s!=null&&s.renderer!=null){s.renderer.enabled=s.enabled;s.renderer.color=s.color;}original.Clear();
        if(overlay!=null)Destroy(overlay);overlay=null;clones.Clear();sourceVisual=null;morphRenderer=null;
    }
    void OnDisable(){Clear();}
}
