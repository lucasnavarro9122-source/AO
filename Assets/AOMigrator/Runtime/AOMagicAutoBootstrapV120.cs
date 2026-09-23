using UnityEngine;

public static class AOMagicAutoBootstrapV120
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureMagic(){
        AOTestPlayer p=Object.FindFirstObjectByType<AOTestPlayer>();if(p==null){Debug.LogWarning("[AO v0.12.9] No encontré AO Test Player.");return;}
        if(p.GetComponent<AOPlayerMagicStatusV120>()==null)p.gameObject.AddComponent<AOPlayerMagicStatusV120>();
        if(p.GetComponent<AOMagicEffectRuntimeV129>()==null)p.gameObject.AddComponent<AOMagicEffectRuntimeV129>();
        if(p.GetComponent<AOPlayerMagicV120>()==null)p.gameObject.AddComponent<AOPlayerMagicV120>();
        Debug.Log("[AO v0.12.9] Magia local completa activa: pergaminos, meditación, EOT, invocación, materialización y portal.");
    }
}
