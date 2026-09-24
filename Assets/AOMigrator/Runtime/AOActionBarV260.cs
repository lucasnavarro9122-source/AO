using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public partial class AOActionBarV260 : MonoBehaviour
{
    AOTestPlayer player;
    AOPlayerCombatV09 combat;
    AOPlayerMagicV120 magic;
    AOInventoryV10 inventory;
    AOWorldManagerV07 world;
    AOGridMap routeMap;
    AOControlProfile lastProfile;
    AOInteractable target;
    Vector2Int destination;
    readonly Queue<Vector2Int> route=new Queue<Vector2Int>();
    bool ordered;
    float nextRepath;
    int routeTargetX,routeTargetY;
    public bool HasOrder=>ordered;
#if UNITY_EDITOR
    public string TestLastCommand { get; private set; } = "No input";
#endif
    static Camera activeCamera;
    public static Camera GameCamera {
        get {
            if(activeCamera==null)activeCamera=Object.FindFirstObjectByType<AOCameraFollow>()?.GetComponent<Camera>();
            return activeCamera!=null?activeCamera:Camera.main;
        }
    }
    string CharacterName=>GetComponent<AOCharacterIdentityV170>()?.CharacterName??"Aventurero";

    void Awake()
    {
        player=GetComponent<AOTestPlayer>();combat=GetComponent<AOPlayerCombatV09>();
        magic=GetComponent<AOPlayerMagicV120>();inventory=GetComponent<AOInventoryV10>();
        world=Object.FindFirstObjectByType<AOWorldManagerV07>();lastProfile=AOPlayerSettingsV230.Profile;
    }
    public void CancelOrder(){ordered=false;target=null;route.Clear();}
    void Update()
    {
        if(player==null||!AOMainMenuV140.SessionActive)return;
        if(combat==null)combat=GetComponent<AOPlayerCombatV09>();
        if(lastProfile!=AOPlayerSettingsV230.Profile){CancelOrder();lastProfile=AOPlayerSettingsV230.Profile;}
        if(routeMap!=player.CurrentGrid){CancelOrder();routeMap=player.CurrentGrid;}
        if(AOInterfaceV0101.InputCaptured||AOCityUIV130.ModalOpen||AOQuestUIV150.ModalOpen||
            AOOnlineClientV240.InputBlocked||combat==null||!player.enabled||world!=null&&world.IsLoading)
        {CancelOrder();return;}
        if(magic==null)magic=GetComponent<AOPlayerMagicV120>();
        if(inventory==null)inventory=GetComponent<AOInventoryV10>();
        if(AOPlayerSettingsV230.Pressed(AOGameAction.Stop)){CancelOrder();magic?.CancelTargeting();return;}
        if(AOPlayerSettingsV230.Pressed(AOGameAction.Meditate)){CancelOrder();magic?.ToggleMeditation();}
        for(int i=0;i<4;i++) {
            if(AOPlayerSettingsV230.Pressed(AOGameAction.Spell1+i))UseSpell(i);
            if(AOPlayerSettingsV230.Pressed(AOGameAction.Consumable1+i))UseConsumable(i);
        }
        if(!AOPlayerSettingsV230.IsMoba)return;
        if(AOPlayerSettingsV230.Pressed(AOGameAction.WorldCommand)&&!(magic!=null&&magic.ConsumedInputThisFrame)) {
            bool valid=CursorTile(player,out int x,out int y);
#if UNITY_EDITOR
            TestLastCommand="Tile="+x+","+y+" valid="+valid;
#endif
            if(valid){magic?.CancelTargeting();Order(x,y);}
        }
        if(ordered&&!player.IsMoving)FollowOrder();
    }
    public void UseSpell(int slot)
    {
        if(!AOPlayerSettingsV230.SpellMacrosEnabled||magic==null)return;
        CancelOrder();
        int id=AOPlayerSettingsV230.SlotAssignment(CharacterName,true,slot);
        if(id<=0){AOInterfaceV0101.PushMessage("Asigná Hechizo "+(slot+1)+" en Ajustes > Controles > Hechizos.");return;}
        magic.CastShortcut(id,AOPlayerSettingsV230.QuickCast);
    }
    public void UseConsumable(int slot)
    {
        int id=AOPlayerSettingsV230.SlotAssignment(CharacterName,false,slot);
        if(id<=0){AOInterfaceV0101.PushMessage("Asigná Consumible "+(slot+1)+" en Ajustes > Controles > Consumibles.");return;}
        inventory?.UseConsumableById(id);
    }
    public static bool CursorTile(AOTestPlayer player,out int x,out int y)
    {
        x=y=0;var camera=GameCamera;if(camera==null||player==null||player.CurrentGrid==null)return false;
#if ENABLE_INPUT_SYSTEM
        if(Mouse.current==null)return false;Vector2 mouse=Mouse.current.position.ReadValue();
#else
        Vector2 mouse=Input.mousePosition;
#endif
        if(!camera.pixelRect.Contains(mouse))return false;
        var controls=player.GetComponent<AOActionBarV260>();
        if(controls!=null&&controls.ShowBar()&&controls.BarRect().Contains(new Vector2(mouse.x,Screen.height-mouse.y)))return false;
        var point=camera.ScreenToWorldPoint(new Vector3(mouse.x,mouse.y,Mathf.Abs(camera.transform.position.z)));
        x=Mathf.RoundToInt(point.x+.5f);y=Mathf.RoundToInt(-point.y);
        // NPC bodies extend above their foot tile. Clicking their sprite targets the NPC.
        float closest=float.MaxValue;AONPCMetadata hit=null;
        foreach(var npc in Object.FindObjectsByType<AONPCMetadata>(FindObjectsSortMode.None)) {
            var c=npc.GetComponent<AONPCCombatV09>();if(c!=null&&!c.IsAlive)continue;
            foreach(var sprite in npc.GetComponentsInChildren<SpriteRenderer>()) {
                if(!sprite.enabled||sprite.sprite==null)continue;var bounds=sprite.bounds;
                if(bounds.Contains(new Vector3(point.x,point.y,bounds.center.z))) {
                    float distance=(npc.transform.position-point).sqrMagnitude;
                    if(distance<closest){closest=distance;hit=npc;}
                }
            }
        }
        if(hit!=null){x=hit.TileX;y=hit.TileY;}
        return player.CurrentGrid.InBounds(x,y);
    }
    public void Order(int x,int y)
    {
        CancelOrder();if(player.CurrentGrid==null||!player.CurrentGrid.InBounds(x,y))return;
        destination=new Vector2Int(x,y);target=AOInteractionRegistry.FindFirst(x,y);
        if(target==null) {var door=AODoorV210.FindForInteraction(x,y);if(door!=null)target=door.GetComponent<AOInteractable>();}
        ordered=true;routeMap=player.CurrentGrid;nextRepath=0;
    }
    void FollowOrder()
    {
        if(target!=null)destination=new Vector2Int(target.TileX,target.TileY);
        int distance=Mathf.Abs(player.TileX-destination.x)+Mathf.Abs(player.TileY-destination.y);
        var enemy=target==null?null:target.GetComponent<AONPCCombatV09>();
        if(enemy!=null&&(!enemy.IsAlive||enemy.Attackable&&combat.IsDead)){CancelOrder();return;}
        if(target!=null&&distance<=1) {
            if(distance==1)player.RestoreHeading(Heading(destination.x-player.TileX,destination.y-player.TileY));
            if(enemy!=null&&enemy.Attackable) {if(distance==1)combat.AttackFromControls();return;}
            if(distance==0&&target is AOLootPickupV09)combat.PickupFromControls();
            else player.InteractFromControls();
            CancelOrder();return;
        }
        if(target==null&&distance==0){CancelOrder();return;}
        if(route.Count==0||routeTargetX!=destination.x||routeTargetY!=destination.y) {
            if(Time.unscaledTime<nextRepath)return;nextRepath=Time.unscaledTime+.25f;
            route.Clear();routeTargetX=destination.x;routeTargetY=destination.y;
            foreach(var step in FindPath(player.CurrentGrid,new Vector2Int(player.TileX,player.TileY),destination,target!=null))route.Enqueue(step);
            if(route.Count==0){AOInterfaceV0101.PushMessage("No hay camino libre. Abrí la puerta o elegí otra casilla.");CancelOrder();return;}
        }
        var next=route.Peek();int dx=next.x-player.TileX,dy=next.y-player.TileY;
        if((dx==0&&dy==0)||Mathf.Abs(dx)>1||Mathf.Abs(dy)>1){route.Clear();return;}
        if(player.StepVectorFromControls(dx,dy))route.Dequeue();else route.Clear();
    }
    static int Heading(int dx,int dy)=>dx>0?AOGridMap.EAST:dx<0?AOGridMap.WEST:dy>0?AOGridMap.SOUTH:AOGridMap.NORTH;

    static readonly Vector2Int[] PathOffsets = new[]
    {
        new Vector2Int(0,-1), new Vector2Int(1,0), new Vector2Int(0,1), new Vector2Int(-1,0),
        new Vector2Int(1,-1), new Vector2Int(1,1), new Vector2Int(-1,1), new Vector2Int(-1,-1)
    };

    static bool DynamicStepBlocked(Vector2Int current,Vector2Int next)
    {
        if(AOInteractionRegistry.IsBlocked(next.x,next.y))return true;
        int dx=next.x-current.x,dy=next.y-current.y;
        if(dx!=0&&dy!=0)
            return AOInteractionRegistry.IsBlocked(current.x+dx,current.y)||
                   AOInteractionRegistry.IsBlocked(current.x,current.y+dy);
        return false;
    }

    static int Octile(Vector2Int a,Vector2Int b)
    {
        int dx=Mathf.Abs(a.x-b.x),dy=Mathf.Abs(a.y-b.y);
        int diagonal=Mathf.Min(dx,dy),straight=Mathf.Max(dx,dy)-diagonal;
        return diagonal*14+straight*10;
    }

    public static List<Vector2Int> FindPath(AOGridMap grid,Vector2Int start,Vector2Int goal,bool adjacent)
    {
        var result=new List<Vector2Int>();
        if(grid==null||!grid.InBounds(goal.x,goal.y))return result;

        var open=new List<Vector2Int>{start};
        var closed=new HashSet<Vector2Int>();
        var from=new Dictionary<Vector2Int,Vector2Int>();
        var gScore=new Dictionary<Vector2Int,int>{{start,0}};
        from[start]=start;
        Vector2Int end=start;bool found=false;int visited=0;

        while(open.Count>0&&visited++<=10000)
        {
            int bestIndex=0,bestScore=int.MaxValue;
            for(int i=0;i<open.Count;i++)
            {
                Vector2Int node=open[i];
                int h=Octile(node,goal);
                if(adjacent)h=Mathf.Max(0,h-10);
                int f=gScore[node]+h;
                if(f<bestScore){bestScore=f;bestIndex=i;}
            }

            Vector2Int current=open[bestIndex];open.RemoveAt(bestIndex);
            if(closed.Contains(current))continue;
            closed.Add(current);

            int manhattan=Mathf.Abs(current.x-goal.x)+Mathf.Abs(current.y-goal.y);
            if((!adjacent&&current==goal)||(adjacent&&manhattan==1))
            {
                end=current;found=true;break;
            }

            foreach(Vector2Int offset in PathOffsets)
            {
                Vector2Int next=current+offset;
                if(closed.Contains(next)||!grid.InBounds(next.x,next.y))continue;
                if(!grid.CanStep(current.x,current.y,next.x,next.y)||DynamicStepBlocked(current,next))continue;

                int cost=(offset.x!=0&&offset.y!=0)?14:10;
                int tentative=gScore[current]+cost;
                if(!gScore.TryGetValue(next,out int old)||tentative<old)
                {
                    gScore[next]=tentative;from[next]=current;
                    if(!open.Contains(next))open.Add(next);
                }
            }
        }

        if(!found)return result;
        while(end!=start){result.Add(end);end=from[end];}
        result.Reverse();return result;
    }
}
