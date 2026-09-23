using System.Collections.Generic;
using UnityEngine;

public partial class AOWorldManagerV07
{
    struct TreeVisual
    {
        public int x;
        public int y;
        public SpriteRenderer renderer;
    }

    readonly List<TreeVisual> treeVisuals = new List<TreeVisual>();

    void ResetTreeVisuals()
    {
        treeVisuals.Clear();
    }

    void RegisterTreeVisual(int x, int y, SpriteRenderer renderer)
    {
        treeVisuals.Add(new TreeVisual { x = x, y = y, renderer = renderer });
    }

    void UpdateTreeVisibility()
    {
        if (player == null)
            return;
        int playerX = player.TileX;
        int playerY = player.TileY;
        foreach (TreeVisual tree in treeVisuals)
        {
            if (tree.renderer == null)
                continue;
            bool underCanopy = Mathf.Abs(playerX - tree.x) <= 3 &&
                               playerY < tree.y && tree.y - playerY < 8;
            float target = underCanopy ? 0.42f : 1f;
            Color color = tree.renderer.color;
            color.a = Mathf.MoveTowards(color.a, target,
                                       Time.unscaledDeltaTime * 3f);
            tree.renderer.color = color;
        }
    }
}
