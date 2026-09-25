#if UNITY_EDITOR && UNITY_EDITOR_WIN
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using UnityEditor;
using UnityEngine;

// V280 · QA: the protected Play tests (AOControlsQA260, AOModulesQA270, AOInterfaceQA274, AODuelQA284, AOLightingQA287, AOHDQA288, AODemoSaveQA289) write their
// preferences under a temporary prefix. When Play ends (or on the Temp/qa_prefs_cleanup marker), drop the
// prefix and delete ONLY those temporary keys (AO.<Test>QA.<32 hex>.*). The player's own preferences are
// never touched. Two steps: PlayerPrefs.DeleteKey (Unity's cache) and then the registry value itself,
// because DeleteKey alone left keys written in earlier editor sessions.
[InitializeOnLoad]
public static class AOQATestPrefsV280
{
    static readonly Regex TestKey=new Regex(@"^AO\.(ControlsQA|ModulesQA|InterfaceQA|DuelQA|LightingQA|HDQA|DemoSaveQA)\.[0-9a-f]{32}\.");
    static string Root=>Path.GetFullPath(Path.Combine(Application.dataPath,".."));
    static string Marker=>Path.Combine(Root,"Temp","qa_prefs_cleanup");
    static string RegistryPath=>@"Software\Unity\UnityEditor\"+PlayerSettings.companyName+@"\"+PlayerSettings.productName;

    static AOQATestPrefsV280()
    {
        EditorApplication.playModeStateChanged+=mode=>{if(mode==PlayModeStateChange.EnteredEditMode)Cleanup();};
        EditorApplication.update+=()=>{
            if(!File.Exists(Marker)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)return;
            File.Delete(Marker);
            string result=Cleanup();
            File.WriteAllText(Path.Combine(Root,"Temp","qa_prefs_cleanup_result.txt"),result);
        };
    }

    // Unity stores each key as "<name>_h<hash>".
    static string KeyName(string value){int hash=value.LastIndexOf("_h");return hash>0?value.Substring(0,hash):value;}

    static List<string> TestValues()
    {
        var found=new List<string>();
        using(RegistryKey key=Registry.CurrentUser.OpenSubKey(RegistryPath))
            if(key!=null)foreach(string value in key.GetValueNames())if(TestKey.IsMatch(KeyName(value)))found.Add(value);
        return found;
    }

    public static string Cleanup()
    {
        AOPlayerSettingsV230.TestPrefixOverride=null;
        List<string> values=TestValues();
        if(values.Count==0)return "matched=0 remaining=0";
        foreach(string value in values)PlayerPrefs.DeleteKey(KeyName(value));
        PlayerPrefs.Save();
        List<string> left=TestValues();
        if(left.Count>0)
            using(RegistryKey key=Registry.CurrentUser.OpenSubKey(RegistryPath,true))
                if(key!=null)foreach(string value in left)key.DeleteValue(value,false);
        int remaining=TestValues().Count;
        string result="matched="+values.Count+" deleteKeyLeft="+left.Count+" remaining="+remaining;
        Debug.Log("AO QA: preferencias temporales de prueba borradas ("+result+").");
        return result;
    }
}
#endif
