using UnityEngine;

[DisallowMultipleComponent]
public class AODoorV210 : MonoBehaviour
{
    AOGridMap grid;
    SpriteRenderer visual;
    Sprite closedSprite;
    Sprite openSprite;
    int tileX;
    int tileY;
    int firstPassageX;
    int[] closedNorthFlags;
    bool locked;
    bool open;

    public bool IsOpen => open;

    public void Configure(AOGridMap map, SpriteRenderer renderer,
                          Sprite opened, int x, int y, bool requiresKey)
    {
        grid = map;
        visual = renderer;
        closedSprite = renderer == null ? null : renderer.sprite;
        openSprite = opened;
        tileX = x;
        tileY = y;
        locked = requiresKey;
        int width = closedSprite == null ? 1 :
            Mathf.Clamp(Mathf.CeilToInt(closedSprite.rect.width / 32f), 1, 2);
        firstPassageX = x - width + 1;
        closedNorthFlags = new int[width];
        if (grid != null)
            for (int offset = 0; offset < width; offset++)
            {
                int flags = grid.GetFlags(firstPassageX + offset, y);
                if ((flags & AOGridMap.FLAG_ALL_SIDES) !=
                    AOGridMap.FLAG_ALL_SIDES)
                    closedNorthFlags[offset] = flags & 1;
            }
    }

    public bool CoversInteractionTile(int x, int y) =>
        y == tileY && x >= firstPassageX && x <= tileX;

    public static AODoorV210 FindForInteraction(int x, int y)
    {
        foreach (AODoorV210 door in
                 Object.FindObjectsByType<AODoorV210>(FindObjectsSortMode.None))
            if (door.CoversInteractionTile(x, y))
                return door;
        return null;
    }

    public bool TryToggle(out string message)
    {
        if (locked)
        {
            message = "Puerta cerrada con llave.";
            return true;
        }
        if (grid == null || visual == null || openSprite == null)
        {
            message = "No hay imagen de puerta abierta disponible.";
            return false;
        }
        if (open)
        {
            AOTestPlayer player = Object.FindFirstObjectByType<AOTestPlayer>();
            if (player != null && CoversInteractionTile(player.TileX, player.TileY))
            {
                message = "Salí del umbral antes de cerrar la puerta.";
                return true;
            }
            for (int offset = 0; offset < closedNorthFlags.Length; offset++)
                grid.OrFlags(firstPassageX + offset, tileY,
                             closedNorthFlags[offset]);
            visual.sprite = closedSprite;
            open = false;
            message = "Puerta cerrada.";
        }
        else
        {
            for (int offset = 0; offset < closedNorthFlags.Length; offset++)
                grid.ClearFlags(firstPassageX + offset, tileY,
                                closedNorthFlags[offset]);
            visual.sprite = openSprite;
            open = true;
            message = "Puerta abierta.";
        }
        return true;
    }
}
