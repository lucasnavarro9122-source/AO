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

// Explicit marker only (Temp/run_interface_qa). Duel UI V272 (INGRESAR demo panel, form validations,
// challenge, invitation, spectator, errors) with the simulated backend, plus hotbar V261 (right click,
// tooltip text). Screenshots at 16:9 and 4:3. Temporary preferences, isolated online client and
// protected saves (see Tools/test_interface_unity.py).
public static class AOInterfaceQA274
{
    static string Root=>Path.GetFullPath(Path.Combine(Application.dataPath,".."));
    static string Flag=>Path.Combine(Root,"Temp","run_interface_qa");
    static string StopFlag=>Path.Combine(Root,"Temp","stop_interface_qa");
    static string ShotsDir=>Path.Combine(Root,"MigrationReports","interface_v274");
    const string RunningKey="AOInterfaceQA274.Running";
    const string TestCharacter="RetosPrueba";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly int[][] Aspects={new[]{1600,900},new[]{1024,768}};
    static readonly string[] AspectTags={"169","43"};

    class Step {public string module;public double delay,timeout=20;public Func<bool> ready;public string waitingFor;public Action action;}
    static readonly List<Step> steps=new List<Step>();
    static int index;
    static double started,next,stepSince;
    static readonly List<string> failures=new List<string>();
    static readonly List<string> notes=new List<string>();
    static readonly List<string> errors=new List<string>();
    static readonly List<string> shots=new List<string>();
    static EditorWindow testView;
    static bool previousMaximized,resolutionMissing;
    static object previousSizeIndex;
    static AOTestPlayer player;
    static AOInterfaceV0101 ui;
    static AOActionBarV260 controls;
    static AOActionBarDragDropV261 drag;
    static AOPlayerMagicV120 magic;
    static Mouse testMouse,previousMouse;
    static InputSettings previousInputSettings,testInputSettings;

    [Serializable] class Result {public bool passed;public int step;public string message;public string[] failures,notes,errors,shots;}

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Protect()
    {
        if(!File.Exists(Flag))return;
        AOPlayerSettingsV230.TestPrefixOverride="AO.InterfaceQA."+Guid.NewGuid().ToString("N")+".";
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
            previousSizeIndex=SizeIndexProperty()?.GetValue(testView);
            failures.Clear();notes.Clear();errors.Clear();shots.Clear();resolutionMissing=false;
            player=null;ui=null;controls=null;drag=null;magic=null;
            Plan();index=0;started=EditorApplication.timeSinceStartup;next=started+4;stepSince=next;
            EditorApplication.update+=Tick;Application.logMessageReceived+=Log;
        };
    }
    static void AbortAfterReload()
    {
        AOPlayerSettingsV230.TestPrefixOverride="AO.InterfaceQA."+Guid.NewGuid().ToString("N")+".";
        AOOnlineClientV240.BeginIsolatedTest(0);
        SessionState.EraseBool(RunningKey);
        if(File.Exists(StopFlag))File.Delete(StopFlag);
        if(File.Exists(Flag)) {
            string message="QA: recarga de scripts durante la prueba; se protegió el guardado y se detuvo Play";
            WriteReport(new Result{passed=false,step=-1,message=message,failures=new[]{message},notes=new string[0],errors=new string[0],shots=new string[0]});
            File.Delete(Flag);
        }
        Debug.LogWarning("AO Interface QA: recarga de scripts en Play; guardado protegido y Play detenido.");
        EditorApplication.delayCall+=()=>{RestoreInputAfterReload();EditorApplication.isPlaying=false;};
    }
    static void RestoreInputAfterReload()
    {
        try {
            foreach(var device in InputSystem.devices.Where(d=>d.name.StartsWith("AOInterfaceQAMouse")).ToArray())InputSystem.RemoveDevice(device);
            if(EditorBuildSettings.TryGetConfigObject("com.unity.input.settings",out InputSettings asset)&&asset!=null)InputSystem.settings=asset;
            else InputSystem.settings=ScriptableObject.CreateInstance<InputSettings>();
        } catch(Exception error){Debug.LogWarning("AO Interface QA: no pude restaurar el Input System: "+error.Message);}
    }
    static void Log(string message,string trace,LogType type)
    {if((type==LogType.Error||type==LogType.Exception)&&errors.Count<10)errors.Add(message+" | "+trace.Split('\n')[0]);}
    static void Expect(bool ok,string module,string message){if(!ok)failures.Add(module+": "+message);}
    static void Note(string module,string message)=>notes.Add(module+": "+message);

    // ---- Step runner
    static void Add(string module,double delay,Action action)=>steps.Add(new Step{module=module,delay=delay,action=action});
    static void WaitFor(string module,string what,Func<bool> ready,double timeout,Action action)=>
        steps.Add(new Step{module=module,delay=.1,ready=ready,waitingFor=what,timeout=timeout,action=action});
    static void Shots(string name,Action<int,int> layout=null)
    {
        for(int i=0;i<Aspects.Length;i++) {
            int w=Aspects[i][0],h=Aspects[i][1];string file=name+"_"+AspectTags[i]+".png";
            Add("Captura",.1,()=>{if(!SetResolution(w,h))resolutionMissing=true;});
            Add("Captura",.5,()=>{
                if(layout!=null)layout(w,h);
                Directory.CreateDirectory(ShotsDir);ScreenCapture.CaptureScreenshot(Path.Combine(ShotsDir,file));shots.Add(file);
            });
        }
        Add("Captura",.3,()=>{});
    }
    static void Tick()
    {
        double now=EditorApplication.timeSinceStartup;if(now<next)return;
        if(!EditorApplication.isPlaying){failures.Add("QA: Play detenido");Finish();return;}
        if(now-started>300){failures.Add("QA: tiempo agotado en el paso "+index);Finish();return;}
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

    // ---- Plan
    static void Plan()
    {
        steps.Clear();
        WaitFor("QA","mundo cargado y personaje",()=>{
            var world=UnityEngine.Object.FindFirstObjectByType<AOWorldManagerV07>();
            return world!=null&&!world.IsLoading&&UnityEngine.Object.FindFirstObjectByType<AOSaveGameV140>()!=null&&UnityEngine.Object.FindFirstObjectByType<AOTestPlayer>()!=null;
        },60,()=>{
            if(!AOOnlineClientV240.ProtectLocalSave)throw new Exception("guardado original desprotegido, no se prueba");
            player=UnityEngine.Object.FindFirstObjectByType<AOTestPlayer>();
            OpenDemoEntrance();
        });
        Add("Menú",.3,CheckEntranceTextsFit);
        Shots("menu_demo");
        Add("Menú",.1,()=>{SetMenu(false);PrepareCharacter();});
        Add("QA",3,SetupRefs);
        Add("V261",.2,TestHotbar);

        // Retos sin backend: el formulario no abre.
        Add("V272",.1,()=>{AOInterfaceDuelSimV272.Deactivate();ClearChat();Command("/RETAR Pepe");});
        Add("V272",.5,()=>{
            Expect(TopDialog()!="Retos","V272","El formulario abrió sin backend");
            Expect(ChatHas("Los retos se juegan en la Demo"),"V272","Sin aviso de que los retos son de la demo");
        });

        // Formulario: /RETAR, validaciones y envío (reto amistoso contra Pepe).
        Add("V272",.1,()=>{AOInterfaceDuelSimV272.Activate();Command("/RETAR Pepe");});
        Add("V272",.5,CheckFormOpen);
        Shots("form");
        Add("V272",.1,FormValidations);
        Shots("form_error");
        Add("V272",.1,SubmitValidForm);
        Shots("pending",(w,h)=>CheckLayout("reto enviado",w,h,false,false,true,false));
        WaitFor("V272","conteo de la ronda 1",()=>AODuelUI.CountdownActive,10,()=>{
            Expect(AODuelUI.InDuel&&AODuelUI.Round==1&&AODuelUI.Ring==1,"V272","Estado del reto al empezar: ronda "+AODuelUI.Round+", sala "+AODuelUI.Ring);
            Expect(AODuelUI.CountdownSecondsLeft>=1&&AODuelUI.CountdownSecondsLeft<=5,"V272","Conteo fuera de rango: "+AODuelUI.CountdownSecondsLeft);
            Expect(AODuelUI.TeamA.Contains(TestCharacter)&&AODuelUI.TeamB.Contains("Pepe"),"V272","Equipos mal armados");
            Expect(ChatHas("Retos » Sala 1"),"V272","Sin anuncio de la sala en el chat");
        });
        Shots("countdown",(w,h)=>CheckLayout("conteo",w,h,true,true,false,false));
        WaitFor("V272","pelea de la ronda 1",()=>AODuelUI.Phase==AODuelPhase.Fight,10,()=>
            Expect(AODuelUI.FightSecondsLeft>0&&AODuelUI.FightSecondsLeft<=600,"V272","Reloj del reto: "+AODuelUI.FightSecondsLeft));
        Shots("fight",(w,h)=>CheckLayout("pelea",w,h,true,false,false,false));
        WaitFor("V272","cartel 'Ronda para…'",()=>AODuelUI.BannerVisible&&AODuelUI.BannerTitle.StartsWith("Ronda para"),12,()=>
            Expect(AODuelUI.ScoreA==1&&AODuelUI.ScoreB==0,"V272","Marcador tras la ronda 1: "+AODuelUI.ScoreA+"-"+AODuelUI.ScoreB));
        Shots("round",(w,h)=>CheckLayout("cartel de ronda",w,h,true,true,false,false));
        WaitFor("V272","victoria del reto enviado",()=>!AODuelUI.InDuel&&AODuelUI.BannerTitle=="¡VICTORIA!",20,()=>{
            Expect(ChatHas("Ganaste el reto."),"V272","Sin línea de victoria en el chat");
            Expect(AODuelUI.BannerDetail=="Reto amistoso","V272","Detalle del cartel: "+AODuelUI.BannerDetail);
        });
        Shots("victory",(w,h)=>CheckLayout("victoria",w,h,false,true,false,false));

        // Invitación recibida + /ACEPTAR (apuesta 10.000, gana el jugador).
        WaitFor("V272","fin del cartel de victoria",()=>!AODuelUI.BannerVisible,8,()=>{ClearChat();AOInterfaceDuelSimV272.SimulateInvite();});
        Add("V272",.3,()=>{
            var invite=AODuelUI.FindInvite("Pepe");
            Expect(invite!=null&&invite.bet==10000&&invite.teamB.Contains(TestCharacter),"V272","La invitación no llegó bien");
        });
        Shots("invite",(w,h)=>CheckLayout("invitación",w,h,false,false,true,false));
        Add("V272",.1,()=>{Command("/ACEPTAR Pepe");Expect(AODuelUI.FindInvite("Pepe")?.accepted??false,"V272","/ACEPTAR no marcó la invitación");});
        WaitFor("V272","reto aceptado en curso",()=>AODuelUI.InDuel,10,()=>
            Expect(AODuelUI.Bet==10000&&AODuelUI.TeamB.Contains(TestCharacter),"V272","Reto aceptado con datos distintos a la invitación"));
        WaitFor("V272","'Ronda para' con el nombre del jugador",()=>AODuelUI.BannerVisible&&AODuelUI.BannerTitle.StartsWith("Ronda para"),15,()=>
            Expect(AODuelUI.BannerTitle.Contains(TestCharacter),"V272","El cartel de ronda no nombra al ganador: "+AODuelUI.BannerTitle));
        WaitFor("V272","victoria del reto aceptado",()=>!AODuelUI.InDuel&&AODuelUI.BannerTitle=="¡VICTORIA!",25,()=>{
            Expect(ChatHas("Has ganado 18.000 monedas de oro."),"V272","Sin el premio en el chat (18.000 = pozo 20.000 − 10 %)");
            Expect(AODuelUI.BannerDetail.StartsWith("+18.000"),"V272","Detalle del cartel: "+AODuelUI.BannerDetail);
        });
        Shots("invite_victory",(w,h)=>CheckLayout("victoria con premio",w,h,false,true,false,false));

        // Espectador, /RETOS y error del servidor.
        WaitFor("V272","fin del cartel de victoria",()=>!AODuelUI.BannerVisible,8,()=>{ClearChat();AOInterfaceDuelSimV272.SimulateSpectator();});
        Add("V272",.3,()=>{
            var ring=AODuelUI.Rings.FirstOrDefault(r=>r.ring==3);
            Expect(ring!=null&&ring.phase=="pelea"&&ring.names=="Pepe vs Juan","V272","El estado del ring 3 no llegó");
            Expect(ChatHas("Retos » Sala 3"),"V272","Sin anuncio para espectadores");
            var tile=AODuelUI.RingLabelTile?.Invoke(3);
            Expect(tile.HasValue&&tile.Value==new Vector2Int(player.TileX,player.TileY-3),"V272","El cartel del ring no está sobre el jugador");
        });
        Shots("spectator",(w,h)=>CheckLayout("espectador",w,h,false,false,false,true));
        Add("V272",.1,()=>{ClearChat();Command("/RETOS");Expect(ChatHas("Retos activos:"),"V272","/RETOS no respondió");});
        Add("V272",.6,()=>Expect(ChatHas("Pepe vs Juan"),"V272","/RETOS no listó la sala 3"));
        Add("V272",.1,()=>{AOInterfaceDuelSimV272.SimulateError();Expect(AODuelUI.LastError.Contains("10.000"),"V272","El error del servidor no quedó registrado");});
        Shots("error");
        Add("V272",.1,AOInterfaceDuelSimV272.Deactivate);
    }

    // ---- Menu INGRESAR → DEMO AO BATTLESERVER (only the panel; no button is pressed).
    static void OpenDemoEntrance()
    {
        Expect(AOMainMenuV140.EntranceOpen,"Menú","INGRESAR no está abierto al iniciar");
        Expect(!AOMainMenuV140.BattleDemo,"Menú","BattleDemo activo antes de elegir la demo");
        SetMenu(true);
    }
    // Each INGRESAR / demo panel button is 324 px wide at the 1024×768 reference (the menu scales with GUI.matrix).
    static void CheckEntranceTextsFit()
    {
        var menu=UnityEngine.Object.FindFirstObjectByType<AOMainMenuV140>();
        var style=menu==null?null:typeof(AOMainMenuV140).GetField("entrancePrimary",Private)?.GetValue(menu) as GUIStyle;
        if(style==null){Expect(false,"Menú","No encuentro el estilo de los botones de INGRESAR");return;}
        var demo=typeof(AOMainMenuV140).GetField("entranceDemo",Private)?.GetValue(menu) as GUIStyle;
        if(demo==null){Expect(false,"Menú","No encuentro el estilo del botón DEMO AO BATTLESERVER");demo=style;}
        FitsButton(style,"JUGAR SIN CONEXIÓN");FitsButton(style,"JUGAR CON AMIGOS");FitsButton(style,"SOLO DUNGEON");
        FitsButton(demo,"DEMO AO BATTLESERVER");
    }
    static void FitsButton(GUIStyle style,string text)
    {
        float width=style.CalcSize(new GUIContent(text)).x;
        Expect(width>0f&&width<=324f,"Menú","El texto '"+text+"' no entra en su botón ("+width.ToString("0")+" px de 324)");
    }

    // ---- Layout: drawn rects from the last Repaint (AODuelUI.*Rect, chat lines, HotbarRect) must not overlap,
    // stay inside AODuelUI.ViewportRect and sit where ui.md says.
    static void CheckLayout(string state,int width,int height,bool needScoreboard,bool needCenter,bool needNotice,bool needRing)
    {
        string tag=state+" "+width+"x"+height;
        var rects=new List<KeyValuePair<string,Rect>> {
            new KeyValuePair<string,Rect>("marcador",AODuelUI.ScoreboardRect),
            new KeyValuePair<string,Rect>("centro",AODuelUI.CenterRect),
            new KeyValuePair<string,Rect>("aviso",AODuelUI.NoticeRect),
            new KeyValuePair<string,Rect>("menú de usuario",AODuelUI.UserMenuRect),
            new KeyValuePair<string,Rect>("chat",ui.ChatLinesRectGUI)
        };
        for(int i=0;i<AODuelUI.RingLabelRects.Count;i++)rects.Add(new KeyValuePair<string,Rect>("cartel de ring "+(i+1),AODuelUI.RingLabelRects[i]));
        rects.Add(new KeyValuePair<string,Rect>("hotbar",AODuelUI.HotbarRect));
        Expect(AODuelUI.HotbarRect.width>0f,"Layout",tag+": no se dibujó la hotbar");
        var drawn=rects.Where(r=>r.Value.width>0f&&r.Value.height>0f).ToList();
        for(int a=0;a<drawn.Count;a++)
            for(int b=a+1;b<drawn.Count;b++)
                Expect(!drawn[a].Value.Overlaps(drawn[b].Value),"Layout",tag+": se superponen "+drawn[a].Key+" y "+drawn[b].Key);
        if(needScoreboard)Expect(AODuelUI.ScoreboardRect.width>0f,"Layout",tag+": no se dibujó el marcador");
        if(needCenter)Expect(AODuelUI.CenterRect.width>0f,"Layout",tag+": no se dibujó el conteo o el resultado");
        if(needNotice)Expect(AODuelUI.NoticeRect.width>0f,"Layout",tag+": no se dibujó el aviso");
        if(needRing)Expect(AODuelUI.RingLabelRects.Count>0,"Layout",tag+": no se dibujó el cartel del ring");
        var view=AODuelUI.ViewportRect;
        Expect(view.width>0f&&view.x>=0f&&view.y>=0f&&view.xMax<=width+1f&&view.yMax<=height+1f,"Layout",tag+": viewport fuera de la pantalla de "+width+"x"+height+": "+view);
        if(view.width<=0f)return;
        foreach(var item in drawn.Where(r=>r.Key!="chat"&&r.Key!="hotbar"))
            Expect(view.Contains(item.Value.min)&&view.Contains(item.Value.max-Vector2.one*.5f),"Layout",tag+": "+item.Key+" se sale del viewport");
        var score=AODuelUI.ScoreboardRect;
        if(score.width>0f)Expect(Mathf.Abs(score.center.x-view.center.x)<=2f&&score.y<=view.y+view.height*.15f,"Layout",tag+": el marcador no está arriba al centro");
        var center=AODuelUI.CenterRect;
        if(center.width>0f)Expect(center.y>=view.y&&center.center.y<=view.y+view.height/3f,"Layout",tag+": el conteo o el resultado no están en el primer tercio");
        var notice=AODuelUI.NoticeRect;
        if(notice.width>0f)Expect(notice.xMax<=view.xMax+1f&&notice.xMax>=view.xMax-40f&&notice.y<=view.y+view.height*.15f,"Layout",tag+": el aviso no está arriba a la derecha");
    }
    static void SetMenu(bool demo)
    {
        var menu=UnityEngine.Object.FindFirstObjectByType<AOMainMenuV140>();
        if(menu==null){Expect(false,"Menú","No encuentro AOMainMenuV140");return;}
        typeof(AOMainMenuV140).GetField("showOnlineSetup",Private).SetValue(menu,demo);
        typeof(AOMainMenuV140).GetField("demoSetup",Private).SetValue(menu,demo);
    }
    static void PrepareCharacter()
    {
        var save=UnityEngine.Object.FindFirstObjectByType<AOSaveGameV140>();
        var data=JsonUtility.FromJson<AOSaveGameV140.SaveData>(save.CaptureOnline());
        data.character.name=TestCharacter;data.world.map=1;data.world.x=57;data.world.y=44;
        data.rpg.raceId=1;data.rpg.genderId=1;data.rpg.headIndex=1;data.rpg.classId=1;data.rpg.level=25;
        data.rpg.maxHp=100;data.rpg.mana=10000;data.rpg.stamina=1000;data.rpg.skills=Enumerable.Repeat(100,24).ToArray();data.rpg.hunger=50;
        data.combat.hp=100;data.combat.dead=false;data.combat.gold=5000;
        data.inventory.itemIndices=new int[24];data.inventory.amounts=new int[24];data.inventory.itemIndices[0]=1;data.inventory.amounts[0]=5;
        data.inventory.weapon=data.inventory.armor=data.inventory.helmet=data.inventory.shield=0;
        if(!save.ApplyOnline(JsonUtility.ToJson(data)))throw new Exception("No se pudo preparar personaje de prueba");
        AOMainMenuV140.StartSessionFromCreator();
    }
    static void SetupRefs()
    {
        if(!AOOnlineClientV240.ProtectLocalSave)throw new Exception("guardado original desprotegido");
        ui=UnityEngine.Object.FindFirstObjectByType<AOInterfaceV0101>();controls=player.GetComponent<AOActionBarV260>();
        drag=player.GetComponent<AOActionBarDragDropV261>();magic=player.GetComponent<AOPlayerMagicV120>();
        if(ui==null||controls==null||magic==null)throw new Exception("faltan la interfaz o componentes del jugador");
        Expect(AOMainMenuV140.SessionActive,"QA","La sesión no empezó");
        Expect(!AOMainMenuV140.BattleDemo,"Menú","Abrir el panel de la demo activó BattleDemo sin elegirla");
        string identity=player.GetComponent<AOCharacterIdentityV170>()?.CharacterName;
        Expect(identity==TestCharacter,"QA","El personaje de prueba no se aplicó (nombre "+identity+")");
        AOPlayerSettingsV230.Profile=AOControlProfile.MOBA;
        SetupMouse();
    }

    // ---- V261: right click clears a slot (and is blocked while input is captured), tooltip texts.
    static void TestHotbar()
    {
        int spellId=1;var spell=AOSpellDatabaseV120.Get(spellId);
        Expect(spell!=null&&magic.LearnSpell(spellId),"V261","No aprende el hechizo "+spellId);
        for(int i=0;i<4;i++){Assign(true,i,0);Assign(false,i,0);}
        Assign(true,0,spellId);Assign(false,0,1);
        string spellText=(string)typeof(AOActionBarV260).GetMethod("SpellTooltipBody",Private).Invoke(controls,new object[]{spell,spellId,"Q"});
        foreach(string part in new[]{"Tecla: Q","Maná: ","CD: ","Objetivo: "})Expect(spellText.Contains(part),"V261","Tooltip del hechizo sin '"+part+"'");
        if(AOSkillShotConfigV267.IsSkillShot(spellId))Expect(spellText.Contains("Dirección (skill shot)"),"V261","Tooltip no marca el skill shot");
        string itemText=(string)typeof(AOActionBarV260).GetMethod("ItemTooltipBody",Private).Invoke(controls,new object[]{AOItemDatabaseV10.Get(1),1,"1"});
        Expect(itemText.Contains("Tecla: 1")&&itemText.Contains("Cantidad: 5"),"V261","Tooltip del consumible: "+itemText.Replace("\n"," | "));
        Note("V261","El dibujo del tooltip usa el mouse real (IMGUI): se prueba el texto, no la captura.");
        if(drag==null){Expect(false,"V261","AOActionBarDragDropV261 no está en el jugador");return;}
        var capturedField=typeof(AOInterfaceV0101).GetField("inputCaptured",BindingFlags.Static|BindingFlags.NonPublic);
        capturedField?.SetValue(null,true);
        RightClickSlot(0);
        capturedField?.SetValue(null,false);
        Expect(Slot(true,0)==spellId,"V261","El clic derecho vació el slot con la entrada capturada (chat o ventana)");
        ClearChat();RightClickSlot(0);
        Expect(Slot(true,0)==0&&ChatHas("vaciado"),"V261","El clic derecho no vació Q");
        RightClickSlot(4);
        Expect(Slot(false,0)==0,"V261","El clic derecho no vació el consumible 1");
        ClearChat();RightClickSlot(0);
        Expect(!ChatHas("vaciado"),"V261","Vaciar un slot vacío avisa igual");
    }
    static void RightClickSlot(int index)
    {
        var gui=controls.GetSlotRectGUI(index).center;var screen=new Vector2(gui.x,Screen.height-gui.y);
        InputSystem.QueueStateEvent(testMouse,new MouseState{position=screen}.WithButton(MouseButton.Right));InputSystem.Update();testMouse.MakeCurrent();
        typeof(AOActionBarDragDropV261).GetMethod("Update",Private).Invoke(drag,null);
        InputSystem.QueueStateEvent(testMouse,new MouseState{position=screen});InputSystem.Update();testMouse.MakeCurrent();
        InputSystem.Update(); // Clear wasReleasedThisFrame before a regular frame reads it.
    }

    // ---- V272 form
    static void CheckFormOpen()
    {
        Expect(TopDialog()=="Retos","V272","/RETAR no abrió el formulario");
        var names=(string[])Field("duelNames");
        Expect(names[0]==TestCharacter&&names[1]=="Pepe","V272","Formulario precargado mal: "+names[0]+" / "+names[1]);
        Expect((int)Field("duelTeamSize")==1,"V272","Tamaño de equipo inicial: "+Field("duelTeamSize"));
    }
    static void FormValidations()
    {
        var names=(string[])Field("duelNames");
        Submit("",null,"Faltan jugadores.");
        Submit("Pepe123",null,"Nombre inválido 'Pepe123'.");
        Submit(TestCharacter.ToLowerInvariant(),null,"Hay jugadores repetidos.");
        Submit("Pepe","500","La apuesta mínima es de");
        Submit("Pepe","200000000","La apuesta máxima es de");
        Submit("Pepe","10000","No tenés oro suficiente.");
        SetField("duelPotionLimit",true);SetField("duelPotions","");
        Submit("Pepe","0","Cantidad de pociones inválida.");
        SetField("duelPotionLimit",false);
        Submit("","0","Faltan jugadores."); // Leaves the error visible for the screenshot.
        Expect(TopDialog()=="Retos","V272","Una validación cerró el formulario");
        Expect(!AODuelUI.ChallengePending,"V272","Una apuesta inválida se envió igual");
    }
    static void Submit(string rival,string bet,string expected)
    {
        var names=(string[])Field("duelNames");names[1]=rival;
        if(bet!=null)SetField("duelBet",bet);
        typeof(AOInterfaceV0101).GetMethod("SubmitDuelForm",Private).Invoke(ui,null);
        string error=(string)Field("duelFormError");
        Expect(error.StartsWith(expected),"V272","Validación '"+rival+"'/"+bet+": '"+error+"' en vez de '"+expected+"'");
    }
    static void SubmitValidForm()
    {
        ClearChat();
        var names=(string[])Field("duelNames");names[1]="Pepe";SetField("duelBet","0");SetField("duelPotionLimit",false);
        typeof(AOInterfaceV0101).GetMethod("SubmitDuelForm",Private).Invoke(ui,null);
        Expect(TopDialog()!="Retos"&&AODuelUI.ChallengePending,"V272","El formulario válido no se envió: '"+Field("duelFormError")+"'");
        Expect(ChatHas("Has enviado una solicitud"),"V272","Sin confirmación del envío en el chat");
    }

    // ---- Helpers
    static object Field(string name)=>typeof(AOInterfaceV0101).GetField(name,Private).GetValue(ui);
    static void SetField(string name,object value)=>typeof(AOInterfaceV0101).GetField(name,Private).SetValue(ui,value);
    static string TopDialog()=>Field("topDialog")?.ToString()??"";
    static void Command(string text)
    {
        bool handled=(bool)typeof(AOInterfaceV0101).GetMethod("TryHandleDuelCommand",Private).Invoke(ui,new object[]{text});
        Expect(handled,"V272","El chat no reconoce "+text);
    }
    static List<string> Chat=>typeof(AOInterfaceV0101).GetField("chat",Private)?.GetValue(ui) as List<string>;
    static void ClearChat()=>Chat?.Clear();
    static bool ChatHas(string text)=>Chat!=null&&Chat.Any(m=>m!=null&&m.Contains(text));
    static void Assign(bool spell,int slot,int id)=>AOPlayerSettingsV230.AssignSlot(TestCharacter,spell,slot,id);
    static int Slot(bool spell,int slot)=>AOPlayerSettingsV230.SlotAssignment(TestCharacter,spell,slot);

    static PropertyInfo SizeIndexProperty()=>testView?.GetType().GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
    static bool SetResolution(int width,int height)
    {
        try {
            var type=typeof(EditorWindow).Assembly.GetType("UnityEditor.PlayModeWindow");
            var method=type?.GetMethod("SetCustomRenderingResolution",BindingFlags.Public|BindingFlags.Static);
            if(method==null)return false;
            method.Invoke(null,new object[]{(uint)width,(uint)height,"AO QA "+width+"x"+height});
            return true;
        } catch(Exception error){Note("Captura","No pude cambiar la resolución: "+error.Message);return false;}
    }
    static void SetupMouse()
    {
        previousInputSettings=InputSystem.settings;
        testInputSettings=UnityEngine.Object.Instantiate(previousInputSettings);
        testInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        testInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        testInputSettings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;
        InputSystem.settings=testInputSettings;
        previousMouse=Mouse.current;testMouse=InputSystem.AddDevice<Mouse>("AOInterfaceQAMouse");testMouse.MakeCurrent();
    }
    static void WriteReport(Result result)
    {
        Directory.CreateDirectory(Path.Combine(Root,"MigrationReports"));
        File.WriteAllText(Path.Combine(Root,"MigrationReports","interface_v274.json"),JsonUtility.ToJson(result,true));
    }
    static void Finish()
    {
        EditorApplication.update-=Tick;Application.logMessageReceived-=Log;SessionState.EraseBool(RunningKey);
        try{AOInterfaceDuelSimV272.Deactivate();}catch{}
        if(testMouse!=null&&testMouse.added)InputSystem.RemoveDevice(testMouse);
        testMouse=null;if(previousMouse!=null&&previousMouse.added)previousMouse.MakeCurrent();
        if(previousInputSettings!=null)InputSystem.settings=previousInputSettings;
        if(testInputSettings!=null)UnityEngine.Object.DestroyImmediate(testInputSettings);
        previousInputSettings=testInputSettings=null;
        try{if(previousSizeIndex!=null)SizeIndexProperty()?.SetValue(testView,previousSizeIndex);}catch(Exception error){notes.Add("Captura: no pude restaurar la resolución del Game view: "+error.Message);}
        if(resolutionMissing)notes.Add("Captura: sin PlayModeWindow.SetCustomRenderingResolution; las capturas 16:9 y 4:3 salen con la resolución actual.");
        var all=failures.Concat(errors.Select(e=>"Log: "+e)).ToList();
        var result=new Result{passed=all.Count==0,step=index,message=all.Count==0?"":all[0],failures=failures.ToArray(),notes=notes.ToArray(),errors=errors.ToArray(),shots=shots.ToArray()};
        WriteReport(result);
        if(File.Exists(Flag))File.Delete(Flag);
        Debug.Log("AO Interface QA: "+(result.passed?"PASS":"FAIL "+all.Count+" fallas")+" "+result.message);
        if(testView!=null)testView.maximized=previousMaximized;
        EditorApplication.isPlaying=false;
    }
}
#endif
