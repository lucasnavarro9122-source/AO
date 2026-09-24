#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

// Explicit marker only. All tests use temporary preferences and protected saves.
public static class AOControlsQA260
{
    static string Root=>Path.GetFullPath(Path.Combine(Application.dataPath,".."));
    static string Flag=>Path.Combine(Root,"Temp","run_controls_qa");
    static int stage;
    static double started,next;
    static string failure="";
    static AOTestPlayer player;
    static AOActionBarV260 controls;
    static AOPlayerMagicV120 magic;
    static AOInterfaceV0101 ui;
    static EditorWindow testView;
    static bool previousMaximized;
    static int spellId,previousMana;
    static Vector2Int destination;
#if ENABLE_INPUT_SYSTEM
    static Mouse testMouse;
    static Mouse previousMouse;
    static InputSettings previousInputSettings;
    static InputSettings testInputSettings;
#endif
    const string TestCharacter="ControlsPrueba";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Protect()
    {
        if(!File.Exists(Flag))return;
        AOPlayerSettingsV230.TestPrefixOverride="AO.ControlsQA."+Guid.NewGuid().ToString("N")+".";
        AOOnlineClientV240.BeginIsolatedTest(0);
    }
    [InitializeOnLoadMethod]
    static void Initialize()
    {
        EditorApplication.update+=()=>{if(File.Exists(Flag)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling)EditorApplication.isPlaying=true;};
        EditorApplication.playModeStateChanged+=mode=> {
            if(mode!=PlayModeStateChange.EnteredPlayMode||!File.Exists(Flag))return;
            EditorApplication.isPaused=false;
            testView=EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));
            previousMaximized=testView.maximized;testView.maximized=true;
            stage=0;failure="";started=EditorApplication.timeSinceStartup;next=started+4;
            EditorApplication.update+=Tick;Application.logMessageReceived+=Log;
        };
    }
    static void Log(string message,string trace,LogType type)
    {if(type==LogType.Error||type==LogType.Exception)failure=message+"\n"+trace;}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Tick()
    {
        double now=EditorApplication.timeSinceStartup;if(now<next)return;
        if(!EditorApplication.isPlaying){Finish(false,"Play detenido");return;}
        if(now-started>100){Finish(false,"Tiempo agotado");return;}
        try {
            var world=UnityEngine.Object.FindFirstObjectByType<AOWorldManagerV07>();if(world==null||world.IsLoading)return;
            if(stage==0) {
                Check(AOAudioV190.CurrentMapMusicId==2,"La intro original no comenzó");
                Check(File.Exists(AOAudioV190.PrepareMidiPath(4)),"Falta caché de música de Ullathorpe");
                var save=UnityEngine.Object.FindFirstObjectByType<AOSaveGameV140>();player=UnityEngine.Object.FindFirstObjectByType<AOTestPlayer>();
                if(save==null||player==null)return;
                var data=JsonUtility.FromJson<AOSaveGameV140.SaveData>(save.CaptureOnline());
                data.character.name=TestCharacter;data.world.map=1;data.world.x=57;data.world.y=44;
                data.rpg.raceId=1;data.rpg.genderId=1;data.rpg.headIndex=1;data.rpg.classId=1;data.rpg.level=25;
                data.rpg.maxHp=100;data.rpg.mana=10000;data.rpg.stamina=1000;data.rpg.skills=Enumerable.Repeat(100,24).ToArray();data.rpg.hunger=50;
                data.combat.hp=40;data.combat.dead=false;
                data.inventory.itemIndices=new int[24];data.inventory.amounts=new int[24];data.inventory.itemIndices[0]=1;data.inventory.amounts[0]=2;
                data.inventory.weapon=data.inventory.armor=data.inventory.helmet=data.inventory.shield=0;
                Check(save.ApplyOnline(JsonUtility.ToJson(data)),"No se pudo preparar personaje de prueba");
                AOMainMenuV140.StartSessionFromCreator();stage=1;next=now+3;return;
            }
            if(stage==1) {
                Check(AOOnlineClientV240.ProtectLocalSave,"Guardado original desprotegido");
                controls=player.GetComponent<AOActionBarV260>();magic=player.GetComponent<AOPlayerMagicV120>();ui=UnityEngine.Object.FindFirstObjectByType<AOInterfaceV0101>();
                AOPlayerSettingsV230.Profile=AOControlProfile.AO;
                Check(!AOPlayerSettingsV230.SpellMacrosEnabled,"AO habilitó macros por defecto");
                Check(!AOPlayerSettingsV230.SetKey(AOGameAction.Spell1,KeyCode.W,out _),"Una macro AO pisa movimiento");
                Check(AOPlayerSettingsV230.SetSpellMacros(true,out _)&&AOPlayerSettingsV230.ActionEnabled(AOGameAction.Spell1),"No habilita macros AO");
                AOPlayerSettingsV230.SetSpellMacros(false,out _);
                Check(AOPlayerSettingsV230.SetKey(AOGameAction.Interact,KeyCode.T,out _),"No guarda tecla AO");
                AOPlayerSettingsV230.Profile=AOControlProfile.MOBA;
                Check(AOPlayerSettingsV230.Key(AOGameAction.WorldCommand)==KeyCode.Mouse1,"Falta clic derecho");
                Check(AOPlayerSettingsV230.Key(AOGameAction.Spell1)==KeyCode.Q&&AOPlayerSettingsV230.Key(AOGameAction.Spell4)==KeyCode.R,"Faltan QWER");
                Check(!AOPlayerSettingsV230.ActionEnabled(AOGameAction.MoveUp),"W mueve en MOBA");
                Check(!AOPlayerSettingsV230.SetKey(AOGameAction.Map,KeyCode.Q,out _),"Acepta teclas en conflicto");
                Check(AOPlayerSettingsV230.SetKey(AOGameAction.WorldCommand,KeyCode.Mouse2,out _),"No admite botones de mouse");
                AOPlayerSettingsV230.SetKey(AOGameAction.WorldCommand,KeyCode.Mouse1,out _);
                AOPlayerSettingsV230.Profile=AOControlProfile.AO;
                Check(AOPlayerSettingsV230.Key(AOGameAction.Interact)==KeyCode.T,"MOBA sobrescribió teclas AO");
                AOPlayerSettingsV230.Profile=AOControlProfile.MOBA;
                // Closed walls require a detour, never walking through a blocked tile.
                var gridObject=new GameObject("Grid QA");var grid=gridObject.AddComponent<AOGridMap>();grid.Initialize(1,5,1,5);
                grid.OrFlags(3,2,15);var path=AOActionBarV260.FindPath(grid,new Vector2Int(2,2),new Vector2Int(4,2),false);
                Check(path.Count==4&&!path.Contains(new Vector2Int(3,2)),"Ruta atraviesa pared o no rodea obstáculo");
                UnityEngine.Object.Destroy(gridObject);
                AOPlayerSettingsV230.AssignSlot(TestCharacter,false,0,1);controls.UseConsumable(0);
                Check(player.GetComponent<AOInventoryV10>().CountItem(1)==1,"Consumible no usa exactamente una unidad");
                var spell=AOSpellDatabaseV120.Get(21); // Celeridad: valid low-cost self spell for the fixture.
                spellId=spell.id;Check(magic.LearnSpell(spellId),"No aprende hechizo QA");AOPlayerSettingsV230.AssignSlot(TestCharacter,true,0,spellId);
                previousMana=player.GetComponent<AOPlayerRPGV11>().Mana;controls.UseSpell(0);
                Check(magic.CooldownRemaining(spellId)>0,"Macro no lanzó hechizo; mana="+player.GetComponent<AOPlayerRPGV11>().Mana+" stamina="+player.GetComponent<AOPlayerRPGV11>().Stamina);
                int after=player.GetComponent<AOPlayerRPGV11>().Mana;Check(after<previousMana,"Hechizo no consume maná");
                controls.UseSpell(0);Check(player.GetComponent<AOPlayerRPGV11>().Mana==after,"Macro ignora cooldown");
                var start=new Vector2Int(player.TileX,player.TileY);destination=start;
                foreach(var offset in new[]{Vector2Int.right*2,Vector2Int.left*2,Vector2Int.up*2,Vector2Int.down*2})
                    if(AOActionBarV260.FindPath(player.CurrentGrid,start,start+offset,false).Count==2){destination=start+offset;break;}
                Check(destination!=start,"No hay destino QA libre");
#if ENABLE_INPUT_SYSTEM
                // Process a synthetic device explicitly: Editor-only updates consume queued
                // native events when GameView is not focused.
                previousInputSettings=InputSystem.settings;
                testInputSettings=UnityEngine.Object.Instantiate(previousInputSettings);
                testInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                testInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                testInputSettings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;
                InputSystem.settings=testInputSettings;
                previousMouse=Mouse.current;testMouse=InputSystem.AddDevice<Mouse>("AO Controls QA Mouse");
                controls.SendMessage("Update");
                var camera=AOActionBarV260.GameCamera;var screen=camera.WorldToScreenPoint(player.CurrentGrid.TileToWorld(destination.x,destination.y));
                InputSystem.QueueStateEvent(testMouse,new MouseState{position=new Vector2(screen.x,screen.y)}.WithButton(MouseButton.Right));
                InputSystem.Update();
                Check(AOPlayerSettingsV230.Pressed(AOGameAction.WorldCommand),"Clic sintético no se reconoce en Input System");
                controls.SendMessage("Update");
                InputSystem.QueueStateEvent(testMouse,new MouseState{position=new Vector2(screen.x,screen.y)});
                InputSystem.Update();
#else
                controls.Order(destination.x,destination.y);
#endif
                stage=2;next=now+.2;return;
            }
            if(stage==2) {
#if ENABLE_INPUT_SYSTEM
                InputSystem.QueueStateEvent(testMouse,new MouseState{position=testMouse.position.ReadValue()});
                InputSystem.Update();
#endif
                stage=3;next=now+2;return;
            }
            if(stage==3) {
                Check(player.TileX==destination.x&&player.TileY==destination.y,"Clic derecho no movió al destino: "+player.TileX+","+player.TileY+" destino="+destination+" orden="+controls.HasOrder+" input="+controls.TestLastCommand+" ui="+AOInterfaceV0101.InputCaptured+" enabled="+player.enabled+" scale="+Time.timeScale);
                Check(AOAudioV190.CurrentMapMusicId==4,"No cambió de intro a música de ciudad");
                ui.ShowControlsForQA(1);stage=4;next=now+1;return;
            }
            if(stage==4) {
                ScreenCapture.CaptureScreenshot(Path.Combine(Root,"MigrationReports","controls_moba_v260.png"));
                int amount=player.GetComponent<AOInventoryV10>().CountItem(1);controls.UseConsumable(0);
                Check(player.GetComponent<AOInventoryV10>().CountItem(1)==amount,"Configuración deja consumir detrás del modal");
                stage=5;next=now+1;return;
            }
            if(stage==5) {AOPlayerSettingsV230.Profile=AOControlProfile.AO;ui.ShowControlsForQA(0);stage=6;next=now+1;return;}
            if(stage==6) {ScreenCapture.CaptureScreenshot(Path.Combine(Root,"MigrationReports","controls_ao_v260.png"));stage=7;next=now+1;return;}
            Finish(string.IsNullOrEmpty(failure),failure);
        } catch(Exception error){Finish(false,error.ToString());}
    }
    static void Finish(bool ok,string message)
    {
        EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
#if ENABLE_INPUT_SYSTEM
        if(testMouse!=null&&testMouse.added)InputSystem.RemoveDevice(testMouse);
        testMouse=null;if(previousMouse!=null&&previousMouse.added)previousMouse.MakeCurrent();
        if(previousInputSettings!=null)InputSystem.settings=previousInputSettings;
        if(testInputSettings!=null)UnityEngine.Object.DestroyImmediate(testInputSettings);
#endif
        File.WriteAllText(Path.Combine(Root,"MigrationReports","controls_v260.json"),JsonUtility.ToJson(new Result{passed=ok,stage=stage,message=message}));
        if(File.Exists(Flag))File.Delete(Flag);
        Debug.Log("AO Controls QA: "+(ok?"PASS":"FAIL")+" "+message);
        if(testView!=null)testView.maximized=previousMaximized;
        EditorApplication.isPlaying=false;
    }
    [Serializable] class Result {public bool passed;public int stage;public string message;}
}
#endif
