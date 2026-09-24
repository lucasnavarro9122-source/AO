#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Opt-in, isolated QA. The flag carries only the temporary test room connection.
public static class AOCoopQA250
{
    [Serializable] class Config { public int port; public string key; }
    static string Root => Path.GetFullPath(Path.Combine(Application.dataPath,".."));
    static string Flag => Path.Combine(Root,"Temp","run_coop_qa.json");
    static int stage;
    static double started, next;
    static long originalGold;
    static AOSaveGameV140 save;
    static AOTestPlayer player;
    static AODoorV210 door;
    static string error;
    static Config config;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ProtectTest()
    {
        if(!File.Exists(Flag))return;
        config=JsonUtility.FromJson<Config>(File.ReadAllText(Flag));
        AOOnlineClientV240.BeginIsolatedTest(config.port);
    }
    [InitializeOnLoadMethod]
    static void Initialize()
    {
        EditorApplication.playModeStateChanged+=Mode;
        EditorApplication.update+=Poll;
    }
    static void Poll()
    {
        if(File.Exists(Flag)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling)
            EditorApplication.isPlaying=true;
    }
    static void Mode(PlayModeStateChange mode)
    {
        if(mode!=PlayModeStateChange.EnteredPlayMode||!File.Exists(Flag))return;
        stage=0;started=EditorApplication.timeSinceStartup;next=started+3;error="";
        EditorApplication.update+=Tick;
        Application.logMessageReceived+=Log;
    }
    static void Log(string message,string trace,LogType type)
    {
        if(type==LogType.Exception||type==LogType.Error)error=message+"\n"+trace;
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying){Finish(false,"Play detenido");return;}
        double now=EditorApplication.timeSinceStartup;
        if(now-started>120){Finish(false,"Tiempo agotado en etapa "+stage);return;}
        if(now<next)return;
        try
        {
            var world=UnityEngine.Object.FindFirstObjectByType<AOWorldManagerV07>();
            if(world==null||world.IsLoading)return;
            if(stage==0)
            {
                save=UnityEngine.Object.FindFirstObjectByType<AOSaveGameV140>();player=UnityEngine.Object.FindFirstObjectByType<AOTestPlayer>();
                if(save==null||player==null||string.IsNullOrEmpty(save.CaptureOnline()))return;
                var data=JsonUtility.FromJson<AOSaveGameV140.SaveData>(save.CaptureOnline());
                data.character.name="AlphaPrueba";data.world.map=1;data.world.x=57;data.world.y=44;
                data.rpg.raceId=1;data.rpg.genderId=1;data.rpg.headIndex=1;data.rpg.maxHp=100;
                data.combat.hp=100;data.combat.dead=false;data.combat.gold=200;
                data.inventory.itemIndices=new int[20];data.inventory.amounts=new int[20];
                data.inventory.itemIndices[0]=1;data.inventory.amounts[0]=2;
                data.inventory.weapon=data.inventory.armor=data.inventory.helmet=data.inventory.shield=0;
                save.ApplyOnline(JsonUtility.ToJson(data));
                if(!AOOnlineClientV240.Prepare("127.0.0.1",config.key,out string message))throw new Exception(message);
                AOMainMenuV140.StartSessionFromCreator();stage=1;next=now+3;return;
            }
            if(stage==1)
            {
                if(!AOOnlineClientV240.Connected||AOOnlineClientV240.TestRemotePlayers<1||AOOnlineClientV240.TestNpcCount<20)return;
                if(!AOOnlineClientV240.ProtectLocalSave)throw new Exception("Guardado local desprotegido");
                originalGold=player.GetComponent<AOPlayerCombatV09>().Gold;
                if(!AOOnlineClientV240.Drop(1,1))throw new Exception("Entrega no enviada");
                stage=2;next=now+2;return;
            }
            if(stage==2)
            {
                if(AOOnlineClientV240.InputBlocked)return;
                if(player.GetComponent<AOInventoryV10>().CountItem(1)!=1)throw new Exception("Inventario no recibió la entrega");
                var loot=UnityEngine.Object.FindObjectsByType<AOLootPickupV09>(FindObjectsSortMode.None).FirstOrDefault(l=>l.NetworkId>0&&l.ItemIndex==1);
                if(loot==null)throw new Exception("Botín no renderizado");
                AOOnlineClientV240.Pickup(loot);stage=3;next=now+2;return;
            }
            if(stage==3)
            {
                if(AOOnlineClientV240.InputBlocked)return;
                if(player.GetComponent<AOInventoryV10>().CountItem(1)!=2)throw new Exception("Recogida no aplicada");
                if(player.GetComponent<AOPlayerCombatV09>().Gold!=originalGold)throw new Exception("Oro cambiado sin operación");
                door=UnityEngine.Object.FindObjectsByType<AODoorV210>(FindObjectsSortMode.None).FirstOrDefault(d=>d.TileX==72&&d.TileY==35);
                if(door==null)throw new Exception("Falta puerta de prueba");
                world.MagicTeleport(1,72,36,out _);stage=4;next=now+2;return;
            }
            if(stage==4)
            {
                door=UnityEngine.Object.FindObjectsByType<AODoorV210>(FindObjectsSortMode.None).First(d=>d.TileX==72&&d.TileY==35);
                AOOnlineClientV240.ToggleDoor(door);stage=5;next=now+2;return;
            }
            if(stage==5)
            {
                if(!door.IsOpen)throw new Exception("Puerta no sincronizada");
                world.MagicTeleport(1,57,44,out _);stage=6;next=now+2;return;
            }
            if(stage==6)
            {
                AOOnlineClientV240.TestShowGroup();
                ScreenCapture.CaptureScreenshot(Path.Combine(Root,"MigrationReports","unity_coop_v250.png"));
                stage=7;next=now+2;return;
            }
            Finish(string.IsNullOrEmpty(error),error);
        }
        catch(Exception ex){Finish(false,ex.ToString());}
    }
    static void Finish(bool ok,string message)
    {
        EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
        File.WriteAllText(Path.Combine(Root,"MigrationReports","unity_coop_v250.json"),
            "{\"passed\":"+(ok?"true":"false")+",\"stage\":"+stage+",\"details\":"+JsonUtility.ToJson(new TextResult {text=message})+"}");
        if(File.Exists(Flag))File.Delete(Flag);
        Debug.Log("AO cooperative QA: "+(ok?"PASS":"FAIL")+" "+message);
        EditorApplication.isPlaying=false;
    }
    [Serializable] class TextResult { public string text; }
}
#endif
