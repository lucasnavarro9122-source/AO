using UnityEngine;

public static class AOSaveBootstrapV140
{
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureSaveSystem()
    {
        AOTestPlayer player =
            UnityEngine.Object
                .FindFirstObjectByType
                    <AOTestPlayer>();

        if (player == null)
            return;

        AOSaveGameV140 save =
            player.GetComponent
                <AOSaveGameV140>();

        if (save == null)
        {
            save =
                player.gameObject
                    .AddComponent
                        <AOSaveGameV140>();
        }

        AOMainMenuV140 menu =
            player.GetComponent
                <AOMainMenuV140>();

        if (menu == null)
        {
            menu =
                player.gameObject
                    .AddComponent
                        <AOMainMenuV140>();
        }

        Debug.Log(
            "[AO v0.14] Save/Load activo. " +
            "F1 guardar | F3 cargar.");
    }
}
