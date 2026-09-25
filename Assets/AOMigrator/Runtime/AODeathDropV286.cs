using System;
using System.Collections.Generic;
using UnityEngine;

// AODeathDropV286: original death drop, offline (decision 18, docs/claude/contenido/muerte-reglas.md).
// Only on maps with "dropOnDeath" (demo dungeon floors) and never on a fight-zone tile. Rules: AODeathDropRulesV275.
// Online the server does it (gold through the wallet, items with "remove" + loot on the floor): nothing local.
// Placement adapted from the cloud patch muerte-juego (Tilelibre: nearest free tile, same item stacked up to MaxPile).
// Called by AODeathRespawnV160.OnDeathStarted after everything was unequipped (equipped items fall too).
public static class AODeathDrop
{
    const int MaxPile = 10000;       // MaxInventoryObjs
    const int SearchRadius = 15;     // Tilelibre

    struct Drop
    {
        public int item, amount, x, y;
        public bool gold;
        public bool Placed => x != 0 || y != 0;
    }

    public static void OnPlayerDeath(GameObject player)
    {
        if (player == null || AOOnlineClientV240.Requested)
            return;

        AOWorldManagerV07 world = UnityEngine.Object.FindFirstObjectByType<AOWorldManagerV07>();
        AOTestPlayer movement = player.GetComponent<AOTestPlayer>();
        AOPlayerCombatV09 combat = player.GetComponent<AOPlayerCombatV09>();
        AOInventoryV10 inventory = player.GetComponent<AOInventoryV10>();
        AOPlayerRPGV11 rpg = player.GetComponent<AOPlayerRPGV11>();
        AOGridMap grid = movement != null ? movement.CurrentGrid : null;
        if (world == null || movement == null || combat == null || inventory == null || grid == null)
            return;

        int cx = movement.TileX, cy = movement.TileY;
        if (!AODeathDropRules.DropsOnTile(world.CurrentMapDropsOnDeath, grid.GetTrigger(cx, cy)))
            return;

        int level = rpg != null ? Mathf.Max(1, rpg.Level) : 1;
        var stacks = new List<(int item, int amount)>();
        var slots = new List<int>();
        for (int i = 0; i < inventory.SlotCount; i++)
        {
            int item = inventory.GetSlotItemIndex(i), amount = inventory.GetSlotAmount(i);
            AOItemDatabaseV10.ItemDef def = item > 0 ? AOItemDatabaseV10.Get(item) : null;
            if (def == null || amount <= 0)
                continue;
            AODeathDropRules.ItemFlags flags = AODeathDropRules.FlagsFrom(def.noSeCae, def.intirable, def.destroyOnSell, def.untransferable, def.newbie);
            if (!AODeathDropRules.ItemFalls(def.objType, flags, level))
                continue;
            stacks.Add((item, amount));
            slots.Add(i);
        }

        int goldItem = AONPCLootDatabaseV180.GoldItemIndex;
        List<Drop> plan = Plan(cx, cy, AODeathDropRules.GoldToDrop(combat.Gold, level), goldItem, stacks,
            (x, y) => CanDropAt(grid, world, x, y), FloorAt);

        long goldLost = 0;
        int itemsLost = 0, stackIndex = 0;
        foreach (Drop d in plan)
        {
            if (d.gold)
            {
                if (!combat.SpendGold(d.amount))
                    continue;
                goldLost += d.amount;
                AOLootPickupV09.Create(d.item, AOItemDatabaseV10.Get(d.item)?.name ?? "Oro", d.amount, d.x, d.y);
                continue;
            }

            int slot = slots[stackIndex++];
            // Remove from the inventory first: if that fails nothing is created on the floor (no duplicates).
            if (!inventory.RemoveItemAtPublic(slot, d.amount))
                continue;
            itemsLost++;
            if (d.Placed)
                AOLootPickupV09.Create(d.item, AOItemDatabaseV10.Get(d.item)?.name ?? "Objeto", d.amount, d.x, d.y);
        }

        if (itemsLost > 0 || goldLost > 0)
            AOInterfaceV0101.PushMessage("Al morir se te cayeron " + itemsLost + " objeto(s) y " + goldLost + " de oro.");
    }

    // Gold first (piles up to MaxPile), then each inventory stack whole. Gold without room stays with the player;
    // an item without room is lost (original: no free backpack).
    static List<Drop> Plan(int cx, int cy, long goldToDrop, int goldItem, IList<(int item, int amount)> stacks,
        Func<int, int, bool> canDrop, Func<int, int, (int item, int amount)> floorAt)
    {
        var drops = new List<Drop>();
        var placed = new Dictionary<long, (int item, int amount)>();
        long remaining = Math.Max(0L, goldToDrop);
        while (remaining > 0 && goldItem > 0)
        {
            int pile = (int)Math.Min(remaining, MaxPile);
            if (!FindTile(cx, cy, goldItem, pile, canDrop, floorAt, placed, out int gx, out int gy))
                break;
            drops.Add(new Drop { item = goldItem, amount = pile, x = gx, y = gy, gold = true });
            remaining -= pile;
        }
        for (int i = 0; i < stacks.Count; i++)
        {
            var (item, amount) = stacks[i];
            FindTile(cx, cy, item, amount, canDrop, floorAt, placed, out int x, out int y);
            drops.Add(new Drop { item = item, amount = amount, x = x, y = y });
        }
        return drops;
    }

    // Tilelibre: squares of radius 0..15, row by row from the top left. The dead player's tile is taken by him.
    static bool FindTile(int cx, int cy, int item, int amount, Func<int, int, bool> canDrop,
        Func<int, int, (int item, int amount)> floorAt, Dictionary<long, (int item, int amount)> placed, out int fx, out int fy)
    {
        for (int r = 0; r <= SearchRadius; r++)
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (Math.Max(Math.Abs(x - cx), Math.Abs(y - cy)) != r || (x == cx && y == cy)) continue;
                    if (!canDrop(x, y)) continue;
                    long key = (long)y * 100000 + x;
                    var here = placed.TryGetValue(key, out var p) ? p : floorAt(x, y);
                    if (here.item != 0 && (here.item != item || here.amount + amount > MaxPile)) continue;
                    placed[key] = (item, (here.item == item ? here.amount : 0) + amount);
                    fx = x; fy = y;
                    return true;
                }
        fx = 0; fy = 0;
        return false;
    }

    // Walkable land without map exit, NPC or anything else on the tile (same idea as CoopRoom.CanDropAt).
    static bool CanDropAt(AOGridMap grid, AOWorldManagerV07 world, int x, int y)
    {
        if (!grid.IsSpawnCandidate(x, y)) return false;
        if (grid.GetTrigger(x, y) == AOGridMap.TRIGGER_INVALID_POSITION) return false;
        if (world.IsExitTile(x, y)) return false;
        AOInteractable here = AOInteractionRegistry.FindFirst(x, y);
        return here == null || here is AOLootPickupV09;
    }

    static (int item, int amount) FloorAt(int x, int y)
    {
        AOLootPickupV09 loot = AOLootPickupV09.FindAt(x, y);
        return loot == null ? (0, 0) : (loot.ItemIndex, loot.Amount);
    }
}
