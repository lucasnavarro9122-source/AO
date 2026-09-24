using System.Collections.Generic;
using UnityEngine;

public abstract class AOInteractable : MonoBehaviour
{
    [SerializeField] int tileX;
    [SerializeField] int tileY;
    [SerializeField] string displayName;
    [TextArea(2, 6)]
    [SerializeField] string description;
    [SerializeField] bool blocksTile;

    public int TileX => tileX;
    public int TileY => tileY;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? gameObject.name : displayName;
    public string Description => description ?? "";
    public bool BlocksTile => blocksTile;

    public void ConfigureInteraction(int x, int y, string title, string text, bool blocks)
    {
        AOInteractionRegistry.Unregister(this);
        tileX = x;
        tileY = y;
        displayName = title ?? "";
        description = text ?? "";
        blocksTile = blocks;
        if (isActiveAndEnabled) AOInteractionRegistry.Register(this);
    }

    protected virtual void OnEnable()
    {
        AOInteractionRegistry.Register(this);
    }

    protected virtual void OnDisable()
    {
        AOInteractionRegistry.Unregister(this);
    }
}

public static class AOInteractionRegistry
{
    static readonly Dictionary<int, List<AOInteractable>> byTile =
        new Dictionary<int, List<AOInteractable>>();

    static int Key(int x, int y)
    {
        unchecked { return (x << 16) ^ (y & 0xFFFF); }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRegistry()
    {
        byTile.Clear();
    }

    public static void Clear()
    {
        byTile.Clear();
    }

    public static void Register(AOInteractable item)
    {
        if (item == null) return;
        int key = Key(item.TileX, item.TileY);
        if (!byTile.TryGetValue(key, out var list))
        {
            list = new List<AOInteractable>();
            byTile[key] = list;
        }
        if (!list.Contains(item)) list.Add(item);
    }

    public static void Unregister(AOInteractable item)
    {
        if (item == null) return;
        int key = Key(item.TileX, item.TileY);
        if (!byTile.TryGetValue(key, out var list)) return;
        list.Remove(item);
        if (list.Count == 0) byTile.Remove(key);
    }

    static void Clean(List<AOInteractable> list)
    {
        for (int i = list.Count - 1; i >= 0; i--)
            if (list[i] == null) list.RemoveAt(i);
    }

    public static AOInteractable FindFirst(int x, int y)
    {
        if (!byTile.TryGetValue(Key(x, y), out var list)) return null;
        Clean(list);
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null && list[i].isActiveAndEnabled) return list[i];
        return null;
    }

    public static bool IsBlocked(int x, int y) => IsBlocked(x, y, true);

    // npcsBlock=false: NPC bodies do not count (skill shots resolve NPC hits by radius).
    public static bool IsBlocked(int x, int y, bool npcsBlock)
    {
        if (!byTile.TryGetValue(Key(x, y), out var list)) return false;
        Clean(list);
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null && list[i].isActiveAndEnabled && list[i].BlocksTile &&
                (npcsBlock || list[i].GetComponent<AONPCCombatV09>() == null))
                return true;
        return false;
    }
}
