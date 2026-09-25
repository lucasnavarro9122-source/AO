using System.Text.Json;

// Append-only gold ledger (protocol 3): the only source of truth for gold.
// Every line moves an amount between two accounts, so all balances always add up to 0
// ("world" goes negative by the gold that entered the game). Each op key is written at most once,
// which makes repeated requests, reconnects and restarts idempotent.
sealed class Ledger : IDisposable
{
    public const string World = "world", Tax = "tax";
    public static string Wallet(string character) => "player:" + character;
    public static string Bank(string character) => "bank:" + character;
    public static string Escrow(string reto) => "escrow:" + reto;

    public sealed class Entry
    {
        public long txId, amount, time;
        public string op = "", reto = "", from = "", to = "", reason = "";
    }

    readonly FileStream file;
    readonly Dictionary<string, long> balances = new();
    readonly HashSet<string> ops = new();
    readonly List<Entry> entries = new();
    public long LastTx { get; private set; }
    public IReadOnlyDictionary<string, long> Balances => balances;
    public IEnumerable<string> Ops => ops;
    // Kept in memory to rebuild duel custody after a restart (settle plans live in their reason).
    public IReadOnlyList<Entry> Entries => entries;

    public Ledger(string directory)
    {
        string path = Path.Combine(directory, "ledger.jsonl");
        int valid = 0;
        if (File.Exists(path))
        {
            byte[] bytes = File.ReadAllBytes(path);
            for (int i = 0, start = 0; i < bytes.Length; i++)
            {
                if (bytes[i] != (byte)'\n') continue;
                Entry entry;
                try { entry = JsonSerializer.Deserialize<Entry>(bytes.AsSpan(start, i - start), CoopRoom.Json) ?? throw new JsonException(); }
                catch (JsonException e) { throw new IOException("Libro contable dañado cerca del byte " + start + "; no se arranca para no perder oro.", e); }
                Apply(entry);
                start = valid = i + 1;
            }
            if (valid < bytes.Length)
            {
                // Torn last line from a crash mid-write: its request was never answered. Keep a copy and cut it.
                File.WriteAllBytes(path + ".torn-" + DateTime.UtcNow.Ticks, bytes[valid..]);
                Console.WriteLine("Libro contable: se descartó una línea incompleta del final.");
            }
        }
        file = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
        file.SetLength(valid);
        file.Seek(valid, SeekOrigin.Begin);
    }

    void Apply(Entry e)
    {
        if (!ops.Add(e.op)) throw new IOException("Libro contable: operación repetida " + e.op + ".");
        balances[e.from] = balances.GetValueOrDefault(e.from) - e.amount;
        balances[e.to] = balances.GetValueOrDefault(e.to) + e.amount;
        entries.Add(e);
        LastTx = Math.Max(LastTx, e.txId);
    }

    public long Balance(string account) => balances.GetValueOrDefault(account);
    public bool Has(string op) => ops.Contains(op);

    // Returns false when the op already exists (replay). The line reaches the disk before the caller answers.
    public bool Transfer(string op, string from, string to, long amount, string reason, string reto = "")
    {
        if (ops.Contains(op)) return false;
        if (string.IsNullOrEmpty(op) || amount < 0 || from == to) throw new ArgumentException("Movimiento de oro inválido.");
        if (from != World && Balance(from) < amount) throw new InvalidOperationException("No tenés suficiente oro.");
        var entry = new Entry { txId = LastTx + 1, op = op, reto = reto, from = from, to = to, amount = amount, reason = reason,
                                time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(entry, CoopRoom.Json);
        byte[] line = new byte[json.Length + 1];
        json.CopyTo(line, 0); line[^1] = (byte)'\n';
        file.Write(line); file.Flush(true);
        Apply(entry);
        return true;
    }

    public void Dispose() => file.Dispose();
}
