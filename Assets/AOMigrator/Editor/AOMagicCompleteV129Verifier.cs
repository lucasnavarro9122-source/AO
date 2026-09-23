using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AOMagicCompleteV129Verifier
{
    const string Diagnostic="Assets/AOMigrator/magic_v129_diagnostic.json";

    [Serializable] class Report {
        public string version="0.12.9-local"; public string unity; public bool success; public string message;
        public int spells,declared,effects,magicTextures,magicAudio,worldTextures; public bool map281,itemDb,player,rpg,combat,magic,status,effectRuntime;
    }

    [MenuItem("AO Migrador/Finalizar magia v0.12.9 - instalar/verificar")]
    public static void Verify(){
        Report r=new Report();
        try{
            ConfigureFolder("Assets/Resources/AOMigrator/MagicV129/Textures");
            ConfigureFolder("Assets/Resources/AOMigrator/WorldV07/Textures");
            ConfigureFolder("Assets/Resources/AOMigrator/MinimapsV0103");

            r.unity=Application.unityVersion;r.spells=AOSpellDatabaseV120.Count;r.declared=AOSpellDatabaseV120.DeclaredSlots;
            r.effects=CountJsonEntries("Assets/Resources/AOMigrator/MagicV129/effects.json","\"id\"");
            r.magicTextures=CountFiles("Assets/Resources/AOMigrator/MagicV129/Textures","tex_*.png");
            r.magicAudio=CountFiles("Assets/Resources/AOMigrator/MagicV129/Audio","wav_*.wav");
            r.worldTextures=CountFiles("Assets/Resources/AOMigrator/WorldV07/Textures","tex_*.png");
            r.map281=File.Exists("Assets/Resources/AOMigrator/WorldV07/Maps/map_281.json");
            r.itemDb=File.Exists("Assets/Resources/AOMigrator/ItemsV10/items.json");

            AOTestPlayer p=UnityEngine.Object.FindFirstObjectByType<AOTestPlayer>();r.player=p!=null;
            if(p!=null){r.rpg=p.GetComponent<AOPlayerRPGV11>()!=null;r.combat=p.GetComponent<AOPlayerCombatV09>()!=null;r.magic=p.GetComponent<AOPlayerMagicV120>()!=null;r.status=p.GetComponent<AOPlayerMagicStatusV120>()!=null;r.effectRuntime=p.GetComponent<AOMagicEffectRuntimeV129>()!=null;}

            r.success=r.spells>=140&&r.declared==293&&r.effects>=60&&r.magicTextures>0&&r.map281&&r.itemDb;
            r.message="Spells="+r.spells+"/"+r.declared+" EOT="+r.effects+" MagicTex="+r.magicTextures+" WAV="+r.magicAudio+" Map281="+r.map281+
                "\nPlayer="+r.player+" RPG="+r.rpg+" Combat="+r.combat+" Magic="+r.magic+" Status="+r.status+" Effects="+r.effectRuntime+
                "\n(Components runtime aparecen después de entrar en Play.)";
            File.WriteAllText(Diagnostic,JsonUtility.ToJson(r,true));AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("AO Magia v0.12.9",(r.success?"OK\n\n":"Revisar\n\n")+r.message+"\n\nPlay -> F12 -> HECHIZOS. Doble click a pergaminos para aprender.","OK");
        }catch(Exception e){r.success=false;r.message=e.ToString();File.WriteAllText(Diagnostic,JsonUtility.ToJson(r,true));AssetDatabase.Refresh();Debug.LogError(e);}
        finally{EditorUtility.ClearProgressBar();}
    }

    [MenuItem("AO Migrador/Magia v0.12.9 - borrar spellbook local")]
    public static void ClearSpellbook(){
        string path=Path.Combine(Application.persistentDataPath,"ao_magic_v129_spellbook.json");
        if(File.Exists(path))File.Delete(path);
        EditorUtility.DisplayDialog("AO Magia v0.12.9","Spellbook local borrado. Volvé a entrar en Play.","OK");
    }

    static int CountFiles(string folder,string pattern){return Directory.Exists(folder)?Directory.GetFiles(folder,pattern,SearchOption.TopDirectoryOnly).Length:0;}
    static int CountJsonEntries(string file,string marker){if(!File.Exists(file))return 0;string t=File.ReadAllText(file);int n=0,pos=0;while((pos=t.IndexOf(marker,pos,StringComparison.Ordinal))>=0){n++;pos+=marker.Length;}return n;}

    static void ConfigureFolder(string folder){
        if(!Directory.Exists(folder))return;string[] files=Directory.GetFiles(folder,"*.png",SearchOption.TopDirectoryOnly);
        for(int i=0;i<files.Length;i++){if(i%20==0)EditorUtility.DisplayProgressBar("AO v0.12.9","Configurando texturas "+(i+1)+"/"+files.Length,files.Length==0?1f:(float)i/files.Length);
            string p=files[i].Replace("\\","/");int idx=p.IndexOf("Assets/",StringComparison.OrdinalIgnoreCase);if(idx>=0)p=p.Substring(idx);
            AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceSynchronousImport);TextureImporter im=AssetImporter.GetAtPath(p) as TextureImporter;if(im==null)continue;
            im.textureType=TextureImporterType.Default;im.mipmapEnabled=false;im.textureCompression=TextureImporterCompression.Uncompressed;im.filterMode=FilterMode.Point;im.wrapMode=TextureWrapMode.Clamp;im.alphaIsTransparency=true;im.npotScale=TextureImporterNPOTScale.None;im.maxTextureSize=16384;im.SaveAndReimport();
        }
        AssetDatabase.Refresh();
    }
}
