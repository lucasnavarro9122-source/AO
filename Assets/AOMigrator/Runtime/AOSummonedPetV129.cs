using UnityEngine;

[DisallowMultipleComponent]
public class AOSummonedPetV129 : MonoBehaviour
{
    AOTestPlayer owner; AOPlayerCombatV09 ownerCombat; AOGridMap grid; AOCharacterRenderer visual;
    AOSummonDatabaseV129.SummonDef def; int tileX,tileY; float expiresAt,nextThink; bool stored;
    public int NpcIndex=>def==null?0:def.npcIndex; public bool Stored=>stored; public float Remaining=>Mathf.Max(0f,expiresAt-Time.time);
    public int TileX=>tileX; public int TileY=>tileY;

    public void Configure(AOTestPlayer newOwner,AOPlayerCombatV09 combat,AOSummonDatabaseV129.SummonDef summon,int x,int y,float lifetime){
        owner=newOwner;ownerCombat=combat;def=summon;grid=owner==null?null:owner.CurrentGrid;tileX=x;tileY=y;expiresAt=Time.time+Mathf.Max(30f,lifetime);
        transform.position=grid==null?transform.position:grid.TileToWorld(x,y);
        visual=gameObject.AddComponent<AOCharacterRenderer>();visual.Configure(AOSummonDatabaseV129.BuildVisuals(def),def.walkFps<=0?18f:def.walkFps,def.headOffsetX/32f,-def.headOffsetY/32f,def.bodyShiftX/32f);visual.SetHeading(def.heading<=0?AOGridMap.SOUTH:def.heading);visual.UpdateSorting(AORenderOrderV210.Character(y));
        gameObject.AddComponent<AOMagicEffectRuntimeV129>();
    }

    void Update(){
        if(stored)return;if(owner==null||grid==null){Destroy(gameObject);return;}if(Time.time>=expiresAt){Destroy(gameObject);return;}if(Time.time<nextThink)return;
        AOMagicEffectRuntimeV129 localEffects=GetComponent<AOMagicEffectRuntimeV129>();float speed=localEffects==null?1f:localEffects.SpeedMultiplier;nextThink=Time.time+.45f/Mathf.Max(.25f,speed);
        AONPCCombatV09 target=NearestEnemy(7);
        if(target!=null){AONPCMovementV08 mv=target.GetComponent<AONPCMovementV08>();if(mv!=null){int dist=Mathf.Abs(mv.TileX-tileX)+Mathf.Abs(mv.TileY-tileY);if(dist<=1){Attack(target);return;}StepToward(mv.TileX,mv.TileY);return;}}
        int ownerDist=Mathf.Abs(owner.TileX-tileX)+Mathf.Abs(owner.TileY-tileY);if(ownerDist>2)StepToward(owner.TileX,owner.TileY);
    }

    void Attack(AONPCCombatV09 target){
        if(target==null||!target.IsAlive)return;int raw=UnityEngine.Random.Range(Mathf.Max(1,def.minHit),Mathf.Max(def.minHit,def.maxHit)+1);
        AOMagicEffectRuntimeV129 fx=GetComponent<AOMagicEffectRuntimeV129>();if(fx!=null)raw=fx.ModifyOutgoingPhysical(raw);
        target.TakeDamage(raw,ownerCombat);AOCombatFeedbackV113.PlayAttack(visual,visual.Heading,0);
    }

    AONPCCombatV09 NearestEnemy(int range){
        AONPCCombatV09 best=null;int bestD=999;foreach(var n in Object.FindObjectsByType<AONPCCombatV09>(FindObjectsSortMode.None)){if(n==null||!n.IsAlive||!n.Attackable)continue;AONPCMovementV08 m=n.GetComponent<AONPCMovementV08>();if(m==null)continue;int d=Mathf.Abs(m.TileX-tileX)+Mathf.Abs(m.TileY-tileY);if(d<=range&&d<bestD){best=n;bestD=d;}}return best;
    }

    void StepToward(int tx,int ty){
        int dx=tx==tileX?0:(tx>tileX?1:-1),dy=ty==tileY?0:(ty>tileY?1:-1);int nx=tileX,ny=tileY,heading=visual==null?AOGridMap.SOUTH:visual.Heading;
        if(Mathf.Abs(tx-tileX)>=Mathf.Abs(ty-tileY)&&dx!=0){nx+=dx;heading=dx>0?AOGridMap.EAST:AOGridMap.WEST;}else if(dy!=0){ny+=dy;heading=dy>0?AOGridMap.SOUTH:AOGridMap.NORTH;}
        if(grid.InBounds(nx,ny)&&grid.CanEnter(nx,ny,heading)&&!AOInteractionRegistry.IsBlocked(nx,ny)){tileX=nx;tileY=ny;transform.position=grid.TileToWorld(nx,ny);if(visual!=null){visual.SetHeading(heading);visual.PlayCombatBurst(.14f);visual.UpdateSorting(AORenderOrderV210.Character(ny));}}
    }

    public void WarpNearOwner(){
        if(owner==null||grid==null)return;int[,] offsets={{1,0},{-1,0},{0,1},{0,-1},{1,1},{-1,1},{1,-1},{-1,-1}};
        for(int i=0;i<offsets.GetLength(0);i++){int x=owner.TileX+offsets[i,0],y=owner.TileY+offsets[i,1];if(grid.InBounds(x,y)&&grid.CanEnter(x,y,AOGridMap.SOUTH)&&!AOInteractionRegistry.IsBlocked(x,y)){tileX=x;tileY=y;transform.position=grid.TileToWorld(x,y);if(visual!=null)visual.UpdateSorting(AORenderOrderV210.Character(y));return;}}
        tileX=owner.TileX;tileY=owner.TileY;transform.position=grid.TileToWorld(tileX,tileY);
    }

    public void SetStored(bool value){stored=value;gameObject.SetActive(!value);if(!value)WarpNearOwner();}
}
