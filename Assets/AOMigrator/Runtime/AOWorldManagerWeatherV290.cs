using UnityEngine;

// AOWorldManagerWeatherV290 (nube, 26/09): lo que el clima necesita saber de los techos, sin tocar
// AOWorldManagerRoofV210. Solo lectura.
public partial class AOWorldManagerV07
{
    // Disparador (trigger) del techo bajo el que está el jugador; 0 = cielo abierto.
    public int PlayerRoofTrigger
    {
        get
        {
            if (player == null || grid == null)
                return 0;
            int trigger = grid.GetTrigger(player.TileX, player.TileY);
            return roofTriggers.Contains(trigger) ? trigger : 0;
        }
    }

    public bool PlayerUnderRoof => PlayerRoofTrigger != 0;

    // Personaje del jugador (para que la lluvia rebote en su armadura).
    public Transform PlayerTransform => player != null ? player.transform : null;

    // Disparador de la casilla que contiene un punto del mundo (mundo = (px/32, -py/32); casilla x = piso(x) + 1).
    public int TriggerAtWorld(Vector2 position)
    {
        if (grid == null)
            return 0;
        return grid.GetTrigger(Mathf.FloorToInt(position.x) + 1,
                               Mathf.FloorToInt(-position.y) + 1);
    }
}
