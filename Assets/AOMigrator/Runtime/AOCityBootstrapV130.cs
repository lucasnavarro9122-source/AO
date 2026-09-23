using UnityEngine;

public static class AOCityBootstrapV130
{
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureCity()
    {
        AOTestPlayer player =
            UnityEngine.Object
                .FindFirstObjectByType
                    <AOTestPlayer>();

        if (player == null)
            return;

        AOCityBankV130 bank =
            player.GetComponent
                <AOCityBankV130>();

        if (bank == null)
        {
            bank =
                player.gameObject
                    .AddComponent
                        <AOCityBankV130>();
        }

        AOCityNPCSystemV130 city =
            player.GetComponent
                <AOCityNPCSystemV130>();

        if (city == null)
        {
            city =
                player.gameObject
                    .AddComponent
                        <AOCityNPCSystemV130>();
        }

        AOCityUIV130 ui =
            player.GetComponent
                <AOCityUIV130>();

        if (ui == null)
        {
            ui =
                player.gameObject
                    .AddComponent
                        <AOCityUIV130>();
        }

        ui.Configure(
            city,
            bank);

        Debug.Log(
            "[AO v0.13] NPC de ciudad activos. " +
            "E = interactuar | F4 = 20k oro test.");
    }
}
