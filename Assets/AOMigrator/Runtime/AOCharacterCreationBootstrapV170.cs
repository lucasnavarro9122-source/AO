using UnityEngine;

public static class AOCharacterCreationBootstrapV170
{
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureCharacterCreation()
    {
        AOTestPlayer player =
            UnityEngine.Object
                .FindFirstObjectByType
                    <AOTestPlayer>();

        if (player == null)
            return;

        if (player.GetComponent
                <AOCharacterIdentityV170>() ==
            null)
        {
            player.gameObject
                .AddComponent
                    <AOCharacterIdentityV170>();
        }

        if (player.GetComponent
                <AOCharacterCreationV170>() ==
            null)
        {
            player.gameObject
                .AddComponent
                    <AOCharacterCreationV170>();
        }

        Debug.Log(
            "[AO v0.17] Creación de personaje activa.");
    }
}
