#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Explicit marker only (Temp/run_hd_qa, see Tools/test_hd_unity.py). HD remaster of the map textures
// (AOWorldManagerV07.SetHDTextures): Ullathorpe next to the fountain in Original vs HD, each with
// "Luz: Original" and "Luz: Mejorada". HD must not change the grid (flags hash) nor the world size of the
// map layers (SpriteRenderer bounds of Layer_1..4). Temporary preferences, isolated online client,
// protected saves; the HD and lighting modes are restored at the end.
public static class AOHDQA288
{
    [Serializable] class Result {public bool passed;public int step;public string message;public string[] failures,notes,errors,shots;public float vx,vy,vw,vh;}
    class Step {public string module;public double delay,timeout=30;public Func<bool> ready;public string waitingFor;public Action action;}
    struct Piece {public int layer;public Vector3 position;public Vector3 size;public float ppu;}

    static string Root=>Path.GetFullPath(Path.Combine(Application.dataPath,".."));
    static string Flag=>Path.Combine(Root,"Temp","run_hd_qa");
    static string ShotsDir=>Path.Combine(Root,"MigrationReports","hd_v288");
    const string RunningKey="AOHDQA288.Running";
    static readonly Vector2Int Fountain=new Vector2Int(25,45);

    static readonly List<Step> steps=new List<Step>();
    static int index;
    static double started,next,stepSince;
    static readonly List<string> failures=new List<string>(),notes=new List<string>(),errors=new List<string>(),shots=new List<string>();
    static bool hdBefore,enhancedBefore;
    static AOWorldManagerV07 world;
    static AOTestPlayer player;
    static Rect viewport;
    static string gridOriginal;
    static List<Piece> piecesOriginal;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Protect()
    {
        if(!File.Exists(Flag))return;
        AOPlayerSettingsV230.TestPrefixOverride="AO.HDQA."+Guid.NewGuid().ToString("N")+".";
        AOOnlineClientV240.BeginIsolatedTest(0);
    }
    [InitializeOnLoadMethod]
    static void Initialize()
    {
        // A script reload during Play resets the static save protection and drops Tick: protect again and stop Play.
        if(EditorApplication.isPlaying&&SessionState.GetBool(RunningKey,false)) {
            AOPlayerSettingsV230.TestPrefixOverride="AO.HDQA."+Guid.NewGuid().ToString("N")+".";
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
            failures.Clear();notes.Clear();errors.Clear();shots.Clear();world=null;player=null;gridOriginal=null;piecesOriginal=null;viewport=default;
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
            data.character.name="HDPrueba";data.world.map=1;data.world.x=Fountain.x;data.world.y=Fountain.y;
            data.combat.hp=100;data.combat.dead=false;
            if(!save.ApplyOnline(JsonUtility.ToJson(data)))throw new Exception("No se pudo preparar personaje de prueba");
            AOMainMenuV140.StartSessionFromCreator();
        });
        WaitFor("QA","sesión en el mapa 1",()=>AOMainMenuV140.SessionActive&&world!=null&&!world.IsLoading&&world.CurrentMapNumber==1,30,()=>{
            if(!AOOnlineClientV240.ProtectLocalSave)throw new Exception("guardado local desprotegido");
            player=UnityEngine.Object.FindFirstObjectByType<AOTestPlayer>();
            hdBefore=AOWorldManagerV07.UseHDTextures;enhancedBefore=AOLighting2DV283.Enhanced;
            Note("QA","Jugador en "+player.TileX+","+player.TileY+"; HD inicial "+hdBefore+"; luz inicial "+(enhancedBefore?"Mejorada":"Original"));
            AOWorldManagerV07.SetHDTextures(false);AOLighting2DV283.SetEnhanced(false);
        });
        // ---- Original textures
        Add("Original",.6,()=>{
            Expect(world.HDTextureCount==0,"Original","Con HD apagado quedan "+world.HDTextureCount+" atlas HD");
            gridOriginal=GridHash();piecesOriginal=Pieces();
            Expect(piecesOriginal.Count>0,"Original","No encuentro sprites en Layer_1..4");
            Expect(piecesOriginal.All(p=>Mathf.Approximately(p.ppu,32f)),"Original","Hay sprites de capa que no están a 32 px por unidad");
            viewport=AODuelUI.ViewportRect;
            Note("Original",piecesOriginal.Count+" sprites en Layer_1..4");
        });
        Shot("hd_off_luz_original");
        Add("Original",.1,()=>AOLighting2DV283.SetEnhanced(true));
        Add("Original",1.2,()=>{});
        Shot("hd_off_luz_mejorada");
        // ---- HD textures
        Add("HD",.1,()=>{AOLighting2DV283.SetEnhanced(false);AOWorldManagerV07.SetHDTextures(true);});
        Add("HD",.6,()=>{
            Expect(world.HDTextureCount>0,"HD","Ningún atlas HD cargado en Ullathorpe");
            var pieces=Pieces();int hd=pieces.Count(p=>Mathf.Approximately(p.ppu,128f));
            Expect(hd>0,"HD","Ningún sprite de capa a 128 px por unidad");
            Expect(GridHash()==gridOriginal,"HD","La grilla (colisiones) cambió al pasar a HD");
            Expect(pieces.Count==piecesOriginal.Count,"HD","Cantidad de sprites de capa: "+piecesOriginal.Count+" → "+pieces.Count);
            int moved=0,resized=0;
            for(int i=0;i<Math.Min(pieces.Count,piecesOriginal.Count);i++) {
                if((pieces[i].position-piecesOriginal[i].position).sqrMagnitude>1e-6f)moved++;
                if((pieces[i].size-piecesOriginal[i].size).sqrMagnitude>1e-6f)resized++;
            }
            Expect(moved==0,"HD",moved+" sprites de capa cambiaron de posición");
            Expect(resized==0,"HD",resized+" sprites de capa cambiaron de tamaño en el mundo");
            Note("HD",world.HDTextureCount+" atlas HD; "+hd+"/"+pieces.Count+" sprites de capa en HD");
        });
        Shot("hd_on_luz_original");
        Add("HD",.1,()=>AOLighting2DV283.SetEnhanced(true));
        Add("HD",1.2,()=>{});
        Shot("hd_on_luz_mejorada");
        Add("QA",.5,()=>{AOWorldManagerV07.SetHDTextures(hdBefore);AOLighting2DV283.SetEnhanced(enhancedBefore);});
        Add("QA",.6,()=>Expect(GridHash()==gridOriginal,"QA","La grilla cambió al restaurar el modo inicial"));
    }

    static string GridHash()
    {
        var grid=player.CurrentGrid;var hash=new System.Text.StringBuilder();long sum=17;
        for(int y=1;y<=100;y++)for(int x=1;x<=100;x++) if(grid.InBounds(x,y))sum=unchecked(sum*31+grid.GetFlags(x,y));
        return sum.ToString();
    }

    // Map layer sprites (Layer_1..4 under the grid's GameObject), ordered by layer and position.
    static List<Piece> Pieces()
    {
        var root=player.CurrentGrid.transform;var list=new List<Piece>();
        for(int layer=1;layer<=4;layer++) {
            var t=root.Find("Layer_"+layer);if(t==null)continue;
            foreach(var r in t.GetComponentsInChildren<SpriteRenderer>(false)) {
                if(r.sprite==null)continue;
                list.Add(new Piece{layer=layer,position=r.transform.position,size=r.bounds.size,ppu=r.sprite.pixelsPerUnit});
            }
        }
        return list.OrderBy(p=>p.layer).ThenBy(p=>Mathf.Round(p.position.y*1000f)).ThenBy(p=>Mathf.Round(p.position.x*1000f)).ThenBy(p=>p.position.z).ToList();
    }

    static void WriteReport(Result result)
    {
        Directory.CreateDirectory(Path.Combine(Root,"MigrationReports"));
        File.WriteAllText(Path.Combine(Root,"MigrationReports","hd_v288.json"),JsonUtility.ToJson(result,true));
    }
    static void Finish()
    {
        EditorApplication.update-=Tick;Application.logMessageReceived-=Log;SessionState.EraseBool(RunningKey);
        try{AOWorldManagerV07.SetHDTextures(hdBefore);AOLighting2DV283.SetEnhanced(enhancedBefore);}catch{}
        var all=failures.Concat(errors.Select(e=>"Log: "+e)).ToList();
        var result=new Result{passed=all.Count==0,step=index,message=all.Count==0?"":all[0],failures=failures.ToArray(),notes=notes.ToArray(),errors=errors.ToArray(),shots=shots.ToArray(),
            vx=viewport.x,vy=viewport.y,vw=viewport.width,vh=viewport.height};
        WriteReport(result);
        if(File.Exists(Flag))File.Delete(Flag);
        Debug.Log("AO HD QA: "+(result.passed?"PASS":"FAIL "+all.Count+" fallas")+" "+result.message);
        EditorApplication.isPlaying=false;
    }
}
#endif
