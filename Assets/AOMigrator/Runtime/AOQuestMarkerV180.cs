using UnityEngine;

[DisallowMultipleComponent]
public class AOQuestMarkerV180 : MonoBehaviour
{
    AONPCMetadata metadata;
    AONPCCombatV09 combat;
    AOQuestSystemV150 quests;

    SpriteRenderer marker;

    float nextRefreshAt;
    float baseLocalY = 1.4f;
    int currentState;

    void Awake()
    {
        metadata =
            GetComponent<AONPCMetadata>();

        combat =
            GetComponent<AONPCCombatV09>();

        EnsureRenderer();
        ResolvePlayer();
        RecalculateHeight();
    }

    void Update()
    {
        if (metadata == null)
            return;

        if (combat == null)
            combat =
                GetComponent<AONPCCombatV09>();

        bool alive =
            combat == null ||
            combat.IsAlive;

        if (!alive)
        {
            if (marker != null)
                marker.enabled = false;

            return;
        }

        if (Time.time >=
            nextRefreshAt)
        {
            nextRefreshAt =
                Time.time +
                0.25f;

            if (quests == null)
                ResolvePlayer();

            RefreshState();
            RecalculateHeight();
        }

        if (marker != null &&
            marker.enabled)
        {
            // El cliente AO original mueve el símbolo unos 8px con Sin².
            float bob =
                0.25f *
                Mathf.Sin(
                    Time.time *
                    3.4f);

            bob *= bob / 0.25f;

            marker.transform.localPosition =
                new Vector3(
                    0.18f,
                    baseLocalY +
                    bob,
                    0f);
        }
    }

    void ResolvePlayer()
    {
        AOTestPlayer player =
            UnityEngine.Object
                .FindFirstObjectByType
                    <AOTestPlayer>();

        if (player != null)
        {
            quests =
                player.GetComponent
                    <AOQuestSystemV150>();
        }
    }

    void EnsureRenderer()
    {
        if (marker != null)
            return;

        Transform child =
            transform.Find(
                "QuestSymbolV180");

        GameObject go;

        if (child == null)
        {
            go =
                new GameObject(
                    "QuestSymbolV180");

            go.transform.SetParent(
                transform,
                false);
        }
        else
        {
            go =
                child.gameObject;
        }

        marker =
            go.GetComponent<SpriteRenderer>();

        if (marker == null)
        {
            marker =
                go.AddComponent
                    <SpriteRenderer>();
        }

        marker.sortingOrder =
            30000;

        marker.enabled =
            false;
    }

    void RefreshState()
    {
        EnsureRenderer();

        if (quests == null ||
            metadata == null)
        {
            marker.enabled =
                false;
            return;
        }

        AOCityNPCDatabaseV130.NPCDef npc =
            AOCityNPCDatabaseV130.Get(
                metadata.NpcIndex);

        int[] offered =
            npc == null
            ? null
            : npc.questNumbers;

        int state =
            quests.GetNpcQuestSymbolState(
                metadata.NpcIndex,
                offered);

        if (state !=
            currentState)
        {
            currentState =
                state;

            marker.sprite =
                AOQuestMarkerSpritesV180
                    .Get(
                        currentState);
        }

        marker.enabled =
            currentState > 0 &&
            marker.sprite != null;
    }

    void RecalculateHeight()
    {
        SpriteRenderer[] renderers =
            GetComponentsInChildren
                <SpriteRenderer>(true);

        float top =
            transform.position.y +
            1.15f;

        foreach (
            SpriteRenderer sr in
            renderers)
        {
            if (sr == null ||
                sr == marker)
                continue;

            if (sr.sprite == null)
                continue;

            top =
                Mathf.Max(
                    top,
                    sr.bounds.max.y);
        }

        baseLocalY =
            Mathf.Max(
                0.9f,
                top -
                transform.position.y +
                0.35f);
    }
}
