using UnityEngine;

public static class AODeathRespawnBootstrapV160
{
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureDeathSystem()
    {
        AOTestPlayer player =
            UnityEngine.Object
                .FindFirstObjectByType
                    <AOTestPlayer>();

        if (player == null)
            return;

        if (player.GetComponent
                <AODeathRespawnV160>() ==
            null)
        {
            player.gameObject
                .AddComponent
                    <AODeathRespawnV160>();
        }

        Debug.Log(
            "[AO v0.16] Muerte AO activa: " +
            "BODY829, sin respawn automático, /HOGAR=105s.");
    }
}
