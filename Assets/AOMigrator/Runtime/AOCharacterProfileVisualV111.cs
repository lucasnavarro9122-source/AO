using UnityEngine;

[DisallowMultipleComponent]
public class AOCharacterProfileVisualV111 : MonoBehaviour
{
    [SerializeField] int headIndex;
    [SerializeField] float walkFps = 18f;
    [SerializeField] bool deadVisual;

    AOPlayerRPGV11 rpg;
    AOCharacterRenderer visual;
    AOInventoryV10 inventory;

    public int HeadIndex => headIndex;
    public bool DeadVisual => deadVisual;

    void Awake()
    {
        FindReferences();
    }

    void Start()
    {
        SyncFromRPG();
    }

    void FindReferences()
    {
        if (rpg == null)
            rpg =
                GetComponent<AOPlayerRPGV11>();

        if (visual == null)
            visual =
                GetComponentInChildren
                    <AOCharacterRenderer>(true);

        if (inventory == null)
            inventory =
                GetComponent<AOInventoryV10>();
    }

    public void ApplyProfile(
        AOPlayerRPGV11 source,
        int requestedHead)
    {
        rpg = source;

        if (requestedHead > 0)
            headIndex =
                requestedHead;

        SyncFromRPG();
    }

    public void SetHead(
        int requestedHead)
    {
        headIndex =
            requestedHead;

        SyncFromRPG();
    }

    public void SetDeadVisual(
        bool value)
    {
        deadVisual =
            value;

        if (deadVisual)
            ApplyDeadVisual();
        else
            SyncFromRPG();
    }

    void ApplyDeadVisual()
    {
        FindReferences();

        if (visual == null)
            return;

        if (!AODeathVisualV160.TryBuild(
                out AOCharacterRenderer.DirectionVisual[]
                    dirs,
                out float deadWalkFps,
                out float headX,
                out float headY,
                out float bodyX))
        {
            Debug.LogError(
                "[AO v0.16] No pude construir BODY829.");
            return;
        }

        int heading =
            visual.Heading;

        bool walking =
            visual.Walking;

        visual.Configure(
            dirs,
            deadWalkFps,
            headX,
            headY,
            bodyX);

        visual.ConfigureEquipment(
            null,
            false,
            0f,
            0f,
            0f);

        visual.SetHeading(
            heading);

        visual.SetWalking(
            walking);

        visual.ForceRefreshVisuals();
    }

    public void SyncFromRPG()
    {
        FindReferences();

        if (rpg == null ||
            visual == null)
            return;

        if (deadVisual)
        {
            ApplyDeadVisual();
            return;
        }

        if (!AOCharacterVisualDatabaseV111
            .IsValidHead(
                rpg.RaceId,
                rpg.GenderId,
                headIndex))
        {
            headIndex =
                AOCharacterVisualDatabaseV111
                    .DefaultHead(
                        rpg.RaceId,
                        rpg.GenderId);
        }

        if (!AOCharacterVisualDatabaseV111
            .TryBuildBase(
                rpg.RaceId,
                rpg.GenderId,
                headIndex,
                out AOCharacterRenderer.DirectionVisual[]
                    dirs,
                out float headX,
                out float headY,
                out float bodyX))
        {
            Debug.LogError(
                "[AO v0.11.1] No pude construir visual base.");
            return;
        }

        int heading =
            visual.Heading;

        bool walking =
            visual.Walking;

        visual.Configure(
            dirs,
            walkFps,
            headX,
            headY,
            bodyX);

        visual.SetHeading(
            heading);

        visual.SetWalking(
            walking);

        if (inventory != null)
            inventory.RefreshVisualEquipment();

        Debug.Log(
            "[AO v0.11.1] Visual sincronizado: " +
            rpg.RaceName +
            " / " +
            rpg.GenderName +
            " head=" +
            headIndex);
    }
}
