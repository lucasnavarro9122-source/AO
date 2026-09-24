using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// Small private-room relay for the Unity migration. It does not run AO20 rules.
const int maxPlayers = 11; // Anfitrión y hasta diez amigos.
const int maxLineBytes = 2048;
const int port = 7777;
var json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var peers = new Dictionary<int, Peer>();
var gate = new object();
var nextId = 0;
var keyPath = Path.Combine(AppContext.BaseDirectory, "room-key.txt");
string roomKey;
if (File.Exists(keyPath))
{
    roomKey = File.ReadAllText(keyPath).Trim();
    if (roomKey.Length < 24) throw new InvalidOperationException("room-key.txt es demasiado corto.");
}
else
{
    roomKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(18));
    File.WriteAllText(keyPath, roomKey + Environment.NewLine);
}

var listener = new TcpListener(IPAddress.Any, port);
listener.Start(maxPlayers);
Console.WriteLine($"AO Online: puerto TCP {port}; máximo {maxPlayers} jugadores.");
Console.WriteLine($"Clave de sala: {roomKey}");
Console.WriteLine("Compartí la clave sólo con tus amigos. Ctrl+C detiene el servidor.");
using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); listener.Stop(); };

_ = Task.Run(async () =>
{
    while (!stop.IsCancellationRequested)
    {
        await Task.Delay(120, stop.Token).ConfigureAwait(false);
        Peer[] active;
        lock (gate) active = peers.Values.ToArray();
        var snapshot = new Wire
        {
            type = "state",
            players = active.Select(p => p.State).ToArray()
        };
        foreach (var p in active) p.Send(snapshot);
    }
}, stop.Token);

try
{
    while (!stop.IsCancellationRequested)
    {
        TcpClient socket;
        try { socket = await listener.AcceptTcpClientAsync(stop.Token); }
        catch (OperationCanceledException) { break; }
        catch (SocketException) when (stop.IsCancellationRequested) { break; }
        _ = Task.Run(() => Handle(socket));
    }
}
finally
{
    listener.Stop();
    lock (gate) foreach (var p in peers.Values) p.Close();
}

void Handle(TcpClient socket)
{
    Peer? peer = null;
    try
    {
        socket.NoDelay = true;
        socket.ReceiveTimeout = 15000;
        socket.SendTimeout = 3000;
        using (socket)
        {
            var stream = socket.GetStream();
            Wire? hello = Read(stream);
            if (hello?.type != "hello" || hello.version != 1 ||
                !SameKey(hello.key ?? "", roomKey) ||
                !ValidPlayer(hello))
            {
                SendRaw(stream, new Wire { type = "error", text = "Clave o datos inválidos." });
                return;
            }

            lock (gate)
            {
                if (peers.Count >= maxPlayers)
                {
                    SendRaw(stream, new Wire { type = "error", text = "Sala completa (11/11)." });
                    return;
                }
                int id = ++nextId;
                peer = new Peer(id, socket, stream, new Player
                {
                    id = id, name = Clean(hello.name, 20), map = hello.map,
                    x = hello.x, y = hello.y, heading = hello.heading,
                    race = hello.race, gender = hello.gender, head = hello.head
                });
                peers.Add(id, peer);
            }
            peer.Send(new Wire { type = "welcome", id = peer.Id });
            Console.WriteLine($"Entró {peer.State.name} ({peer.Id}); jugadores: {Count()}.");
            socket.ReceiveTimeout = 10000;
            while (!stop.IsCancellationRequested)
            {
                Wire? msg = Read(stream);
                if (msg == null) break;
                long now = Environment.TickCount64;
                if (msg.type == "position" && ValidPosition(msg) && now - peer.LastPosition >= 55)
                {
                    peer.LastPosition = now;
                    lock (gate)
                    {
                        peer.State.map = msg.map;
                        peer.State.x = msg.x;
                        peer.State.y = msg.y;
                        peer.State.heading = msg.heading;
                    }
                }
                else if (msg.type == "chat" && now - peer.LastChat >= 700)
                {
                    string line = Clean(msg.text, 160);
                    if (line.Length == 0) continue;
                    peer.LastChat = now;
                    Peer[] recipients;
                    lock (gate) recipients = peers.Values.Where(p => p.State.map == peer.State.map).ToArray();
                    var outgoing = new Wire { type = "chat", id = peer.Id, name = peer.State.name, text = line };
                    foreach (var p in recipients) p.Send(outgoing);
                }
            }
        }
    }
    catch (Exception ex) when (ex is IOException or SocketException or JsonException)
    {
        // A malformed or disconnected client cannot stop the room.
    }
    finally
    {
        if (peer != null)
        {
            lock (gate) peers.Remove(peer.Id);
            Console.WriteLine($"Salió {peer.State.name}; jugadores: {Count()}.");
        }
        socket.Dispose();
    }
}

int Count() { lock (gate) return peers.Count; }

static bool SameKey(string provided, string expected)
{
    byte[] left = Encoding.UTF8.GetBytes(provided);
    byte[] right = Encoding.UTF8.GetBytes(expected);
    return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
}

static bool ValidPlayer(Wire p) => ValidPosition(p) &&
    !string.IsNullOrWhiteSpace(p.name) && p.name.Length <= 20 &&
    p.race is >= 1 and <= 6 && p.gender is >= 1 and <= 2 &&
    p.head is >= 1 and <= 10000;

static bool ValidPosition(Wire p) => p.map is >= 1 and <= 5000 &&
    p.x is >= 1 and <= 100 && p.y is >= 1 and <= 100 &&
    p.heading is >= 1 and <= 4;

static string Clean(string? value, int max)
{
    if (string.IsNullOrWhiteSpace(value)) return "";
    var chars = value.Trim().Where(c => !char.IsControl(c)).Take(max).ToArray();
    return new string(chars).Trim();
}

static Wire? Read(NetworkStream stream)
{
    using var buffer = new MemoryStream();
    while (buffer.Length < maxLineBytes)
    {
        int b = stream.ReadByte();
        if (b < 0) return null;
        if (b == '\n')
        {
            if (buffer.Length == 0) return null;
            return JsonSerializer.Deserialize<Wire>(buffer.ToArray());
        }
        buffer.WriteByte((byte)b);
    }
    throw new IOException("Paquete demasiado grande.");
}

static void SendRaw(NetworkStream stream, Wire message)
{
    byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message) + "\n");
    stream.Write(bytes);
}

sealed class Peer(int id, TcpClient socket, NetworkStream stream, Player state)
{
    readonly object writeGate = new();
    public int Id { get; } = id;
    public Player State { get; } = state;
    public long LastPosition { get; set; }
    public long LastChat { get; set; }
    public void Send(Wire message)
    {
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message) + "\n");
            lock (writeGate) stream.Write(bytes);
        }
        catch (IOException) { socket.Close(); }
        catch (ObjectDisposedException) { }
    }
    public void Close() => socket.Close();
}

sealed class Wire
{
    public string? type { get; set; }
    public int version { get; set; }
    public string? key { get; set; }
    public int id { get; set; }
    public string? name { get; set; }
    public int map { get; set; }
    public int x { get; set; }
    public int y { get; set; }
    public int heading { get; set; }
    public int race { get; set; }
    public int gender { get; set; }
    public int head { get; set; }
    public string? text { get; set; }
    public Player[]? players { get; set; }
}

sealed class Player
{
    public int id { get; set; }
    public string name { get; set; } = "";
    public int map { get; set; }
    public int x { get; set; }
    public int y { get; set; }
    public int heading { get; set; }
    public int race { get; set; }
    public int gender { get; set; }
    public int head { get; set; }
}
