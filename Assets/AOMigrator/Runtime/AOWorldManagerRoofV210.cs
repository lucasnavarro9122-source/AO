using System.Collections.Generic;
using UnityEngine;

public partial class AOWorldManagerV07
{
    readonly HashSet<int> roofTriggers = new HashSet<int>();
    readonly Dictionary<int, List<SpriteRenderer>> roofsByTrigger =
        new Dictionary<int, List<SpriteRenderer>>();
    readonly Dictionary<int, float> roofAlpha = new Dictionary<int, float>();

    static bool IsRoofTrigger(int trigger) =>
        trigger == 1 || trigger == 4 || trigger == 16 ||
        (trigger >= 20 && trigger < 200);

    void ResetRoofGroups(WorldMapData data)
    {
        roofTriggers.Clear();
        roofsByTrigger.Clear();
        roofAlpha.Clear();
        if (data.cells == null)
            return;

        foreach (Cell cell in data.cells)
        {
            if (cell.layer != 4)
                continue;
            int trigger = grid.GetTrigger(cell.x, cell.y);
            if (IsRoofTrigger(trigger))
                roofTriggers.Add(trigger);
        }
    }

    int RoofTriggerNear(int x, int y)
    {
        int trigger = grid.GetTrigger(x, y);
        if (roofTriggers.Contains(trigger))
            return trigger;

        for (int distance = 1; distance <= 2; distance++)
            for (int dy = -distance; dy <= distance; dy++)
                for (int dx = -distance; dx <= distance; dx++)
                {
                    trigger = grid.GetTrigger(x + dx, y + dy);
                    if (roofTriggers.Contains(trigger))
                        return trigger;
                }
        return 0;
    }

    void RegisterRoofCell(Cell cell, SpriteRenderer renderer)
    {
        int trigger = RoofTriggerNear(cell.x, cell.y);
        if (trigger == 0)
            return;
        if (!roofsByTrigger.TryGetValue(trigger, out List<SpriteRenderer> roof))
        {
            roof = new List<SpriteRenderer>();
            roofsByTrigger[trigger] = roof;
            roofAlpha[trigger] = 1f;
        }
        roof.Add(renderer);
    }

    void UpdateRoofVisibility()
    {
        if (player == null || grid == null)
            return;
        int current = grid.GetTrigger(player.TileX, player.TileY);
        if (!roofTriggers.Contains(current))
            current = 0;

        foreach (KeyValuePair<int, List<SpriteRenderer>> group in roofsByTrigger)
        {
            float target = group.Key == current ? 0.06f : 1f;
            float alpha = Mathf.MoveTowards(roofAlpha[group.Key], target,
                                           Time.unscaledDeltaTime * 3f);
            roofAlpha[group.Key] = alpha;
            foreach (SpriteRenderer renderer in group.Value)
            {
                if (renderer == null)
                    continue;
                Color color = renderer.color;
                color.a = alpha;
                renderer.color = color;
            }
        }
    }
}
