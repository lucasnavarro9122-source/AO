using System;
using System.Collections.Generic;
using UnityEngine;

// AODuelClientV281: client side of the AO BATTLESERVER demo duels (docs/claude/demo/arquitectura.md §3.7 and §4, red.md §4).
// The server decides everything; this module applies what it sends:
// - the generated arena (AOArenaGen) on the grid: Solid blocks movement and projectiles, Water only movement;
// - warps, the duel HP (duelHurt), going down without the normal death, reviving and restoring at the end.
// AODuelUI (Interfaz) keeps the UI state (InDuel, Phase, Down...). The obstacle sprites are drawn by Arte from LayoutApplied.
public static class AODuelClient
{
    public sealed class ActiveArena
    {
        public int Ring, Map, OriginX, OriginY, Seed;
        public AOArenaLayout Layout;
        public AOGridMap Grid;
        public readonly List<Vector2Int> SolidTiles = new List<Vector2Int>();
        public readonly List<Vector2Int> WaterTiles = new List<Vector2Int>();
    }

    // ring, origin X/Y (map tile of layout cell 0,0), layout. Arte draws the obstacles with arena_palette.json.
    public static event Action<int, int, int, AOArenaLayout> LayoutApplied;
    public static event Action<int> LayoutCleared;

    static readonly Dictionary<int, ActiveArena> arenas = new Dictionary<int, ActiveArena>();
    static readonly HashSet<int> enemies = new HashSet<int>();
    static readonly HashSet<int> allies = new HashSet<int>();
    static int manaBeforeDuel = -1;

    public static IEnumerable<ActiveArena> Arenas => arenas.Values;
    public static bool IsEnemy(int playerId) => AODuelUI.InDuel && enemies.Contains(playerId);
    public static bool IsAlly(int playerId) => AODuelUI.InDuel && allies.Contains(playerId);

    // Fight phase and still standing: attacks and spells against rivals are allowed (the server checks again).
    public static bool CanFight => AODuelUI.InDuel && AODuelUI.Phase == AODuelPhase.Fight && !AODuelUI.Down;

    // Countdown (positions are fixed) or down until the next round: no movement, attacks or spells.
    public static bool Frozen => AODuelUI.InDuel && (AODuelUI.Phase == AODuelPhase.Countdown || AODuelUI.Down);

    // Actions the original blocks during a duel (summon, hide, /regresar) plus trade and dropping or picking up items.
    public static bool Blocks => AODuelUI.InDuel;
    public const string BlockedMessage = "No podés hacer eso durante un reto.";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        arenas.Clear();
        enemies.Clear();
        allies.Clear();
        manaBeforeDuel = -1;
        AOOnlineClientV240.DuelMessage -= OnMessage;
        AOOnlineClientV240.DuelMessage += OnMessage;
        AOOnlineClientV240.DuelEvent -= OnEvent;
        AOOnlineClientV240.DuelEvent += OnEvent;
        AODuelUI.RingLabelTile = RingLabelTile;
    }

    // Spectator sign: grada row above the ring border (ui.md §5), centered. Only rings with an active arena on this map.
    static Vector2Int? RingLabelTile(int ring)
    {
        if (!arenas.TryGetValue(ring, out ActiveArena arena))
            return null;
        AOWorldManagerV07 world = UnityEngine.Object.FindFirstObjectByType<AOWorldManagerV07>();
        if (world == null || world.CurrentMapNumber != arena.Map)
            return null;
        return new Vector2Int(arena.OriginX + AOArenaGen.W / 2, arena.OriginY - 2);
    }

    static void OnMessage(AOCoopMessage m)
    {
        AODuelUI.RingLabelTile = RingLabelTile;   // AODuelUI.ResetAll clears it when a session ends

        AOCoopDuel d = m == null ? null : m.duel;
        if (d == null)
            return;

        switch (m.type)
        {
            case "duelStart":
                SetTeams(d);
                AOPlayerRPGV11 rpg = FindPlayerComponent<AOPlayerRPGV11>();
                manaBeforeDuel = rpg == null ? -1 : rpg.Mana;
                ApplyArena(d);
                break;
            case "duelRoundStart":
                SetTeams(d);
                ApplyArena(d);
                FindPlayerComponent<AOPlayerCombatV09>()?.DuelRevive(d.hp);
                if (d.mana > 0)
                    FindPlayerComponent<AOPlayerRPGV11>()?.SetManaFromServer(d.mana);
                break;
            case "duelRingState":
                if (d.phase == "libre")
                    ClearArena(d.sala);
                else
                    ApplyArena(d);
                break;
        }
    }

    static void OnEvent(AOCoopEvent e)
    {
        if (e == null)
            return;

        AOPlayerCombatV09 combat = FindPlayerComponent<AOPlayerCombatV09>();
        switch (e.type)
        {
            case "warp":
                Warp(e.map, e.x, e.y);
                if (e.hp > 0)
                    combat?.SetDuelHp(e.hp);
                break;
            case "duelHurt":
                combat?.ApplyDuelHurt(e.hp);
                break;
            case "duelEnd":
                combat?.EndDuel(e.hp);
                if (manaBeforeDuel >= 0)
                    FindPlayerComponent<AOPlayerRPGV11>()?.SetManaFromServer(manaBeforeDuel);
                manaBeforeDuel = -1;
                enemies.Clear();
                allies.Clear();
                break;
        }
    }

    static void SetTeams(AOCoopDuel d)
    {
        if (d.idsA == null && d.idsB == null)
            return;

        // Server (AOCoopDuel): team 0 = A (challenger, idsA), 1 = B (idsB).
        int[] mine = d.team == 1 ? d.idsB : d.idsA;
        int[] theirs = d.team == 1 ? d.idsA : d.idsB;
        enemies.Clear();
        allies.Clear();
        if (theirs != null) foreach (int id in theirs) enemies.Add(id);
        if (mine != null) foreach (int id in mine) allies.Add(id);
    }

    // The same seed on every client and on the server gives the same layout (AOArenaGen, golden table).
    static void ApplyArena(AOCoopDuel d)
    {
        AOWorldManagerV07 world = UnityEngine.Object.FindFirstObjectByType<AOWorldManagerV07>();
        AOTestPlayer player = UnityEngine.Object.FindFirstObjectByType<AOTestPlayer>();
        AOGridMap grid = player == null ? null : player.CurrentGrid;
        if (world == null || grid == null || d.map != world.CurrentMapNumber)
            return;
        if (d.width != AOArenaGen.W || d.height != AOArenaGen.H)
            return;
        if (d.genVersion != 0 && d.genVersion != AOArenaGen.GenVersion)
        {
            AOInterfaceV0101.PushMessage("Tu versión del juego genera otras arenas: actualizá el cliente para ver este reto.");
            return;
        }

        if (arenas.TryGetValue(d.sala, out ActiveArena current) && current.Grid == grid && current.Seed == d.seed && current.Map == d.map)
            return;

        ClearArena(d.sala);

        var arena = new ActiveArena
        {
            Ring = d.sala, Map = d.map, OriginX = d.x, OriginY = d.y, Seed = d.seed, Grid = grid,
            Layout = AOArenaGen.Generate(d.seed, (AOArenaGen.Theme)d.theme),
        };

        for (int cy = 0; cy < AOArenaGen.H; cy++)
        for (int cx = 0; cx < AOArenaGen.W; cx++)
        {
            var tile = new Vector2Int(d.x + cx, d.y + cy);
            AOArenaGen.Cell cell = arena.Layout.At(cx, cy);
            if (cell == AOArenaGen.Cell.Solid && (grid.GetFlags(tile.x, tile.y) & AOGridMap.FLAG_ALL_SIDES) == 0)
            {
                grid.OrFlags(tile.x, tile.y, AOGridMap.FLAG_ALL_SIDES);
                arena.SolidTiles.Add(tile);
            }
            else if (cell == AOArenaGen.Cell.Water && (grid.GetFlags(tile.x, tile.y) & AOGridMap.FLAG_WATER) == 0)
            {
                grid.OrFlags(tile.x, tile.y, AOGridMap.FLAG_WATER);
                arena.WaterTiles.Add(tile);
            }
        }

        arenas[d.sala] = arena;
        LayoutApplied?.Invoke(arena.Ring, arena.OriginX, arena.OriginY, arena.Layout);
    }

    static void ClearArena(int ring)
    {
        if (!arenas.TryGetValue(ring, out ActiveArena arena))
            return;

        arenas.Remove(ring);
        AOTestPlayer player = UnityEngine.Object.FindFirstObjectByType<AOTestPlayer>();
        // After a map reload the grid is rebuilt: the old flags are already gone.
        if (player != null && player.CurrentGrid == arena.Grid)
        {
            foreach (Vector2Int tile in arena.SolidTiles) arena.Grid.ClearFlags(tile.x, tile.y, AOGridMap.FLAG_ALL_SIDES);
            foreach (Vector2Int tile in arena.WaterTiles) arena.Grid.ClearFlags(tile.x, tile.y, AOGridMap.FLAG_WATER);
        }
        LayoutCleared?.Invoke(ring);
    }

    public static bool IsInsideActiveRing(int x, int y)
    {
        foreach (ActiveArena arena in arenas.Values)
            if (x >= arena.OriginX && x < arena.OriginX + AOArenaGen.W && y >= arena.OriginY && y < arena.OriginY + AOArenaGen.H)
                return true;
        return false;
    }

    static void Warp(int map, int x, int y)
    {
        AOWorldManagerV07 world = UnityEngine.Object.FindFirstObjectByType<AOWorldManagerV07>();
        AOTestPlayer player = UnityEngine.Object.FindFirstObjectByType<AOTestPlayer>();
        if (world == null || player == null)
            return;

        if (map > 0 && map != world.CurrentMapNumber)
            world.LoadMap(map, x, y, true);
        else
            player.TeleportTo(x, y);
    }

    static T FindPlayerComponent<T>() where T : Component
    {
        AOTestPlayer player = UnityEngine.Object.FindFirstObjectByType<AOTestPlayer>();
        return player == null ? null : player.GetComponent<T>();
    }
}
