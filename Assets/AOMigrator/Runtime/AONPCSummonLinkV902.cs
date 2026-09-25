using UnityEngine;

// AONPCSummonLinkV902 (nube, provisorio): a creature summoned by an NPC without connection (Invoca).
// It never respawns (it leaves shortly after dying) and dies with its summoner (MuereNpc: no killer, no experience).
// Online the room owns the summons (CoopRoom.NpcMagic.cs) and AOOnlineClientV240 creates and removes them.
[DisallowMultipleComponent]
public class AONPCSummonLinkV902 : MonoBehaviour
{
    public AONPCCombatV09 Summoner { get; private set; }
    AONPCCombatV09 self;
    float goneAt = -1f;

    public void Configure(AONPCCombatV09 summoner) { Summoner = summoner; self = GetComponent<AONPCCombatV09>(); }

    public bool Alive => self != null && self.IsAlive && goneAt < 0f;

    void Update()
    {
        if (goneAt >= 0f) { if (Time.time >= goneAt) Destroy(gameObject); return; }
        if (self == null) { Destroy(gameObject); return; }
        // Dead (before the NPC's own 350 ms respawn) or its summoner is gone: the creature leaves.
        if (!self.IsAlive) goneAt = Time.time + .3f;
        else if (Summoner == null || !Summoner.IsAlive) goneAt = Time.time;
    }
}
