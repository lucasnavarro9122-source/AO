#if UNITY_EDITOR && ENABLE_INPUT_SYSTEM
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Explicit marker only (Temp/run_modules_qa). Covers V261 drag & drop, V267 skill shots,
// V268 cast animation, V269 meditation and V130 persistent effects with temporary
// preferences, an isolated online client and protected saves (see Tools/test_modules_unity.py).
public static class AOModulesQA270
{
    static string Root=>Path.GetFullPath(Path.Combine(Application.dataPath,".."));
    static string Flag=>Path.Combine(Root,"Temp","run_modules_qa");
    static string StopFlag=>Path.Combine(Root,"Temp","stop_modules_qa");
    const string RunningKey="AOModulesQA270.Running";
    static string Data=>Path.Combine(Application.streamingAssetsPath,"AOMigrator");
    const string TestCharacter="ModulosPrueba";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly Vector2Int[] Directions={Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down};

    static int stage;
    static double started,next;
    static readonly List<string> failures=new List<string>();
    static readonly List<string> notes=new List<string>();
    static readonly List<string> errors=new List<string>();
    static readonly List<GameObject> dummies=new List<GameObject>();
    static AOTestPlayer player;
    static AOActionBarV260 controls;
    static AOPlayerMagicV120 magic;
    static AOPlayerRPGV11 rpg;
    static AOInterfaceV0101 ui;
    static AOActionBarDragDropV261 drag;
    static MethodInfo dragUpdate;
    static EditorWindow testView;
    static bool previousMaximized;
    static Mouse testMouse,previousMouse;
    static InputSettings previousInputSettings,testInputSettings;

    static MeditationFile meditation;
    static AOCharacterRenderer npcVisual;
    static AOCharacterRenderer.DirectionVisual[] castDirs;
    static AOMagicEffectRuntimeV129 eotRuntime;
    static GameObject eotShort;
    static int eotId,buffSpellId,manaBeforeMeditation,skillShotId,npcHpBefore;
    static float flightTime;
    static AOSkillShotProjectileV267 shot;
    static AONPCCombatV09 targetNpc;
    static string wallInfo,npcInfo;

    [Serializable] class CastFile {public CastNpc[] npcs;public CastBody[] bodies;}
    [Serializable] class CastNpc {public int npcIndex,castBody;}
    [Serializable] class CastBody {public int bodyId;public CastDirection[] directions;}
    [Serializable] class CastDirection {public int heading;public string[] frames;}
    [Serializable] class MeditationFile {public int soundId;public MeditationEntry[] entries;}
    [Serializable] class MeditationEntry {public int fx,minLevel,maxLevel;public string[] frames;}
    [Serializable] class SkillShotFile {public SkillShotEntry[] entries;}
    [Serializable] class SkillShotEntry {public int spellId;public float speed,range,hitRadius;}
    [Serializable] class TuningFile {public TuningEntry[] entries;}
    [Serializable] class TuningEntry {public int spellId;public float persistentScale=1f,persistentFps=8f,projectileScale=1f;}
    [Serializable] class Result {public bool passed;public int stage;public string message;public string[] failures,notes,errors;}

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Protect()
    {
        if(!File.Exists(Flag))return;
        AOPlayerSettingsV230.TestPrefixOverride="AO.ModulesQA."+Guid.NewGuid().ToString("N")+".";
        AOOnlineClientV240.BeginIsolatedTest(0);
    }
    [InitializeOnLoadMethod]
    static void Initialize()
    {
        // A script reload during Play resets the static save protection and drops Tick:
        // protect again at once and stop Play instead of leaving the test character running.
        if(EditorApplication.isPlaying&&(SessionState.GetBool(RunningKey,false)||File.Exists(StopFlag)))AbortAfterReload();
        EditorApplication.update+=()=>{
            if(File.Exists(StopFlag)){if(EditorApplication.isPlaying)AbortAfterReload();else if(!EditorApplication.isPlayingOrWillChangePlaymode)File.Delete(StopFlag);}
            if(File.Exists(Flag)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling)EditorApplication.isPlaying=true;
        };
        EditorApplication.playModeStateChanged+=mode=> {
            if(mode==PlayModeStateChange.EnteredEditMode)SessionState.EraseBool(RunningKey);
            if(mode!=PlayModeStateChange.EnteredPlayMode||!File.Exists(Flag))return;
            SessionState.SetBool(RunningKey,true);
            EditorApplication.isPaused=false;
            testView=EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));
            previousMaximized=testView.maximized;testView.maximized=true;
            stage=0;failures.Clear();notes.Clear();errors.Clear();dummies.Clear();
            player=null;npcVisual=null;castDirs=null;eotRuntime=null;eotShort=null;shot=null;targetNpc=null;meditation=null;
            eotId=buffSpellId=skillShotId=0;wallInfo=npcInfo="";
            started=EditorApplication.timeSinceStartup;next=started+4;
            EditorApplication.update+=Tick;Application.logMessageReceived+=Log;
        };
    }
    static void AbortAfterReload()
    {
        AOPlayerSettingsV230.TestPrefixOverride="AO.ModulesQA."+Guid.NewGuid().ToString("N")+".";
        AOOnlineClientV240.BeginIsolatedTest(0);
        SessionState.EraseBool(RunningKey);
        if(File.Exists(StopFlag))File.Delete(StopFlag);
        if(File.Exists(Flag)) {
            string message="QA: recarga de scripts durante la prueba; se protegió el guardado y se detuvo Play";
            File.WriteAllText(Path.Combine(Root,"MigrationReports","modules_v270.json"),JsonUtility.ToJson(new Result{passed=false,stage=-1,message=message,
                failures=new[]{message},notes=new string[0],errors=new string[0]},true));
            File.Delete(Flag);
        }
        Debug.LogWarning("AO Modules QA: recarga de scripts en Play; guardado protegido y Play detenido.");
        EditorApplication.delayCall+=()=>{RestoreInputAfterReload();EditorApplication.isPlaying=false;};
    }
    // The reload loses the saved references: drop the synthetic mouse and point the Input System
    // back to the project settings asset (a destroyed test instance breaks it even in Edit mode).
    static void RestoreInputAfterReload()
    {
        try {
            foreach(var device in InputSystem.devices.Where(d=>d.name.StartsWith("AOModulesQAMouse")||d.name.StartsWith("AO Modules QA Mouse")).ToArray())InputSystem.RemoveDevice(device);
            if(EditorBuildSettings.TryGetConfigObject("com.unity.input.settings",out InputSettings asset)&&asset!=null)InputSystem.settings=asset;
            else InputSystem.settings=ScriptableObject.CreateInstance<InputSettings>();
        } catch(Exception error){Debug.LogWarning("AO Modules QA: no pude restaurar el Input System: "+error.Message);}
    }
    static void Log(string message,string trace,LogType type)
    {if((type==LogType.Error||type==LogType.Exception)&&errors.Count<10)errors.Add(message+" | "+trace.Split('\n')[0]);}
    static void Expect(bool ok,string module,string message){if(!ok)failures.Add(module+": "+message);}
    static void Note(string module,string message)=>notes.Add(module+": "+message);
    static void Run(string module,Action body)
    {try{body();}catch(Exception error){failures.Add(module+": excepción "+error.GetType().Name+": "+error.Message+" | "+error.StackTrace?.Split('\n')[0]);}}

    static void Tick()
    {
        double now=EditorApplication.timeSinceStartup;if(now<next)return;
        if(!EditorApplication.isPlaying){failures.Add("QA: Play detenido");Finish();return;}
        if(now-started>120){failures.Add("QA: tiempo agotado");Finish();return;}
        try {
            var world=UnityEngine.Object.FindFirstObjectByType<AOWorldManagerV07>();if(world==null||world.IsLoading)return;
            switch(stage) {
                case 0:
                    if(!AOOnlineClientV240.ProtectLocalSave){failures.Add("QA: guardado original desprotegido, no se prueba");Finish();return;}
                    if(!PrepareCharacter())return;
                    stage=1;next=now+3;return;
                case 1:
                    if(!AOOnlineClientV240.ProtectLocalSave){failures.Add("QA: guardado original desprotegido");Finish();return;}
                    controls=player.GetComponent<AOActionBarV260>();magic=player.GetComponent<AOPlayerMagicV120>();rpg=player.GetComponent<AOPlayerRPGV11>();
                    ui=UnityEngine.Object.FindFirstObjectByType<AOInterfaceV0101>();drag=player.GetComponent<AOActionBarDragDropV261>();
                    if(controls==null||magic==null||rpg==null||ui==null){failures.Add("QA: faltan componentes del jugador o la interfaz");Finish();return;}
                    string identity=player.GetComponent<AOCharacterIdentityV170>()?.CharacterName;
                    Expect(identity==TestCharacter,"QA","El personaje de prueba no se aplicó (nombre "+identity+")");
                    AOPlayerSettingsV230.Profile=AOControlProfile.MOBA;
                    SetupMouse();
                    Run("V267",TestSkillShotConfig);
                    Run("V261",TestDragDrop);
                    Run("V268",StartCastChecks);
                    Run("V269",TestMeditationData);
                    Run("V130",StartEffectChecks);
                    stage=2;next=now+.3;return;
                case 2:
                    Run("V130",()=>{if(eotShort!=null)Expect(HasPersistent(eotShort,"eot:"+eotId+":"),"V130","El visual del EOT desapareció antes de su duración");});
                    Run("V268",()=>{if(npcVisual!=null)Expect(npcVisual.CastingAnimating,"V268","La animación de casteo del NPC se cortó antes de tiempo");});
                    Run("V269",StartMeditation);
                    stage=3;next=now+1.6;return;
                case 3:
                    Run("V130",CheckEffectExpiry);
                    Run("V268",()=>{if(npcVisual!=null)Expect(!npcVisual.CastingAnimating,"V268","La animación de casteo del NPC no terminó sola");});
                    Run("V269",()=>{
                        Expect(magic.IsMeditating,"V269","La meditación se cortó sola");
                        Expect(rpg.Mana>manaBeforeMeditation,"V269","Meditar no recuperó maná ("+manaBeforeMeditation+"→"+rpg.Mana+")");
                    });
                    ScreenCapture.CaptureScreenshot(Path.Combine(Root,"MigrationReports","modules_v270.png"));
                    stage=4;next=now+.5;return;
                case 4:
                    Run("V267",CastSkillShotFromBar);
                    stage=5;next=now+(shot!=null?flightTime+.6f:.1f);return;
                case 5:
                    Run("V267",()=>{if(skillShotId>0)Expect(shot==null,"V267","El proyectil sigue vivo después de su alcance ("+flightTime.ToString("0.00")+" s)");});
                    Run("V268",()=>{var visual=player.GetComponentInChildren<AOCharacterRenderer>(true);if(skillShotId>0)Expect(visual!=null&&!visual.CastingAnimating,"V268","La animación de lanzamiento del jugador no terminó");});
                    Run("V130",()=>{if(eotRuntime!=null)Expect(!HasPersistent(eotRuntime.gameObject,"eot:"),"V130","ClearAll dejó visuales persistentes");});
                    float wait=.1f;Run("V267",()=>wait=LaunchAtWall());
                    stage=6;next=now+wait;return;
                case 6:
                    Run("V267",()=>{if(!string.IsNullOrEmpty(wallInfo))Expect(shot==null,"V267","El proyectil atravesó la pared ("+wallInfo+")");});
                    float npcWait=.1f;Run("V267",()=>npcWait=LaunchAtNpc());
                    stage=7;next=now+npcWait;return;
                default:
                    Run("V267",CheckNpcHit);
                    Finish();return;
            }
        } catch(Exception error){failures.Add("QA: "+error);Finish();}
    }

    static bool PrepareCharacter()
    {
        var save=UnityEngine.Object.FindFirstObjectByType<AOSaveGameV140>();player=UnityEngine.Object.FindFirstObjectByType<AOTestPlayer>();
        if(save==null||player==null)return false;
        var data=JsonUtility.FromJson<AOSaveGameV140.SaveData>(save.CaptureOnline());
        data.character.name=TestCharacter;data.world.map=1;data.world.x=57;data.world.y=44;
        data.rpg.raceId=1;data.rpg.genderId=1;data.rpg.headIndex=1;data.rpg.classId=1;data.rpg.level=25;
        data.rpg.maxHp=100;data.rpg.mana=10000;data.rpg.stamina=1000;data.rpg.skills=Enumerable.Repeat(100,24).ToArray();data.rpg.hunger=50;
        data.combat.hp=100;data.combat.dead=false;
        data.inventory.itemIndices=new int[24];data.inventory.amounts=new int[24];data.inventory.itemIndices[0]=1;data.inventory.amounts[0]=5;
        data.inventory.weapon=data.inventory.armor=data.inventory.helmet=data.inventory.shield=0;
        if(!save.ApplyOnline(JsonUtility.ToJson(data)))throw new Exception("No se pudo preparar personaje de prueba");
        AOMainMenuV140.StartSessionFromCreator();return true;
    }

    // ---- Synthetic mouse: processed manually so neither the real mouse nor other frames see it.
    static void SetupMouse()
    {
        previousInputSettings=InputSystem.settings;
        testInputSettings=UnityEngine.Object.Instantiate(previousInputSettings);
        testInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        testInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        testInputSettings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;
        InputSystem.settings=testInputSettings;
        previousMouse=Mouse.current;testMouse=InputSystem.AddDevice<Mouse>("AOModulesQAMouse");testMouse.MakeCurrent();
    }
    static void MouseAt(Vector2 screen,bool left)
    {
        var state=new MouseState{position=screen};if(left)state=state.WithButton(MouseButton.Left);
        InputSystem.QueueStateEvent(testMouse,state);InputSystem.Update();testMouse.MakeCurrent();
    }
    static Vector2 ToScreen(Vector2 gui)=>new Vector2(gui.x,Screen.height-gui.y);
    static void DragGui(Vector2 from,Vector2 to)
    {
        MouseAt(ToScreen(from),true);dragUpdate.Invoke(drag,null);
        MouseAt(ToScreen(to),true);dragUpdate.Invoke(drag,null);
        MouseAt(ToScreen(to),false);dragUpdate.Invoke(drag,null);
        InputSystem.Update(); // Clear wasReleasedThisFrame before a regular frame reads it.
    }

    static List<string> Chat=>typeof(AOInterfaceV0101).GetField("chat",Private)?.GetValue(ui) as List<string>;
    static void ClearChat()=>Chat?.Clear();
    static bool ChatHas(string text)=>Chat!=null&&Chat.Any(m=>m!=null&&m.Contains(text));
    static string LastChat(){var chat=Chat;return chat==null||chat.Count==0?"":chat[chat.Count-1];}
    static void Assign(bool spell,int slot,int id)=>AOPlayerSettingsV230.AssignSlot(TestCharacter,spell,slot,id);
    static int Slot(bool spell,int slot)=>AOPlayerSettingsV230.SlotAssignment(TestCharacter,spell,slot);
    static Vector2 SlotCenter(int index)=>controls.GetSlotRectGUI(index).center;
    static GameObject Dummy(string name,Vector2 offset)
    {var g=new GameObject(name);g.transform.position=player.transform.position+(Vector3)offset;dummies.Add(g);return g;}
    static bool HasPersistent(GameObject target,string key)=>target!=null&&target.GetComponentsInChildren<SpriteRenderer>(true)
        .Any(r=>r.gameObject.name.StartsWith("AO Persistent "+key)&&r.sprite!=null);
    static T Load<T>(params string[] path){string full=Path.Combine(new[]{Data}.Concat(path).ToArray());return File.Exists(full)?JsonUtility.FromJson<T>(File.ReadAllText(full)):default;}

    // ---- V267: config, launch from the action bar, range, walls and NPC impact.
    static void TestSkillShotConfig()
    {
        int[] ids=Enumerable.Range(1,1000).Where(AOSkillShotConfigV267.IsSkillShot).ToArray();
        Expect(ids.Length>0,"V267","No hay hechizos marcados como skill shot");
        int[] orphan=ids.Where(id=>AOSpellDatabaseV120.Get(id)==null).ToArray();
        Expect(orphan.Length==0,"V267","Skill shots sin hechizo en la base: "+string.Join(",",orphan));
        var file=Load<SkillShotFile>("SpellOverrides","skillshot_tuning.json");
        Expect(file?.entries!=null&&file.entries.Length>0,"V267","skillshot_tuning.json falta o está vacío");
        if(file?.entries!=null) foreach(var entry in file.entries) {
            var config=AOSkillShotConfigV267.Get(entry.spellId);
            Expect(AOSkillShotConfigV267.IsSkillShot(entry.spellId),"V267","El tuning incluye "+entry.spellId+" pero no es skill shot");
            Expect(entry.speed>0&&entry.range>0&&entry.hitRadius>0,"V267","Tuning inválido para "+entry.spellId);
            Expect(Mathf.Approximately(config.speed,entry.speed)&&Mathf.Approximately(config.range,entry.range),"V267","No se aplica el tuning de "+entry.spellId);
        }
        skillShotId=PickSkillShot();
        Expect(skillShotId>0,"V267","Ningún skill shot lanzable por el personaje de prueba");
        if(skillShotId>0){Expect(magic.LearnSpell(skillShotId),"V267","No aprende el skill shot "+skillShotId);Note("V267","Prueba con "+skillShotId+" "+AOSpellDatabaseV120.Get(skillShotId).name);}
    }
    static int PickSkillShot()
    {
        AOSpellDatabaseV120.SpellDef best=null;
        for(int i=0;i<AOSpellDatabaseV120.Count;i++) {
            var s=AOSpellDatabaseV120.GetAt(i);
            if(s==null||!s.supportedLocal||!AOSkillShotConfigV267.IsSkillShot(s.id)||(s.target!=2&&s.target!=3))continue;
            if(s.needStaff>0||s.requiredObject>0||s.requiredObject2>0||s.requireWeaponType>0||s.requirementMask!=0||s.requiredHp>0)continue;
            if(s.maxLevelCastable>0&&s.maxLevelCastable<rpg.Level||s.manaRequired>rpg.Mana/2||s.staminaRequired>rpg.Stamina)continue;
            if(best==null||s.raiseHp==2&&best.raiseHp!=2)best=s;
        }
        return best==null?0:best.id;
    }
    static void CastSkillShotFromBar()
    {
        if(skillShotId<=0)return;
        var config=AOSkillShotConfigV267.Get(skillShotId);flightTime=Mathf.Max(1f,config.range)/Mathf.Max(1f,config.speed);
        var grid=player.CurrentGrid;var start=new Vector2Int(player.TileX,player.TileY);var aim=start;
        foreach(var direction in Directions) {
            bool clear=true;for(int k=1;k<=3&&clear;k++)clear=!grid.ProjectileBlockedBetween(start.x+direction.x*(k-1),start.y+direction.y*(k-1),start.x+direction.x*k,start.y+direction.y*k);
            if(clear){aim=start+direction*3;break;}
        }
        Expect(aim!=start,"V267","No hay dirección libre para apuntar");if(aim==start)return;
        Assign(true,0,skillShotId);
        var screen=AOActionBarV260.GameCamera.WorldToScreenPoint(grid.TileToWorld(aim.x,aim.y));
        MouseAt(new Vector2(screen.x,screen.y),false);
        bool wasMeditating=magic.IsMeditating;int mana=rpg.Mana;ClearChat();
        controls.UseSpell(0);
        shot=UnityEngine.Object.FindObjectsByType<AOSkillShotProjectileV267>(FindObjectsSortMode.None).FirstOrDefault();
        Expect(shot!=null,"V267","La barra no lanzó el proyectil ("+LastChat()+")");
        Expect(rpg.Mana<mana,"V267","El skill shot no consumió maná");
        Expect(magic.CooldownRemaining(skillShotId)>0,"V267","El skill shot no quedó en enfriamiento");
        var visual=player.GetComponentInChildren<AOCharacterRenderer>(true);
        Expect(visual!=null&&visual.CastingAnimating,"V268","El jugador no reprodujo la animación de lanzamiento");
        var aura=player.GetComponent<AOMeditationVisualV269>();
        if(wasMeditating)Expect(!magic.IsMeditating&&(aura==null||!aura.IsActive),"V269","Lanzar un hechizo no cortó la meditación ni su aura");
    }
    static float LaunchAtWall()
    {
        shot=null;if(skillShotId<=0)return .1f;
        var spell=AOSpellDatabaseV120.Get(skillShotId);var config=AOSkillShotConfigV267.Get(skillShotId);
        // Real map wall (not an interactable): launch from two free tiles before it.
        var grid=player.CurrentGrid;var start=new Vector2Int(player.TileX,player.TileY);
        float check=3f/Mathf.Max(1f,config.speed)+.3f;
        if(check>=flightTime-.1f){Note("V267","Alcance muy corto para probar el choque");return .1f;}
        for(int radius=1;radius<=15;radius++) for(int dx=-radius;dx<=radius;dx++) for(int dy=-radius;dy<=radius;dy++) {
            if(Mathf.Max(Mathf.Abs(dx),Mathf.Abs(dy))!=radius)continue;
            var wall=start+new Vector2Int(dx,dy);
            if(!grid.InBounds(wall.x,wall.y)||AOInteractionRegistry.IsBlocked(wall.x,wall.y))continue;
            foreach(var direction in Directions) {
                var a=wall-direction;var origin=wall-direction*3;
                if(!grid.InBounds(origin.x,origin.y)||!grid.ProjectileBlockedBetween(a.x,a.y,wall.x,wall.y))continue;
                bool clear=true;for(int k=0;k<2&&clear;k++){var p=origin+direction*k;var q=p+direction;clear=!grid.ProjectileBlockedBetween(p.x,p.y,q.x,q.y)&&!AOInteractionRegistry.IsBlocked(q.x,q.y);}
                if(!clear)continue;
                wallInfo="pared en "+wall.x+","+wall.y+", lanzado desde "+origin.x+","+origin.y;Note("V267","Choque: "+wallInfo);
                shot=AOSkillShotProjectileV267.Launch(magic,spell,grid,grid.TileToWorld(origin.x,origin.y),new Vector2(direction.x,-direction.y));
                Expect(shot!=null,"V267","Launch devolvió null");return check;
            }
        }
        Note("V267","Sin pared cerca para probar el choque");return .1f;
    }
    static float LaunchAtNpc()
    {
        shot=null;if(skillShotId<=0)return .1f;
        var grid=player.CurrentGrid;
        targetNpc=UnityEngine.Object.FindObjectsByType<AONPCCombatV09>(FindObjectsSortMode.None)
            .Where(n=>n.IsAlive&&n.Attackable&&n.isActiveAndEnabled&&n.GetComponent<AONPCMetadata>()!=null)
            .Where(n=>!(AONPCMagicDatabaseV129.Get(n.GetComponent<AONPCMetadata>().NpcIndex)?.immuneToSpells??false))
            .OrderBy(n=>(n.transform.position-player.transform.position).sqrMagnitude).FirstOrDefault();
        if(targetNpc==null){Note("V267","Sin NPC atacable en la escena para probar el impacto");return .1f;}
        var meta=targetNpc.GetComponent<AONPCMetadata>();var tile=new Vector2Int(meta.TileX,meta.TileY);
        foreach(var direction in Directions) {
            var origin=tile+direction;
            if(!grid.InBounds(origin.x,origin.y)||grid.ProjectileBlockedBetween(tile.x,tile.y,origin.x,origin.y))continue;
            npcHpBefore=targetNpc.HP;ClearChat();
            npcInfo=targetNpc.DisplayName+" en "+tile.x+","+tile.y+"; casilla bloqueada por el registro="+AOInteractionRegistry.IsBlocked(tile.x,tile.y);
            Vector3 from=grid.TileToWorld(origin.x,origin.y);
            shot=AOSkillShotProjectileV267.Launch(magic,AOSpellDatabaseV120.Get(skillShotId),grid,from,(Vector2)(targetNpc.transform.position-from));
            return .5f;
        }
        Note("V267","El NPC "+targetNpc.DisplayName+" no tiene casilla libre al lado");targetNpc=null;return .1f;
    }
    static void CheckNpcHit()
    {
        if(string.IsNullOrEmpty(npcInfo))return;
        Expect(shot==null,"V267","El proyectil lanzado al NPC no terminó");
        bool reached=targetNpc==null||!targetNpc.IsAlive||targetNpc.HP<npcHpBefore||ChatHas("impactó a")||ChatHas("no es atacable")||ChatHas("inmune");
        Expect(reached,"V267","El skill shot no impactó al NPC ("+npcInfo+", HP "+npcHpBefore+"→"+(targetNpc==null?0:targetNpc.HP)+")");
    }

    // ---- V261: swap, move, incompatible drops, inventory and spell list to the bar.
    static void TestDragDrop()
    {
        Expect(drag!=null,"V261","AOActionBarDragDropV261 no se instaló en el jugador");if(drag==null)return;
        dragUpdate=typeof(AOActionBarDragDropV261).GetMethod("Update",Private);
        Expect(controls.ShowBarPublic(),"V261","La barra de acciones no está visible");
        int[] spells=Enumerable.Range(0,AOSpellDatabaseV120.Count).Select(AOSpellDatabaseV120.GetAt).Where(s=>s!=null).Take(2).Select(s=>s.id).ToArray();
        for(int i=0;i<4;i++){Assign(true,i,0);Assign(false,i,0);}
        Assign(true,0,spells[0]);Assign(true,1,spells[1]);
        ClearChat();DragGui(SlotCenter(0),SlotCenter(1));
        Expect(Slot(true,0)==spells[1]&&Slot(true,1)==spells[0],"V261","Arrastrar Q sobre W no intercambió ("+Slot(true,0)+","+Slot(true,1)+")");
        Expect(ChatHas("intercambiados"),"V261","Sin mensaje de intercambio");
        DragGui(SlotCenter(1),SlotCenter(3));
        Expect(Slot(true,1)==0&&Slot(true,3)==spells[0],"V261","Mover W a un slot vacío falló");
        ClearChat();DragGui(SlotCenter(0),SlotCenter(5));
        Expect(Slot(true,0)==spells[1]&&Slot(false,1)==0,"V261","Un hechizo se soltó en un slot de consumible");
        Expect(ChatHas("solo pueden ordenarse"),"V261","Sin aviso al soltar un hechizo en un consumible");
        Assign(false,0,1);DragGui(SlotCenter(4),SlotCenter(6));
        Expect(Slot(false,0)==0&&Slot(false,2)==1,"V261","Mover el consumible 1 al 3 falló");
        if(ui.IsInventoryTabVisible&&ui.VisibleInventorySlots>0&&ui.InventoryItemIdAt(0)==1) {
            DragGui(ui.InventorySlotRectGUI(0).center,SlotCenter(7));
            Expect(Slot(false,3)==1,"V261","Arrastrar del inventario al consumible 4 falló");
            ClearChat();int before=Slot(true,2);DragGui(ui.InventorySlotRectGUI(0).center,SlotCenter(2));
            Expect(Slot(true,2)==before&&ChatHas("consumibles solo"),"V261","Un consumible se soltó en un slot de hechizo");
        } else Expect(false,"V261","Inventario no visible o sin el objeto de prueba en el primer casillero");
        var tab=typeof(AOInterfaceV0101).GetField("upperTab",Private);var previous=tab.GetValue(ui);
        tab.SetValue(ui,Enum.Parse(tab.FieldType,"Spells"));
        try {
            var first=magic.GetKnownSpellAt(0);
            Expect(first!=null,"V261","Sin hechizos conocidos para arrastrar");
            if(first!=null) {
                Rect view=ui.SpellViewportRectGUI;Assign(true,2,0);
                DragGui(new Vector2(view.center.x,view.y+ui.SpellRowHeightGUI*.5f-ui.SpellScrollValue.y),SlotCenter(2));
                Expect(Slot(true,2)==first.id,"V261","Arrastrar desde la lista de hechizos a E falló ("+Slot(true,2)+" en vez de "+first.id+")");
            }
        } finally {tab.SetValue(ui,previous);}
    }

    // ---- V268: every NPC in the database builds 4 directions; a real renderer shows and ends the cast.
    static void StartCastChecks()
    {
        string root=Path.Combine(Data,"CastV268");var file=Load<CastFile>("CastV268","cast_animations.json");
        Expect(file?.npcs!=null&&file.bodies!=null,"V268","cast_animations.json falta o está vacío");if(file?.npcs==null||file.bodies==null)return;
        int missing=file.bodies.Where(b=>b.directions!=null).SelectMany(b=>b.directions).Where(d=>d.frames!=null).SelectMany(d=>d.frames)
            .Count(f=>!File.Exists(Path.Combine(root,f.Replace('/',Path.DirectorySeparatorChar))));
        Expect(missing==0,"V268",missing+" frames de casteo no existen");
        int built=0;
        foreach(var npc in file.npcs) {
            bool ok=AOCastAnimationDatabaseV268.TryBuildNpc(npc.npcIndex,out var dirs,out _,out _,out _,out _)
                &&dirs!=null&&dirs.Length==4&&dirs.All(d=>d!=null&&d.body!=null&&d.body.Length>0);
            Expect(ok,"V268","El NPC "+npc.npcIndex+" no arma las 4 direcciones");
            if(ok){built++;if(castDirs==null)castDirs=dirs;}
        }
        Note("V268",built+"/"+file.npcs.Length+" NPC con casteo, "+file.bodies.Length+" cuerpos");
        var meta=UnityEngine.Object.FindObjectsByType<AONPCMetadata>(FindObjectsSortMode.None)
            .Where(m=>AOCastAnimationDatabaseV268.CastBodyForNpc(m.NpcIndex)==0)
            .Where(m=>{var v=m.GetComponentInChildren<AOCharacterRenderer>(true);return v!=null&&v.isActiveAndEnabled;})
            .OrderBy(m=>(m.transform.position-player.transform.position).sqrMagnitude).FirstOrDefault();
        if(meta==null||castDirs==null){Note("V268","Sin NPC visible para probar el renderer");return;}
        Expect(!AOCastAnimationRuntimeV268.PlayNpc(meta.gameObject),"V268","PlayNpc animó un NPC que no está en la base");
        npcVisual=meta.GetComponentInChildren<AOCharacterRenderer>(true);
        npcVisual.PlayCastAnimation(castDirs,10f,.6f);
        var frames=new HashSet<Sprite>(castDirs.SelectMany(d=>d.body));
        Expect(npcVisual.CastingAnimating,"V268","El renderer no entró en casteo");
        Expect(npcVisual.GetComponentsInChildren<SpriteRenderer>(true).Any(r=>r.sprite!=null&&frames.Contains(r.sprite)),"V268","El cuerpo del NPC no muestra los frames de casteo");
    }

    // ---- V269: every tier loads frames; the player aura follows meditation.
    static void TestMeditationData()
    {
        string root=Path.Combine(Data,"MeditationV269");meditation=Load<MeditationFile>("MeditationV269","meditation_fx.json");
        Expect(meditation?.entries!=null&&meditation.entries.Length>0,"V269","meditation_fx.json falta o está vacío");if(meditation?.entries==null)return;
        int missing=meditation.entries.Where(e=>e.frames!=null).SelectMany(e=>e.frames).Count(f=>!File.Exists(Path.Combine(root,f.Replace('/',Path.DirectorySeparatorChar))));
        Expect(missing==0,"V269",missing+" frames de meditación no existen");
        var dummy=Dummy("QA Meditación",new Vector2(2f,0f));var visual=dummy.AddComponent<AOMeditationVisualV269>();
        foreach(var entry in meditation.entries) {
            visual.BeginFx(entry.fx);
            Expect(visual.IsActive&&visual.ActiveFx==entry.fx,"V269","BeginFx("+entry.fx+") no lo activó (activo "+visual.ActiveFx+")");
            Expect(dummy.GetComponentsInChildren<SpriteRenderer>(true).Any(r=>r.sprite!=null),"V269","El FX "+entry.fx+" no tiene sprite");
            visual.End(true);
        }
        // The level → FX table is still being decided (Contenido/Arte): only require an aura with frames.
        var shown=new List<string>();
        foreach(int level in new[]{1,13,18,25,35,45,47,60}) {
            visual.Begin(level);
            Expect(visual.IsActive&&visual.ActiveFx>0,"V269","El nivel "+level+" no muestra aura");
            shown.Add(level+"→"+visual.ActiveFx+"(tabla "+AOMeditationVisualV269.FxForLevel(level,false)+")");
            visual.End(true);
        }
        Expect(!visual.IsActive,"V269","End() no apagó el aura");
        Expect(Resources.Load<AudioClip>("AOMigrator/MeditationV269/Audio/meditation_158")!=null,"V269","Falta el sonido de meditación");
        Note("V269","Aura por nivel: "+string.Join(", ",shown));
    }
    static void StartMeditation()
    {
        Expect(rpg.MaxMana>0,"V269","El personaje de prueba no tiene maná");if(rpg.MaxMana<=0)return;
        rpg.ModifyMana(-(rpg.MaxMana/2+1));manaBeforeMeditation=rpg.Mana;
        magic.ToggleMeditation();
        var aura=player.GetComponent<AOMeditationVisualV269>();
        Expect(magic.IsMeditating,"V269","No empezó a meditar ("+LastChat()+")");
        Expect(aura!=null&&aura.IsActive&&aura.ActiveFx>0,"V269","Meditar no mostró el aura");
    }

    // ---- V130: EOT and buff visuals appear, expire on time and clear with the runtime.
    static void StartEffectChecks()
    {
        var tuning=Load<TuningFile>("SpellOverrides","spell_visual_tuning.json");
        Expect(tuning?.entries!=null&&tuning.entries.Length>0,"V130","spell_visual_tuning.json falta o está vacío");
        if(tuning?.entries!=null&&tuning.entries.Length>0) {
            var entry=tuning.entries[0];var loaded=AOSpellVisualOverridesV130.Tuning(entry.spellId);
            Expect(Mathf.Approximately(loaded.persistentFps,entry.persistentFps)&&Mathf.Approximately(loaded.persistentScale,entry.persistentScale),"V130","No se aplica el tuning del hechizo "+entry.spellId);
        }
        int withVisual=0;
        for(int id=1;id<=2000;id++) {
            var effect=AOMagicEffectDatabaseV129.Get(id);if(effect==null)continue;
            if(AOSpellVisualOverridesV130.EffectClientFrames(id).Length==0&&AOSpellVisualOverridesV130.EffectAuraFrames(id).Length==0)continue;
            withVisual++;if(eotId==0&&effect.SupportedLocal&&effect.type!=10&&effect.type!=16)eotId=id;
        }
        Note("V130",withVisual+" EOT con visual; prueba con EOT "+eotId);
        Expect(eotId>0,"V130","Ningún EOT local tiene visual");
        if(eotId>0) {
            eotRuntime=Dummy("QA EOT",new Vector2(-2f,0f)).AddComponent<AOMagicEffectRuntimeV129>();
            Expect(eotRuntime.ApplyEffect(eotId),"V130","ApplyEffect("+eotId+") falló");
            Expect(HasPersistent(eotRuntime.gameObject,"eot:"+eotId+":"),"V130","El EOT "+eotId+" no creó su visual persistente");
            eotShort=Dummy("QA EOT corto",new Vector2(-3f,0f));AOSpellPersistentVisualV130.ApplyEffect(eotShort,eotId,.5f);
            Expect(HasPersistent(eotShort,"eot:"+eotId+":"),"V130","El visual corto del EOT no apareció");
        }
        for(int i=0;i<AOSpellDatabaseV120.Count&&buffSpellId==0;i++){var s=AOSpellDatabaseV120.GetAt(i);if(s!=null&&AOSpellVisualOverridesV130.HasBuffVisual(s.id))buffSpellId=s.id;}
        if(buffSpellId==0){Note("V130","Ningún hechizo tiene visual de buff");return;}
        if(eotShort==null)eotShort=Dummy("QA EOT corto",new Vector2(-3f,0f));
        AOSpellPersistentVisualV130.ApplySpellBuff(eotShort,buffSpellId,.5f);
        Expect(HasPersistent(eotShort,"spell:"+buffSpellId+":buff"),"V130","El buff del hechizo "+buffSpellId+" no mostró su visual");
    }
    static void CheckEffectExpiry()
    {
        if(eotShort!=null)Expect(!HasPersistent(eotShort,""),"V130","Los visuales de 0,5 s no vencieron");
        if(eotRuntime!=null){eotRuntime.ClearAll();Expect(eotRuntime.ActiveCount==0,"V130","ClearAll dejó efectos activos");}
    }

    static void Finish()
    {
        EditorApplication.update-=Tick;Application.logMessageReceived-=Log;SessionState.EraseBool(RunningKey);
        if(testMouse!=null&&testMouse.added)InputSystem.RemoveDevice(testMouse);
        testMouse=null;if(previousMouse!=null&&previousMouse.added)previousMouse.MakeCurrent();
        if(previousInputSettings!=null)InputSystem.settings=previousInputSettings;
        if(testInputSettings!=null)UnityEngine.Object.DestroyImmediate(testInputSettings);
        previousInputSettings=testInputSettings=null;
        foreach(var dummy in dummies)if(dummy!=null)UnityEngine.Object.Destroy(dummy);
        dummies.Clear();
        var all=failures.Concat(errors.Select(e=>"Log: "+e)).ToList();
        var result=new Result{passed=all.Count==0,stage=stage,message=all.Count==0?"":all[0],failures=failures.ToArray(),notes=notes.ToArray(),errors=errors.ToArray()};
        File.WriteAllText(Path.Combine(Root,"MigrationReports","modules_v270.json"),JsonUtility.ToJson(result,true));
        if(File.Exists(Flag))File.Delete(Flag);
        Debug.Log("AO Modules QA: "+(result.passed?"PASS":"FAIL "+all.Count+" fallas")+" "+result.message);
        if(testView!=null)testView.maximized=previousMaximized;
        EditorApplication.isPlaying=false;
    }
}
#endif
