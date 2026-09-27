// AOWorldManagerSkyV291 (nube, 27/09): lo que el cielo (AOSkyV291) necesita saber del mapa, sin tocar
// AOWorldManagerV07. Solo lectura.
public partial class AOWorldManagerV07
{
    // Luz base fija del mapa (0 = sigue la hora del día, como en el AO).
    public int CurrentBaseLight => currentBaseLight;

    // Zona del mapa en el AO (CIUDAD, CAMPO, DUNGEON...).
    public string CurrentMapZone => currentMap != null ? currentMap.zone : null;

    // ¿Hay un techo (disparador de techo) en ese punto del mundo?
    public bool IsRoofAt(UnityEngine.Vector2 position) => roofTriggers.Contains(TriggerAtWorld(position));

    // Al aire libre: el mapa sigue la hora (sin luz base fija) y no es un dungeon.
    public bool CurrentMapOutdoor =>
        currentMap != null && !loading && currentBaseLight == 0 &&
        !string.Equals(currentMap.zone, "DUNGEON", System.StringComparison.OrdinalIgnoreCase);
}
