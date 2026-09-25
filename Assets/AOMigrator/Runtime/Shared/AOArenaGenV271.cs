#nullable disable
// AOArenaGenV271: arena interior generator for the AO BATTLESERVER demo (docs/claude/demo/arquitectura.md §3).
// Shared source: compiled by Unity (Mono, netstandard2.1, C# 9) and by OnlineServer (.NET).
// Rules: no UnityEngine, no System.Random, no float/double, no hash-ordered collections.
// Any change to the algorithm, constants or theme rules must bump GenVersion and regenerate
// Tools/demo_arena_golden.json (dotnet run --project Tools/ArenaGen/Check -c Release -- golden write).
using System;
using System.Security.Cryptography;

public static class AOArenaGen
{
    public const int GenVersion = 1;
    public const int W = 23;
    public const int H = 19;
    public const int CellCount = W * H;
    public const int MidColumn = W / 2;          // column 11: central lane, always free
    public const int PocketWidth = 4;            // columns 0..3 (team A) and 19..22 (team B) never get obstacles
    public const int MaxAttempts = 8;
    public const int FallbackAttempt = 255;
    public const int MaxThrows = 160;
    public const int ThemeCount = 6;
    public const int MaxTeam = 5;

    public enum Theme : byte { Bosque = 0, Desierto = 1, Nieve = 2, Mazmorra = 3, Pantano = 4, Ciudad = 5 }

    // Rules: Solid blocks movement and projectiles; Water blocks movement only; Deco is walkable (visual only).
    public enum Cell : byte { Floor = 0, Solid = 1, Water = 2, Deco = 3 }

    // Visual category for Arte's palette. Does not change rules.
    public enum Kind : byte { None = 0, Pillar = 1, Rock = 2, Wall = 3, Tree = 4, Pool = 5, Deco = 6 }

    public sealed class ThemeRule
    {
        public readonly int DensityMinPct, DensityMaxPct, DecoMin, DecoMax;
        public readonly int[] Weights;   // indexed by shape: Pillar, Rock2x2, WallH3, WallV3, L3, Tree, Pool2x1, Pool2x2
        public ThemeRule(int densityMinPct, int densityMaxPct, int decoMin, int decoMax, int[] weights)
        {
            DensityMinPct = densityMinPct; DensityMaxPct = densityMaxPct; DecoMin = decoMin; DecoMax = decoMax; Weights = weights;
        }
        public int MinCells => CellCount * DensityMinPct / 100;
        public int MaxCells => CellCount * DensityMaxPct / 100;
    }

    const int ShapePillar = 0, ShapeRock = 1, ShapeWallH = 2, ShapeWallV = 3, ShapeL = 4, ShapeTree = 5, ShapePool2x1 = 6, ShapePool2x2 = 7;

    static readonly ThemeRule[] Rules =
    {
        new ThemeRule(10, 18, 2, 5, new[] { 1, 2, 0, 0, 0, 4, 1, 0 }),   // Bosque: trees, rocks, some pools
        new ThemeRule( 8, 14, 1, 4, new[] { 2, 3, 1, 1, 0, 0, 0, 0 }),   // Desierto: rocks and pillars, no water
        new ThemeRule( 8, 15, 1, 4, new[] { 2, 2, 0, 0, 0, 2, 1, 0 }),   // Nieve: pool = ice
        new ThemeRule(12, 20, 1, 4, new[] { 3, 0, 2, 2, 2, 0, 0, 0 }),   // Mazmorra: pillars and walls
        new ThemeRule(10, 18, 2, 5, new[] { 0, 1, 0, 0, 0, 2, 2, 2 }),   // Pantano: lots of water
        new ThemeRule( 8, 14, 1, 3, new[] { 3, 0, 1, 1, 0, 0, 0, 1 }),   // Ciudad: pillars, walls, fountain
    };

    static readonly int[][] ShapeCells =
    {
        new[] { 0, 0 },
        new[] { 0, 0, 1, 0, 0, 1, 1, 1 },
        new[] { 0, 0, 1, 0, 2, 0 },
        new[] { 0, 0, 0, 1, 0, 2 },
        null,                               // L3: one of LCells by orientation
        new[] { 0, 0 },
        new[] { 0, 0, 1, 0 },
        new[] { 0, 0, 1, 0, 0, 1, 1, 1 },
    };

    static readonly int[][] LCells =
    {
        new[] { 0, 0, 1, 0, 0, 1 },
        new[] { 0, 0, 1, 0, 1, 1 },
        new[] { 1, 0, 0, 1, 1, 1 },
        new[] { 0, 0, 0, 1, 1, 1 },
    };

    static readonly Cell[] ShapeCell = { Cell.Solid, Cell.Solid, Cell.Solid, Cell.Solid, Cell.Solid, Cell.Solid, Cell.Water, Cell.Water };
    static readonly Kind[] ShapeKind = { Kind.Pillar, Kind.Rock, Kind.Wall, Kind.Wall, Kind.Wall, Kind.Tree, Kind.Pool, Kind.Pool };

    // Team A spawns in the west pocket; order = 1v1 uses slot 0, 2v2 slots 0..1, ... 5v5 slots 0..4.
    static readonly int[] SpawnAX = { 2, 2, 2, 2, 2 };
    static readonly int[] SpawnAY = { 9, 7, 11, 5, 13 };

    // Fallback layout: always valid (4 pillars), used when every attempt fails.
    static readonly int[] FallbackPillars = { 6, 4, 6, 14 };

    public static ThemeRule RuleFor(Theme theme) => Rules[(int)Normalize(theme)];

    public static Theme Normalize(Theme theme) => (int)theme < ThemeCount ? theme : Theme.Bosque;

    public static void MirrorOf(int x, int y, int symmetry, out int mx, out int my)
    {
        mx = W - 1 - x;
        my = symmetry == 0 ? H - 1 - y : y;
    }

    // Never throws and always terminates: bounded attempts plus an always-valid fallback.
    public static AOArenaLayout Generate(int seed, Theme theme)
    {
        theme = Normalize(theme);
        var scratch = new Scratch();
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            AOArenaLayout layout = TryBuild(seed, theme, attempt, scratch);
            if (layout != null && AOArenaValidator.Validate(layout, out _)) return layout;
        }
        return Fallback(seed, theme);
    }

    static AOArenaLayout TryBuild(int seed, Theme theme, int attempt, Scratch scratch)
    {
        var rng = new Rng(seed, theme, attempt);
        var layout = new AOArenaLayout(seed, theme, attempt, rng.Bounded(2));
        ThemeRule rule = Rules[(int)theme];
        int minCells = rule.MinCells, maxCells = rule.MaxCells;
        int target = minCells + rng.Bounded(maxCells - minCells + 1);
        int totalWeight = 0;
        for (int i = 0; i < rule.Weights.Length; i++) totalWeight += rule.Weights[i];

        int count = 0;
        for (int t = 0; t < MaxThrows && count < target; t++)
        {
            int shape = PickShape(ref rng, rule, totalWeight);
            int[] cells = shape == ShapeL ? LCells[rng.Bounded(4)] : ShapeCells[shape];
            int ax = PocketWidth + rng.Bounded(MidColumn - PocketWidth);
            int ay = rng.Bounded(H);
            int stampCells = cells.Length / 2 * 2;   // the stamp plus its mirror
            if (count + stampCells > maxCells) continue;
            if (!CanPlace(layout, cells, ax, ay)) continue;
            Place(layout, cells, ax, ay, ShapeCell[shape], ShapeKind[shape], ref rng);
            if (!AOArenaValidator.IsConnected(layout, scratch))
            {
                Unplace(layout, cells, ax, ay);
                continue;
            }
            count += stampCells;
        }
        if (count < minCells) return null;

        PlaceDeco(layout, rule, ref rng);
        AssignSpawns(layout);
        return layout;
    }

    static int PickShape(ref Rng rng, ThemeRule rule, int totalWeight)
    {
        int roll = rng.Bounded(totalWeight);
        for (int i = 0; i < rule.Weights.Length; i++)
        {
            roll -= rule.Weights[i];
            if (roll < 0) return i;
        }
        return ShapePillar;
    }

    static bool CanPlace(AOArenaLayout layout, int[] cells, int ax, int ay)
    {
        for (int i = 0; i < cells.Length; i += 2)
        {
            int x = ax + cells[i], y = ay + cells[i + 1];
            if (x < PocketWidth || x >= MidColumn || y < 0 || y >= H) return false;
            if (layout.Cells[AOArenaLayout.Index(x, y)] != (byte)Cell.Floor) return false;
            // One free cell around every obstacle keeps rings open (no mazes).
            for (int ny = y - 1; ny <= y + 1; ny++)
            for (int nx = x - 1; nx <= x + 1; nx++)
            {
                if (nx < 0 || nx >= W || ny < 0 || ny >= H) continue;
                byte c = layout.Cells[AOArenaLayout.Index(nx, ny)];
                if (c == (byte)Cell.Solid || c == (byte)Cell.Water) return false;
            }
        }
        return true;
    }

    // One variant per stamp: Arte draws a 2x2 rock as a single sprite. Stamps never touch (1-cell gap),
    // so adjacent cells with the same Kind always belong to the same stamp.
    static void Place(AOArenaLayout layout, int[] cells, int ax, int ay, Cell cell, Kind kind, ref Rng rng)
    {
        byte variant = (byte)rng.Bounded(256);
        for (int i = 0; i < cells.Length; i += 2)
            layout.SetMirrored(ax + cells[i], ay + cells[i + 1], cell, kind, variant);
    }

    static void Unplace(AOArenaLayout layout, int[] cells, int ax, int ay)
    {
        for (int i = 0; i < cells.Length; i += 2)
            layout.SetMirrored(ax + cells[i], ay + cells[i + 1], Cell.Floor, Kind.None, 0);
    }

    static void PlaceDeco(AOArenaLayout layout, ThemeRule rule, ref Rng rng)
    {
        int wanted = rule.DecoMin + rng.Bounded(rule.DecoMax - rule.DecoMin + 1);
        int placed = 0;
        for (int t = 0; t < wanted * 8 && placed < wanted; t++)
        {
            int x = rng.Bounded(MidColumn), y = rng.Bounded(H);
            byte variant = (byte)rng.Bounded(256);
            if (layout.Cells[AOArenaLayout.Index(x, y)] != (byte)Cell.Floor || IsSpawnA(x, y)) continue;
            layout.SetMirrored(x, y, Cell.Deco, Kind.Deco, variant);
            placed++;
        }
    }

    static bool IsSpawnA(int x, int y)
    {
        for (int i = 0; i < MaxTeam; i++) if (SpawnAX[i] == x && SpawnAY[i] == y) return true;
        return false;
    }

    static void AssignSpawns(AOArenaLayout layout)
    {
        for (int i = 0; i < MaxTeam; i++)
        {
            layout.SpawnA[i * 2] = SpawnAX[i];
            layout.SpawnA[i * 2 + 1] = SpawnAY[i];
            MirrorOf(SpawnAX[i], SpawnAY[i], layout.Symmetry, out layout.SpawnB[i * 2], out layout.SpawnB[i * 2 + 1]);
        }
    }

    static AOArenaLayout Fallback(int seed, Theme theme)
    {
        var layout = new AOArenaLayout(seed, theme, FallbackAttempt, 0);
        for (int i = 0; i < FallbackPillars.Length; i += 2)
            layout.SetMirrored(FallbackPillars[i], FallbackPillars[i + 1], Cell.Solid, Kind.Pillar, 0);
        AssignSpawns(layout);
        return layout;
    }

    internal sealed class Scratch
    {
        public readonly int[] Queue = new int[CellCount];
        public readonly int[] Mark = new int[CellCount];
        public int Stamp;
        public int NextStamp()
        {
            if (++Stamp == int.MaxValue) { Array.Clear(Mark, 0, Mark.Length); Stamp = 1; }
            return Stamp;
        }
    }

    // SplitMix64 with integer-only bounded draws (Lemire, 32-bit). Identical on Mono and .NET.
    struct Rng
    {
        ulong state;

        public Rng(int seed, Theme theme, int attempt)
        {
            unchecked
            {
                state = Mix((ulong)(uint)seed) ^ Mix(((ulong)(uint)GenVersion << 32) | ((ulong)(byte)theme << 8) | (ulong)(uint)attempt);
            }
        }

        static ulong Mix(ulong z)
        {
            unchecked
            {
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        ulong Next()
        {
            unchecked
            {
                state += 0x9E3779B97F4A7C15UL;
                return Mix(state);
            }
        }

        public int Bounded(int n)
        {
            if (n <= 1) return 0;
            unchecked
            {
                uint bound = (uint)n;
                ulong m = (ulong)(uint)(Next() >> 32) * bound;
                uint low = (uint)m;
                if (low < bound)
                {
                    uint threshold = (0u - bound) % bound;
                    while (low < threshold)
                    {
                        m = (ulong)(uint)(Next() >> 32) * bound;
                        low = (uint)m;
                    }
                }
                return (int)(m >> 32);
            }
        }
    }
}

public sealed class AOArenaLayout
{
    public readonly int GenVersion = AOArenaGen.GenVersion;
    public readonly int Seed;
    public readonly int Attempt;               // 0..7, or AOArenaGen.FallbackAttempt
    public readonly AOArenaGen.Theme Theme;
    public readonly int Symmetry;              // 0 = 180° rotation, 1 = vertical-axis mirror
    public readonly byte[] Cells = new byte[AOArenaGen.CellCount];     // AOArenaGen.Cell, index y*W+x, (0,0) = top-left of the interior
    public readonly byte[] Kinds = new byte[AOArenaGen.CellCount];     // AOArenaGen.Kind (visual)
    public readonly byte[] Variants = new byte[AOArenaGen.CellCount];  // 0..255, client uses palette[v % count] (visual)
    public readonly int[] SpawnA = new int[AOArenaGen.MaxTeam * 2];    // (x,y) pairs
    public readonly int[] SpawnB = new int[AOArenaGen.MaxTeam * 2];

    public AOArenaLayout(int seed, AOArenaGen.Theme theme, int attempt, int symmetry)
    {
        Seed = seed; Theme = theme; Attempt = attempt; Symmetry = symmetry;
    }

    public static int Index(int x, int y) => y * AOArenaGen.W + x;
    public static bool InBounds(int x, int y) => x >= 0 && x < AOArenaGen.W && y >= 0 && y < AOArenaGen.H;

    public AOArenaGen.Cell At(int x, int y) => InBounds(x, y) ? (AOArenaGen.Cell)Cells[Index(x, y)] : AOArenaGen.Cell.Solid;
    public bool Walkable(int x, int y) { var c = At(x, y); return c == AOArenaGen.Cell.Floor || c == AOArenaGen.Cell.Deco; }
    public bool BlocksProjectile(int x, int y) => At(x, y) == AOArenaGen.Cell.Solid;

    public int ObstacleCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < Cells.Length; i++) if (Cells[i] == (byte)AOArenaGen.Cell.Solid || Cells[i] == (byte)AOArenaGen.Cell.Water) n++;
            return n;
        }
    }

    internal void SetMirrored(int x, int y, AOArenaGen.Cell cell, AOArenaGen.Kind kind, byte variant)
    {
        AOArenaGen.MirrorOf(x, y, Symmetry, out int mx, out int my);
        int a = Index(x, y), b = Index(mx, my);
        Cells[a] = Cells[b] = (byte)cell;
        Kinds[a] = Kinds[b] = (byte)kind;
        Variants[a] = Variants[b] = variant;
    }

    // Cell traversal between two cell centers (supercover). Endpoints are not checked.
    // Permissive on exact corners: blocked only if both side cells block (the server validates hits that
    // the client already accepted, so it must not be stricter than AOGridMap.ProjectileBlockedBetween).
    public bool SegmentClear(int x0, int y0, int x1, int y1)
    {
        if (!InBounds(x0, y0) || !InBounds(x1, y1)) return false;
        int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
        int sx = x1 > x0 ? 1 : -1, sy = y1 > y0 ? 1 : -1;
        int x = x0, y = y0;
        int error = dx - dy;
        dx *= 2; dy *= 2;
        for (int n = 1 + (dx + dy) / 2; n > 0; n--)
        {
            bool endpoint = (x == x0 && y == y0) || (x == x1 && y == y1);
            if (!endpoint && BlocksProjectile(x, y)) return false;
            if (x == x1 && y == y1) break;
            if (error > 0) { x += sx; error -= dy; }
            else if (error < 0) { y += sy; error += dx; }
            else
            {
                if (BlocksProjectile(x + sx, y) && BlocksProjectile(x, y + sy)) return false;
                x += sx; y += sy; error += dx - dy; n--;
            }
        }
        return true;
    }

    // Canonical bytes for the golden table: "AOAG" + header ints (little-endian) + cells + kinds + variants + spawns.
    public byte[] Serialize()
    {
        int headerInts = 7, spawnInts = SpawnA.Length + SpawnB.Length;
        var bytes = new byte[4 + headerInts * 4 + Cells.Length * 3 + spawnInts * 4];
        int p = 0;
        bytes[p++] = (byte)'A'; bytes[p++] = (byte)'O'; bytes[p++] = (byte)'A'; bytes[p++] = (byte)'G';
        p = WriteInt(bytes, p, GenVersion);
        p = WriteInt(bytes, p, Seed);
        p = WriteInt(bytes, p, Attempt);
        p = WriteInt(bytes, p, (int)Theme);
        p = WriteInt(bytes, p, Symmetry);
        p = WriteInt(bytes, p, AOArenaGen.W);
        p = WriteInt(bytes, p, AOArenaGen.H);
        Buffer.BlockCopy(Cells, 0, bytes, p, Cells.Length); p += Cells.Length;
        Buffer.BlockCopy(Kinds, 0, bytes, p, Kinds.Length); p += Kinds.Length;
        Buffer.BlockCopy(Variants, 0, bytes, p, Variants.Length); p += Variants.Length;
        for (int i = 0; i < SpawnA.Length; i++) p = WriteInt(bytes, p, SpawnA[i]);
        for (int i = 0; i < SpawnB.Length; i++) p = WriteInt(bytes, p, SpawnB[i]);
        return bytes;
    }

    public string Sha256Hex()
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(Serialize());
            var chars = new char[hash.Length * 2];
            const string hex = "0123456789abcdef";
            for (int i = 0; i < hash.Length; i++) { chars[i * 2] = hex[hash[i] >> 4]; chars[i * 2 + 1] = hex[hash[i] & 15]; }
            return new string(chars);
        }
    }

    static int WriteInt(byte[] bytes, int p, int value)
    {
        unchecked
        {
            bytes[p] = (byte)value; bytes[p + 1] = (byte)(value >> 8); bytes[p + 2] = (byte)(value >> 16); bytes[p + 3] = (byte)(value >> 24);
        }
        return p + 4;
    }
}

public static class AOArenaValidator
{
    // Full check used by the generator and by QA (pruebas.md G-03..G-09).
    public static bool Validate(AOArenaLayout layout, out string reason)
    {
        var scratch = new AOArenaGen.Scratch();
        if (layout == null) { reason = "layout nulo"; return false; }
        if (!IsSymmetric(layout)) { reason = "simetría"; return false; }
        if (!PocketsClear(layout)) { reason = "bolsillo de spawn con obstáculo"; return false; }
        if (!SpawnsValid(layout)) { reason = "spawns"; return false; }
        if (!IsConnected(layout, scratch)) { reason = "celdas caminables aisladas"; return false; }
        if (layout.Attempt != AOArenaGen.FallbackAttempt && !DensityInRange(layout)) { reason = "densidad"; return false; }
        if (!MinCutAtLeast2(layout, scratch)) { reason = "cuello de botella único"; return false; }
        reason = "";
        return true;
    }

    public static bool IsSymmetric(AOArenaLayout layout)
    {
        for (int y = 0; y < AOArenaGen.H; y++)
        for (int x = 0; x < AOArenaGen.W; x++)
        {
            AOArenaGen.MirrorOf(x, y, layout.Symmetry, out int mx, out int my);
            int a = AOArenaLayout.Index(x, y), b = AOArenaLayout.Index(mx, my);
            if (layout.Cells[a] != layout.Cells[b] || layout.Kinds[a] != layout.Kinds[b] || layout.Variants[a] != layout.Variants[b]) return false;
        }
        for (int i = 0; i < AOArenaGen.MaxTeam; i++)
        {
            AOArenaGen.MirrorOf(layout.SpawnA[i * 2], layout.SpawnA[i * 2 + 1], layout.Symmetry, out int mx, out int my);
            if (layout.SpawnB[i * 2] != mx || layout.SpawnB[i * 2 + 1] != my) return false;
        }
        return true;
    }

    public static bool PocketsClear(AOArenaLayout layout)
    {
        for (int y = 0; y < AOArenaGen.H; y++)
        for (int x = 0; x < AOArenaGen.PocketWidth; x++)
            if (!layout.Walkable(x, y) || !layout.Walkable(AOArenaGen.W - 1 - x, y)) return false;
        return true;
    }

    public static bool SpawnsValid(AOArenaLayout layout)
    {
        for (int i = 0; i < AOArenaGen.MaxTeam; i++)
        {
            if (!SpawnOk(layout, layout.SpawnA[i * 2], layout.SpawnA[i * 2 + 1])) return false;
            if (!SpawnOk(layout, layout.SpawnB[i * 2], layout.SpawnB[i * 2 + 1])) return false;
            for (int j = 0; j < i; j++)
            {
                if (layout.SpawnA[i * 2] == layout.SpawnA[j * 2] && layout.SpawnA[i * 2 + 1] == layout.SpawnA[j * 2 + 1]) return false;
                if (layout.SpawnB[i * 2] == layout.SpawnB[j * 2] && layout.SpawnB[i * 2 + 1] == layout.SpawnB[j * 2 + 1]) return false;
            }
            for (int j = 0; j < AOArenaGen.MaxTeam; j++)
            {
                int d = Math.Abs(layout.SpawnA[i * 2] - layout.SpawnB[j * 2]) + Math.Abs(layout.SpawnA[i * 2 + 1] - layout.SpawnB[j * 2 + 1]);
                if (d < MinSpawnDistance) return false;
            }
        }
        return true;
    }

    public const int MinSpawnDistance = 17;

    static bool SpawnOk(AOArenaLayout layout, int x, int y)
    {
        if (layout.At(x, y) != AOArenaGen.Cell.Floor) return false;
        int free = 0;
        if (layout.Walkable(x + 1, y)) free++;
        if (layout.Walkable(x - 1, y)) free++;
        if (layout.Walkable(x, y + 1)) free++;
        if (layout.Walkable(x, y - 1)) free++;
        return free >= 2;
    }

    public static bool DensityInRange(AOArenaLayout layout)
    {
        AOArenaGen.ThemeRule rule = AOArenaGen.RuleFor(layout.Theme);
        int n = layout.ObstacleCount;
        return n >= rule.MinCells && n <= rule.MaxCells;
    }

    // Every walkable cell reachable from team A's first spawn (4-neighbour moves, like AO).
    public static bool IsConnected(AOArenaLayout layout) => IsConnected(layout, new AOArenaGen.Scratch());

    internal static bool IsConnected(AOArenaLayout layout, AOArenaGen.Scratch scratch)
    {
        int walkable = 0;
        for (int i = 0; i < layout.Cells.Length; i++)
            if (layout.Cells[i] == (byte)AOArenaGen.Cell.Floor || layout.Cells[i] == (byte)AOArenaGen.Cell.Deco) walkable++;
        int start = AOArenaLayout.Index(2, 9);
        return Flood(layout, scratch, start, -1, false) == walkable;
    }

    // No single cell can separate the two pockets: remove each non-pocket cell of one shortest path and re-check.
    public static bool MinCutAtLeast2(AOArenaLayout layout) => MinCutAtLeast2(layout, new AOArenaGen.Scratch());

    internal static bool MinCutAtLeast2(AOArenaLayout layout, AOArenaGen.Scratch scratch)
    {
        int[] parent = new int[AOArenaGen.CellCount];
        int end = ShortestPathToB(layout, scratch, parent);
        if (end < 0) return false;
        for (int c = end; c >= 0; c = parent[c])
        {
            int x = c % AOArenaGen.W;
            if (x < AOArenaGen.PocketWidth || x >= AOArenaGen.W - AOArenaGen.PocketWidth) continue;
            if (!ReachesB(layout, scratch, c)) return false;
        }
        return true;
    }

    // Shortest path length from any team A spawn to the nearest team B spawn, and the reverse (pruebas.md G-08).
    public static int ShortestSpawnPath(AOArenaLayout layout, bool fromA)
    {
        var scratch = new AOArenaGen.Scratch();
        int[] from = fromA ? layout.SpawnA : layout.SpawnB, to = fromA ? layout.SpawnB : layout.SpawnA;
        int best = int.MaxValue;
        int[] dist = new int[AOArenaGen.CellCount];
        for (int i = 0; i < AOArenaGen.MaxTeam; i++)
        {
            Distances(layout, scratch, AOArenaLayout.Index(from[i * 2], from[i * 2 + 1]), dist);
            for (int j = 0; j < AOArenaGen.MaxTeam; j++)
            {
                int d = dist[AOArenaLayout.Index(to[j * 2], to[j * 2 + 1])];
                if (d >= 0 && d < best) best = d;
            }
        }
        return best == int.MaxValue ? -1 : best;
    }

    static int Flood(AOArenaLayout layout, AOArenaGen.Scratch scratch, int start, int blocked, bool stopAtB)
    {
        int stamp = scratch.NextStamp();
        int[] queue = scratch.Queue, mark = scratch.Mark;
        int head = 0, tail = 0, reached = 0;
        if (!WalkableIndex(layout, start) || start == blocked) return 0;
        mark[start] = stamp; queue[tail++] = start;
        while (head < tail)
        {
            int c = queue[head++];
            reached++;
            int x = c % AOArenaGen.W, y = c / AOArenaGen.W;
            if (stopAtB && x >= AOArenaGen.W - AOArenaGen.PocketWidth) return -1;
            TryPush(layout, scratch, x + 1, y, blocked, stamp, ref tail);
            TryPush(layout, scratch, x - 1, y, blocked, stamp, ref tail);
            TryPush(layout, scratch, x, y + 1, blocked, stamp, ref tail);
            TryPush(layout, scratch, x, y - 1, blocked, stamp, ref tail);
        }
        return reached;
    }

    static bool ReachesB(AOArenaLayout layout, AOArenaGen.Scratch scratch, int blocked) =>
        Flood(layout, scratch, AOArenaLayout.Index(2, 9), blocked, true) == -1;

    static int ShortestPathToB(AOArenaLayout layout, AOArenaGen.Scratch scratch, int[] parent)
    {
        int stamp = scratch.NextStamp();
        int[] queue = scratch.Queue, mark = scratch.Mark;
        int head = 0, tail = 0;
        int start = AOArenaLayout.Index(2, 9);
        mark[start] = stamp; parent[start] = -1; queue[tail++] = start;
        while (head < tail)
        {
            int c = queue[head++];
            int x = c % AOArenaGen.W, y = c / AOArenaGen.W;
            if (x >= AOArenaGen.W - AOArenaGen.PocketWidth) return c;
            PushWithParent(layout, scratch, c, x + 1, y, stamp, parent, ref tail);
            PushWithParent(layout, scratch, c, x - 1, y, stamp, parent, ref tail);
            PushWithParent(layout, scratch, c, x, y + 1, stamp, parent, ref tail);
            PushWithParent(layout, scratch, c, x, y - 1, stamp, parent, ref tail);
        }
        return -1;
    }

    static void Distances(AOArenaLayout layout, AOArenaGen.Scratch scratch, int start, int[] dist)
    {
        for (int i = 0; i < dist.Length; i++) dist[i] = -1;
        int[] queue = scratch.Queue;
        int head = 0, tail = 0;
        dist[start] = 0; queue[tail++] = start;
        while (head < tail)
        {
            int c = queue[head++];
            int x = c % AOArenaGen.W, y = c / AOArenaGen.W;
            int[] nx = { x + 1, x - 1, x, x }, ny = { y, y, y + 1, y - 1 };
            for (int k = 0; k < 4; k++)
            {
                if (!layout.Walkable(nx[k], ny[k])) continue;
                int n = AOArenaLayout.Index(nx[k], ny[k]);
                if (dist[n] >= 0) continue;
                dist[n] = dist[c] + 1; queue[tail++] = n;
            }
        }
    }

    static bool WalkableIndex(AOArenaLayout layout, int c) =>
        layout.Cells[c] == (byte)AOArenaGen.Cell.Floor || layout.Cells[c] == (byte)AOArenaGen.Cell.Deco;

    static void TryPush(AOArenaLayout layout, AOArenaGen.Scratch scratch, int x, int y, int blocked, int stamp, ref int tail)
    {
        if (!AOArenaLayout.InBounds(x, y)) return;
        int n = AOArenaLayout.Index(x, y);
        if (n == blocked || scratch.Mark[n] == stamp || !WalkableIndex(layout, n)) return;
        scratch.Mark[n] = stamp; scratch.Queue[tail++] = n;
    }

    static void PushWithParent(AOArenaLayout layout, AOArenaGen.Scratch scratch, int from, int x, int y, int stamp, int[] parent, ref int tail)
    {
        if (!AOArenaLayout.InBounds(x, y)) return;
        int n = AOArenaLayout.Index(x, y);
        if (scratch.Mark[n] == stamp || !WalkableIndex(layout, n)) return;
        scratch.Mark[n] = stamp; parent[n] = from; scratch.Queue[tail++] = n;
    }
}
