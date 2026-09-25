#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Explicit marker only (Temp/run_demo_save_qa, see Tools/test_demo_save_unity.py). Unlike the other QA tests,
// this one SAVES FOR REAL (no isolated online client): the offline demo must write only to AO_BattleDemo/.
// Steps: DEMO -> offline -> new character (hub 1000) -> save -> switch the menu to normal -> save again
// (like an autosave). The Python side checks the 7 normal saves byte for byte and that new files appear only
// in AO_BattleDemo/. Preferences use a temporary prefix.
public static class AODemoSaveQA289
{
    [Serializable] class Result {public bool passed;public int step;public string message;public string[] failures,notes,errors;}
    class Step {public string module;public double delay,timeout=30;public Func<bool> ready;public string waitingFor;public Action action;}

    static string Root=>Path.GetFullPath(Path.Combine(Application.dataPath,".."));
    static string Flag=>Path.Combine(Root,"Temp","run_demo_save_qa");
    const string RunningKey="AODemoSaveQA289.Running";
    const string TestCharacter="DemoPrueba";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;

    static readonly List<Step> steps=new List<Step>();
    static int index;
    static double started,next,stepSince;
    static readonly List<string> failures=new List<string>(),notes=new List<string>(),errors=new List<string>();
    static AOWorldManagerV07 world;
    static AOSaveGameV140 save;
    static AOMainMenuV140 menu;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Protect()
    {
        if(!File.Exists(Flag))return;
        // Only the preferences are isolated: the save itself is what we test.
        AOPlayerSettingsV230.TestPrefixOverride="AO.DemoSaveQA."+Guid.NewGuid().ToString("N")+".";
    }
    [InitializeOnLoadMethod]
    static void Initialize()
    {
        // A script reload during Play drops Tick: stop Play (the Python side still checks the saves).
        if(EditorApplication.isPlaying&&SessionState.GetBool(RunningKey,false)) {
            SessionState.EraseBool(RunningKey);
            if(File.Exists(Flag)){WriteReport(new Result{passed=false,step=-1,message="QA: recarga de scripts durante la prueba",failures=new[]{"QA: recarga de scripts durante la prueba"},notes=new string[0],errors=new string[0]});File.Delete(Flag);}
            EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
        }
        EditorApplication.update+=()=>{if(File.Exists(Flag)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling)EditorApplication.isPlaying=true;};
        EditorApplication.playModeStateChanged+=mode=> {
            if(mode==PlayModeStateChange.EnteredEditMode)SessionState.EraseBool(RunningKey);
            if(mode!=PlayModeStateChange.EnteredPlayMode||!File.Exists(Flag))return;
            SessionState.SetBool(RunningKey,true);
            EditorApplication.isPaused=false;
            failures.Clear();notes.Clear();errors.Clear();world=null;save=null;menu=null;
            Plan();index=0;started=EditorApplication.timeSinceStartup;next=started+3;stepSince=next;
            EditorApplication.update+=Tick;Application.logMessageReceived+=Log;
        };
    }
    static void Log(string message,string trace,LogType type)
    {if((type==LogType.Error||type==LogType.Exception)&&errors.Count<10)errors.Add(message+" | "+trace.Split('\n')[0]);}
    static void Expect(bool ok,string module,string message){if(!ok)failures.Add(module+": "+message);}
    static void Note(string module,string message)=>notes.Add(module+": "+message);
    static void Add(string module,double delay,Action action)=>steps.Add(new Step{module=module,delay=delay,action=action});
    static void WaitFor(string module,string what,Func<bool> ready,double timeout,Action action)=>
        steps.Add(new Step{module=module,delay=.1,ready=ready,waitingFor=what,timeout=timeout,action=action});

    static void Tick()
    {
        double now=EditorApplication.timeSinceStartup;if(now<next)return;
        if(!EditorApplication.isPlaying){failures.Add("QA: Play detenido");Finish();return;}
        if(now-started>150){failures.Add("QA: tiempo agotado en el paso "+index);Finish();return;}
        if(index>=steps.Count){Finish();return;}
        var step=steps[index];
        try {
            if(step.ready!=null&&!step.ready()) {
                if(now-stepSince<step.timeout)return;
                failures.Add(step.module+": no llegó a tiempo: "+step.waitingFor);
                if(step.module=="QA"){Finish();return;}
            } else step.action?.Invoke();
        } catch(Exception error) {
            failures.Add(step.module+": excepción "+error.GetType().Name+": "+error.Message+" | "+error.StackTrace?.Split('\n')[0]);
            if(step.module=="QA"){Finish();return;}
        }
        index++;next=now+(index<steps.Count?steps[index].delay:0);stepSince=next;
    }

    static void Plan()
    {
        steps.Clear();
        WaitFor("QA","mundo cargado y menú de ingreso",()=>{
            world=UnityEngine.Object.FindFirstObjectByType<AOWorldManagerV07>();save=UnityEngine.Object.FindFirstObjectByType<AOSaveGameV140>();
            menu=UnityEngine.Object.FindFirstObjectByType<AOMainMenuV140>();
            return world!=null&&!world.IsLoading&&save!=null&&menu!=null;
        },60,()=>{
            if(AOOnlineClientV240.Connected)throw new Exception("hay una sesión online abierta: no se prueba");
            Expect(!AOMainMenuV140.SessionActive,"QA","Ya había una sesión activa");
            // Same as the INGRESAR > DEMO > "SOLO DUNGEON (SIN CONEXIÓN)" button.
            AOOnlineClientV240.UseLocalMode();
            typeof(AOMainMenuV140).GetMethod("SetBattleDemo",Private).Invoke(menu,new object[]{true,false});
            Expect(AOMainMenuV140.BattleDemo&&!AOMainMenuV140.BattleDemoOnline,"Demo","El menú no quedó en demo sin conexión");
            Expect(!AOOnlineClientV240.ProtectLocalSave,"QA","El guardado está protegido: esta prueba necesita guardar de verdad");
            Expect(save.NewGameWithCharacter(TestCharacter,1,1,1,1),"Demo","NewGameWithCharacter falló");
            AOMainMenuV140.StartSessionFromCreator();
        });
        WaitFor("Demo","sesión de demo en el hub 1000",()=>AOMainMenuV140.SessionActive&&!world.IsLoading&&world.CurrentMapNumber==AOSaveGameV140.DemoHubMap,40,()=>{
            var player=UnityEngine.Object.FindFirstObjectByType<AOTestPlayer>();
            Expect(AOSaveGameV140.SessionIsDemo,"Demo","La sesión no quedó marcada como demo");
            Expect(player.TileX==52&&player.TileY==58,"Demo","Aparición en "+player.TileX+","+player.TileY+" (esperado 52,58)");
            Expect(player.Heading==AOGridMap.SOUTH,"Demo","Mirando a "+player.Heading+" (esperado sur)");
            Expect(player.GetComponent<AOCharacterIdentityV170>()?.CharacterName==TestCharacter,"Demo","El personaje de demo no se llama "+TestCharacter);
        });
        Add("Guardar",.5,()=>Expect(save.SaveGame(false),"Guardar","SaveGame en demo devolvió falso"));
        // Back to "normal" in the menu while the demo session is still the one loaded: an autosave must stay in the demo.
        Add("Cambio",.3,()=>{
            typeof(AOMainMenuV140).GetMethod("SetBattleDemo",Private).Invoke(menu,new object[]{false,false});
            Expect(!AOMainMenuV140.BattleDemo,"Cambio","El menú no volvió a modo normal");
            Expect(AOSaveGameV140.SessionIsDemo,"Cambio","Al cambiar el menú, la sesión dejó de ser demo");
            Expect(save.SaveGame(false),"Cambio","El guardado automático después del cambio devolvió falso");
            Note("Cambio","Guardado después de pasar el menú a normal (lo revisa Python: tiene que caer en AO_BattleDemo/)");
        });
    }

    static void WriteReport(Result result)
    {
        Directory.CreateDirectory(Path.Combine(Root,"MigrationReports"));
        File.WriteAllText(Path.Combine(Root,"MigrationReports","demo_save_v289.json"),JsonUtility.ToJson(result,true));
    }
    static void Finish()
    {
        EditorApplication.update-=Tick;Application.logMessageReceived-=Log;SessionState.EraseBool(RunningKey);
        var all=failures.Concat(errors.Select(e=>"Log: "+e)).ToList();
        var result=new Result{passed=all.Count==0,step=index,message=all.Count==0?"":all[0],failures=failures.ToArray(),notes=notes.ToArray(),errors=errors.ToArray()};
        WriteReport(result);
        if(File.Exists(Flag))File.Delete(Flag);
        Debug.Log("AO Demo Save QA: "+(result.passed?"PASS":"FAIL "+all.Count+" fallas")+" "+result.message);
        EditorApplication.isPlaying=false;
    }
}
#endif
