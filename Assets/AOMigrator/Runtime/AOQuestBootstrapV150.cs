using UnityEngine;

public static class AOQuestBootstrapV150
{
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureQuests()
    {
        AOTestPlayer player =
            UnityEngine.Object
                .FindFirstObjectByType
                    <AOTestPlayer>();

        if (player == null)
            return;

        AOQuestSystemV150 quests =
            player.GetComponent
                <AOQuestSystemV150>();

        if (quests == null)
        {
            quests =
                player.gameObject
                    .AddComponent
                        <AOQuestSystemV150>();
        }

        AOQuestUIV150 ui =
            player.GetComponent
                <AOQuestUIV150>();

        if (ui == null)
        {
            ui =
                player.gameObject
                    .AddComponent
                        <AOQuestUIV150>();
        }

        Debug.Log(
            "[AO v0.15] Quests activas. " +
            "Q = diario | E = interactuar con NPC quest.");
    }
}
