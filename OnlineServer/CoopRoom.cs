using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

// Test: loopback only + testLedger; TimeScale: duel timers only (never cooldowns); Demo: demo rules.
sealed record RoomOptions(bool Test = false, double TimeScale = 1, bool Demo = false);

// Private alpha: the room owns NPCs, floor items, gold (ledger) and the durable event journal.
// Character formulas remain in Unity; this is cooperative authority, not MMO anti-cheat.
sealed partial class CoopRoom
{
    public static readonly JsonSerializerOptions Json = new() { IncludeFields = true, MaxDepth = 40 };
    readonly JsonObject catalog;
    readonly Dictionary<int, JsonObject> templates, lootDefs, items, spells, summons, quests;
    readonly Dictionary<int, Session> sessions = new();
    readonly string statePath;
    readonly Random random = new();
    readonly Ledger ledger;
    public readonly RoomOptions Options;
    RoomSave store;
    int nextSession;
    long lastSave;
    bool dirty;
    public readonly object Gate = new();
    public CoopRoom(string catalogPath, string dataDirectory, RoomOptions? options = null)
    {
        Options = options ?? new RoomOptions();
        using (var file = File.OpenRead(catalogPath))
        using (Stream input = catalogPath.EndsWith(".gz") ? new GZipStream(file, CompressionMode.Decompress) : file)
            catalog = JsonNode.Parse(input)!.AsObject();
        templates = Index(catalog["maps"], "mapNumber");
        lootDefs = Index(catalog["loot"]?["npcs"], "npcIndex");
        items = Index(catalog["items"], "index");
        spells = Index(catalog["spells"], "id");
        summons = Index(catalog["summons"]?["summons"], "npcIndex");
        quests = Index(catalog["quests"], "id");
        Directory.CreateDirectory(dataDirectory);
        statePath = Path.Combine(dataDirectory, "world.json");
        store = LoadStore();
        foreach (var map in store.maps.Values) { BindMap(map); foreach(var npc in map.npcs) npc.provoked=0; }
        ledger = new Ledger(dataDirectory);
        foreach (var (id, record) in store.characters) OpenAccounts(id, record);
        // world.json may lag behind the ledger after a crash: never reuse a loot id already paid.
        foreach (string op in ledger.Ops)
            if (op.StartsWith("pickup:") && int.TryParse(op.AsSpan(7), out int loot)) store.nextLoot = Math.Max(store.nextLoot, loot);
        RecoverDuelCustody();
        Console.WriteLine($"Mundo cooperativo: {templates.Count} mapas; {store.characters.Count} personajes guardados; libro en la operación {ledger.LastTx}.");
    }
    public void Close() => ledger.Dispose();
    // First protocol-3 sight of a character: the only time gold reported by the client is trusted.
    void OpenAccounts(string id, CharacterRecord record)
    {
        if (ledger.Has("open:" + id)) return;
        var data = JsonNode.Parse(record.snapshot)!;
        ledger.Transfer("open:" + id, Ledger.World, Ledger.Wallet(id), Math.Max(0, Long(data["combat"], "gold")), "apertura");
        ledger.Transfer("openbank:" + id, Ledger.World, Ledger.Bank(id), Math.Max(0, Long(data["bank"], "gold")), "apertura banco");
    }
    long WalletOf(Session s) => ledger.Balance(Ledger.Wallet(s.CharacterId));
    long BankOf(Session s) => ledger.Balance(Ledger.Bank(s.CharacterId));
    // Stored and sent snapshots always carry the ledger's gold, so an older server could still read them.
    string WithServerGold(Session s, JsonObject data)
    {
        data["combat"]!["gold"] = WalletOf(s);
        if (data["bank"] is JsonObject bank) bank["gold"] = BankOf(s);
        return data.ToJsonString();
    }
    RoomSave LoadStore()
    {
        if (!File.Exists(statePath)) return new RoomSave();
        try { return JsonSerializer.Deserialize<RoomSave>(File.ReadAllText(statePath), Json) ?? throw new IOException("Guardado vacío."); }
        catch (Exception e) when (e is IOException or JsonException)
        {
            if (!File.Exists(statePath + ".bak")) throw new IOException("Guardado inválido; se conserva para recuperación.", e);
            var backup = JsonSerializer.Deserialize<RoomSave>(File.ReadAllText(statePath + ".bak"), Json)
                         ?? throw new IOException("Respaldo inválido.");
            File.Copy(statePath, statePath + ".damaged-" + DateTime.UtcNow.Ticks);
            File.Copy(statePath + ".bak", statePath, true);
            Console.WriteLine("Se recuperó world.json desde su respaldo.");
            return backup;
        }
    }
    // Never throws (review #3): a world.json locked by OneDrive, the antivirus or a backup is logged, stays dirty and is
    // retried by the tick. Gold never depends on it (the ledger is written on its own).
    long lastSaveError;
    public bool Save()
    {
        try
        {
            string temp = statePath + ".tmp";
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(store, Json);
            using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            { file.Write(bytes); file.Flush(true); }
            if (File.Exists(statePath)) File.Replace(temp, statePath, statePath + ".bak", true);
            else File.Move(temp, statePath);
            lastSave = Now; dirty = false;
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            dirty = true; lastSave = Now;
            if (Now - lastSaveError > 10000) { lastSaveError = Now; Console.Error.WriteLine("No se pudo guardar world.json (¿OneDrive o antivirus?); se reintenta: " + e.Message); }
            return false;
        }
    }
    public Session Join(AOCoopMessage hello)
    {
        if (hello.version != AOCoopMessage.Protocol) throw new InvalidOperationException("Cliente incompatible. Instalá el ZIP cooperativo nuevo.");
        if (sessions.Count >= 11) throw new InvalidOperationException("Sala completa (11/11).");
        if (!Guid.TryParseExact(hello.characterId, "N", out _) || hello.token?.Length != 64 || !hello.token.All(Uri.IsHexDigit))
            throw new InvalidOperationException("Identidad de personaje inválida.");
        if (sessions.Values.Any(s => s.CharacterId == hello.characterId)) throw new InvalidOperationException("Ese personaje ya está conectado.");
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hello.token)));
        bool isNew = !store.characters.TryGetValue(hello.characterId, out CharacterRecord? record);
        if (isNew)
        {
            JsonObject data = ValidateSnapshot(hello.snapshot);
            string name = Text(data["character"], "name").Trim();
            if (!NamePattern.IsMatch(name)) throw new InvalidOperationException("Nombre inválido: de 3 a 18 letras (A a Z) y espacios simples.");
            if (store.characters.Values.Any(r => NameSkeleton(r.name) == NameSkeleton(name)))
                throw new InvalidOperationException("Ese nombre ya pertenece a otro personaje en esta sala.");
            // Review #6: with the key alone nobody can fill world.json or book every friend's name.
            if (store.characters.Count >= MaxCharacters) throw new InvalidOperationException($"La sala ya tiene {MaxCharacters} personajes guardados.");
            record = new CharacterRecord { name = name, tokenHash = hash, snapshot = hello.snapshot };
        }
        else if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(hash), Encoding.ASCII.GetBytes(record!.tokenHash)))
            throw new InvalidOperationException("La identidad local no corresponde a ese personaje.");
        // Validate everything before registering (nube: servidor-sesion-fantasma): an unreadable save used to leave a
        // ghost session on map 0 that stopped the whole room on the next tick.
        var session = new Session(nextSession + 1, hello.characterId, record!);
        try { RefreshPlayer(session, hello.player, JsonNode.Parse(record!.snapshot)!.AsObject()); Map(session.State.map); }
        catch (Exception e) when (e is not InvalidOperationException) { throw new InvalidOperationException("Guardado inválido."); }
        if (isNew)
        {
            store.characters.Add(hello.characterId, record!); OpenAccounts(hello.characterId, record!); Save();
            Console.WriteLine($"Personaje nuevo en la sala: {record!.name} ({store.characters.Count}/{MaxCharacters}).");
        }
        nextSession++;
        sessions.Add(session.Id, session);
        DuelResume(session);
        return session;
    }
    // Review #9 (Cerebro: alpha among friends): movement speed is only logged, never cut, because warps and "back home"
    // are legitimate jumps. More than 2 tiles + 1 per 100 ms on the same map counts as suspicious (once a minute per player).
    void NoteSpeed(Session s, int map, int x, int y)
    {
        long now = Now, elapsed = Math.Max(1, now - s.LastMove); s.LastMove = now;
        if (map != s.State.map || Distance(s.State.x, s.State.y, x, y) <= 2 + elapsed / 100 || now - s.LastSpeedLog < 60000) return;
        s.LastSpeedLog = now;
        Console.WriteLine($"Aviso: {s.State.name} se movió {Distance(s.State.x, s.State.y, x, y)} casillas en {elapsed} ms (mapa {map}).");
    }
    // Review #11 (Interfaz): new names follow the original ValidarNombre (General.bas): 3 to 18 characters, only A–Z and
    // single spaces. They are also unique by their skeleton (no accents, lower case), so "Аlpha" (Cyrillic А), "Álpha"
    // or "ALPHA" cannot pose as "Alpha". Existing characters are not touched.
    static readonly System.Text.RegularExpressions.Regex NamePattern = new(@"^(?=.{3,18}$)[A-Za-z]+( [A-Za-z]+)*$");
    static string NameSkeleton(string name)
    {
        var skeleton = new StringBuilder();
        foreach (char c in name.Normalize(NormalizationForm.FormD))
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                skeleton.Append(char.ToLowerInvariant(c));
        return skeleton.ToString();
    }
    public AOCoopMessage Welcome(Session s) => new() { type = "welcome", id = s.Id, version = AOCoopMessage.Protocol,
        snapshot = WithServerGold(s, JsonNode.Parse(s.Record.snapshot)!.AsObject()), ack = s.Record.ack, events = s.Record.events.ToArray(),
        wallet = WalletOf(s), bank = BankOf(s), serverTime = Now };
    public int[] Recipients(int map) => sessions.Values.Where(s=>s.State.map==map).Select(s=>s.Id).ToArray();
    public void Leave(Session s) { sessions.Remove(s.Id); DuelLeave(s); Save(); }
    public AOCoopMessage Process(Session s, AOCoopMessage m)
    {
        if (m.type == "position")
        {
            if (m.player != null && ValidPosition(m.player.map, m.player.x, m.player.y))
            {
                int before = s.State.map;
                if (DuelPositionAllowed(s, m.player.map, m.player.x, m.player.y))
                {
                    NoteSpeed(s, m.player.map, m.player.x, m.player.y);
                    s.State.map = m.player.map; s.State.x = m.player.x; s.State.y = m.player.y; s.State.heading = Math.Clamp(m.player.heading, 1, 4);
                }
                if (s.State.map != before) PushRingsFor(s);
                s.State.meditationFx = s.State.dead ? 0 : Math.Clamp(m.player.meditationFx, 0, 1000);
                if (m.player.castSeq != s.State.castSeq && (m.player.castSpell == 0 || spells.ContainsKey(m.player.castSpell)))
                { s.State.castSeq = m.player.castSeq; s.State.castSpell = m.player.castSpell;
                  s.State.castX = Math.Clamp(m.player.castX, 0, 100); s.State.castY = Math.Clamp(m.player.castY, 0, 100); }
            }
            return new AOCoopMessage { type = "noop" };
        }
        if (!string.IsNullOrEmpty(m.request) && s.Replies.TryGetValue(m.request, out var previous)) return previous;
        var reply = new AOCoopMessage { type = "result", request = m.request, ok = true };
        try
        {
            if (!string.IsNullOrEmpty(m.snapshot)) Checkpoint(s, m);
            if (m.type == "sync") { reply.ack = s.Record.ack; }
            else
            {
                if (Now - s.LastAction < 20) throw new InvalidOperationException("Esperá un instante.");
                // A client that stops acking cannot keep playing (review #12): its journal only grows.
                if (s.Record.events.Count > 1024) throw new InvalidOperationException("Demasiados eventos sin confirmar; reconectá.");
                s.LastAction = Now;
                var map = Map(s.State.map);
                // ModRetos + red.md §1: no trading, dropping, picking up or pets while in a started duel.
                if ((m.type is "pickup" or "drop" or "buy" or "sell" or "bank" or "petHit" or "door" or "questReward") && Fighting(s, out _, out _))
                    throw new InvalidOperationException("No podés hacer eso durante un reto.");
                switch (m.type)
                {
                    case "duelChallenge": DuelChallenge(s, m); break;
                    case "duelAccept": DuelAccept(s, m); break;
                    case "duelReject": DuelReject(s, m); break;
                    case "duelCancel": DuelCancel(s); break;
                    case "duelAbandon": DuelAbandon(s); break;
                    case "duelList": reply.duels = Arenas.Select(RingState).ToArray(); break;
                    case "use": DuelUse(s, m); break;
                    case "attack": Attack(s, map, m); break;
                    case "cast": Cast(s, map, m); break;
                    case "petHit": PetHit(s, map, m); break;
                    case "pickup": Pickup(s, map, m); break;
                    case "drop": Drop(s, map, m); break;
                    case "door": Door(s, map, m); break;
                    case "buy": case "sell": Trade(s,map,m); break;
                    case "bank": BankMove(s,m); break;
                    case "questReward": QuestReward(s,m); break;
                    case "testLedger": reply.text = TestLedger(); break;
                    default: throw new InvalidOperationException("Acción desconocida.");
                }
                // Items and gold are saved at once; everything else rides on the tick's save every second (review #5).
                if (m.type is "pickup" or "drop" or "buy" or "sell" or "bank" or "questReward" or "use") Save(); else dirty = true;
            }
        }
        catch (InvalidOperationException e) { reply.ok = false; reply.text = e.Message; }
        reply.events = s.Record.events.ToArray();
        reply.wallet = WalletOf(s); reply.bank = BankOf(s);
        if (!string.IsNullOrEmpty(m.request))
        {
            s.Replies[m.request] = reply;
            if (s.Replies.Count > 64) s.Replies.Remove(s.Replies.Keys.First());
        }
        return reply;
    }
    void Checkpoint(Session s, AOCoopMessage m)
    {
        if (m.ack < s.Record.ack || m.ack > s.Record.nextEvent) throw new InvalidOperationException("Guardado fuera de secuencia; reconectá.");
        var data = ValidateSnapshot(m.snapshot);
        if (Text(data["character"], "name") != s.Record.name) throw new InvalidOperationException("Nombre de personaje diferente.");
        // Read the state first: a save that cannot be read is never stored (it would break the next join).
        try { RefreshPlayer(s, m.player, data); }
        catch (Exception e) when (e is not InvalidOperationException) { throw new InvalidOperationException("Guardado inválido."); }
        // Gold in the client's snapshot is ignored (A-07): the stored copy always carries the ledger's balances.
        s.Record.snapshot = WithServerGold(s, data); s.Record.ack = m.ack;
        s.Record.events.RemoveAll(e => e.seq <= m.ack);
        dirty = true;
    }
    void RefreshPlayer(Session s, AOCoopPlayer? reported, JsonObject data)
    {
        var p = s.State; var r = data["rpg"]!; var c = data["combat"]!; var w = data["world"]!; var inv = data["inventory"]!;
        p.id = s.Id; p.name = s.Record.name; p.race = Int(r, "raceId"); p.gender = Int(r, "genderId"); p.head = Int(r, "headIndex");
        p.map = Int(w, "map"); p.x = Int(w, "x"); p.y = Int(w, "y"); p.heading = Math.Clamp(Int(w,"heading"),1,4);
        p.level = Math.Clamp(Int(r,"level"),1,100); p.maxHp = Math.Clamp(Int(r,"maxHp"),1,100000); p.hp = Math.Clamp(Int(c,"hp"),0,p.maxHp);
        p.dead = Bool(c,"dead") || p.hp <= 0; p.mana = Math.Max(0,Int(r,"mana"));
        p.weapon = Int(inv,"weapon"); p.armor = Int(inv,"armor"); p.helmet = Int(inv,"helmet"); p.shield = Int(inv,"shield");
        if (reported != null)
        {
            p.attack = Math.Clamp(reported.attack,0,10000); p.evasion = Math.Clamp(reported.evasion,0,10000);
            p.defense = Math.Clamp(reported.defense,0,10000); p.minHit = Math.Clamp(reported.minHit,0,10000);
            p.maxHit = Math.Clamp(reported.maxHit,p.minHit,10000); p.strength = Math.Clamp(reported.strength,1,200);
            p.damageModifier = float.IsFinite(reported.damageModifier) ? Math.Clamp(reported.damageModifier,.1f,10f) : 1;
            p.maxMana = Math.Clamp(reported.maxMana,0,100000); p.magicDefense = Math.Clamp(reported.magicDefense,0,95);
            p.pets =(reported.pets ?? Array.Empty<AOCoopPet>()).Where(t => t != null && summons.ContainsKey(t.npc) &&
                ValidPosition(p.map,t.x,t.y) && Distance(p.x,p.y,t.x,t.y) < 20).Take(3).ToArray();
        }
        foreach(var pending in s.Record.events) if(pending.type=="hurt") p.hp=Math.Max(0,p.hp-pending.damage);
        p.dead |= p.hp==0;
        // Death as reported by the client (its life is its own outside duels), handled once per death.
        bool reportedDead=Bool(c,"dead")||Int(c,"hp")<=0;
        if(!reportedDead&&s.Record.deathDropped){s.Record.deathDropped=false;dirty=true;}
        else if(reportedDead&&!s.Record.deathDropped&&!Fighting(s,out _,out _))DeathDrop(s,data);
        p.arena = p.team = 0;
        // In a started duel, life, mana and position belong to the server (red.md §12 bis).
        if (Fighting(s, out var duel, out var me)) { SyncVitals(s, me); p.map = duel.Ring!.Map; p.x = me.X; p.y = me.Y; }
    }
    JsonObject ValidateSnapshot(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 96000) throw new InvalidOperationException("Guardado inválido.");
        JsonObject data;
        try { data = JsonNode.Parse(value, documentOptions:new JsonDocumentOptions { MaxDepth=32 })!.AsObject(); }
        catch (Exception e) when(e is JsonException or InvalidOperationException or NullReferenceException) { throw new InvalidOperationException("Guardado inválido."); }
        foreach(var field in new[]{"character","rpg","combat","inventory","world","quests"})
            if(data[field] is not JsonObject) throw new InvalidOperationException("Guardado incompleto.");
        string name=Text(data["character"],"name");
        if(name.Length<3 || name.Length>20 || name.Any(char.IsControl)) throw new InvalidOperationException("Nombre inválido.");
        var w=data["world"]!;
        if(!ValidPosition(Int(w,"map"),Int(w,"x"),Int(w,"y"))) throw new InvalidOperationException("Mapa o posición inválidos.");
        var inv=data["inventory"]!;
        if(inv["itemIndices"] is not JsonArray ids || inv["amounts"] is not JsonArray amounts || ids.Count!=amounts.Count || ids.Count<1 || ids.Count>100)
            throw new InvalidOperationException("Inventario inválido.");
        for(int i=0;i<ids.Count;i++) if((ids[i]?.GetValue<int>()??0)<0 || (amounts[i]?.GetValue<int>()??0)<0)
            throw new InvalidOperationException("Cantidad inválida.");
        return data;
    }
    public IEnumerable<(Session, AOCoopMessage)> Tick()
    {
        DuelTick();
        foreach(int id in sessions.Values.Select(s=>s.State.map).Distinct().ToArray()) TickMap(Map(id));
        if(dirty && Now-lastSave>1000) Save();
        foreach(var s in sessions.Values)
        {
            var map=Map(s.State.map);
            yield return (s,new AOCoopMessage { type="state", map=map.id, players=sessions.Values.Select(t=>JsonSerializer.Deserialize<AOCoopPlayer>(JsonSerializer.Serialize(t.State,Json),Json)!).ToArray(),
                npcs=map.npcs.Select(n=>new AOCoopNpc{id=n.state.id,npc=n.state.npc,x=n.state.x,y=n.state.y,heading=n.state.heading,hp=n.state.hp,maxHp=n.state.maxHp,dead=n.state.dead,target=n.state.target}).ToArray(), loot=map.loot.Select(l=>new AOCoopLoot{id=l.id,item=l.item,amount=l.amount,x=l.x,y=l.y,name=l.name}).ToArray(),
                doors=map.doors.Select(d=>new AOCoopDoor{x=d.x,y=d.y,open=d.open}).ToArray(), stocks=store.stocks.Select(k=>new AOCoopStock{npc=int.Parse(k.Key.Split(':')[0]),item=int.Parse(k.Key.Split(':')[1]),amount=k.Value}).ToArray(), events=s.Record.events.ToArray(),
                wallet=WalletOf(s), bank=BankOf(s), serverTime=Now });
        }
    }
    void Attack(Session s, MapRecord map, AOCoopMessage message)
    {
        if(message.id>0){PvpAttack(s,message);return;}
        Alive(s); if(Now<s.NextAttack) throw new InvalidOperationException("Todavía no podés atacar.");
        var n=Target(map,message.target);
        var weapon=items.GetValueOrDefault(s.State.weapon); bool ranged=Int(weapon,"projectile")>0;
        if(ranged?!InBowRange(s.State.x,s.State.y,n.state.x,n.state.y):Distance(s.State.x,s.State.y,n.state.x,n.state.y)>1)
            throw new InvalidOperationException("Enemigo fuera de alcance.");
        var ammo=ranged?TakeAmmo(s,weapon):null;
        s.NextAttack=Now+(ranged?AOPvpFormulas.IntervalArrowMs:700); n.provoked=s.Id;
        if(random.Next(1,101)>Math.Clamp(50+(s.State.attack-Int(n.source,"evasionPower"))*.4,5,95)) return;
        int weaponMin=0,weaponMax=0;
        if(weapon!=null){weaponMin=Int(weapon,"minHitToNpc")>0?Int(weapon,"minHitToNpc"):Int(weapon,"minHit");weaponMax=Int(weapon,"maxHitToNpc")>0?Int(weapon,"maxHitToNpc"):Int(weapon,"maxHit");}
        // CalcularDaño: an arrow adds its own roll to the weapon's, and its maximum to the weapon's maximum.
        int weaponRoll=Roll(weaponMin,weaponMax);
        if(ammo!=null){weaponRoll+=Roll(Int(ammo,"minHit"),Int(ammo,"maxHit"));weaponMax+=Int(ammo,"maxHit");}
        int raw=(int)Math.Round((3f*weaponRoll+weaponMax*.2f*Math.Max(0,s.State.strength-15)+Roll(s.State.minHit,s.State.maxHit))*s.State.damageModifier);
        Hit(s,map,n,Math.Max(0,raw-Int(n.source,"defense")));
    }
    // Bows (docs/claude/contenido/arco-original.md §3): up to 11 columns and 9 rows away.
    static bool InBowRange(int x,int y,int tx,int ty)=>Math.Abs(tx-x)<=11&&Math.Abs(ty-y)<=9;
    // One unit per shot, hit or miss. The equipped munition (snapshot inventory.munition) must match the weapon's
    // munition subtype (1 = arrow, 2 = bullet); a weapon with munition 0 shoots without it.
    JsonNode? TakeAmmo(Session s,JsonNode? weapon)
    {
        int kind=Int(weapon,"munition");
        if(kind<=0)return null;
        var inv=JsonNode.Parse(s.Record.snapshot)!["inventory"]!;int id=Int(inv,"munition");
        if(id<=0||!items.TryGetValue(id,out var ammo)||Int(ammo,"subType")!=kind)throw new InvalidOperationException("No tenés munición equipada para esa arma.");
        int pending=s.Record.events.Where(e=>e.type=="remove"&&e.item==id).Sum(e=>e.amount);
        if(CountItem(inv,id)-pending<1)throw new InvalidOperationException("No te queda munición.");
        RoomForEvent(s);
        Event(s,new AOCoopEvent{type="remove",item=id,amount=1});
        return ammo;
    }
    void PetHit(Session s, MapRecord map, AOCoopMessage m)
    {
        Alive(s); var pets=s.State.pets??Array.Empty<AOCoopPet>(); int slot=Array.FindIndex(pets,p=>p.id==m.item);
        if(slot<0 || !summons.TryGetValue(pets[slot].npc,out var def)) throw new InvalidOperationException("Mascota inexistente.");
        // Cooldown per pet slot (at most 3), not per the id the client sends: new ids cannot skip it (review #10).
        if(s.PetCooldown.TryGetValue(slot,out long next) && Now<next) return;
        var pet=pets[slot]; var n=Target(map,m.target);
        if(Distance(pet.x,pet.y,n.state.x,n.state.y)>2) return;
        s.PetCooldown[slot]=Now+400; Hit(s,map,n,Math.Max(1,Roll(Int(def,"minHit"),Int(def,"maxHit"))-Int(n.source,"defense")));
    }
    const int MaxSkillShotFlightMs = 1500;
    void Cast(Session s, MapRecord map, AOCoopMessage m)
    {
        Alive(s);
        if(!spells.TryGetValue(m.spell,out var spell)) throw new InvalidOperationException("Hechizo inexistente.");
        var save=JsonNode.Parse(s.Record.snapshot)!;
        if(save["magic"]?["learnedSpells"] is not JsonArray known || !known.Any(k=>k?.GetValue<int>()==m.spell))
            throw new InvalidOperationException("No conocés ese hechizo.");
        // Skill shots report their flight time in amount: cooldown counts from the launch, as in the client.
        long castAt=Now-Math.Clamp(m.amount,0,MaxSkillShotFlightMs);
        if(castAt<s.NextCast || (s.SpellCooldown.TryGetValue(m.spell,out long next)&&castAt<next)) throw new InvalidOperationException("Hechizo en recuperación.");
        if(Distance(s.State.x,s.State.y,m.x,m.y)>12) throw new InvalidOperationException("Objetivo fuera de alcance.");
        bool duelCast=false;
        if(m.id>0)
        {
            if(!sessions.TryGetValue(m.id,out var friend) || friend.State.map!=map.id || Distance(s.State.x,s.State.y,friend.State.x,friend.State.y)>12)
                throw new InvalidOperationException("Compañero fuera de alcance.");
            if(!(duelCast=DuelSpell(s,friend,spell,m)))
            {
                if(Harmful(spell)) throw new InvalidOperationException("Esta sala cooperativa no permite daño entre amigos.");
                Event(friend,new AOCoopEvent {type="spell",spell=m.spell,text=s.State.name});
            }
        }
        else if(Int(spell,"materializeObject")>0)
            AddLoot(map,Int(spell,"materializeObject"),Math.Max(1,Int(spell,"materializeCount")),m.x,m.y);
        else
        {
            if(Int(spell,"eotId")>0 || Int(spell,"stealBuff")!=0 || Float(spell,"speed")>0 || (Int(spell,"raiseHp")==0 && Int(spell,"paralyze")==0 && Int(spell,"immobilize")==0 && Int(spell,"removeParalysis")==0 && Int(spell,"poison")==0 && Int(spell,"incinerate")==0 && Int(spell,"curePoison")==0 && Int(spell,"removeDebuff")==0))
                throw new InvalidOperationException("Este efecto sobre criaturas todavía no está disponible en la alpha cooperativa.");
            int radius=Math.Clamp(Int(spell,"areaRadius")/2,0,12);
            if(radius>0&&!Harmful(spell)&&(Int(spell,"areaAffects")==1||Int(spell,"areaAffects")==3))
                foreach(var ally in sessions.Values.Where(a=>a.State.map==map.id&&Distance(a.State.x,a.State.y,m.x,m.y)<=radius)) Event(ally,new AOCoopEvent{type="spell",spell=m.spell});
            var targets=radius>0 ? map.npcs.Where(n=>!n.state.dead&&Distance(n.state.x,n.state.y,m.x,m.y)<=radius).ToArray()
                                : new[]{Target(map,m.target,false)};
            // Range from the creature's real position, not from the tile the client reports (review #9).
            if(radius==0&&Distance(s.State.x,s.State.y,targets[0].state.x,targets[0].state.y)>12)throw new InvalidOperationException("Objetivo fuera de alcance.");
            bool affected=radius>0;
            foreach(var n in targets)
            {
                if(Harmful(spell)&&!Bool(n.source,"attackable")) continue;
                var magicDef=catalog["npcMagic"]?["npcs"]?.AsArray().FirstOrDefault(d=>Int(d,"npcIndex")==n.state.npc);
                if(Harmful(spell)&&Bool(magicDef,"immuneToSpells"))continue;
                if(Harmful(spell)) n.provoked=s.Id; affected=true;
                int duration=Math.Clamp(Int(spell,"duration"),1,120)*1000;
                if(Int(spell,"paralyze")!=0) n.paralyzedUntil=Now+duration/2;
                if(Int(spell,"immobilize")!=0) n.immobileUntil=Now+duration/2;
                if(Int(spell,"removeParalysis")!=0) n.paralyzedUntil=n.immobileUntil=0;
                if(Int(spell,"poison")!=0){n.poisonUntil=Now+Math.Max(duration,20000);n.nextPoison=Now+3640;}
                if(Int(spell,"incinerate")!=0){n.fireUntil=Now+Math.Max(duration,3000);n.nextFire=Now+750;}
                if(Int(spell,"curePoison")!=0) n.poisonUntil=0;
                if(Int(spell,"removeDebuff")!=0) n.paralyzedUntil=n.immobileUntil=n.poisonUntil=n.fireUntil=0;
                if(Int(spell,"raiseHp")==1) n.state.hp=Math.Min(n.state.maxHp,n.state.hp+Roll(Int(spell,"minHp"),Int(spell,"maxHp")));
                if(Int(spell,"raiseHp")==2) Hit(s,map,n,MagicDamage(s,spell,magicDef,Roll(Int(spell,"minHp"),Int(spell,"maxHp"))));
            }
            if(!affected) throw new InvalidOperationException("Sin objetivo válido.");
        }
        // Duels use the original interval (intervalos.ini); the cooperative room keeps its tolerant 1.1 s.
        s.NextCast=castAt+(duelCast?AOPvpFormulas.IntervalSpellMs:1100); s.SpellCooldown[m.spell]=castAt+(long)(Float(spell,"cooldown")*1000);
    }
    int MagicDamage(Session s,JsonNode spell,JsonNode? npc,int raw)
    {
        var save=JsonNode.Parse(s.Record.snapshot)!;var r=save["rpg"]!;var inv=save["inventory"]!;
        int damage=raw+(int)Math.Round(raw*3f*s.State.level/100),penetration=0;
        var weapon=items.GetValueOrDefault(s.State.weapon);
        if(Int(spell,"staffAffected")!=0&&Int(r,"classId")==1)
            damage=(int)Math.Round(damage*(Int(weapon,"staffPower")>0?70f+Int(weapon,"magicDamageBonus"):70f)/100);
        else foreach(string slot in new[]{"weapon","magicAccessory","amulet"})
        {var item=items.GetValueOrDefault(Int(inv,slot));damage+=(int)Math.Round(damage*Int(item,"magicDamageBonus")/100f)+Int(item,"magicAbsoluteBonus");penetration+=Int(item,"magicPenetration");}
        if(Int(spell,"antiRm")==0 && Int(npc,"magicResistance")>0)
        {int skill=r["skills"] is JsonArray skills&&skills.Count>0?skills[0]?.GetValue<int>()??0:0;
         int diff=Int(npc,"magicResistance")-skill;int pct=Math.Clamp(Int(npc,"magicDef")+Math.Max(0,diff)*2-penetration,0,95);damage-=(int)Math.Round(damage*pct/100f);}
        return Math.Max(0,damage);
    }
    void Hit(Session? attacker,MapRecord map,NpcRecord n,int damage)
    {
        if(n.state.dead)return;
        n.state.hp=Math.Max(0,n.state.hp-damage); dirty=true;
        if(attacker!=null)n.provoked=attacker.Id;
        if(n.state.hp>0)return;
        n.state.dead=true;
        NpcDied(map,n);   // CoopRoom.NpcMagic.cs: its summons die with it
        var def=lootDefs.GetValueOrDefault(n.state.npc)??n.source;
        // One rule online and offline (AODemoRates.RespawnRange): demo maps use their designed times, the original
        // maps use npcs.dat (npc_loot). 0 = original instant respawn (350 ms floor so it never revives in the same tick).
        AODemoRates.RespawnRange(map.id,Int(n.source,"respawnMinSeconds"),Int(n.source,"respawnMaxSeconds"),
            Int(def,"respawnMinSeconds"),Int(def,"respawnMaxSeconds"),out int respawnMin,out int respawnMax);
        n.respawnAt=Bool(def,"respawnDisabled")?long.MaxValue:Now+Math.Max(350,Roll(respawnMin,respawnMax)*1000L);
        var group=sessions.Values.Where(p=>p.Record.events.Count<240&&p.State.map==map.id&&!p.State.dead&&Distance(p.State.x,p.State.y,n.state.x,n.state.y)<=12).ToArray();
        int total=Math.Max(0,Int(def,"giveExp"));
        for(int i=0;i<group.Length;i++)
        {
            var extras=new List<AOCoopItem>();
            if(def["questDrops"] is JsonArray questDrops)foreach(var d in questDrops)
                if(d!=null&&Roll(1,Math.Max(1,Int(d,"probabilityDenominator")))==1)
                    extras.Add(new AOCoopItem {item=Int(d,"itemIndex"),amount=Math.Max(1,Int(d,"amount")),quest=Int(d,"questId")});
            int share=total/group.Length+(i<total%group.Length?1:0);
            // Original (decision 17): a player more than 4 levels above the NPC (NPCLVL > 0) gets 5 % less per extra level.
            share=(int)Math.Min(int.MaxValue,AOExpRules.ApplyNpcLevelPenalty(share,group[i].State.level,Int(def,"level")));
            // Demo: the tier multiplier uses the level of each player who receives the share.
            if(Options.Demo)share=(int)Math.Min(int.MaxValue,AODemoRates.ApplyExp(share,group[i].State.level));
            Event(group[i],new AOCoopEvent {type="kill",npc=n.state.npc,exp=share,items=extras.ToArray()});
        }
        if(def["inventoryDrops"] is JsonArray drops)foreach(var d in drops)if(d!=null)AddLoot(map,Int(d,"itemIndex"),Int(d,"amount"),n.state.x,n.state.y);
        if(Int(def,"giveGold")>0)AddLoot(map,Int(catalog["loot"],"goldItemIndex"),
            Options.Demo?(int)Math.Min(int.MaxValue,AODemoRates.ApplyGold(Int(def,"giveGold"))):Int(def,"giveGold"),n.state.x,n.state.y);
        if(def["randomDrops"] is JsonArray choices&&choices.Count>0&&Roll(1,Math.Max(1,Int(def,"randomDropDenominator")))==1)
        {var d=choices[random.Next(choices.Count)]!;AddLoot(map,Int(d,"itemIndex"),Int(d,"amount"),n.state.x,n.state.y);}
        dirty=true;
    }
    void Pickup(Session s,MapRecord map,AOCoopMessage m)
    {
        Alive(s); var item=map.loot.FirstOrDefault(l=>l.id==m.target)??throw new InvalidOperationException("Otro jugador ya recogió ese objeto.");
        if(Distance(s.State.x,s.State.y,item.x,item.y)>1)throw new InvalidOperationException("Acercate al objeto.");
        if(!CanReceive(s,item.item))throw new InvalidOperationException("Inventario lleno; liberá un espacio.");
        RoomForEvent(s);
        if(item.item==Int(catalog["loot"],"goldItemIndex"))
            ledger.Transfer("pickup:"+item.id,Ledger.World,Ledger.Wallet(s.CharacterId),item.amount,"botín");
        Event(s,new AOCoopEvent {type="item",item=item.item,amount=item.amount});
        map.loot.Remove(item); map.lootBorn.Remove(item.id);
    }
    void Drop(Session s,MapRecord map,AOCoopMessage m)
    {
        Alive(s); if(!items.TryGetValue(m.item,out var def)||Bool(def,"untransferable")||Bool(def,"newbie"))throw new InvalidOperationException("Ese objeto no se puede entregar.");
        var data=JsonNode.Parse(s.Record.snapshot)!; var inv=data["inventory"]!;
        if(new[]{"weapon","armor","helmet","shield","amulet","magicAccessory"}.Any(k=>Int(inv,k)==m.item))throw new InvalidOperationException("Desequipá el objeto antes de soltarlo.");
        int count=CountItem(inv,m.item);
        if(m.amount<=0||m.amount>count)throw new InvalidOperationException("Cantidad no disponible.");
        if(s.Record.events.Any(e=>e.type=="remove"))throw new InvalidOperationException("Esperá a confirmar la entrega anterior.");
        Event(s,new AOCoopEvent {type="remove",item=m.item,amount=m.amount});
        AddLoot(map,m.item,m.amount,s.State.x,s.State.y);
    }
    bool CanReceive(Session s,int item)
    {
        if(item==Int(catalog["loot"],"goldItemIndex"))return true;
        var inv=JsonNode.Parse(s.Record.snapshot)!["inventory"]!;
        var ids=inv["itemIndices"]!.AsArray();
        return ids.Any(i=>i?.GetValue<int>()==0||i?.GetValue<int>()==item);
    }
    void Trade(Session s,MapRecord map,AOCoopMessage m)
    {
        Alive(s);
        var merchant=catalog["shops"]?["npcs"]?.AsArray().FirstOrDefault(n=>Int(n,"npcIndex")==m.id);
        if(merchant==null||!Bool(merchant,"trades")||!map.npcs.Any(n=>!n.state.dead&&n.state.npc==m.id&&Distance(n.state.x,n.state.y,s.State.x,s.State.y)<=3))
            throw new InvalidOperationException("Acercate al comerciante.");
        if(m.amount<=0||m.amount>100000||!items.TryGetValue(m.item,out var item))throw new InvalidOperationException("Cantidad u objeto inválidos.");
        if(s.Record.events.Any(e=>e.type=="buy"||e.type=="sell"||e.type=="remove"))throw new InvalidOperationException("Esperá la operación anterior.");
        var data=JsonNode.Parse(s.Record.snapshot)!;var inv=data["inventory"]!;
        if(m.type=="buy")
        {
            var entry=merchant["stock"]?.AsArray().FirstOrDefault(e=>Int(e,"itemIndex")==m.item)??throw new InvalidOperationException("El comerciante no vende ese objeto.");
            string key=m.id+":"+m.item;int stock=store.stocks.GetValueOrDefault(key,Int(entry,"amount"));
            if(!Bool(entry,"infinite")&&stock<m.amount)throw new InvalidOperationException("No queda esa cantidad en el comercio.");
            var skills=data["rpg"]?["skills"]?.AsArray();int trading=skills!=null&&skills.Count>=9?Math.Clamp(skills[8]?.GetValue<int>()??0,0,100):0;
            long cost=(long)Math.Max(1,(int)Math.Ceiling(Int(item,"value")/(1f+trading/100f)))*m.amount;
            if(WalletOf(s)<cost)throw new InvalidOperationException("No tenés suficiente oro.");
            if(!CanReceive(s,m.item))throw new InvalidOperationException("Inventario lleno.");
            RoomForEvent(s);
            ledger.Transfer("buy:"+RequestKey(m),Ledger.Wallet(s.CharacterId),Ledger.World,cost,"compra "+m.item);
            Event(s,new AOCoopEvent{type="buy",item=m.item,amount=m.amount,gold=-cost});
            if(!Bool(entry,"infinite"))
            {
                store.stocks[key]=stock-m.amount;
                // Original (QuitarNpcInvItem → CargarInvent): a merchant left with nothing to sell reloads its whole
                // inventory. Crucial (infinite) items never run out, so a merchant with any of them never empties.
                var goods=merchant["stock"]!.AsArray();
                if(goods.All(e=>!Bool(e,"infinite")&&store.stocks.GetValueOrDefault(m.id+":"+Int(e,"itemIndex"),Int(e,"amount"))<=0))
                    foreach(var e in goods)store.stocks.Remove(m.id+":"+Int(e,"itemIndex"));
            }
        }
        else
        {
            if(Bool(item,"newbie")||Bool(item,"untransferable")||Bool(item,"destroyOnSell")||(Int(item,"objType")>=38&&Int(item,"objType")<=43))throw new InvalidOperationException("Ese objeto no se vende.");
            if(Int(merchant,"itemType")!=100&&Int(merchant,"itemType")!=Int(item,"objType")&&!(merchant["stock"]?.AsArray().Any(e=>Int(e,"itemIndex")==m.item)??false))throw new InvalidOperationException("El comerciante no compra ese objeto.");
            if(new[]{"weapon","armor","helmet","shield","amulet","magicAccessory"}.Any(k=>Int(inv,k)==m.item))throw new InvalidOperationException("Desequipá el objeto antes de venderlo.");
            if(CountItem(inv,m.item)<m.amount)throw new InvalidOperationException("No tenés esa cantidad.");
            long price=(long)(Int(item,"value")/3)*m.amount;if(price<=0)throw new InvalidOperationException("Objeto sin valor de venta.");
            RoomForEvent(s);
            ledger.Transfer("sell:"+RequestKey(m),Ledger.World,Ledger.Wallet(s.CharacterId),price,"venta "+m.item);
            Event(s,new AOCoopEvent{type="sell",item=m.item,amount=m.amount,gold=price});
        }
    }
    void Door(Session s,MapRecord map,AOCoopMessage m)
    {
        var def=map.source["doors"]!.AsArray().FirstOrDefault(d=>Int(d,"x")==m.x&&Int(d,"y")==m.y)
                ??throw new InvalidOperationException("Puerta inexistente.");
        if(Bool(def,"locked"))throw new InvalidOperationException("Puerta cerrada con llave.");
        if(Distance(s.State.x,s.State.y,m.x,m.y)>2)throw new InvalidOperationException("Acercate a la puerta.");
        var door=map.doors.FirstOrDefault(d=>d.x==m.x&&d.y==m.y);
        if(door==null){door=new AOCoopDoor{x=m.x,y=m.y};map.doors.Add(door);}
        int width=Math.Max(1,Int(def,"width"));
        if(door.open&&sessions.Values.Any(p=>p.State.map==map.id&&p.State.y==m.y&&p.State.x<=m.x&&p.State.x>m.x-width))
            throw new InvalidOperationException("Hay alguien en el umbral.");
        door.open=!door.open;
    }
    void AddLoot(MapRecord map,int item,int amount,int x,int y)
    {
        if(item<=0||amount<=0||!items.TryGetValue(item,out var def))return;
        var existing=map.loot.FirstOrDefault(l=>l.item==item&&l.x==x&&l.y==y);
        if(existing!=null)existing.amount=(int)Math.Min(int.MaxValue,(long)existing.amount+amount);
        else
        {
            var loot=new AOCoopLoot{id=++store.nextLoot,item=item,amount=amount,x=x,y=y,name=Text(def,"name")};
            map.loot.Add(loot);map.lootBorn[loot.id]=Now;
            // Review #4: at most 200 objects per map; the oldest one goes first.
            if(map.loot.Count>MaxLootPerMap){var oldest=map.loot.OrderBy(l=>map.lootBorn.GetValueOrDefault(l.id)).First();map.loot.Remove(oldest);map.lootBorn.Remove(oldest.id);}
        }
        dirty=true;
    }
    const int MaxLootPerMap=200, MaxCharacters=30;
    const long LootLifetimeMs=10*60*1000;   // review #4: floor loot vanishes after 10 minutes
    void ExpireLoot(MapRecord map)
    {
        if(map.loot.Count==0)return;
        long lifetime=(long)(LootLifetimeMs*(Options.Test?Options.TimeScale:1));   // --test-time-scale speeds it up in tests
        foreach(var l in map.loot.Where(l=>Now-map.lootBorn.GetValueOrDefault(l.id,Now)>lifetime).ToList())
        {map.loot.Remove(l);map.lootBorn.Remove(l.id);dirty=true;}
    }
    void TickMap(MapRecord map)
    {
        ExpireLoot(map);
        // Everybody on the map is a target, also a client that never acks its events (review #12: it used to be invulnerable).
        var present=sessions.Values.Where(p=>p.State.map==map.id&&!p.State.dead).ToArray();
        foreach(var n in map.npcs)
        {
            if(n.state.dead)
            {
                if(n.summoned)continue;   // summons never respawn: FlushSummons removes them
                if(Now>=n.respawnAt)
                {n.state.dead=false;n.state.hp=n.state.maxHp;n.state.x=Int(n.source,"x");n.state.y=Int(n.source,"y");n.provoked=0;n.poisonUntil=n.fireUntil=n.paralyzedUntil=n.immobileUntil=0;dirty=true;}
                continue;
            }
            Session? owner=sessions.GetValueOrDefault(n.provoked);
            if(Now<n.poisonUntil&&Now>=n.nextPoison){n.nextPoison=Now+3640;Hit(owner,map,n,Math.Max(1,n.state.maxHp*4/100));}
            if(!n.state.dead&&Now<n.fireUntil&&Now>=n.nextFire){n.nextFire=Now+750;Hit(owner,map,n,Roll(20,30));}
            if(n.state.dead||Now<n.nextMove)continue;
            n.nextMove=Now+Math.Clamp(Int(n.source,"moveIntervalMs"),250,4000);
            bool hostile=Bool(n.source,"hostile")||n.provoked>0;
            // Original vision per axis (15 x 13); older catalogs only carry visionRange (nube: npc-vision-15x13-servidor).
            int visionX=Int(n.source,"visionRangeX"),visionY=Int(n.source,"visionRangeY");
            if(visionX<=0||visionY<=0)visionX=visionY=Math.Clamp(Int(n.source,"visionRange"),4,15);
            visionX=Math.Clamp(visionX,1,30);visionY=Math.Clamp(visionY,1,30);
            var target=hostile?present.Where(p=>Math.Abs(p.State.x-n.state.x)<=visionX&&Math.Abs(p.State.y-n.state.y)<=visionY)
                .OrderBy(p=>Distance(p.State.x,p.State.y,n.state.x,n.state.y)).FirstOrDefault():null;
            n.state.target=target?.Id??0;
            if(Now<n.paralyzedUntil)continue;
            // Support AI (Movement 11/13): helps or attacks with spells only, and wanders near its origin.
            if(SupportAi(n)){NpcSupportTurn(map,n,present);WanderNearOrigin(map,n);continue;}
            if(target!=null)
            {
                // Original magic AI (CoopRoom.NpcMagic.cs): spells first; melee only if it did not cast this turn.
                bool cast=NpcCast(map,n,target,present);
                int dist=Distance(target.State.x,target.State.y,n.state.x,n.state.y),range=Math.Max(1,Int(n.source,"attackRange"));
                if(dist<=range&&Now>=n.nextAttack&&NpcMayMelee(n,cast))
                {
                    n.nextAttack=Now+Math.Clamp(Int(n.source,"attackIntervalMs"),500,10000);
                    n.state.heading=Heading(target.State.x-n.state.x,target.State.y-n.state.y);
                    if(random.Next(1,101)<=Math.Clamp(50+(Int(n.source,"attackPower")-target.State.evasion)*.4,10,90))
                    {
                        int damage=Math.Max(0,Roll(Int(n.source,"minHit"),Int(n.source,"maxHit"))-target.State.defense-EquipmentDefense(target.State));
                        Event(target,new AOCoopEvent{type="hurt",damage=damage,text=Text(n.source,"name")},true);
                        target.State.hp=Math.Max(0,target.State.hp-damage); target.State.dead=target.State.hp<=0;
                    }
                }
                if(dist>range&&Now>=n.immobileUntil)MoveToward(map,n,target.State.x,target.State.y);
            }
            else if(Now>=n.immobileUntil && Int(n.source,"movement")==20 && n.source["walkRoute"] is JsonArray route && route.Count>0)
            {
                var point=route[n.route%route.Count]!;int x=Int(n.source,"x")+Int(point,"offsetX"),y=Int(n.source,"y")+Int(point,"offsetY");
                if(n.state.x==x&&n.state.y==y){n.route++;n.nextMove+=Math.Max(0,Int(point,"waitMs"));}
                else MoveToward(map,n,x,y);
            }
            else if(Now>=n.immobileUntil&&!IsStationary(n)&&random.Next(3)==0)
            {int h=random.Next(1,5);Move(map,n,h);}
        }
        FlushSummons(map);
    }
    int EquipmentDefense(AOCoopPlayer p)
    {
        int RollItem(int id){var item=items.GetValueOrDefault(id);return Roll(Int(item,"minDef"),Int(item,"maxDef"));}
        return random.Next(4)==0?RollItem(p.helmet):RollItem(p.armor)+RollItem(p.shield);
    }
    void MoveToward(MapRecord map,NpcRecord n,int x,int y)
    {
        int h=Heading(x-n.state.x,y-n.state.y);
        if(!Move(map,n,h)){int other=Math.Abs(x-n.state.x)>=Math.Abs(y-n.state.y)?(y>n.state.y?3:1):(x>n.state.x?2:4);Move(map,n,other);}
    }
    // Imported modes 0 (no movement), 1 (static), and 3 (fixed position)
    // cannot wander or pursue a target. Combat in range still works.
    static bool IsStationary(NpcRecord n) => Int(n.source,"movement") is 0 or 1 or 3;
    bool Move(MapRecord map,NpcRecord n,int heading)
    {
        if(IsStationary(n))return false;
        int x=n.state.x+(heading==2?1:heading==4?-1:0),y=n.state.y+(heading==3?1:heading==1?-1:0);
        if(!ValidPosition(map.id,x,y))return false;
        int flags=map.flags.GetValueOrDefault(y*101+x),trigger=map.triggers.GetValueOrDefault(y*101+x);
        foreach(var door in map.doors.Where(d=>d.open))
        {
            var def=map.source["doors"]!.AsArray().First(d=>Int(d,"x")==door.x&&Int(d,"y")==door.y);
            if(x<=door.x&&x>door.x-Math.Max(1,Int(def,"width"))){if(y==door.y)flags&=~1;if(y==door.y+1)flags&=~4;}
        }
        bool water=(flags&32)!=0&&(flags&128)==0;
        if((flags&(1<<(heading-1)))!=0||trigger==3||map.exits.Contains(y*101+x)||
            (water&&!Bool(n.source,"waterValid"))||(!water&&Bool(n.source,"landInvalid"))||((flags&256)!=0&&!Bool(n.source,"lavaValid")))return false;
        if(map.npcs.Any(other=>other!=n&&!other.state.dead&&other.state.x==x&&other.state.y==y)||
            sessions.Values.Any(p=>p.State.map==map.id&&p.State.x==x&&p.State.y==y))return false;
        n.state.x=x;n.state.y=y;n.state.heading=heading;dirty=true;return true;
    }
    MapRecord Map(int id)
    {
        if(store.maps.TryGetValue(id,out var map))return map;
        map=new MapRecord{id=id,npcs=FreshNpcs(templates[id]),npcLayoutVersion=Text(templates[id],"npcLayoutVersion")};
        BindMap(map);store.maps.Add(id,map);dirty=true;return map;
    }
    static List<NpcRecord> FreshNpcs(JsonObject template)
    {
        var npcs=new List<NpcRecord>();int index=0;
        foreach(var def in template["npcs"]!.AsArray())
        {
            index++;if(def==null)continue;
            npcs.Add(new NpcRecord{state=new AOCoopNpc{id=index,npc=Int(def,"npcIndex"),x=Int(def,"x"),y=Int(def,"y"),
                heading=Math.Clamp(Int(def,"heading"),1,4),hp=Math.Max(1,Int(def,"maxHp")),maxHp=Math.Max(1,Int(def,"maxHp"))}});
        }
        return npcs;
    }
    // NPC ids are positions in the catalog list. If the list changed, saved NPC states would point at the
    // wrong creature (or past the end), so that map's NPCs restart. Legacy saves without a version are
    // adopted only when every saved NPC still matches its slot.
    void CheckNpcLayout(MapRecord map)
    {
        string version=Text(map.source,"npcLayoutVersion");
        if(map.npcLayoutVersion==version)return;
        var defs=map.source["npcs"]!.AsArray();
        bool matches=map.npcLayoutVersion.Length==0&&map.npcs.All(n=>n.state.id>=1&&n.state.id<=defs.Count&&defs[n.state.id-1] is JsonNode d&&Int(d,"npcIndex")==n.state.npc);
        if(!matches){map.npcs=FreshNpcs(map.source);Console.WriteLine($"Mapa {map.id}: cambió la lista de NPC; se reiniciaron.");}
        map.npcLayoutVersion=version;dirty=true;
    }
    void BindMap(MapRecord map)
    {
        map.source=templates[map.id];
        // Summoned creatures are not part of the map (ids past the catalog list): a saved one is dropped.
        int defined=map.source["npcs"]?.AsArray().Count??0;
        map.npcs.RemoveAll(n=>n.state.id<1||n.state.id>defined);
        CheckNpcLayout(map);
        // Loot saved by older servers has no time: its 10 minutes start now.
        foreach(var l in map.loot)map.lootBorn.TryAdd(l.id,Now);
        foreach(var n in map.npcs)
        {
            n.source=map.source["npcs"]![n.state.id-1]!.AsObject();
            if(!IsStationary(n))continue;
            // Repair positions persisted by older servers that moved scenery NPCs.
            int x=Int(n.source,"x"),y=Int(n.source,"y"),heading=Math.Clamp(Int(n.source,"heading"),1,4);
            if(n.state.x!=x||n.state.y!=y||n.state.heading!=heading)
            {n.state.x=x;n.state.y=y;n.state.heading=heading;dirty=true;}
        }
        if(map.source["grid"] != null)
        {
            using var packed=new MemoryStream(Convert.FromBase64String(Text(map.source,"grid")));
            using var gzip=new GZipStream(packed,CompressionMode.Decompress);using var reader=new BinaryReader(gzip);
            for(int i=0;i<10201;i++){int f=reader.ReadUInt16();if(f!=0)map.flags[i]=f;}
            for(int i=0;i<10201;i++){int t=reader.ReadUInt16();if(t!=0)map.triggers[i]=t;}
        }
        else
        {
            foreach(var b in map.source["blocks"]!.AsArray())map.flags[Int(b,"y")*101+Int(b,"x")]=Int(b,"flags");
            foreach(var b in map.source["triggers"]!.AsArray())map.triggers[Int(b,"y")*101+Int(b,"x")]=Int(b,"trigger");
        }
        foreach(var b in map.source["exits"]!.AsArray())map.exits.Add(Int(b,"y")*101+Int(b,"x"));
    }
    NpcRecord Target(MapRecord map,int id,bool attackable=true)
    {
        var n=map.npcs.FirstOrDefault(n=>n.state.id==id&&!n.state.dead)??throw new InvalidOperationException("La criatura ya no está disponible.");
        if(attackable&&!Bool(n.source,"attackable"))throw new InvalidOperationException("Ese NPC no es atacable.");return n;
    }
    void Event(Session s,AOCoopEvent e,bool force=false)=>Event(s.Record,e,force);
    // force: server-driven duel events (warp, hurt, duelEnd) never fail: they can come from the tick, where
    // an exception would stop the world. They are few per duel and are cleared by the next ack.
    void Event(CharacterRecord r,AOCoopEvent e,bool force=false)
    {
        if(!force&&r.events.Count>=256)throw new InvalidOperationException("Demasiadas recompensas pendientes; liberá inventario y reconectá.");
        e.seq=++r.nextEvent;r.events.Add(e);dirty=true;
    }
    public bool ValidPosition(int map,int x,int y)=>templates.TryGetValue(map,out var t)&&x>=Int(t,"xmin")&&x<=Int(t,"xmax")&&y>=Int(t,"ymin")&&y<=Int(t,"ymax");
    static void Alive(Session s){if(s.State.dead)throw new InvalidOperationException("No podés hacer eso muerto.");}
    // Anything that can hurt a friend (review #8): damage, harmful EOT (hostileEffect from the export), states and
    // negative hunger, thirst, charisma, stamina, strength, agility or mana. Only the rest can target an ally.
    static bool Harmful(JsonNode s)=>Int(s,"hostileEffect")!=0||Int(s,"raiseHunger")==2||Int(s,"raiseThirst")==2||Int(s,"raiseCharisma")==2||Int(s,"raiseHp")==2||Int(s,"paralyze")!=0||Int(s,"immobilize")!=0||Int(s,"poison")!=0||Int(s,"incinerate")!=0||Int(s,"curse")!=0||Int(s,"blindness")!=0||Int(s,"dumb")!=0||Int(s,"raiseMana")==2||Int(s,"raiseStamina")==2||Int(s,"raiseStrength")==2||Int(s,"raiseAgility")==2||Int(s,"stealBuff")!=0;
    static int CountItem(JsonNode inv,int item){long total=0;var ids=inv["itemIndices"]!.AsArray();var amounts=inv["amounts"]!.AsArray();for(int i=0;i<ids.Count;i++)if(ids[i]?.GetValue<int>()==item)total+=amounts[i]?.GetValue<int>()??0;return (int)Math.Min(int.MaxValue,total);}
    static int Heading(int dx,int dy)=>Math.Abs(dx)>=Math.Abs(dy)&&dx!=0?(dx>0?2:4):(dy>0?3:1);
    static int Distance(int x,int y,int tx,int ty)=>Math.Max(Math.Abs(x-tx),Math.Abs(y-ty));
    int Roll(int min,int max)=>random.Next(Math.Min(min,max),Math.Max(min,max)+1);
    static long Now=>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    internal static int Int(JsonNode? n,string k)=>n?[k]?.GetValue<int>()??0;
    static long Long(JsonNode? n,string k)=>n?[k]?.GetValue<long>()??0;
    static float Float(JsonNode? n,string k)=>n?[k]?.GetValue<float>()??0;
    static bool Bool(JsonNode? n,string k)=>n?[k]?.GetValue<bool>()??false;
    static string Text(JsonNode? n,string k)=>n?[k]?.GetValue<string>()??"";
    static Dictionary<int,JsonObject> Index(JsonNode? node,string key)=>(node?.AsArray()??new JsonArray()).OfType<JsonObject>().ToDictionary(n=>Int(n,key));
}
sealed class Session(int id,string characterId,CharacterRecord record)
{
    public readonly int Id=id; public readonly string CharacterId=characterId; public readonly CharacterRecord Record=record;
    public AOCoopPlayer State=new(); public long LastAction,NextAttack,NextCast,LastMove,LastSpeedLog;
    public Dictionary<int,long> SpellCooldown=new(),PetCooldown=new();
    public Dictionary<string,AOCoopMessage> Replies=new();
}
sealed class RoomSave { public Dictionary<string,int> stocks=new(); public int nextLoot; public Dictionary<string,CharacterRecord> characters=new(); public Dictionary<int,MapRecord> maps=new(); }
sealed class CharacterRecord
{
    public string name="",tokenHash="",snapshot=""; public long ack,nextEvent; public List<AOCoopEvent> events=new();
    // Death drops (demo dungeon): one per death, even across reconnections and restarts.
    public int deaths; public bool deathDropped;
}
sealed class MapRecord
{
    public int id; public string npcLayoutVersion=""; public List<NpcRecord> npcs=new(); public List<AOCoopLoot> loot=new(); public List<AOCoopDoor> doors=new();
    public Dictionary<int,long> lootBorn=new();   // loot id → when it fell (expiry)
    [JsonIgnore] public JsonObject source=new();
    [JsonIgnore] public Dictionary<int,int> flags=new(),triggers=new();
    [JsonIgnore] public HashSet<int> exits=new();
}
sealed class NpcRecord
{
    public AOCoopNpc state=new(); public long respawnAt,poisonUntil,fireUntil,paralyzedUntil,immobileUntil,nextPoison,nextFire;
    public int provoked,route;
    [JsonIgnore] public long nextMove,nextAttack,nextCast;
    // CoopRoom.NpcMagic.cs: last use of each spell slot (support AI cooldowns) and summons (never saved: they vanish on restart).
    [JsonIgnore] public long[]? slotUse; [JsonIgnore] public NpcRecord? summoner; [JsonIgnore] public bool summoned;
    [JsonIgnore] public JsonObject source=new();
}
