using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

// Private-room presence and local chat. AO20 combat and inventory remain local.
public class AOOnlineClientV240 : MonoBehaviour
{
    [Serializable]
    class Wire
    {
        public string type;
        public int version;
        public string key;
        public int id;
        public string name;
        public int map;
        public int x;
        public int y;
        public int heading;
        public int race;
        public int gender;
        public int head;
        public string text;
        public PeerState[] players;
    }

    [Serializable]
    class PeerState
    {
        public int id;
        public string name;
        public int map;
        public int x;
        public int y;
        public int heading;
        public int race;
        public int gender;
        public int head;
    }

    class Avatar
    {
        public GameObject root;
        public AOCharacterRenderer visual;
        public string name;
        public string speech;
        public float speechUntil;
        public Vector3 target;
    }

    static AOOnlineClientV240 instance;
    readonly ConcurrentQueue<string> incoming = new ConcurrentQueue<string>();
    readonly Dictionary<int, Avatar> avatars = new Dictionary<int, Avatar>();
    readonly object writeGate = new object();
    TcpClient socket;
    NetworkStream stream;
    Thread reader;
    AOTestPlayer player;
    AOWorldManagerV07 world;
    Camera gameCamera;
    bool connecting;
    bool connected;
    bool requested;
    bool shuttingDown;
    string address;
    string roomKey;
    int myId;
    float nextPosition;

    public static bool Connected => instance != null && instance.connected;
    public static bool Requested => instance != null && instance.requested;
    public static string SavedAddress => PlayerPrefs.GetString("AO.Online.Address", "127.0.0.1");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        GameObject root = new GameObject("AO Online Client");
        instance = root.AddComponent<AOOnlineClientV240>();
    }

    public static bool Prepare(string host, string secret, out string error)
    {
        error = "";
        host = (host ?? "").Trim();
        secret = (secret ?? "").Trim();
        if (host.Length == 0 || host.Length > 253 ||
            !System.Text.RegularExpressions.Regex.IsMatch(host,
                @"^[a-zA-Z0-9][a-zA-Z0-9.\-]*$"))
        {
            error = "Dirección inválida. Usá IP de Tailscale o nombre de equipo.";
            return false;
        }
        if (secret.Length < 24 || secret.Length > 128)
        {
            error = "Pegá la clave de sala del servidor.";
            return false;
        }
        if (instance == null) Bootstrap();
        instance.address = host;
        instance.roomKey = secret;
        instance.requested = true;
        PlayerPrefs.SetString("AO.Online.Address", host);
        PlayerPrefs.Save();
        return true;
    }

    public static void UseLocalMode()
    {
        if (instance == null) return;
        instance.requested = false;
        instance.Disconnect();
    }

    public static void StartSession()
    {
        if (instance != null && instance.requested)
            instance.Connect();
    }

    public static void SendChat(string text)
    {
        if (instance == null || !instance.connected) return;
        text = (text ?? "").Trim();
        if (text.Length == 0) return;
        if (text.Length > 160) text = text.Substring(0, 160);
        instance.Send(new Wire { type = "chat", text = text });
    }

    void Connect()
    {
        Disconnect();
        player = UnityEngine.Object.FindFirstObjectByType<AOTestPlayer>();
        world = UnityEngine.Object.FindFirstObjectByType<AOWorldManagerV07>();
        gameCamera = Camera.main;
        if (player == null || world == null)
        {
            AOInterfaceV0101.PushMessage("No se pudo iniciar red: falta jugador o mapa.");
            return;
        }

        AOCharacterIdentityV170 identity = player.GetComponent<AOCharacterIdentityV170>();
        AOPlayerRPGV11 rpg = player.GetComponent<AOPlayerRPGV11>();
        AOCharacterProfileVisualV111 profile = player.GetComponent<AOCharacterProfileVisualV111>();
        if (identity == null || rpg == null || profile == null)
        {
            AOInterfaceV0101.PushMessage("No se pudo iniciar red: falta perfil del personaje.");
            return;
        }
        var hello = new Wire
        {
            type = "hello", version = 1, key = roomKey,
            name = identity.CharacterName, map = world.CurrentMapNumber,
            x = player.TileX, y = player.TileY, heading = player.Heading,
            race = rpg.RaceId, gender = rpg.GenderId, head = profile.HeadIndex
        };
        string host = address;
        connecting = true;
        shuttingDown = false;
        AOInterfaceV0101.PushMessage("Conectando a " + host + ":7777...");
        reader = new Thread(() => ReadLoop(host, JsonUtility.ToJson(hello)))
        {
            IsBackground = true, Name = "AO Online Client"
        };
        reader.Start();
    }

    void ReadLoop(string host, string hello)
    {
        try
        {
            socket = new TcpClient { NoDelay = true, SendTimeout = 3000 };
            IAsyncResult pending = socket.BeginConnect(host, 7777, null, null);
            if (!pending.AsyncWaitHandle.WaitOne(5000))
                throw new IOException("Tiempo de conexión agotado.");
            socket.EndConnect(pending);
            stream = socket.GetStream();
            stream.ReadTimeout = 10000;
            WriteLine(hello);
            while (!shuttingDown)
            {
                string line = ReadLine(stream);
                if (line == null) break;
                incoming.Enqueue(line);
            }
        }
        catch (Exception e) when (e is IOException or SocketException or ObjectDisposedException)
        {
            if (!shuttingDown) incoming.Enqueue(JsonUtility.ToJson(new Wire
                { type = "error", text = "Conexión perdida: " + e.Message }));
        }
        finally
        {
            if (!shuttingDown) incoming.Enqueue("{\"type\":\"closed\"}");
        }
    }

    static string ReadLine(NetworkStream input)
    {
        using (var bytes = new MemoryStream())
        {
            while (bytes.Length < 8192)
            {
                int b = input.ReadByte();
                if (b < 0) return null;
                if (b == '\n') return Encoding.UTF8.GetString(bytes.ToArray());
                bytes.WriteByte((byte)b);
            }
        }
        throw new IOException("Respuesta demasiado grande.");
    }

    void WriteLine(string line)
    {
        if (stream == null) return;
        byte[] bytes = Encoding.UTF8.GetBytes(line + "\n");
        lock (writeGate) stream.Write(bytes, 0, bytes.Length);
    }

    void Send(Wire message)
    {
        try { WriteLine(JsonUtility.ToJson(message)); }
        catch (Exception e) when (e is IOException or ObjectDisposedException)
        { incoming.Enqueue(JsonUtility.ToJson(new Wire { type = "error", text = e.Message })); }
    }

    void Update()
    {
        int handled = 0;
        while (handled++ < 10 && incoming.TryDequeue(out string line))
        {
            Wire message;
            try { message = JsonUtility.FromJson<Wire>(line); }
            catch (Exception) { continue; }
            if (message == null) continue;
            if (message.type == "welcome")
            {
                myId = message.id;
                connected = true;
                connecting = false;
                AOInterfaceV0101.PushMessage("Conectado. Sala privada de hasta 10 jugadores.");
            }
            else if (message.type == "state") ApplyState(message.players);
            else if (message.type == "chat" && message.id != myId)
            {
                string speaker = Short(message.name, 20);
                string spoken = Short(message.text, 160);
                AOInterfaceV0101.PushMessage(speaker + ": " + spoken);
                if (avatars.TryGetValue(message.id, out Avatar avatar))
                {
                    avatar.speech = spoken;
                    avatar.speechUntil = Time.unscaledTime + 6f;
                }
            }
            else if (message.type == "error")
                AOInterfaceV0101.PushMessage("Red: " + Short(message.text, 180));
            else if (message.type == "closed") Disconnect();
        }

        if (connected && AOMainMenuV140.SessionActive &&
            Time.unscaledTime >= nextPosition && player != null && world != null)
        {
            nextPosition = Time.unscaledTime + 0.12f;
            Send(new Wire { type = "position", map = world.CurrentMapNumber,
                x = player.TileX, y = player.TileY, heading = player.Heading });
        }
        foreach (Avatar avatar in avatars.Values)
            if (avatar.root != null)
                avatar.root.transform.position = Vector3.Lerp(
                    avatar.root.transform.position, avatar.target,
                    Mathf.Clamp01(Time.unscaledDeltaTime * 15f));
    }

    void ApplyState(PeerState[] states)
    {
        if (states == null || player == null || world == null ||
            !AOMainMenuV140.SessionActive || world.IsLoading) return;
        var seen = new HashSet<int>();
        foreach (PeerState state in states)
        {
            if (state == null || state.id == myId ||
                state.map != world.CurrentMapNumber) continue;
            seen.Add(state.id);
            if (!avatars.TryGetValue(state.id, out Avatar avatar))
            {
                avatar = CreateAvatar(state);
                if (avatar == null) continue;
                avatars.Add(state.id, avatar);
            }
            AOGridMap grid = player.CurrentGrid;
            if (grid == null) continue;
            avatar.target = grid.TileToWorld(state.x, state.y);
            avatar.visual.SetHeading(state.heading);
            avatar.visual.SetWalking(Vector3.Distance(avatar.root.transform.position,
                avatar.target) > 0.03f);
            avatar.visual.UpdateSorting(AORenderOrderV210.Character(-avatar.target.y));
        }
        var remove = new List<int>();
        foreach (var pair in avatars)
            if (!seen.Contains(pair.Key)) remove.Add(pair.Key);
        foreach (int id in remove)
        {
            Destroy(avatars[id].root);
            avatars.Remove(id);
        }
    }

    Avatar CreateAvatar(PeerState state)
    {
        if (!AOCharacterVisualDatabaseV111.TryBuildBase(
            state.race, state.gender, state.head,
            out AOCharacterRenderer.DirectionVisual[] directions,
            out float headX, out float headY, out float bodyX)) return null;
        AOGridMap grid = player.CurrentGrid;
        if (grid == null) return null;
        GameObject root = new GameObject("Jugador: " + Short(state.name, 20));
        root.transform.position = grid.TileToWorld(state.x, state.y);
        AOCharacterRenderer visual = root.AddComponent<AOCharacterRenderer>();
        visual.Configure(directions, 18f, headX, headY, bodyX);
        visual.SetHeading(state.heading);
        visual.UpdateSorting(AORenderOrderV210.Character(-root.transform.position.y));
        return new Avatar { root = root, visual = visual,
            name = Short(state.name, 20), target = root.transform.position };
    }

    void OnGUI()
    {
        if (!connected || gameCamera == null ||
            !AOMainMenuV140.SessionActive) return;
        GUIStyle label = new GUIStyle(GUI.skin.label)
        { alignment = TextAnchor.MiddleCenter, fontSize = 13, fontStyle = FontStyle.Bold };
        label.normal.textColor = new Color(1f, 0.89f, 0.56f);
        foreach (Avatar avatar in avatars.Values)
        {
            if (avatar.root == null) continue;
            Vector3 screen = gameCamera.WorldToScreenPoint(avatar.visual.SpeechAnchor);
            if (screen.z <= 0 || !gameCamera.pixelRect.Contains(new Vector2(screen.x, screen.y)))
                continue;
            string text = avatar.speechUntil > Time.unscaledTime &&
                AOPlayerSettingsV230.ShowSpeech ? avatar.speech : avatar.name;
            GUI.Label(new Rect(screen.x - 90, Screen.height - screen.y - 27,
                180, 30), text, label);
        }
    }

    static string Short(string value, int max)
    {
        if (string.IsNullOrEmpty(value)) return "";
        value = value.Replace('\r', ' ').Replace('\n', ' ');
        return value.Length <= max ? value : value.Substring(0, max);
    }

    void Disconnect()
    {
        shuttingDown = true;
        connected = false;
        connecting = false;
        myId = 0;
        try { socket?.Close(); } catch (SocketException) { }
        socket = null;
        stream = null;
        foreach (Avatar avatar in avatars.Values)
            if (avatar.root != null) Destroy(avatar.root);
        avatars.Clear();
        while (incoming.TryDequeue(out _)) { }
    }

    void OnDestroy()
    {
        Disconnect();
        if (instance == this) instance = null;
    }
}
