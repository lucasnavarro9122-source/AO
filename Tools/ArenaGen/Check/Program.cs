// Checks for AOArenaGenV271 (docs/claude/demo/pruebas.md G-01..G-13) and the golden hash table.
// Usage (from the project root):
//   dotnet run --project Tools/ArenaGen/Check -- check [seedsPerTheme]      (default 2000; QA full run: 10000)
//   dotnet run --project Tools/ArenaGen/Check -- golden write|verify [path]  (default Tools/demo_arena_golden.json)
//   dotnet run --project Tools/ArenaGen/Check -- show <seed> <theme 0-5>
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

static class Program
{
    static readonly int[] EdgeSeeds = { 0, 1, -1, int.MaxValue, int.MinValue };

    static int Main(string[] args)
    {
        string mode = args.Length > 0 ? args[0] : "check";
        try
        {
            switch (mode)
            {
                case "check": return Check(args.Length > 1 ? int.Parse(args[1]) : 2000);
                case "golden": return Golden(args.Length > 1 ? args[1] : "verify", args.Length > 2 ? args[2] : DefaultGoldenPath());
                case "pvp": return PvpChecks();
                case "show": Console.WriteLine(Render(AOArenaGen.Generate(int.Parse(args[1]), (AOArenaGen.Theme)int.Parse(args[2])))); return 0;
                default: Console.Error.WriteLine("Modo desconocido: " + mode); return 2;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ERROR: " + ex);
            return 3;
        }
    }

    static int Check(int seedsPerTheme)
    {
        bool ok = true;
        var sw = new Stopwatch();
        Console.WriteLine($"AOArenaGen GenVersion={AOArenaGen.GenVersion} · {seedsPerTheme} semillas por tema + {EdgeSeeds.Length} límite");
        AOArenaGen.Generate(0, AOArenaGen.Theme.Bosque);   // JIT warm-up: G-13 measures steady-state cost
        for (int t = 0; t < AOArenaGen.ThemeCount; t++)
        {
            var theme = (AOArenaGen.Theme)t;
            var rule = AOArenaGen.RuleFor(theme);
            int failures = 0, fallbacks = 0, nondeterministic = 0, unfair = 0;
            int[] attempts = new int[AOArenaGen.MaxAttempts];
            int[] heat = new int[AOArenaGen.CellCount];
            var distinct = new HashSet<ulong>();
            int minObs = int.MaxValue, maxObs = 0; long sumObs = 0;
            double maxMs = 0, sumMs = 0;
            int remeasured = 0;
            string firstFailure = null;
            int total = 0;
            foreach (int seed in Seeds(seedsPerTheme))
            {
                total++;
                sw.Restart();
                AOArenaLayout a = AOArenaGen.Generate(seed, theme);
                sw.Stop();
                double ms = sw.Elapsed.TotalMilliseconds;
                // The work per seed is deterministic: a slow sample is GC or OS jitter. Re-time it and keep the minimum.
                if (ms > 1.0)
                {
                    remeasured++;
                    for (int r = 0; r < 5; r++)
                    {
                        sw.Restart();
                        AOArenaGen.Generate(seed, theme);
                        sw.Stop();
                        ms = Math.Min(ms, sw.Elapsed.TotalMilliseconds);
                    }
                }
                sumMs += ms; if (ms > maxMs) maxMs = ms;

                AOArenaLayout b = AOArenaGen.Generate(seed, theme);
                if (!Same(a.Serialize(), b.Serialize())) { nondeterministic++; firstFailure ??= $"G-01 semilla {seed}"; }
                if (!AOArenaValidator.Validate(a, out string reason)) { failures++; firstFailure ??= $"semilla {seed}: {reason}"; }
                if (AOArenaValidator.ShortestSpawnPath(a, true) != AOArenaValidator.ShortestSpawnPath(a, false)) { unfair++; firstFailure ??= $"G-08 semilla {seed}"; }
                if (a.Attempt == AOArenaGen.FallbackAttempt) fallbacks++; else attempts[a.Attempt]++;

                int obs = a.ObstacleCount;
                sumObs += obs; if (obs < minObs) minObs = obs; if (obs > maxObs) maxObs = obs;
                for (int i = 0; i < a.Cells.Length; i++) if (a.Cells[i] == (byte)AOArenaGen.Cell.Solid || a.Cells[i] == (byte)AOArenaGen.Cell.Water) heat[i]++;
                distinct.Add(ContentHash(a));
            }
            int heatMax = 0;
            for (int i = 0; i < heat.Length; i++) if (heat[i] > heatMax) heatMax = heat[i];
            double distinctPct = 100.0 * distinct.Count / total;
            double heatPct = 100.0 * heatMax / total;
            bool themeOk = failures == 0 && nondeterministic == 0 && unfair == 0 && distinctPct >= 99.0 && heatPct < 100.0 && maxMs <= 5.0;
            ok &= themeOk;
            Console.WriteLine($"{(themeOk ? "PASA" : "FALLA")} {theme,-9} válidas {total - failures}/{total} · respaldo {fallbacks} · intentos [{string.Join(",", attempts)}]" +
                $" · densidad {Pct(minObs)}–{Pct(maxObs)} % (media {Pct((int)(sumObs / total))}, regla {rule.DensityMinPct}–{rule.DensityMaxPct})" +
                $" · distintas {distinctPct:0.00} % · celda más usada {heatPct:0.0} % · ms media {sumMs / total:0.000} máx {maxMs:0.000} (re-medidas {remeasured})");
            if (firstFailure != null) Console.WriteLine("       primera falla: " + firstFailure);
        }
        Console.WriteLine(ok ? "RESULTADO: PASA" : "RESULTADO: FALLA");
        return ok ? 0 : 1;
    }

    sealed class ScriptedRandom : AOPvpFormulas.IRandom
    {
        readonly Queue<int> values;
        public ScriptedRandom(params int[] v) { values = new Queue<int>(v); }
        public int Range(int min, int max)
        {
            int value = values.Dequeue();
            if (value < min || value > max) throw new InvalidOperationException($"tirada {value} fuera de {min}..{max}");
            return value;
        }
    }

    // Formulas and rules checked against hand-computed cases (pvp-formulas.md §6, muerte-reglas.md, progresion.md §6).
    static int PvpChecks()
    {
        int failures = 0;
        void Expect(string name, long actual, long expected)
        {
            bool ok = actual == expected;
            if (!ok) failures++;
            Console.WriteLine($"{(ok ? "PASA " : "FALLA")} {name}: {actual} (esperado {expected})");
        }
        // Warrior level 20 (STR 19) with Hacha de Bárbaro 8–15 vs armor 17–20 and helmet 10–15, no shield.
        // Rolls: own hit 59, weapon 12, place 3 (body), armor 18 → (36 + 12 + 59) × 1.05 − 18 = 94.35 → 94.
        var attacker = new AOPvpFormulas.MeleeAttacker { MinHit = 58, MaxHit = 59, WeaponMinHit = 8, WeaponMaxHit = 15, Strength = 19, ClassDamageModifier = 1.05 };
        var victim = new AOPvpFormulas.MeleeVictim { HelmetMinDef = 10, HelmetMaxDef = 15, ArmorMinDef = 17, ArmorMaxDef = 20 };
        Expect("golpe (caso de Contenido)", AOPvpFormulas.MeleeDamage(new ScriptedRandom(59, 12, 3, 18), attacker, victim), 94);
        Expect("golpe a la cabeza (casco 15)", AOPvpFormulas.MeleeDamage(new ScriptedRandom(59, 12, 1, 15), attacker, victim), 97);
        Expect("acierto con tope 95", AOPvpFormulas.HitChance(500, 0), 95);
        Expect("acierto con piso 5", AOPvpFormulas.HitChance(0, 500), 5);
        Expect("acierto parejo 50", AOPvpFormulas.HitChance(100, 100), 50);
        Expect("redondeo al par (VB6 CLng 2,5 = 2)", AOPvpFormulas.VbRound(2.5), 2);
        // Spell 10–10, caster level 20: 10 + 60 % = 16; victim MR 5 → 16 − 0,8 = 15,2 → 15.
        var none = new AOPvpFormulas.MagicItem();
        Expect("hechizo con resistencia 5", AOPvpFormulas.SpellDamage(new ScriptedRandom(10), 10, 10, 20, none, none, none, false, 5), 15);
        Expect("hechizo AntiRm ignora resistencia", AOPvpFormulas.SpellDamage(new ScriptedRandom(10), 10, 10, 20, none, none, none, true, 50), 16);
        var staff = new AOPvpFormulas.MagicItem { DamageBonusPercent = 10, AbsoluteBonus = 2, Penetration = 5 };
        // 16 → +10 % = 17,6 → 18 (+2) = 20; MR 5 − penetration 5 = 0 → 20.
        Expect("hechizo con báculo que penetra", AOPvpFormulas.SpellDamage(new ScriptedRandom(10), 10, 10, 20, staff, none, none, false, 5), 20);
        Expect("oro protegido nivel 10 con 25.000", AODeathDropRules.GoldToDrop(25000, 10), 15000);
        Expect("oro bajo el protegido", AODeathDropRules.GoldToDrop(900, 1), 0);
        Expect("llave no cae", AODeathDropRules.ItemFalls(9, AODeathDropRules.ItemFlags.None, 30) ? 1 : 0, 0);
        Expect("newbie nivel 12 no cae", AODeathDropRules.ItemFalls(2, AODeathDropRules.ItemFlags.Newbie, 12) ? 1 : 0, 0);
        Expect("newbie nivel 13 cae", AODeathDropRules.ItemFalls(2, AODeathDropRules.ItemFlags.Newbie, 13) ? 1 : 0, 1);
        Expect("nada cae en trigger 6", AODeathDropRules.DropsOnTile(true, 6) ? 1 : 0, 0);
        Expect("EXP nivel 27 ×44", AODemoRates.ApplyExp(100, 27), 4400);
        Expect("EXP nivel 9 ×1", AODemoRates.ApplyExp(100, 9), 100);
        Expect("oro ×2", AODemoRates.ApplyGold(150), 300);
        // GetExpPenalty: nivel 20 vs NPC 10 → Δ 10 → 1 − 0,05 × 6 = 0,7.
        Expect("EXP penalizada Δ10", AOExpRules.ApplyNpcLevelPenalty(100, 20, 10), 70);
        Expect("EXP sin penalización Δ4", AOExpRules.ApplyNpcLevelPenalty(100, 14, 10), 100);
        Expect("EXP NPC nivel 0 (sin penalización)", AOExpRules.ApplyNpcLevelPenalty(100, 40, 0), 100);
        Expect("EXP tope 0 con Δ30", AOExpRules.ApplyNpcLevelPenalty(100, 40, 10), 0);
        AODemoRates.RespawnRange(1011, 16, 24, 0, 0, out int rMin, out int rMax);
        Expect("respawn demo usa el mapa (máx)", rMax, 24);
        AODemoRates.RespawnRange(1011, 0, 0, 40, 60, out rMin, out rMax);
        Expect("respawn demo sin tiempo en el mapa usa npcs.dat", rMax, 60);
        AODemoRates.RespawnRange(37, 16, 24, 0, 0, out rMin, out rMax);
        Expect("respawn juego normal usa npcs.dat (instantáneo)", rMax, 0);
        AODemoRates.RespawnRange(1, 0, 0, 40, 60, out rMin, out rMax);
        Expect("respawn juego normal jefe (mín)", rMin, 40);
        Console.WriteLine(failures == 0 ? "RESULTADO: PASA" : $"RESULTADO: FALLA ({failures})");
        return failures == 0 ? 0 : 1;
    }

    static IEnumerable<int> Seeds(int count)
    {
        foreach (int s in EdgeSeeds) yield return s;
        for (int i = 0; i < count; i++) yield return unchecked((int)((uint)i * 2654435761u + 12345u));
    }

    static int[] GoldenSeeds()
    {
        var list = new List<int>(EdgeSeeds);
        for (int i = 1; list.Count < 50; i++) list.Add(unchecked((int)((uint)i * 2654435761u)));
        return list.ToArray();
    }

    static int Golden(string action, string path)
    {
        if (action == "write")
        {
            var sb = new StringBuilder();
            sb.Append("{\n  \"genVersion\": ").Append(AOArenaGen.GenVersion).Append(",\n  \"entries\": [\n");
            int[] seeds = GoldenSeeds();
            bool first = true;
            foreach (int seed in seeds)
            for (int t = 0; t < AOArenaGen.ThemeCount; t++)
            {
                if (!first) sb.Append(",\n");
                first = false;
                string hash = AOArenaGen.Generate(seed, (AOArenaGen.Theme)t).Sha256Hex();
                sb.Append("    {\"seed\": ").Append(seed).Append(", \"theme\": ").Append(t).Append(", \"sha256\": \"").Append(hash).Append("\"}");
            }
            sb.Append("\n  ]\n}\n");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            Console.WriteLine($"Tabla dorada escrita: {path} ({seeds.Length * AOArenaGen.ThemeCount} entradas)");
            return 0;
        }

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        int version = doc.RootElement.GetProperty("genVersion").GetInt32();
        if (version != AOArenaGen.GenVersion)
        {
            Console.WriteLine($"FALLA: la tabla es de GenVersion {version} y el generador es {AOArenaGen.GenVersion}");
            return 1;
        }
        int checkedCount = 0, mismatches = 0;
        foreach (JsonElement e in doc.RootElement.GetProperty("entries").EnumerateArray())
        {
            int seed = e.GetProperty("seed").GetInt32(), theme = e.GetProperty("theme").GetInt32();
            string expected = e.GetProperty("sha256").GetString();
            string actual = AOArenaGen.Generate(seed, (AOArenaGen.Theme)theme).Sha256Hex();
            checkedCount++;
            if (actual != expected) { mismatches++; if (mismatches <= 5) Console.WriteLine($"  distinto: semilla {seed} tema {theme}"); }
        }
        Console.WriteLine(mismatches == 0 ? $"PASA: tabla dorada, {checkedCount} entradas iguales" : $"FALLA: {mismatches}/{checkedCount} entradas distintas");
        return mismatches == 0 ? 0 : 1;
    }

    static string DefaultGoldenPath()
    {
        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "AOCoopCompile.csproj"))) return Path.Combine(dir.FullName, "Tools", "demo_arena_golden.json");
        return "demo_arena_golden.json";
    }

    // Layout content without seed or variants: used for the variety check (G-10).
    static ulong ContentHash(AOArenaLayout layout)
    {
        ulong h = 14695981039346656037UL;
        unchecked
        {
            for (int i = 0; i < layout.Cells.Length; i++) { h = (h ^ layout.Cells[i]) * 1099511628211UL; h = (h ^ layout.Kinds[i]) * 1099511628211UL; }
            h = (h ^ (ulong)layout.Symmetry) * 1099511628211UL;
        }
        return h;
    }

    static bool Same(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    static int Pct(int cells) => cells * 100 / AOArenaGen.CellCount;

    static string Render(AOArenaLayout layout)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"semilla {layout.Seed} · tema {layout.Theme} · simetría {(layout.Symmetry == 0 ? "rot180" : "espejo")} · intento {layout.Attempt} · obstáculos {layout.ObstacleCount} ({Pct(layout.ObstacleCount)} %) · sha256 {layout.Sha256Hex().Substring(0, 12)}");
        sb.AppendLine("# sólido  ~ agua  , deco  . piso  A/B spawns (número = orden)");
        for (int y = 0; y < AOArenaGen.H; y++)
        {
            for (int x = 0; x < AOArenaGen.W; x++)
            {
                char c = layout.At(x, y) switch
                {
                    AOArenaGen.Cell.Solid => '#',
                    AOArenaGen.Cell.Water => '~',
                    AOArenaGen.Cell.Deco => ',',
                    _ => '.',
                };
                for (int i = 0; i < AOArenaGen.MaxTeam; i++)
                {
                    if (layout.SpawnA[i * 2] == x && layout.SpawnA[i * 2 + 1] == y) c = (char)('1' + i);
                    if (layout.SpawnB[i * 2] == x && layout.SpawnB[i * 2 + 1] == y) c = (char)('a' + i);
                }
                sb.Append(c).Append(' ');
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
