using UnityEngine;

public class AOQuestMarkerManagerV180 : MonoBehaviour
{
    float nextScanAt;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        GameObject existing =
            GameObject.Find(
                "AO Quest Markers v0.18");

        if (existing != null)
            return;

        GameObject go =
            new GameObject(
                "AO Quest Markers v0.18");

        go.AddComponent
            <AOQuestMarkerManagerV180>();
    }

    void Update()
    {
        if (Time.time <
            nextScanAt)
            return;

        nextScanAt =
            Time.time +
            0.75f;

        AONPCMetadata[] npcs =
            UnityEngine.Object
                .FindObjectsByType
                    <AONPCMetadata>(
                        FindObjectsSortMode.None);

        foreach (
            AONPCMetadata npc in
            npcs)
        {
            if (npc == null)
                continue;

            if (npc.GetComponent
                    <AOQuestMarkerV180>() ==
                null)
            {
                npc.gameObject
                    .AddComponent
                        <AOQuestMarkerV180>();
            }
        }
    }
}
