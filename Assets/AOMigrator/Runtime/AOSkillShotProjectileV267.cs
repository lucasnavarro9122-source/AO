using UnityEngine;

[DisallowMultipleComponent]
public class AOSkillShotProjectileV267 : MonoBehaviour
{
    AOPlayerMagicV120 owner;
    AOSpellDatabaseV120.SpellDef spell;
    AOGridMap grid;
    Vector2 direction;
    Vector2 logicalPosition;
    Vector2 previousLogicalPosition;
    float travelled;
    float speed;
    float maxRange;
    float hitRadius;
    float yOffset;
    Sprite[] frames;
    SpriteRenderer spriteRenderer;
    float animationTime;
    float fps;
    bool finished;
    float launchedAt;

    public static AOSkillShotProjectileV267 Launch(
        AOPlayerMagicV120 owner,
        AOSpellDatabaseV120.SpellDef spell,
        AOGridMap grid,
        Vector3 origin,
        Vector2 direction)
    {
        if(owner==null||spell==null||grid==null||direction.sqrMagnitude<.0001f) return null;
        GameObject go=new GameObject("SkillShot "+spell.id+" "+spell.name);
        AOSkillShotProjectileV267 p=go.AddComponent<AOSkillShotProjectileV267>();
        p.Configure(owner,spell,grid,origin,direction.normalized);
        return p;
    }


    void Configure(AOPlayerMagicV120 newOwner,AOSpellDatabaseV120.SpellDef newSpell,AOGridMap newGrid,Vector3 origin,Vector2 newDirection)
    {
        owner=newOwner;spell=newSpell;grid=newGrid;direction=newDirection;
        launchedAt=Time.time;
        var cfg=AOSkillShotConfigV267.Get(spell.id);
        var tune=AOSpellVisualOverridesV130.Tuning(spell.id);
        speed=Mathf.Max(1f,cfg.speed);
        maxRange=Mathf.Max(1f,cfg.range);
        hitRadius=Mathf.Clamp(cfg.hitRadius,.15f,1.25f);
        yOffset=tune.projectileYOffset;
        fps=Mathf.Max(1f,tune.transientFps);
        frames=AOSpellVisualOverridesV130.ProjectileFrames(spell.id);
        if(frames==null||frames.Length==0)
        {
            Sprite fallback=AOSpellDatabaseV120.Icon(spell.id);
            if(fallback!=null) frames=new[]{fallback};
        }
        spriteRenderer=gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder=16010;
        if(frames!=null&&frames.Length>0) spriteRenderer.sprite=frames[0];
        float visualScale=Mathf.Max(.1f,cfg.visualScale)*Mathf.Max(.1f,tune.projectileScale);
        transform.localScale=Vector3.one*visualScale;
        logicalPosition=new Vector2(origin.x,origin.y);
        previousLogicalPosition=logicalPosition;
        transform.position=new Vector3(logicalPosition.x,logicalPosition.y+yOffset,origin.z);
    }

    void Update()
    {
        if(finished||owner==null||spell==null||grid==null){Destroy(gameObject);return;}
        float step=speed*Time.deltaTime;
        if(step<=0f)return;
        previousLogicalPosition=logicalPosition;
        logicalPosition+=direction*step;
        travelled+=step;
        Animate();

        Vector2 hitPoint;
        if(CheckMapCollision(previousLogicalPosition,logicalPosition,out hitPoint))
        {
            FinishWorld(hitPoint);
            return;
        }

        // Duel: rivals along the segment (tile check; the server validates the line with SegmentClear).
        if(AODuelUI.InDuel&&TryHitDuelRival(previousLogicalPosition,logicalPosition,out int rival,out int rx,out int ry,out hitPoint))
        {
            FinishDuelRival(rival,rx,ry,hitPoint);
            return;
        }

        AONPCCombatV09 npc=FindNpcHit(previousLogicalPosition,logicalPosition,out hitPoint);
        if(npc!=null)
        {
            FinishNpc(npc,hitPoint);
            return;
        }

        transform.position=new Vector3(logicalPosition.x,logicalPosition.y+yOffset,transform.position.z);
        if(travelled>=maxRange) FinishRange();
    }

    void Animate()
    {
        if(frames==null||frames.Length<=1||spriteRenderer==null)return;
        animationTime+=Time.deltaTime;
        int index=Mathf.FloorToInt(animationTime*fps)%frames.Length;
        if(index<0)index=0;
        spriteRenderer.sprite=frames[index];
    }

    bool CheckMapCollision(Vector2 from,Vector2 to,out Vector2 hit)
    {
        hit=to;
        float distance=Vector2.Distance(from,to);
        int samples=Mathf.Max(1,Mathf.CeilToInt(distance/.12f));
        int lastX=Mathf.RoundToInt(from.x+.5f);
        int lastY=Mathf.RoundToInt(-from.y);
        for(int i=1;i<=samples;i++)
        {
            float t=i/(float)samples;
            Vector2 p=Vector2.Lerp(from,to,t);
            int x=Mathf.RoundToInt(p.x+.5f);
            int y=Mathf.RoundToInt(-p.y);
            if(x==lastX&&y==lastY)continue;
            // NPC tiles are not walls here: FindNpcHit resolves them by hitRadius.
            if(grid.ProjectileBlockedBetween(lastX,lastY,x,y,false))
            {
                hit=p;
                return true;
            }
            lastX=x;lastY=y;
        }
        return false;
    }

    AONPCCombatV09 FindNpcHit(Vector2 from,Vector2 to,out Vector2 hit)
    {
        hit=to;
        AONPCCombatV09 best=null;
        float bestT=float.MaxValue;
        foreach(AONPCCombatV09 npc in NpcsThisFrame())
        {
            // Non-attackable NPCs (bankers, merchants) must not absorb the shot; only NPCs of this projectile's map.
            if(npc==null||!npc.IsAlive||!npc.Attackable)continue;
            AONPCMovementV08 movement=npc.GetComponent<AONPCMovementV08>();
            if(movement!=null&&movement.Grid!=null&&movement.Grid!=grid)continue;
            Vector2 center=new Vector2(npc.transform.position.x,npc.transform.position.y);
            float t=ClosestSegmentT(from,to,center);
            Vector2 closest=Vector2.Lerp(from,to,t);
            if((closest-center).sqrMagnitude>hitRadius*hitRadius)continue;
            if(t<bestT){bestT=t;best=npc;hit=closest;}
        }
        return best;
    }

    static AONPCCombatV09[] cachedNpcs;
    static int cachedNpcsFrame=-1;
    static AONPCCombatV09[] NpcsThisFrame()
    {
        if(cachedNpcs==null||cachedNpcsFrame!=Time.frameCount)
        {
            cachedNpcs=Object.FindObjectsByType<AONPCCombatV09>(FindObjectsSortMode.None);
            cachedNpcsFrame=Time.frameCount;
        }
        return cachedNpcs;
    }

    static float ClosestSegmentT(Vector2 a,Vector2 b,Vector2 p)
    {
        Vector2 ab=b-a;
        float d=ab.sqrMagnitude;
        if(d<=.000001f)return 0f;
        return Mathf.Clamp01(Vector2.Dot(p-a,ab)/d);
    }

    void FinishNpc(AONPCCombatV09 npc,Vector2 hit)
    {
        if(finished)return;finished=true;
        Vector3 world=new Vector3(hit.x,hit.y,transform.position.z);
        // Online: the server counts the cooldown from launch, not from impact.
        AOOnlineClientV240.SetSkillShotFlight(Time.time-launchedAt);
        owner.ResolveSkillShotHit(spell,npc,world);
        Destroy(gameObject);
    }

    static bool TryHitDuelRival(Vector2 from,Vector2 to,out int rival,out int tx,out int ty,out Vector2 hit)
    {
        rival=0;tx=ty=0;hit=to;
        int samples=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(from,to)/.12f));
        for(int i=1;i<=samples;i++)
        {
            Vector2 p=Vector2.Lerp(from,to,i/(float)samples);
            int x=Mathf.RoundToInt(p.x+.5f),y=Mathf.RoundToInt(-p.y);
            int id=AOOnlineClientV240.PlayerAt(x,y);
            if(id>0&&AODuelClient.IsEnemy(id)){rival=id;tx=x;ty=y;hit=p;return true;}
        }
        return false;
    }

    void FinishDuelRival(int rival,int tx,int ty,Vector2 hit)
    {
        if(finished)return;finished=true;
        AOOnlineClientV240.SetSkillShotFlight(Time.time-launchedAt);
        AOOnlineClientV240.CastPlayer(spell.id,rival,tx,ty);
        AOSpellFXV120.PlayImpact(spell,new Vector3(hit.x,hit.y,transform.position.z),false);
        Destroy(gameObject);
    }

    void FinishWorld(Vector2 hit)
    {
        if(finished)return;finished=true;
        Vector3 world=new Vector3(hit.x,hit.y,transform.position.z);
        AOSpellFXV120.PlayImpact(spell,world,false);
        Destroy(gameObject);
    }

    void FinishRange()
    {
        if(finished)return;finished=true;
        Destroy(gameObject);
    }
}
