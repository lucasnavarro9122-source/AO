using UnityEngine;

// AOWorldManagerRainV293 (nube, 27/09): lo que la lluvia necesita de los árboles y del suelo, sin tocar
// AOWorldManagerV07 ni AOWorldManagerTreeV210. Solo lectura.
public partial class AOWorldManagerV07
{
    // Árboles del mapa (objetos tipo 4 del AO) ya dibujados.
    public int TreeVisualCount => treeVisuals.Count;
    public SpriteRenderer TreeVisualRenderer(int index) => treeVisuals[index].renderer;

    // Suelo donde se puede formar un charco: casilla caminable de tierra (no agua, no bloqueada) y sin techo.
    public bool IsPuddleGround(int x, int y) =>
        grid != null && grid.IsSpawnCandidate(x, y) && !grid.IsDeepWater(x, y) && !roofTriggers.Contains(grid.GetTrigger(x, y));
}
