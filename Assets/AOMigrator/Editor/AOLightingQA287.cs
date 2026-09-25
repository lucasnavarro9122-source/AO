#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Explicit marker only (Temp/run_lighting_qa, see Tools/test_lighting_unity.py). Arte's request for
// AOLighting2DV283: Ullathorpe next to the fountain with "Luz: Original" (no active Light2D or Volume
// may remain), "Mejorada" and "Mejorada" at night (23 h), one screenshot each. Temporary preferences,
// isolated online client and protected saves; the lighting mode is restored at the end.
public static class AOLightingQA287
{
    // vx..vh: game viewport in screenshot pixels (AODuelUI.ViewportRect), used by Tools/qa_view_check.py.
    [Serializable] class Result {public bool passed;public int step;public string message;public string[] failures,notes,errors,shots;public float vx,vy,vw,vh;public int shotW,shotH;}
    class Step {public string module;public double delay,timeout=30;public Func<bool> ready;public string waitingFor;public Action action;}

    static string Root=>Path.GetFullPath(Path.Combine(Application.dataPath,".."));
    static string Flag=>Path.Combine(Root,"Temp","run_lighting_qa");
    static string ShotsDir=>Path.Combine(Root,"MigrationReports","lighting_v287");
    const string RunningKey="AOLightingQA287.Running";
    static readonly Vector2Int Fountain=new Vector2Int(25,45);

    static readonly List<Step> steps=new List<Step>();
    static int index;
    static double started,next,stepSince;
    static readonly List<string> failures=new List<string>(),notes=new List<string>(),errors=new List<string>(),shots=new List<string>();
    static bool enhancedBefore;
    static AOWorldManagerV07 world;
    static Rect viewport;
    static int shotW,shotH;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Protect()
    {
        if(!File.Exists(Flag))return;
        AOPlayerSettingsV230.TestPrefixOverride="AO.LightingQA."+Guid.NewGuid().ToString("N")+".";
        AOOnlineClientV240.BeginIsolatedTest(0);
    }
    [InitializeOnLoadMethod]
    static void Initialize()
    {
        // A script reload during Play resets the static save protection and drops Tick: protect again and stop Play.
        if(EditorApplication.isPlaying&&SessionState.GetBool(RunningKey,false)) {
            AOPlayerSettingsV230.TestPrefixOverride="AO.LightingQA."+Guid.NewGuid().ToString("N")+".";
            AOOnlineClientV240.BeginIsolatedTest(0);SessionState.EraseBool(RunningKey);
            if(File.Exists(Flag)){WriteReport(new Result{passed=false,step=-1,message="QA: recarga de scripts durante la prueba",failures=new[]{"QA: recarga de scripts durante la prueba"},notes=new string[0],errors=new string[0],shots=new string[0]});File.Delete(Flag);}
            EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
        }
        EditorApplication.update+=()=>{if(File.Exists(Flag)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling)EditorApplication.isPlaying=true;};
        EditorApplication.playModeStateChanged+=mode=> {
            if(mode==PlayModeStateChange.EnteredEditMode)SessionState.EraseBool(RunningKey);
            if(mode!=PlayModeStateChange.EnteredPlayMode||!File.Exists(Flag))return;
            SessionState.SetBool(RunningKey,true);
            EditorApplication.isPaused=false;
            failures.Clear();notes.Clear();errors.Clear();shots.Clear();world=null;
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
    static void Shot(string name)=>Add("Captura",.3,()=>{
        Directory.CreateDirectory(ShotsDir);ScreenCapture.CaptureScreenshot(Path.Combine(ShotsDir,name+".png"));shots.Add(name+".png");
    });

    static void Tick()
    {
        double now=EditorApplication.timeSinceStartup;if(now<next)return;
        if(!EditorApplication.isPlaying){failures.Add("QA: Play detenido");Finish();return;}
        if(now-started>180){failures.Add("QA: tiempo agotado en el paso "+index);Finish();return;}
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
        WaitFor("QA","mundo cargado y personaje",()=>{
            world=UnityEngine.Object.FindFirstObjectByType<AOWorldManagerV07>();
            return world!=null&&!world.IsLoading&&UnityEngine.Object.FindFirstObjectByType<AOSaveGameV140>()!=null&&UnityEngine.Object.FindFirstObjectByType<AOTestPlayer>()!=null;
        },60,()=>{
            if(!AOOnlineClientV240.ProtectLocalSave)throw new Exception("guardado original desprotegido, no se prueba");
            var save=UnityEngine.Object.FindFirstObjectByType<AOSaveGameV140>();
            var data=JsonUtility.FromJson<AOSaveGameV140.SaveData>(save.CaptureOnline());
            data.character.name="LucesPrueba";data.world.map=1;data.world.x=Fountain.x;data.world.y=Fountain.y;
            data.combat.hp=100;data.combat.dead=false;
            if(!save.ApplyOnline(JsonUtility.ToJson(data)))throw new Exception("No se pudo preparar personaje de prueba");
            AOMainMenuV140.StartSessionFromCreator();
        });
        WaitFor("QA","sesión en el mapa 1",()=>AOMainMenuV140.SessionActive&&world!=null&&!world.IsLoading&&world.CurrentMapNumber==1,30,()=>{
            if(!AOOnlineClientV240.ProtectLocalSave)throw new Exception("guardado local desprotegido");
            enhancedBefore=AOLighting2DV283.Enhanced;
            var player=UnityEngine.Object.FindFirstObjectByType<AOTestPlayer>();
            Note("QA","Jugador en "+player.TileX+","+player.TileY+"; modo inicial "+(enhancedBefore?"Mejorada":"Original")+"; luces del mapa: "+(world.CurrentMapLights?.Length??0));
        });
        Add("Original",.1,()=>AOLighting2DV283.SetEnhanced(false));
        Add("Original",1.5,()=>{
            int lights=ActiveLights(),volumes=ActiveVolumes();
            Expect(lights==0,"Original",lights+" Light2D activas con 'Luz: Original'");
            Expect(volumes==0,"Original",volumes+" Volume activos con 'Luz: Original'");
        });
        Shot("luces_original");
        Add("Mejorada",.1,()=>AOLighting2DV283.SetEnhanced(true));
        Add("Mejorada",1.5,()=>{
            int lights=ActiveLights();
            Expect(lights>0,"Mejorada","Ninguna Light2D activa con 'Luz: Mejorada'");
            Note("Mejorada",lights+" Light2D y "+ActiveVolumes()+" Volume activos");
            // The camera math must keep the player centered; the rendered image is checked afterwards in Python.
            var camera=AOActionBarV260.GameCamera;var player=UnityEngine.Object.FindFirstObjectByType<AOTestPlayer>();
            var screen=camera.WorldToScreenPoint(player.transform.position);var rect=camera.pixelRect;
            float dx=(screen.x-rect.center.x)/rect.width,dy=(screen.y-rect.center.y)/rect.height;
            Expect(Mathf.Abs(dx)<.1f&&Mathf.Abs(dy)<.15f,"Mejorada","La cámara no centra al jugador (desvío "+dx.ToString("0.00")+", "+dy.ToString("0.00")+")");
            viewport=AODuelUI.ViewportRect;shotW=Screen.width;shotH=Screen.height;
            Expect(viewport.width>0f,"Mejorada","Sin ViewportRect para recortar las capturas");
        });
        Shot("luces_mejorada");
        Add("Noche",.1,()=>world.SetWorldHour(23f));
        Add("Noche",1.5,()=>Note("Noche",ActiveLights()+" Light2D activas a las 23 h"));
        Shot("luces_noche_mejorada");
        Add("QA",.5,()=>AOLighting2DV283.SetEnhanced(enhancedBefore));
    }

    static int ActiveLights()=>UnityEngine.Object.FindObjectsByType<Light2D>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).Count(l=>l.enabled);
    static int ActiveVolumes()=>UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).Count(v=>v.enabled);

    static void WriteReport(Result result)
    {
        Directory.CreateDirectory(Path.Combine(Root,"MigrationReports"));
        File.WriteAllText(Path.Combine(Root,"MigrationReports","lighting_v287.json"),JsonUtility.ToJson(result,true));
    }
    static void Finish()
    {
        EditorApplication.update-=Tick;Application.logMessageReceived-=Log;SessionState.EraseBool(RunningKey);
        try{AOLighting2DV283.SetEnhanced(enhancedBefore);}catch{}
        var all=failures.Concat(errors.Select(e=>"Log: "+e)).ToList();
        var result=new Result{passed=all.Count==0,step=index,message=all.Count==0?"":all[0],failures=failures.ToArray(),notes=notes.ToArray(),errors=errors.ToArray(),shots=shots.ToArray(),
            vx=viewport.x,vy=viewport.y,vw=viewport.width,vh=viewport.height,shotW=shotW,shotH=shotH};
        WriteReport(result);
        if(File.Exists(Flag))File.Delete(Flag);
        Debug.Log("AO Lighting QA: "+(result.passed?"PASS":"FAIL "+all.Count+" fallas")+" "+result.message);
        EditorApplication.isPlaying=false;
    }
}
#endif
