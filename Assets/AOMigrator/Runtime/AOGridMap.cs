using System;
using UnityEngine;

[DisallowMultipleComponent]
public class AOGridMap : MonoBehaviour
{
    public const int NORTH = 1;
    public const int EAST = 2;
    public const int SOUTH = 3;
    public const int WEST = 4;

    public const int FLAG_NORTH = 0x1;
    public const int FLAG_EAST = 0x2;
    public const int FLAG_SOUTH = 0x4;
    public const int FLAG_WEST = 0x8;
    public const int FLAG_ALL_SIDES = 0xF;
    public const int FLAG_GM = 0x10;
    public const int FLAG_WATER = 0x20;
    public const int FLAG_TREE = 0x40;
    public const int FLAG_COAST = 0x80;
    public const int FLAG_LAVA = 0x100;

    public const int TRIGGER_INVALID_POSITION = 3;
    public const int TRIGGER_WATER_DETAIL = 8;
    public const int TRIGGER_SWIM_VALID = 11;
    public const int TRIGGER_WORKER_ONLY = 13;
    public const int TRIGGER_SWIM_ROOF = 16;
    public const int TRIGGER_BRIDGE_VALID = 17;
    public const int TRIGGER_SWIM_COMBINED = 18;
    public const int TRIGGER_BLOCK_15 = 201;

    [SerializeField] int xMin = 1;
    [SerializeField] int xMax = 100;
    [SerializeField] int yMin = 1;
    [SerializeField] int yMax = 100;
    [SerializeField] int[] tileFlags = new int[0];
    [SerializeField] int[] tileTriggers = new int[0];
    [SerializeField] GameObject debugOverlay;

    public int XMin => xMin;
    public int XMax => xMax;
    public int YMin => yMin;
    public int YMax => yMax;
    public int Width => xMax - xMin + 1;
    public int Height => yMax - yMin + 1;

    public void Initialize(int newXMin, int newXMax, int newYMin, int newYMax)
    {
        xMin = newXMin; xMax = newXMax; yMin = newYMin; yMax = newYMax;
        if (Width <= 0 || Height <= 0 || Width > 1000 || Height > 1000)
            throw new ArgumentOutOfRangeException("Dimensiones AO invalidas.");
        tileFlags = new int[Width * Height];
        tileTriggers = new int[Width * Height];
    }

    int Index(int x, int y)
    {
        if (!InBounds(x, y)) return -1;
        return (y - yMin) * Width + (x - xMin);
    }

    public bool InBounds(int x, int y)
    {
        return x >= xMin && x <= xMax && y >= yMin && y <= yMax;
    }

    public int GetFlags(int x, int y)
    {
        int index = Index(x, y);
        return index >= 0 ? tileFlags[index] : FLAG_ALL_SIDES;
    }

    public int GetTrigger(int x, int y)
    {
        int index = Index(x, y);
        return index >= 0 ? tileTriggers[index] : TRIGGER_BLOCK_15;
    }

    public void OrFlags(int x, int y, int flags)
    {
        int index = Index(x, y);
        if (index >= 0) tileFlags[index] |= flags;
    }

    public void ClearFlags(int x, int y, int flags)
    {
        int index = Index(x, y);
        if (index >= 0) tileFlags[index] &= ~flags;
    }

    public void SetTrigger(int x, int y, int trigger)
    {
        int index = Index(x, y);
        if (index >= 0) tileTriggers[index] = trigger;
    }

    public bool IsDeepWater(int x, int y)
    {
        int flags = GetFlags(x, y);
        return (flags & FLAG_WATER) != 0 && (flags & FLAG_COAST) == 0;
    }

    public bool IsSpawnCandidate(int x, int y)
    {
        if (!InBounds(x, y)) return false;
        int flags = GetFlags(x, y);
        int trigger = GetTrigger(x, y);
        if ((flags & FLAG_ALL_SIDES) != 0) return false;
        if (IsDeepWater(x, y) && trigger != TRIGGER_BRIDGE_VALID) return false;
        if (trigger == TRIGGER_WORKER_ONLY || trigger == TRIGGER_BLOCK_15) return false;
        return true;
    }

    // Replica el nucleo terrestre de LegalPos del cliente AO20: limites,
    // bit direccional del tile destino, triggers de bloqueo y agua/navegacion.
    // NPCs, personajes, monturas, natacion y servidor se agregaran mas adelante.
    public bool CanEnter(int x, int y, int heading)
    {
        if (!InBounds(x, y) || heading < NORTH || heading > WEST) return false;
        int flags = GetFlags(x, y);
        int directionBit = 1 << (heading - 1);
        if ((flags & directionBit) != 0) return false;

        int trigger = GetTrigger(x, y);
        if (trigger == TRIGGER_WORKER_ONLY || trigger == TRIGGER_BLOCK_15) return false;

        // Jugador de prueba: no navega ni nada.
        bool waterOnly = (flags & FLAG_WATER) != 0 && (flags & FLAG_COAST) == 0;
        if (waterOnly && trigger != TRIGGER_BRIDGE_VALID) return false;
        return true;
    }

    // Aproxima LegalWalkNPC del servidor AO20 para esta etapa local:
    // límites, bloqueo direccional, POSINVALIDA y afinidad agua/tierra/lava.
    // TileExit y ocupación dinámica se controlan desde AONPCMovementV08.
    public bool CanEnterNPC(
        int x,
        int y,
        int heading,
        bool canWater,
        bool landInvalid,
        bool lavaValid)
    {
        if (!InBounds(x, y) ||
            heading < NORTH ||
            heading > WEST)
            return false;

        int flags = GetFlags(x, y);
        int trigger = GetTrigger(x, y);
        int directionBit = 1 << (heading - 1);

        if ((flags & directionBit) != 0)
            return false;

        if (trigger == TRIGGER_INVALID_POSITION)
            return false;

        bool deepWater =
            (flags & FLAG_WATER) != 0 &&
            (flags & FLAG_COAST) == 0;

        if (!canWater && deepWater)
            return false;

        if (landInvalid && !deepWater)
            return false;

        if (!lavaValid &&
            (flags & FLAG_LAVA) != 0)
            return false;

        return true;
    }

    public Vector3 TileToWorld(int x, int y)
    {
        // Mismo anclaje inferior-centro usado por los sprites del mapa importado.
        return new Vector3(x - 0.5f, -y, 0f);
    }

    public void SetDebugOverlay(GameObject overlay)
    {
        debugOverlay = overlay;
        if (debugOverlay != null) debugOverlay.SetActive(false);
    }

    public void ToggleDebugOverlay()
    {
        if (debugOverlay != null) debugOverlay.SetActive(!debugOverlay.activeSelf);
    }

    public bool DebugOverlayVisible => debugOverlay != null && debugOverlay.activeSelf;
}
