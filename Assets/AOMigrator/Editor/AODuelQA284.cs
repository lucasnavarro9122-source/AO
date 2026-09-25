#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Explicit marker only (Temp/run_duel_qa.json, written by Tools/test_duel_unity.py with an isolated
// `--test --demo` server). R-2: Unity fights a protocol bot (challenge, countdown, ring layout, 3 rounds,
// prize). R-3: Unity watches two bots from outside the ring (ring state, announce, layout, cannot enter
// or attack). Temporary preferences, isolated online client and protected saves.
public static class AODuelQA284
{
    [Serializable] class Config {public int port;public string key;}
    [Serializable] class Bridge {public int seed;public int sala;public int[] fighters;}
    [Serializable] class Result {public bool passed;public int step;public string message;public string[] failures,notes,errors,shots;}
    class Step {public string module;public double delay,timeout=30;public Func<bool> ready;public string waitingFor;public Action action;}

    static string Root=>Path.GetFullPath(Path.Combine(Application.dataPath,".."));
    static string Flag=>Path.Combine(Root,"Temp","run_duel_qa.json");
    static string StopFlag=>Path.Combine(Root,"Temp","stop_duel_qa");
    static string BridgeFile=>Path.Combine(Root,"Temp","duel_qa_bridge.json");
    static string ProgressFile=>Path.Combine(Root,"Temp","duel_qa_progress.txt");
    static string ShotsDir=>Path.Combine(Root,"MigrationReports","duel_v284");
    const string RunningKey="AODuelQA284.Running";
    const string TestCharacter="UnityPrueba";
    const string Bot="Pepe";
    const int Bet=1000;
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly Vector2Int Plaza=new Vector2Int(50,60);

    static readonly List<Step> steps=new List<Step>();
    static int index,attacks;
    static double started,next,stepSince,nextAttack;
    static readonly List<string> failures=new List<string>(),notes=new List<string>(),errors=new List<string>(),shots=new List<string>();
    static Config config;
    static AOTestPlayer player;
    static AOPlayerCombatV09 combat;
    static AOInterfaceV0101 ui;
    static long goldBefore;
    static bool sawDown,placed;
    static int spectatorRing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Protect()
    {
        if(!File.Exists(Flag))return;
        config=JsonUtility.FromJson<Config>(File.ReadAllText(Flag));
        AOPlayerSettingsV230.TestPrefixOverride="AO.DuelQA."+Guid.NewGuid().ToString("N")+".";
        AOOnlineClientV240.BeginIsolatedTest(config.port);
    }
    [InitializeOnLoadMethod]
    static void Initialize()
    {
        // A script reload during Play resets the static save protection and drops Tick: protect again and stop Play.
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
            failures.Clear();notes.Clear();errors.Clear();shots.Clear();
            player=null;combat=null;ui=null;attacks=0;sawDown=false;placed=false;spectatorRing=0;nextAttack=0;
            Plan();index=0;started=EditorApplication.timeSinceStartup;next=started+3;stepSince=next;
            EditorApplication.update+=Tick;Application.logMessageReceived+=Log;
        };
    }
    static void AbortAfterReload()
    {
        AOPlayerSettingsV230.TestPrefixOverride="AO.DuelQA."+Guid.NewGuid().ToString("N")+".";
        AOOnlineClientV240.BeginIsolatedTest(config!=null?config.port:1);
        SessionState.EraseBool(RunningKey);
        if(File.Exists(StopFlag))File.Delete(StopFlag);
        if(File.Exists(Flag)) {
            string message="QA: recarga de scripts durante la prueba; se protegió el guardado y se detuvo Play";
            WriteReport(new Result{passed=false,step=-1,message=message,failures=new[]{message},notes=new string[0],errors=new string[0],shots=new string[0]});
            File.Delete(Flag);
        }
        Debug.LogWarning("AO Duel QA: recarga de scripts en Play; guardado protegido y Play detenido.");
        EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
    static void Log(string message,string trace,LogType type)
    {if((type==LogType.Error||type==LogType.Exception)&&errors.Count<10)errors.Add(message+" | "+trace.Split('\n')[0]);}
    static void Expect(bool ok,string module,string message){if(!ok)failures.Add(module+": "+message);}
    static void Note(string module,string message)=>notes.Add(module+": "+message);
    static void Progress(string stage){try{File.WriteAllText(ProgressFile,stage);}catch(Exception error){Note("QA","No pude escribir el progreso: "+error.Message);}}

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
        if(now-started>420){failures.Add("QA: tiempo agotado en el paso "+index+" ("+steps[Math.Min(index,steps.Count-1)].waitingFor+")");Finish();return;}
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
            var world=UnityEngine.Object.FindFirstObjectByType<AOWorldManagerV07>();
            return world!=null&&!world.IsLoading&&UnityEngine.Object.FindFirstObjectByType<AOSaveGameV140>()!=null&&UnityEngine.Object.FindFirstObjectByType<AOTestPlayer>()!=null;
        },60,StartOnlineSession);
        WaitFor("QA","conectado a la sala de prueba con el bot a la vista",()=>AOOnlineClientV240.Connected&&AOOnlineClientV240.TestRemotePlayers>=1&&!AOOnlineClientV240.InputBlocked,60,()=>{
            if(!AOOnlineClientV240.ProtectLocalSave)throw new Exception("guardado local desprotegido");
            player=UnityEngine.Object.FindFirstObjectByType<AOTestPlayer>();combat=player.GetComponent<AOPlayerCombatV09>();
            ui=UnityEngine.Object.FindFirstObjectByType<AOInterfaceV0101>();
            Expect(AOMainMenuV140.BattleDemoOnline&&AODuelUI.Available,"R-01","Los retos no están disponibles en la demo online");
            Expect(player.CurrentGrid!=null&&UnityEngine.Object.FindFirstObjectByType<AOWorldManagerV07>().CurrentMapNumber==1001,"R-01","El personaje no está en el mapa de arenas (1001)");
            goldBefore=combat.Gold;Progress("connected");
        });

        // ---- R-2: Unity (team A) challenges the bot; 3 rounds: Unity wins 1 and 3, the bot wins 2.
        Add("R-02",.5,()=>{
            ClearChat();
            Expect(AODuelUI.SendChallenge(new[]{Bot},Bet,-1,out string error),"R-02","No se pudo enviar el reto: "+error);
            Expect(AODuelUI.ChallengePending,"R-02","El reto no quedó pendiente");
        });
        WaitFor("R-02","conteo de la ronda 1",()=>AODuelUI.InDuel&&AODuelUI.CountdownActive,40,()=>{
            Expect(AODuelUI.Round==1&&AODuelUI.Bet==Bet,"R-02","Reto con datos distintos: ronda "+AODuelUI.Round+", apuesta "+AODuelUI.Bet);
            Expect(AODuelUI.TeamA.Contains(TestCharacter)&&AODuelUI.TeamB.Contains(Bot),"R-02","Equipos mal armados");
            Expect(AODuelClient.Frozen,"R-05","El jugador no queda congelado durante el conteo");
            var tile=new Vector2Int(player.TileX,player.TileY);
            player.StepFromControls(AOGridMap.EAST);player.StepFromControls(AOGridMap.NORTH);
            Expect(new Vector2Int(player.TileX,player.TileY)==tile,"R-05","El jugador se movió durante el conteo");
        });
        WaitFor("R-03","arena aplicada y jugador dentro del ring",()=>AODuelClient.Arenas.Any()&&AODuelClient.IsInsideActiveRing(player.TileX,player.TileY),10,()=>CheckArena("R-03 peleador"));
        Shot("r2_countdown");
        WaitFor("R-02","pelea de la ronda 1",()=>AODuelUI.Phase==AODuelPhase.Fight,20,()=>Expect(AODuelClient.CanFight,"R-06","CanFight sigue en falso durante la pelea"));
        Shot("r2_fight");
        WaitFor("R-06","ronda 1 ganada atacando al bot",()=>{AttackAdjacentEnemy();return AODuelUI.ScoreA>=1||AODuelUI.ScoreB>=1;},90,()=>
            Expect(AODuelUI.ScoreA==1&&AODuelUI.ScoreB==0,"R-06","Marcador tras la ronda 1: "+AODuelUI.ScoreA+"-"+AODuelUI.ScoreB+" ("+attacks+" ataques)"));
        Shot("r2_round1");
        WaitFor("R-08","ronda 2 (el bot gana)",()=>{if(AODuelUI.Down)sawDown=true;return AODuelUI.ScoreB>=1||!AODuelUI.InDuel;},90,()=>{
            Expect(AODuelUI.ScoreB==1,"R-08","Marcador tras la ronda 2: "+AODuelUI.ScoreA+"-"+AODuelUI.ScoreB);
            if(!sawDown)Note("R-08","El aviso de 'caído' no llegó antes del fin de la ronda 2 (evento del diario tardío)");
            Expect(ChatHas("Ronda")||AODuelUI.BannerTitle.StartsWith("Ronda para"),"R-08","Sin aviso de ronda perdida");
        });
        // What the player needs: after losing a round, the next one starts standing and able to fight.
        WaitFor("R-08","pelea de la ronda 3",()=>AODuelUI.Round==3&&AODuelUI.Phase==AODuelPhase.Fight,40,()=>{});
        Add("R-08",1.5,()=>Expect(!AODuelUI.Down&&AODuelClient.CanFight,"R-08",
            "En la ronda 3 el jugador sigue 'caído' (Down="+AODuelUI.Down+", CanFight="+AODuelClient.CanFight+"): el 'duelHurt' de la ronda 2 llegó tarde"));
        WaitFor("R-10","ronda 3 y fin con victoria",()=>{AttackAdjacentEnemy();return !AODuelUI.InDuel;},120,()=>{
            Expect(AODuelUI.BannerTitle=="¡VICTORIA!","R-10","Cartel final: '"+AODuelUI.BannerTitle+"'");
            Expect(ChatHas("Has ganado 1.800 monedas de oro."),"R-10","Sin el premio en el chat (pozo 2.000 − 10 %)");
        });
        Shot("r2_victory");
        WaitFor("R-10","oro del servidor y salida del ring",()=>combat.Gold!=goldBefore&&!AODuelClient.IsInsideActiveRing(player.TileX,player.TileY),20,()=>{
            Expect(combat.Gold==goldBefore+800,"R-10","Oro: "+goldBefore+" → "+combat.Gold+" (esperado +800 = −1.000 + 1.800)");
            Expect(!AODuelClient.Arenas.Any(),"R-10","La arena del reto sigue aplicada en la grilla");
        });

        // ---- R-3: the bots fight in whatever ring the server picks; the spectator stands just below it.
        Add("R-07",.3,()=>{ClearChat();Progress("r2_done");});
        WaitFor("R-07","reto Pepe vs Juan y su arena aplicada",()=>{
            var bridge=ReadBridge();
            return bridge?.fighters!=null&&bridge.fighters.Length==2&&AODuelClient.Arenas.Any(a=>a.Ring==bridge.sala);
        },90,()=>{
            var bridge=ReadBridge();var arena=AODuelClient.Arenas.First(a=>a.Ring==bridge.sala);
            spectatorRing=bridge.sala;
            Expect(!AODuelUI.InDuel,"R-07","El espectador figura como peleador");
            CheckArena("R-03 espectador");
            var world=UnityEngine.Object.FindFirstObjectByType<AOWorldManagerV07>();
            // Stands first (above the ring, where the ring label is drawn: OriginY-2), then below it.
            int cx=arena.OriginX+AOArenaGen.W/2;
            var spots=new List<Vector2Int>();
            foreach(int dy in new[]{-3,-4,-5})foreach(int dx in new[]{2,-2,4,-4})spots.Add(new Vector2Int(cx+dx,arena.OriginY+dy));
            for(int below=1;below<=4;below++)spots.Add(new Vector2Int(cx,arena.OriginY+AOArenaGen.H+below));
            foreach(var spot in spots) {
                if(placed)break;
                placed=world.MagicTeleport(1001,spot.x,spot.y,out _)&&player.TileX==spot.x&&player.TileY==spot.y;
                if(placed)Note("R-07","Espectador en "+spot+(spot.y<arena.OriginY?" (grada de arriba)":" (abajo del ring)"));
            }
            Expect(placed,"R-07","No pude parar al espectador debajo del ring "+bridge.sala);
        });
        Add("R-07",1.5,()=>{
            Expect(!AODuelClient.IsInsideActiveRing(player.TileX,player.TileY),"R-07","El espectador quedó dentro del ring");
            Progress("spectator_ready");
        });
        WaitFor("R-07","anuncio de la pelea en el chat (Pepe vs Juan)",()=>ChatHas("Pepe")&&ChatHas("Juan"),8,()=>{});
        WaitFor("R-07","ring del reto en conteo o pelea",()=>AODuelUI.Rings.Any(r=>r.ring==spectatorRing&&(r.phase=="countdown"||r.phase=="fight")),30,()=>{});
        Shot("r3_countdown");
        Add("R-07",.2,()=>{
            for(int i=0;i<3;i++)player.StepFromControls(AOGridMap.NORTH);
            Expect(!AODuelClient.IsInsideActiveRing(player.TileX,player.TileY),"R-07","El espectador entró al ring");
            var bridge=ReadBridge();
            if(bridge?.fighters==null||bridge.fighters.Length==0){Note("R-07","Sin ids de los peleadores para probar el ataque del espectador");return;}
            ClearChat();AOOnlineClientV240.AttackPlayer(bridge.fighters[0]);
        });
        WaitFor("R-07","rechazo del ataque del espectador",()=>ChatHas("atacar")||ChatHas("reto"),6,()=>{});
        WaitFor("R-07","pelea en el ring del reto",()=>AODuelUI.Rings.Any(r=>r.ring==spectatorRing&&r.phase=="fight"),30,()=>{});
        Shot("r3_fight");
        Add("R-07",.1,()=>Expect(AODuelUI.RingLabelRects.Count>0,"R-07","No se dibujó el cartel del ring para el espectador (RingLabelTile)"));
        WaitFor("R-07","anuncio del ganador",()=>ChatHas("venció"),120,()=>{});
        Add("QA",.5,()=>Progress("done"));
    }

    static void StartOnlineSession()
    {
        if(!AOOnlineClientV240.ProtectLocalSave)throw new Exception("guardado original desprotegido, no se prueba");
        var menu=UnityEngine.Object.FindFirstObjectByType<AOMainMenuV140>();
        if(menu==null)throw new Exception("No encuentro AOMainMenuV140");
        typeof(AOMainMenuV140).GetMethod("SetBattleDemo",Private).Invoke(menu,new object[]{true,true});
        var save=UnityEngine.Object.FindFirstObjectByType<AOSaveGameV140>();
        var data=JsonUtility.FromJson<AOSaveGameV140.SaveData>(save.CaptureOnline());
        data.character.name=TestCharacter;data.world.map=1001;data.world.x=Plaza.x;data.world.y=Plaza.y;
        data.rpg.raceId=1;data.rpg.genderId=1;data.rpg.headIndex=1;data.rpg.classId=1;data.rpg.level=25;
        data.rpg.maxHp=100;data.rpg.mana=10000;data.rpg.stamina=1000;data.rpg.skills=Enumerable.Repeat(100,24).ToArray();data.rpg.hunger=50;
        data.combat.hp=100;data.combat.dead=false;data.combat.gold=5000;
        data.inventory.itemIndices=new int[24];data.inventory.amounts=new int[24];
        data.inventory.weapon=data.inventory.armor=data.inventory.helmet=data.inventory.shield=0;
        if(!save.ApplyOnline(JsonUtility.ToJson(data)))throw new Exception("No se pudo preparar personaje de prueba");
        if(!AOOnlineClientV240.Prepare("127.0.0.1",config.key,out string message))throw new Exception(message);
        AOMainMenuV140.StartSessionFromCreator();
    }

    // Faces and hits the rival standing next to the player (the bot walks to us), at most every 0.8 s.
    static void AttackAdjacentEnemy()
    {
        double now=EditorApplication.timeSinceStartup;
        if(now<nextAttack||!AODuelClient.CanFight||AOOnlineClientV240.InputBlocked)return;
        int[] dx={0,1,0,-1},dy={-1,0,1,0};int[] headings={AOGridMap.NORTH,AOGridMap.EAST,AOGridMap.SOUTH,AOGridMap.WEST};
        for(int i=0;i<4;i++) {
            int id=AOOnlineClientV240.PlayerAt(player.TileX+dx[i],player.TileY+dy[i]);
            if(id<=0||!AODuelClient.IsEnemy(id))continue;
            player.FaceHeading(headings[i]);combat.AttackFromControls();attacks++;nextAttack=now+.8;return;
        }
    }

    // G-20 in the real client: the layout regenerated from the seed equals the client's and the grid flags match.
    static void CheckArena(string tag)
    {
        var bridge=ReadBridge();
        foreach(var arena in AODuelClient.Arenas) {
            var again=AOArenaGen.Generate(arena.Seed,arena.Layout.Theme);
            Expect(again.Cells.SequenceEqual(arena.Layout.Cells),tag,"El generador no es determinista para la semilla "+arena.Seed);
            if(bridge!=null&&bridge.sala==arena.Ring&&bridge.seed!=0)Expect(bridge.seed==arena.Seed,tag,"Semilla distinta a la del bot: "+arena.Seed+" vs "+bridge.seed);
            int wrong=0;
            for(int cy=0;cy<AOArenaGen.H;cy++)for(int cx=0;cx<AOArenaGen.W;cx++) {
                var cell=arena.Layout.At(cx,cy);int flags=arena.Grid.GetFlags(arena.OriginX+cx,arena.OriginY+cy);
                if(cell==AOArenaGen.Cell.Solid&&(flags&AOGridMap.FLAG_ALL_SIDES)!=AOGridMap.FLAG_ALL_SIDES)wrong++;
                if(cell==AOArenaGen.Cell.Water&&(flags&AOGridMap.FLAG_WATER)==0)wrong++;
            }
            Expect(wrong==0,tag,wrong+" celdas de la grilla no coinciden con la arena (sala "+arena.Ring+")");
            Note(tag,"sala "+arena.Ring+", semilla "+arena.Seed+", tema "+arena.Layout.Theme+", simetría "+arena.Layout.Symmetry);
        }
    }
    static Bridge ReadBridge(){try{return File.Exists(BridgeFile)?JsonUtility.FromJson<Bridge>(File.ReadAllText(BridgeFile)):null;}catch{return null;}}
    static List<string> Chat=>typeof(AOInterfaceV0101).GetField("chat",Private)?.GetValue(ui) as List<string>;
    static void ClearChat()=>Chat?.Clear();
    static bool ChatHas(string text)=>Chat!=null&&Chat.Any(m=>m!=null&&m.Contains(text));

    static void WriteReport(Result result)
    {
        Directory.CreateDirectory(Path.Combine(Root,"MigrationReports"));
        File.WriteAllText(Path.Combine(Root,"MigrationReports","duel_v284.json"),JsonUtility.ToJson(result,true));
    }
    static void Finish()
    {
        EditorApplication.update-=Tick;Application.logMessageReceived-=Log;SessionState.EraseBool(RunningKey);
        var all=failures.Concat(errors.Select(e=>"Log: "+e)).ToList();
        var result=new Result{passed=all.Count==0,step=index,message=all.Count==0?"":all[0],failures=failures.ToArray(),notes=notes.ToArray(),errors=errors.ToArray(),shots=shots.ToArray()};
        WriteReport(result);
        Progress("finished");
        if(File.Exists(Flag))File.Delete(Flag);
        Debug.Log("AO Duel QA: "+(result.passed?"PASS":"FAIL "+all.Count+" fallas")+" "+result.message);
        EditorApplication.isPlaying=false;
    }
}
#endif
