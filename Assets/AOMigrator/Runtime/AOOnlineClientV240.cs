using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Security.Cryptography;
using System.Threading;
using UnityEngine;

// The room owns shared NPCs, rewards, doors and character checkpoints.
// Local saves are used only as the first import, and are never overwritten online.
public class AOOnlineClientV240 : MonoBehaviour
{
    class Avatar
    {
        public GameObject root;
        public AOCharacterRenderer visual;
        public AOCoopPlayer state;
        public string appearance, speech;
        public float speechUntil;
        public Vector3 target;
        public AOMeditationVisualV269 meditation;
        public int meditationFx, castSeq = -1;
    }
    static AOOnlineClientV240 instance;
    static bool protectUntilRestored;
#if UNITY_EDITOR
    public static int TestPortOverride;
    public static void BeginIsolatedTest(int port) { TestPortOverride=port; protectUntilRestored=true; }
    public static int TestRemotePlayers => instance == null ? 0 : instance.avatars.Count;
    public static int TestNpcCount => instance == null ? 0 : instance.npcs.Count;
    public static void TestShowGroup() { if(instance!=null)instance.showGroup=true; }
#endif
    string localBeforeOnline;
    Vector2 groupScroll;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnlineStatics() { instance = null; protectUntilRestored = false;
#if UNITY_EDITOR
        TestPortOverride=0;
#endif
    }
    readonly ConcurrentQueue<string> incoming = new ConcurrentQueue<string>();
    readonly Dictionary<int, Avatar> avatars = new Dictionary<int, Avatar>();
    readonly Dictionary<string, Avatar> remotePets = new Dictionary<string, Avatar>();
    readonly Dictionary<int, AONPCCombatV09> npcs = new Dictionary<int, AONPCCombatV09>();
    // Creatures summoned by NPCs in the room (CoopRoom.NpcMagic.cs): created here, removed when the room drops them.
    readonly Dictionary<int, AONPCCombatV09> summoned = new Dictionary<int, AONPCCombatV09>();
    readonly Dictionary<int, AOLootPickupV09> drops = new Dictionary<int, AOLootPickupV09>();
    readonly List<AOCoopItem> pendingItems = new List<AOCoopItem>();
    readonly object writeGate = new object();
    TcpClient socket;
    NetworkStream stream;
    AOTestPlayer player;
    AOActionBarV260 actionBar;
    int castSeq, castSpell, castX, castY;
    static int pendingFlightMs, pendingFlightFrame = -1;
    AOWorldManagerV07 world;
    AOSaveGameV140 save;
    Camera gameCamera;
    bool connected, connecting, requested, sessionStarted, restored, showGroup;
    string address, roomKey, status = "", pendingRequest;
    int myId, generation, cachedMap;
    long acknowledged, wallet, bank, serverOffset;
    int duelRound;
    readonly HashSet<string> duelRequests = new HashSet<string>();
    float nextPosition, nextCheckpoint, transactionAt;
    AOCoopPlayer[] players = new AOCoopPlayer[0];
    AOCoopMessage latestState;
    AOCoopStock[] stocks = new AOCoopStock[0];
    public static bool GroupOpen => instance != null && instance.showGroup;

    public static bool Connected => instance != null && instance.connected && instance.restored;
    public static bool Requested => instance != null && instance.requested;
    public static bool ProtectLocalSave => protectUntilRestored || (instance != null && instance.sessionStarted)
#if UNITY_EDITOR
        || TestPortOverride>0
#endif
        ;
    public static bool InputBlocked => instance != null && instance.sessionStarted && (!Connected || !string.IsNullOrEmpty(instance.pendingRequest));
    public static string SavedAddress => PlayerPrefs.GetString("AO.Online.Address", "127.0.0.1");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        Application.runInBackground = true;
        if (instance != null) return;
        instance = new GameObject("AO Online Client").AddComponent<AOOnlineClientV240>();
        // Retos (demo): the duel UI of Interfaz talks to the room through these hooks.
        AODuelUI.Backend = new DuelBackend();
        AODuelUI.ServerNowMs = () => ServerNowMs;
        AODuelUI.RemotePlayerAtGUI = RemotePlayerAtGUI;
    }
    public static bool Prepare(string host, string secret, out string error)
    {
        error = ""; host = (host ?? "").Trim(); secret = (secret ?? "").Trim();
        if (host.Length == 0 || host.Length > 253 || !System.Text.RegularExpressions.Regex.IsMatch(host, @"^[a-zA-Z0-9][a-zA-Z0-9.\-]*$"))
        { error = "Dirección inválida. Usá IP de Tailscale o nombre de equipo."; return false; }
        if (secret.Length < 24 || secret.Length > 128) { error = "Pegá la clave de sala del servidor."; return false; }
        if (instance == null) Bootstrap();
        instance.address = host; instance.roomKey = secret; instance.requested = true;
#if UNITY_EDITOR
        if(TestPortOverride>0)return true;
#endif
        PlayerPrefs.SetString("AO.Online.Address", host); PlayerPrefs.Save(); return true;
    }
    public static void UseLocalMode()
    {
        if (instance == null) return;
        Checkpoint(false); instance.Disconnect();
        if (!string.IsNullOrEmpty(instance.localBeforeOnline) && instance.save != null) instance.save.ApplyOnline(instance.localBeforeOnline);
        instance.localBeforeOnline = null; protectUntilRestored = false; instance.requested = instance.sessionStarted = false;
        instance.pendingItems.Clear();
    }
    public static void StartSession() { if (Requested) { instance.sessionStarted = true; protectUntilRestored = true; instance.Connect(); } }
    public static void SendChat(string text)
    {
        if (Connected) instance.Send(new AOCoopMessage { type = "chat", text = Short(text, 160) });
    }
    public static bool Checkpoint(bool notify)
    {
        // save/player can already be destroyed when leaving Play or closing (QA: MissingReferenceException in OnDestroy).
        if (!Connected || instance.world == null || instance.world.IsLoading || instance.save == null || instance.player == null) return false;
        instance.SendSnapshot(new AOCoopMessage { type = "sync" });
        if (notify) AOInterfaceV0101.PushMessage("Guardado enviado al anfitrión.");
        return true;
    }
    public static AOCoopItem[] CapturePendingItems() => instance != null && ProtectLocalSave ? instance.pendingItems.ToArray() : null;
    public static void RestorePendingItems(AOCoopItem[] items)
    { if (instance == null) return; instance.pendingItems.Clear(); if (items != null) instance.pendingItems.AddRange(items.Where(i => i != null && i.item > 0 && i.amount > 0)); }
    // Everybody on the map sees an NPC cast (CoopRoom.NpcMagic.cs "npcCast"): its animation and the spell toward its target.
    void NpcCastArrived(AOCoopMessage m)
    {
        var spell = AOSpellDatabaseV120.Get(m.spell);
        if (spell == null || player == null || !npcs.TryGetValue(m.id, out AONPCCombatV09 npc) || npc == null) return;
        Vector3 from = npc.transform.position, to;
        if (m.target == myId) to = player.transform.position;
        else if (m.target > 0 && avatars.TryGetValue(m.target, out Avatar a) && a.root != null) to = a.root.transform.position;
        else to = player.CurrentGrid.TileToWorld(m.x, m.y);
        AOSpellFXV120.PlayFromNpc(npc.gameObject, spell, from, to);
    }
    // AONPCSpellCasterV902 online: the server decided the spell and the damage; here the other effects and the message.
    void NpcSpellArrived(int spellId, int npcId, string caster)
    {
        var spell = AOSpellDatabaseV120.Get(spellId);
        if (spell == null || player == null) return;
        npcs.TryGetValue(npcId, out AONPCCombatV09 npc);
        Vector3 from = npc != null ? npc.transform.position : player.transform.position;   // the cast itself came with npcCast
        var magic = player.GetComponent<AOPlayerMagicV120>();
        if (magic != null) magic.ApplyNpcSpell(spellId, from, false);
        if (!string.IsNullOrEmpty(caster)) AOInterfaceV0101.PushMessage(caster + " lanzó " + spell.name + ".");
    }
    public static void Attack(AONPCCombatV09 npc) { if (npc != null) Request(new AOCoopMessage { type = "attack", target = npc.NetworkId }); }
    public static void PetAttack(AOSummonedPetV129 pet, AONPCCombatV09 npc)
    { if (pet != null && npc != null) Request(new AOCoopMessage { type = "petHit", target = npc.NetworkId, item = pet.GetInstanceID() }); }
    public static void Pickup(AOLootPickupV09 loot)
    { if (loot != null && loot.NetworkId > 0) Request(new AOCoopMessage { type = "pickup", target = loot.NetworkId }, true); }
    public static void ToggleDoor(AODoorV210 door)
    { if (door != null) Request(new AOCoopMessage { type = "door", x = door.TileX, y = door.TileY }); }
    public static int Stock(int npc,int item,int fallback) => instance?.stocks.FirstOrDefault(s=>s.npc==npc&&s.item==item)?.amount ?? fallback;
    public static bool Trade(string type,int npc,int item,int amount) => Request(new AOCoopMessage{type=type,id=npc,item=item,amount=amount},true);
    public static bool Drop(int item, int amount) => Request(new AOCoopMessage { type = "drop", item = item, amount = amount }, true);
    // Gold (protocol 3): the server moves it; wallet/bank come back as absolute values.
    public static long Wallet => instance == null ? 0 : instance.wallet;
    public static long BankGold => instance == null ? 0 : instance.bank;
    public static bool RequestBank(long gold) => gold != 0 && Request(new AOCoopMessage { type = "bank", gold = gold }, true);
    // Call after the quest is marked completed; time = completion number (1 for non-repeatable quests).
    public static bool RequestQuestReward(int quest, int time) => Request(new AOCoopMessage { type = "questReward", id = quest, amount = Math.Max(1, time) }, true);
    public static int PlayerAt(int x, int y) => !Connected ? 0 : instance.players.FirstOrDefault(p => p.id != instance.myId && p.map == instance.world.CurrentMapNumber && p.x == x && p.y == y)?.id ?? 0;
    public static bool CastAlly(int spell, int x, int y)
    {
        var s = AOSpellDatabaseV120.Get(spell);
        var eot = s == null || s.eotId <= 0 ? null : AOMagicEffectDatabaseV129.Get(s.eotId);
        bool harmfulEffect = eot != null && (eot.buffType == 2 || eot.buffType == 4 || (eot.type == 1 && eot.tickPowerMax < 0));
        if (harmfulEffect || s == null || s.raiseHp == 2 || s.paralyze != 0 || s.immobilize != 0 || s.poison != 0 || s.incinerate != 0 || s.curse != 0 || s.blindness != 0 || s.dumb != 0 || s.raiseMana == 2)
        { AOInterfaceV0101.PushMessage("Sala cooperativa: no podés atacar a tus compañeros."); return false; }
        return Request(new AOCoopMessage { type = "cast", spell = spell, id = PlayerAt(x, y), x = x, y = y });
    }
    public static bool CastNpc(int spell, AONPCCombatV09 npc)
    {
        if (!SupportsNpcSpell(spell)) return false;
        var tile = npc.GetComponent<AONPCMovementV08>();
        return tile != null && Request(new AOCoopMessage { type = "cast", spell = spell, target = npc.NetworkId, x = tile.TileX, y = tile.TileY, amount = TakeSkillShotFlightMs() });
    }
    // Call right before the skill shot hit is applied, so the server counts the cooldown from the launch.
    public static void SetSkillShotFlight(float seconds) { pendingFlightMs = Mathf.Clamp(Mathf.RoundToInt(seconds * 1000f), 0, 1500); pendingFlightFrame = Time.frameCount; }
    static int TakeSkillShotFlightMs() { int ms = pendingFlightFrame == Time.frameCount ? pendingFlightMs : 0; pendingFlightMs = 0; pendingFlightFrame = -1; return ms; }
    // Local cast animation, shown to the rest of the room via the next position update.
    public static void NotifyLocalCast(int spell) { NotifyLocalSkillShot(spell, 0, 0); }
    // A skill shot also tells the aimed tile: the others draw the projectile (visual only; the hit is still the caster's).
    public static void NotifyLocalSkillShot(int spell, int x, int y)
    { if (!Connected) return; instance.castSeq++; instance.castSpell = spell; instance.castX = x; instance.castY = y; instance.nextPosition = 0; }
    public static bool CastArea(int spell, int x, int y) => SupportsNpcSpell(spell) && Request(new AOCoopMessage { type = "cast", spell = spell, x = x, y = y });
    // Same filter as CoopRoom.Cast; no message, so callers can check before spending mana.
    public static bool CanCastNpcSpell(int spell)
    {
        var s=AOSpellDatabaseV120.Get(spell);
        return s!=null && (s.materializeObject>0 || (s.eotId==0 && s.stealBuff==0 && s.speed<=0 &&
            (s.raiseHp!=0||s.paralyze!=0||s.immobilize!=0||s.removeParalysis!=0||s.poison!=0||s.incinerate!=0||s.curePoison!=0||s.removeDebuff!=0)));
    }
    static bool SupportsNpcSpell(int spell)
    {
        if(CanCastNpcSpell(spell))return true;
        AOInterfaceV0101.PushMessage("Ese efecto sobre criaturas todavía no está disponible en la alpha cooperativa.");return false;
    }
    static bool Request(AOCoopMessage message, bool transaction = false)
    {
        if (!Connected || InputBlocked) return false;
        message.request = Guid.NewGuid().ToString("N");
        if (transaction) { instance.pendingRequest = message.request; instance.transactionAt = Time.unscaledTime; }
        if (message.type.StartsWith("duel")) instance.duelRequests.Add(message.request);
        instance.SendSnapshot(message); return true;
    }

    // ---- Retos de la demo (red.md §8). The server decides; the UI (AODuelUI) and AODuelClient only show. ----
    // duel*/fx pushes and journal events (warp, duelHurt, duelEnd) for Programación's AODuelClient.
    public static event Action<AOCoopMessage> DuelMessage;
    public static event Action<AOCoopEvent> DuelEvent;
    public static long ServerNowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (instance == null ? 0 : instance.serverOffset);
    public static bool AttackPlayer(int playerId) => playerId > 0 && Request(new AOCoopMessage { type = "attack", id = playerId });
    // Spells and skill shots on a player (duel rival or teammate); the flight time is picked up as in CastNpc.
    public static bool CastPlayer(int spell, int playerId, int x, int y) =>
        playerId > 0 && Request(new AOCoopMessage { type = "cast", spell = spell, id = playerId, x = x, y = y, amount = TakeSkillShotFlightMs() });
    // Potions inside a duel go to the server (it heals the duel life and removes the item).
    public static bool UseInDuel(int item) => Request(new AOCoopMessage { type = "use", item = item });
    sealed class DuelBackend : IAODuelBackend
    {
        public bool Available => Connected && AOMainMenuV140.BattleDemoOnline;
        public void Challenge(string[] players, int bet, int maxRedPotions) =>
            Request(new AOCoopMessage { type = "duelChallenge", text = string.Join(";", players ?? new string[0]), gold = bet, item = maxRedPotions });
        public void Accept(string challenger) => Request(new AOCoopMessage { type = "duelAccept", name = challenger });
        public void Reject(string challenger) => Request(new AOCoopMessage { type = "duelReject", name = challenger });
        public void Cancel() => Request(new AOCoopMessage { type = "duelCancel" });
        public void Abandon() => Request(new AOCoopMessage { type = "duelAbandon" });
        public void List() => Request(new AOCoopMessage { type = "duelList" });
    }
    // Remote avatar under the mouse (GUI coordinates, y down) for the "Retar" user menu.
    static string RemotePlayerAtGUI(Vector2 gui)
    {
        var cam = AOActionBarV260.GameCamera;
        if (!Connected || cam == null) return null;
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(gui.x, Screen.height - gui.y, Mathf.Abs(cam.transform.position.z)));
        foreach (var a in instance.avatars.Values)
        {
            if (a.root == null || a.state == null) continue;
            Vector3 p = a.root.transform.position;
            if (Mathf.Abs(world.x - p.x) <= .5f && world.y - p.y >= -.5f && world.y - p.y <= 1.5f) return a.state.name;
        }
        return null;
    }
    static int Gold(long value) => (int)Math.Min(int.MaxValue, Math.Max(0, value));
    static string[] Names(string[] names) => names ?? new string[0];
    void HandleDuel(AOCoopMessage m)
    {
        var d = m.duel ?? new AOCoopDuel();
        switch (m.type)
        {
            case "duelInvite": AODuelUI.ReceiveInvite(d.from, d.level, Names(d.teamA), Names(d.teamB), Gold(d.bet), d.maxPotions, Mathf.Max(0, (d.expiresAt - d.serverTime) / 1000f)); break;
            case "duelInviteClosed": AODuelUI.ReceiveInviteClosed(d.from, d.reason); break;
            case "duelWaiting":
                AODuelUI.ReceiveWaiting(Names(d.missing));
                if (!string.IsNullOrEmpty(m.text)) AOInterfaceV0101.PushMessage(Short(m.text, 180));
                break;
            case "duelStart": AODuelUI.ReceiveStart(d.sala, Names(d.teamA), Names(d.teamB), Gold(d.bet), (int)Math.Max(0, (d.endsAt - d.serverTime) / 1000)); break;
            case "duelRoundStart":
                duelRound = d.round;
                AODuelUI.ReceiveDown(false);
                AODuelUI.ReceiveRoundStart(d.round, Mathf.CeilToInt(Mathf.Max(0, d.startsAt - d.serverTime) / 1000f), d.serverTime);
                break;
            case "duelRoundEnd": AODuelUI.ReceiveRoundEnd(d.round, d.winner, d.winsA, d.winsB); break;
            case "duelAnnounce": AODuelUI.ReceiveAnnounce(m.text); break;
            case "duelNotice": AOInterfaceV0101.PushMessage(Short(m.text, 180)); break;
            case "duelRingState":
                bool free = d.phase == "libre";
                AODuelUI.ReceiveRingState(d.sala, d.phase, free ? "" : string.Join(", ", Names(d.teamA)) + " vs " + string.Join(", ", Names(d.teamB)),
                    Gold(d.bet), d.round, d.winsA, d.winsB, free ? 0 : (int)Math.Max(0, (d.endsAt - d.serverTime) / 1000));
                break;
        }
        DuelMessage?.Invoke(m);
    }
    void Connect()
    {
        Disconnect();
        player = UnityEngine.Object.FindFirstObjectByType<AOTestPlayer>();
        world = UnityEngine.Object.FindFirstObjectByType<AOWorldManagerV07>();
        save = UnityEngine.Object.FindFirstObjectByType<AOSaveGameV140>(); gameCamera = Camera.main;
        if (player == null || world == null || save == null) { status = "Falta el jugador o mapa."; return; }
        string snapshot = save.CaptureOnline();
        if (string.IsNullOrEmpty(snapshot)) { status = "Todavía se está preparando el personaje."; return; }
        if (localBeforeOnline == null) localBeforeOnline = snapshot;
        string name = player.GetComponent<AOCharacterIdentityV170>().CharacterName;
        string roomIdentity; using (var hash = SHA256.Create()) roomIdentity = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(roomKey))).Replace("-", "");
        string identityKey = "AO.Coop.Identity." + roomIdentity + "." + name.ToLowerInvariant();
        string identity = PlayerPrefs.GetString(identityKey, "");
#if UNITY_EDITOR
        if(TestPortOverride>0) identity=Guid.NewGuid().ToString("N")+":"+Guid.NewGuid().ToString("N")+Guid.NewGuid().ToString("N");
#endif
        if (identity.Length != 97)
        {
            identity = Guid.NewGuid().ToString("N") + ":" + Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(identityKey, identity); PlayerPrefs.Save();
        }
        var hello = new AOCoopMessage { type = "hello", version = AOCoopMessage.Protocol, key = roomKey,
            characterId = identity.Substring(0,32), token = identity.Substring(33), snapshot = snapshot, player = CapturePlayer() };
        // The demo is its own server instance (--demo --data SavesDemo --port 7778), apart from the normal room.
        int port = AOMainMenuV140.BattleDemoOnline ? 7778 : 7777;
        connecting = true; status = "Conectando a " + address + ":" + port + "...";
        AODuelUI.LocalName = name;
#if UNITY_EDITOR
        if(TestPortOverride>0)port=TestPortOverride;
#endif
        int current = generation; string host = address, json = JsonUtility.ToJson(hello);
        new Thread(() => ReadLoop(host, port, json, current)) { IsBackground = true, Name = "AO Coop" }.Start();
    }
    void ReadLoop(string host, int port, string hello, int current)
    {
        TcpClient local = null;
        try
        {
            local = new TcpClient { NoDelay = true, SendTimeout = 2000 };
            var pending = local.BeginConnect(host, port, null, null);
            using (pending.AsyncWaitHandle) if (!pending.AsyncWaitHandle.WaitOne(5000)) throw new IOException("Tiempo de conexión agotado.");
            local.EndConnect(pending);
            if (generation != current) return;
            socket = local; stream = local.GetStream(); stream.ReadTimeout = 60000; WriteLine(hello);
            while (generation == current)
            {
                string line = ReadLine(local.GetStream()); if (line == null) break;
                if (incoming.Count > 512) throw new IOException("La sala envía datos demasiado rápido.");
                incoming.Enqueue(line);
            }
        }
        catch (Exception e) when (e is IOException || e is SocketException || e is ObjectDisposedException)
        { if (generation == current) incoming.Enqueue(JsonUtility.ToJson(new AOCoopMessage { type = "error", text = e.Message })); }
        finally { local?.Close(); if (generation == current) incoming.Enqueue("{\"type\":\"closed\"}"); }
    }
    static string ReadLine(NetworkStream input)
    {
        using (var bytes = new MemoryStream())
        {
            while (bytes.Length < 1048576)
            {
                int b = input.ReadByte(); if (b < 0) return null;
                if (b == '\n') return Encoding.UTF8.GetString(bytes.ToArray()); bytes.WriteByte((byte)b);
            }
        }
        throw new IOException("Respuesta demasiado grande.");
    }
    void WriteLine(string line)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(line + "\n");
        lock (writeGate) { if (stream == null) throw new IOException("Sin conexión."); stream.Write(bytes, 0, bytes.Length); }
    }
    void Send(AOCoopMessage message)
    {
        try { WriteLine(JsonUtility.ToJson(message)); }
        catch (Exception e) when (e is IOException || e is SocketException || e is ObjectDisposedException)
        { status = "Se perdió la conexión. Reconectá para recuperar la partida."; Disconnect(); }
    }
    void SendSnapshot(AOCoopMessage message)
    {
        if (save == null || player == null) return;   // scene being torn down
        message.snapshot = save.CaptureOnline(); message.ack = acknowledged; message.player = CapturePlayer(); Send(message);
    }
    AOCoopPlayer CapturePlayer()
    {
        var r = player.GetComponent<AOPlayerRPGV11>(); var c = player.GetComponent<AOPlayerCombatV09>(); var i = player.GetComponent<AOInventoryV10>();
        return new AOCoopPlayer { map = world.CurrentMapNumber, x = player.TileX, y = player.TileY, heading = player.Heading,
            attack = c.AttackPower, evasion = c.EvasionPower, defense = c.Defense, minHit = r.MinHit, maxHit = r.MaxHit,
            strength = r.Strength, damageModifier = r.GetDamageModifier(i), maxMana = r.MaxMana,
            meditationFx = LocalMeditationFx(r), castSpell = castSpell, castSeq = castSeq, castX = castX, castY = castY,
            pets = UnityEngine.Object.FindObjectsByType<AOSummonedPetV129>(FindObjectsSortMode.None).Where(p => !p.Stored).Select(p => new AOCoopPet {
                id = p.GetInstanceID(), npc = p.NpcIndex, x = p.TileX, y = p.TileY, heading = p.GetComponent<AOCharacterRenderer>().Heading }).ToArray() };
    }
    int LocalMeditationFx(AOPlayerRPGV11 r)
    {
        var magic = player.GetComponent<AOPlayerMagicV120>();
        if (magic == null || !magic.IsMeditating) return 0;
        var visual = player.GetComponent<AOMeditationVisualV269>();
        return visual != null && visual.ActiveFx > 0 ? visual.ActiveFx : AOMeditationVisualV269.FxForLevel(r.Level, false);
    }
    void Update()
    {
        int handled = 0;
        while (handled++ < 32 && incoming.TryDequeue(out string line))
        {
            AOCoopMessage m;
            try { m = JsonUtility.FromJson<AOCoopMessage>(line); } catch (Exception) { continue; }
            if (m == null) continue;
            // Server clock estimate for countdowns (latency makes it a little early, never late).
            if (m.serverTime > 0 && (m.type == "welcome" || m.type == "state")) serverOffset = m.serverTime - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (m.type != null && (m.type.StartsWith("duel") || m.type == "fx")) { if (restored) HandleDuel(m); continue; }
            if (m.type == "npcCast") { if (restored) NpcCastArrived(m); continue; }
            if (m.type == "welcome")
            {
                myId = m.id; acknowledged = m.ack; connected = true; connecting = false;
                restored = save.ApplyOnline(m.snapshot);
                if (!restored) { status = "No se pudo recuperar el personaje."; Disconnect(); continue; }
                ApplyEvents(m.events); ApplyServerGold(m); Checkpoint(false);
                status = ""; AOInterfaceV0101.PushMessage("Sala cooperativa. EXP compartida cerca del enemigo; botín único. Botón Grupo para entregar objetos.");
            }
            else if (m.type == "state" && restored) { latestState = m; players = m.players ?? new AOCoopPlayer[0]; stocks = m.stocks ?? new AOCoopStock[0]; ApplyEvents(m.events); ApplyServerGold(m); }
            else if (m.type == "result")
            {
                ApplyEvents(m.events); ApplyServerGold(m);
                if (m.request == pendingRequest) { pendingRequest = null; Checkpoint(false); }
                bool duel = m.request != null && duelRequests.Remove(m.request);
                if (!m.ok && duel) AODuelUI.ReceiveError(Short(m.text, 180));
                else if (!m.ok) AOInterfaceV0101.PushMessage(Short(m.text, 180));
                if (duel && m.duels != null) foreach (var ring in m.duels) HandleDuel(new AOCoopMessage { type = "duelRingState", duel = ring });
            }
            else if (m.type == "chat" && m.id != myId)
            {
                AOInterfaceV0101.PushMessage(Short(m.name,20) + ": " + Short(m.text,160));
                if (avatars.TryGetValue(m.id, out var a)) { a.speech = Short(m.text,160); a.speechUntil = Time.unscaledTime + 6; }
            }
            else if (m.type == "error")
            {
                status = Short(m.text,180);
                // An older server answers "incompatible" too: this client is already the new one.
                if (!restored && status.Contains("incompatible")) status += " Si ya tenés el cliente nuevo (protocolo " + AOCoopMessage.Protocol + "), el anfitrión tiene que actualizar el servidor.";
                AOInterfaceV0101.PushMessage("Red: " + status);
            }
            else if (m.type == "closed") { if (string.IsNullOrEmpty(status)) status = "Servidor desconectado. Reconectá para recuperar la partida."; Disconnect(); }
        }
        if (Connected && world != null && !world.IsLoading)
        {
            if (latestState != null && latestState.map == world.CurrentMapNumber) { ApplyWorld(latestState); latestState = null; }
            DeliverPendingItems();
            if (Time.unscaledTime >= nextPosition) { nextPosition = Time.unscaledTime + .15f; Send(new AOCoopMessage { type = "position", player = CapturePlayer() }); }
            if (Time.unscaledTime >= nextCheckpoint) { nextCheckpoint = Time.unscaledTime + 1f; Checkpoint(false); }
            if (pendingRequest != null && Time.unscaledTime - transactionAt > 8) { status = "La operación no respondió. Reconectá para comprobar su resultado."; Disconnect(); }
        }
        foreach (var a in avatars.Values.Concat(remotePets.Values)) if (a.root != null)
            a.root.transform.position = Vector3.Lerp(a.root.transform.position, a.target, Mathf.Clamp01(Time.unscaledDeltaTime * 15));
    }
    void ApplyEvents(AOCoopEvent[] events)
    {
        if (!restored || events == null) return;
        var combat = player.GetComponent<AOPlayerCombatV09>(); var inv = player.GetComponent<AOInventoryV10>(); var quests = player.GetComponent<AOQuestSystemV150>();
        foreach (var e in events)
        {
            if (e.seq <= acknowledged) continue;
            if (e.seq != acknowledged + 1) { status = "Falta una operación del servidor. Reconectá."; Disconnect(); return; }
            if (e.type == "kill")
            {
                combat.AddRewards(e.exp, 0); quests?.NotifyNpcKilled(e.npc);
                if (e.items != null) foreach (var item in e.items)
                    if (item != null && quests != null && quests.NeedsQuestItem(item.quest,item.item,out _)) pendingItems.Add(item);
            }
            // Protocol 3: gold never comes from events; the absolute wallet follows every message.
            else if (e.type == "buy") pendingItems.Add(new AOCoopItem { item=e.item,amount=e.amount });
            else if (e.type == "sell") inv.RemoveItemByIndexPublic(e.item,e.amount);
            else if (e.type == "item" && e.item == AONPCLootDatabaseV180.GoldItemIndex) AOInterfaceV0101.PushMessage("Recogiste " + e.amount + " monedas de oro.");
            else if (e.type == "item") pendingItems.Add(new AOCoopItem { item = e.item, amount = e.amount });
            else if (e.type == "remove")
            {
                if (!inv.RemoveItemByIndexPublic(e.item,e.amount)) { status = "Inventario cambió durante la entrega. Reconectá para recuperarlo."; Disconnect(); return; }
            }
            else if (e.type == "hurt")
            {
                combat.ReceiveOnlineDamage(e.damage);
                // A creature's spell (server CoopRoom.NpcMagic.cs): e.spell = spell id, e.npc = the caster's network id.
                if (e.spell > 0) NpcSpellArrived(e.spell, e.npc, e.text);
            }
            else if (e.type == "spell") player.GetComponent<AOPlayerMagicV120>().ApplyOnlineSpell(e.spell);
            // Duel journal (it survives a disconnection): the life shown in a duel is the server's (e.hp).
            // A hurt reaches us with the next state, possibly after the next round already started (QA R-08): only the
            // current round's hurts count; an older one is dropped (the life comes back with duelRoundStart anyway).
            else if (e.type == "duelHurt")
            {
                if (e.duel != null && e.duel.round == duelRound) { if (e.hp <= 0) AODuelUI.ReceiveDown(true); DuelEvent?.Invoke(e); }
            }
            else if (e.type == "warp") DuelEvent?.Invoke(e);
            // Death in the demo dungeon: the items already left with "remove" events and the gold with the wallet; this only tells.
            else if (e.type == "deathDrop")
            {
                int lost = e.items == null ? 0 : e.items.Length;
                AOInterfaceV0101.PushMessage("Al morir se te cayeron " + lost + " objeto(s) y " + e.gold + " monedas de oro.");
            }
            else if (e.type == "duelEnd") { AODuelUI.ReceiveEnd(e.duel?.result ?? "", Gold(e.duel?.prize ?? 0), Gold(e.duel?.tax ?? 0)); DuelEvent?.Invoke(e); }
            acknowledged = e.seq;
        }
    }
    // Protocol 3: the server owns gold. The local wallet only mirrors it (AddGold/SpendGold are silent).
    void ApplyServerGold(AOCoopMessage m)
    {
        if (!restored || player == null) return;
        wallet = m.wallet; bank = m.bank;
        var combat = player.GetComponent<AOPlayerCombatV09>();
        if (combat.Gold < wallet) combat.AddGold(wallet - combat.Gold); else combat.SpendGold(combat.Gold - wallet);
        if (cityBank == null) cityBank = UnityEngine.Object.FindFirstObjectByType<AOCityBankV130>();
        if (cityBank != null && cityBank.BankGold != bank) cityBank.SetServerGold(bank);
    }
    AOCityBankV130 cityBank;
    void DeliverPendingItems()
    {
        var inv = player.GetComponent<AOInventoryV10>(); var combat = player.GetComponent<AOPlayerCombatV09>();
        for (int i = pendingItems.Count-1; i >= 0; i--)
        {
            var item = pendingItems[i];
            if ((item.item == AONPCLootDatabaseV180.GoldItemIndex || inv.CountItem(item.item)>0 || inv.FreeSlotCountPublic()>0) && combat.TryAddLootItem(item.item,item.amount,AOItemDatabaseV10.Get(item.item)?.name ?? "Objeto")) pendingItems.RemoveAt(i);
        }
    }
    void ApplyWorld(AOCoopMessage m)
    {
        if (cachedMap != m.map || npcs.Count == 0 || npcs.Values.Any(n => n == null))
        {
            ClearWorld(); cachedMap = m.map;
            // Map NPCs only: summoned creatures are rebuilt from the state below (ClearWorld just destroyed them).
            foreach (var n in UnityEngine.Object.FindObjectsByType<AONPCCombatV09>(FindObjectsSortMode.None)) if (n.NetworkId > 0 && !AONPCSpellCasterV902.IsSummonId(n.NetworkId)) npcs[n.NetworkId] = n;
        }
        foreach (var n in m.npcs ?? new AOCoopNpc[0]) if (npcs.TryGetValue(n.id,out var local) && local != null) local.ApplyOnline(n);
        var summonedNow = new HashSet<int>();
        foreach (var n in m.npcs ?? new AOCoopNpc[0])
        {
            if (!AONPCSpellCasterV902.IsSummonId(n.id)) continue;
            summonedNow.Add(n.id);
            if (summoned.TryGetValue(n.id, out var known) && known != null) continue;
            var created = AONPCSpellCasterV902.SpawnSummoned(n.npc, n.x, n.y, n.id);
            if (created == null) continue;
            summoned[n.id] = npcs[n.id] = created; created.ApplyOnline(n);
        }
        foreach (int id in summoned.Keys.Where(id => !summonedNow.Contains(id)).ToArray())
        { npcs.Remove(id); if (summoned[id] != null) Destroy(summoned[id].gameObject); summoned.Remove(id); }
        var seen = new HashSet<int>();
        foreach (var d in m.loot ?? new AOCoopLoot[0])
        {
            seen.Add(d.id);
            if (!drops.TryGetValue(d.id,out var local) || local == null) drops[d.id] = AOLootPickupV09.Create(d.item,d.name,d.amount,d.x,d.y,d.id);
            else local.SetOnlineAmount(d.amount);
        }
        foreach (int id in drops.Keys.Where(id => !seen.Contains(id)).ToArray()) { if (drops[id] != null) drops[id].Consume(); drops.Remove(id); }
        foreach (var door in UnityEngine.Object.FindObjectsByType<AODoorV210>(FindObjectsSortMode.None))
            door.ApplyOnline(m.doors?.FirstOrDefault(d => d.x == door.TileX && d.y == door.TileY)?.open ?? false);
        ApplyAvatars();
    }
    void ApplyAvatars()
    {
        var seen = new HashSet<int>(); var petsSeen = new HashSet<string>();
        foreach (var p in players)
        {
            if (p.id == myId || p.map != world.CurrentMapNumber) continue;
            seen.Add(p.id);
            string appearance = p.race+":"+p.gender+":"+p.head+":"+p.weapon+":"+p.armor+":"+p.helmet+":"+p.shield+":"+p.dead;
            if (!avatars.TryGetValue(p.id,out var a) || a.appearance != appearance)
            {
                if (a != null) Destroy(a.root); a = CreateAvatar(p); if (a == null) continue;
                a.appearance = appearance; avatars[p.id] = a;
            }
            a.state = p; MoveAvatar(a,p.x,p.y,p.heading); ApplyAvatarEffects(a,p);
            foreach (var pet in p.pets ?? new AOCoopPet[0])
            {
                string key = p.id+":"+pet.id; petsSeen.Add(key);
                if (!remotePets.TryGetValue(key,out var pa))
                {
                    var def = AOSummonDatabaseV129.Get(pet.npc); if (def == null) continue;
                    var root = new GameObject("Mascota de " + Short(p.name,20)); var visual = root.AddComponent<AOCharacterRenderer>();
                    visual.Configure(AOSummonDatabaseV129.BuildVisuals(def),def.walkFps,def.headOffsetX/32f,-def.headOffsetY/32f,def.bodyShiftX/32f);
                    pa = new Avatar {root=root,visual=visual}; root.transform.position=player.CurrentGrid.TileToWorld(pet.x,pet.y); remotePets[key]=pa;
                }
                MoveAvatar(pa,pet.x,pet.y,pet.heading);
            }
        }
        foreach (var id in avatars.Keys.Where(id=>!seen.Contains(id)).ToArray()) { Destroy(avatars[id].root); avatars.Remove(id); }
        foreach (var key in remotePets.Keys.Where(k=>!petsSeen.Contains(k)).ToArray()) { Destroy(remotePets[key].root); remotePets.Remove(key); }
    }
    void ApplyAvatarEffects(Avatar a,AOCoopPlayer p)
    {
        int fx=p.dead?0:p.meditationFx;
        if(fx!=a.meditationFx)
        {
            if(a.meditation==null)a.meditation=a.root.AddComponent<AOMeditationVisualV269>();
            if(fx>0)a.meditation.BeginFx(fx);else a.meditation.End();
            a.meditationFx=fx;
        }
        // castSeq -1 = first sighting: remember it without replaying an old cast.
        if(a.castSeq>=0&&p.castSeq!=a.castSeq&&!p.dead)
        {
            var cast=p.castSpell>0?AOSpellDatabaseV120.Get(p.castSpell):null;
            AOCastAnimationRuntimeV268.PlayPlayer(a.root,cast);
            // A companion's skill shot: the same projectile, visual only (the damage is resolved by its caster).
            if(cast!=null&&p.castX>0&&p.castY>0&&AOSkillShotConfigV267.IsSkillShot(cast.id))
            {
                Vector3 from=a.root.transform.position,to=player.CurrentGrid.TileToWorld(p.castX,p.castY);
                AOSkillShotProjectileV267.LaunchVisual(cast,player.CurrentGrid,from,new Vector2(to.x-from.x,to.y-from.y));
            }
        }
        a.castSeq=p.castSeq;
    }
    void MoveAvatar(Avatar a,int x,int y,int heading)
    {
        a.target=player.CurrentGrid.TileToWorld(x,y); a.visual.SetHeading(heading);
        a.visual.SetWalking(Vector3.Distance(a.root.transform.position,a.target)>.03f); a.visual.UpdateSorting(AORenderOrderV210.Character(y));
    }
    Avatar CreateAvatar(AOCoopPlayer p)
    {
        if (!AOCharacterVisualDatabaseV111.TryBuildBase(p.race,p.gender,p.head,out var dirs,out float hx,out float hy,out float bx)) return null;
        var root=new GameObject("Jugador: "+Short(p.name,20)); var visual=root.AddComponent<AOCharacterRenderer>();
        visual.Configure(dirs,18f,hx,hy,bx); root.transform.position=player.CurrentGrid.TileToWorld(p.x,p.y);
        if (p.dead) AODeathVisualV160.TryApplyToRenderer(visual);
        else
        {
            var equipment=AOItemDatabaseV10.BuildEquipmentVisuals(null,AOItemDatabaseV10.Get(p.helmet),AOItemDatabaseV10.Get(p.weapon),AOItemDatabaseV10.Get(p.shield));
            var armor=AOItemDatabaseV10.Get(p.armor); bool body=false;
            if (armor!=null && AOCharacterVisualDatabaseV111.TryBuildArmorBody(armor.BodyForProfile(p.race,p.gender),out var armorDirs,out hx,out hy,out bx))
            { for(int i=0;i<equipment.Length&&i<armorDirs.Length;i++)equipment[i].body=armorDirs[i].body;body=true; }
            visual.ConfigureEquipment(equipment,body,hx,hy,bx);
        }
        return new Avatar {root=root,visual=visual,state=p,target=root.transform.position};
    }
    // Beside the hotbar (above it if there is no room) so 4:3 screens do not cover Q/W/E.
    Rect GroupButtonRect()
    {
        if(actionBar==null&&player!=null)actionBar=player.GetComponent<AOActionBarV260>();
        if(actionBar==null||!actionBar.ShowBarPublic())return new Rect(10,Screen.height-33,130,26);
        var b=actionBar.GetBarRectGUI();
        if(b.width<=0)return new Rect(10,Screen.height-33,130,26);
        return b.xMax+146<=Screen.width?new Rect(b.xMax+8,b.yMax-26,130,26):new Rect(b.x,b.y-32,130,26);
    }
    void OnGUI()
    {
        if (!sessionStarted || !AOMainMenuV140.SessionActive) return;
        var matrix=GUI.matrix; var color=GUI.color; GUI.matrix=Matrix4x4.identity; GUI.color=Color.white;
        if (!Connected)
        {
            GUI.depth=-200; float w=Mathf.Min(500,Screen.width-30);
            GUILayout.BeginArea(new Rect((Screen.width-w)/2,Screen.height/2-85,w,170),GUI.skin.box);
            GUILayout.Label(connecting ? "Conectando con el anfitrión…" : "Partida online pausada");
            GUILayout.Label(status);
            GUI.enabled=!connecting;
            if(GUILayout.Button("Reconectar")) Connect();
            GUI.enabled=true;
            if(GUILayout.Button("Volver al menú")) AOMainMenuV140.ShowFromCreator();
            GUILayout.EndArea();
        }
        else
        {
            GUI.depth=-20;
            var groupButton=GroupButtonRect();
            if(GUI.Button(groupButton,"Grupo ("+players.Length+"/11)"))showGroup=!showGroup;
            if(showGroup)
            {
                float w=Mathf.Min(420,Screen.width-20),h=Mathf.Min(380,Screen.height-50);
                var panel=new Rect(Mathf.Clamp(groupButton.x,10,Mathf.Max(10,Screen.width-w-10)),Mathf.Max(8,groupButton.y-h-8),w,h);
                GUI.color=new Color(.08f,.07f,.05f,1f);GUI.DrawTexture(panel,Texture2D.whiteTexture);GUI.color=Color.white;
                GUILayout.BeginArea(panel,GUI.skin.box);
                groupScroll=GUILayout.BeginScrollView(groupScroll);
                GUILayout.Label("GRUPO · EXP cerca del enemigo (12 casillas)");
                foreach(var p in players) GUILayout.Label(Short(p.name,20)+" · Nv "+p.level+" · "+p.hp+"/"+p.maxHp+" HP · Mapa "+p.map);
                var inv=player.GetComponent<AOInventoryV10>();int item=inv.GetSlotItemIndex(inv.SelectedSlot),amount=inv.GetSlotAmount(inv.SelectedSlot);
                GUILayout.Label("Seleccionado: "+(AOItemDatabaseV10.Get(item)?.name??"ninguno"));
                GUI.enabled=!InputBlocked && item>0;
                GUILayout.BeginHorizontal();if(GUILayout.Button("Soltar 1"))Drop(item,1);if(GUILayout.Button("Soltar todo"))Drop(item,amount);GUILayout.EndHorizontal();GUI.enabled=true;
                GUILayout.Label("El primero que recoge conserva el objeto. E o tecla Recoger.");
                if(pendingItems.Count>0)GUILayout.Label("Recompensas pendientes: "+pendingItems.Count+". Liberá inventario.");
                if(GUILayout.Button("Cerrar"))showGroup=false;
                GUILayout.EndScrollView();GUILayout.EndArea();
            }
            if(gameCamera!=null)
            {
                var label=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=13,fontStyle=FontStyle.Bold,richText=false};
                foreach(var a in avatars.Values)
                {
                    var screen=gameCamera.WorldToScreenPoint(a.visual.SpeechAnchor);
                    if(screen.z<=0||!gameCamera.pixelRect.Contains(new Vector2(screen.x,screen.y)))continue;
                    string text=a.speechUntil>Time.unscaledTime && AOPlayerSettingsV230.ShowSpeech?a.speech:Short(a.state.name,20)+" · Nv "+a.state.level;
                    var rect=new Rect(screen.x-120,Screen.height-screen.y-30,240,30);label.normal.textColor=Color.black;
                    GUI.Label(new Rect(rect.x-1,rect.y,rect.width,rect.height),text,label);GUI.Label(new Rect(rect.x+1,rect.y+1,rect.width,rect.height),text,label);
                    label.normal.textColor=new Color(1f,.9f,.6f);GUI.Label(rect,text,label);
                }
            }
        }
        GUI.matrix=matrix;GUI.color=color;
    }
    static string Short(string value,int max)
    { value=(value??"").Replace('\r',' ').Replace('\n',' ');return value.Length<=max?value:value.Substring(0,max); }
    void ClearWorld()
    {
        foreach(var d in drops.Values)if(d!=null)Destroy(d.gameObject);drops.Clear();npcs.Clear();cachedMap=0;
        foreach(var s in summoned.Values)if(s!=null)Destroy(s.gameObject);summoned.Clear();
        foreach(var a in avatars.Values.Concat(remotePets.Values))if(a.root!=null)Destroy(a.root);avatars.Clear();remotePets.Clear();
    }
    void Disconnect()
    {
        generation++;connected=connecting=restored=false;myId=0;pendingRequest=null;latestState=null;
        try{socket?.Close();}catch(SocketException){}socket=null;stream=null;ClearWorld();
        while(incoming.TryDequeue(out _)){}
    }
    void OnApplicationQuit(){Checkpoint(false);}
    void OnDestroy(){Checkpoint(false);Disconnect();if(instance==this)instance=null;}
}
