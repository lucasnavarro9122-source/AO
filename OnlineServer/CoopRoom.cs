using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

// Private alpha: the room owns NPCs, floor items and the durable event journal.
// Character formulas remain in Unity; this is cooperative authority, not MMO anti-cheat.
sealed class CoopRoom
{
    public static readonly JsonSerializerOptions Json = new() { IncludeFields = true, MaxDepth = 40 };
    readonly JsonObject catalog;
    readonly Dictionary<int, JsonObject> templates, lootDefs, items, spells, summons;
    readonly Dictionary<int, Session> sessions = new();
    readonly string statePath;
    readonly Random random = new();
    RoomSave store;
    int nextSession;
    long lastSave;
    bool dirty;
    public readonly object Gate = new();
    public CoopRoom(string catalogPath, string dataDirectory)
    {
        using (var file = File.OpenRead(catalogPath))
        using (Stream input = catalogPath.EndsWith(".gz") ? new GZipStream(file, CompressionMode.Decompress) : file)
            catalog = JsonNode.Parse(input)!.AsObject();
        templates = Index(catalog["maps"], "mapNumber");
        lootDefs = Index(catalog["loot"]?["npcs"], "npcIndex");
        items = Index(catalog["items"], "index");
        spells = Index(catalog["spells"], "id");
        summons = Index(catalog["summons"]?["summons"], "npcIndex");
        Directory.CreateDirectory(dataDirectory);
        statePath = Path.Combine(dataDirectory, "world.json");
        store = LoadStore();
        foreach (var map in store.maps.Values) { BindMap(map); foreach(var npc in map.npcs) npc.provoked=0; }
        Console.WriteLine($"Mundo cooperativo: {templates.Count} mapas; {store.characters.Count} personajes guardados.");
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
    public void Save()
    {
        string temp = statePath + ".tmp";
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(store, Json);
        using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        { file.Write(bytes); file.Flush(true); }
        if (File.Exists(statePath)) File.Replace(temp, statePath, statePath + ".bak", true);
        else File.Move(temp, statePath);
        lastSave = Now; dirty = false;
    }
    public Session Join(AOCoopMessage hello)
    {
        if (hello.version != AOCoopMessage.Protocol) throw new InvalidOperationException("Cliente incompatible. Instalá el ZIP cooperativo nuevo.");
        if (sessions.Count >= 11) throw new InvalidOperationException("Sala completa (11/11).");
        if (!Guid.TryParseExact(hello.characterId, "N", out _) || hello.token?.Length != 64 || !hello.token.All(Uri.IsHexDigit))
            throw new InvalidOperationException("Identidad de personaje inválida.");
        if (sessions.Values.Any(s => s.CharacterId == hello.characterId)) throw new InvalidOperationException("Ese personaje ya está conectado.");
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hello.token)));
        if (!store.characters.TryGetValue(hello.characterId, out CharacterRecord? record))
        {
            JsonObject data = ValidateSnapshot(hello.snapshot);
            string name = Text(data["character"], "name").Trim();
            if (store.characters.Values.Any(r => string.Equals(r.name, name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Ese nombre ya pertenece a otro personaje en esta sala.");
            record = new CharacterRecord { name = name, tokenHash = hash, snapshot = hello.snapshot };
            store.characters.Add(hello.characterId, record);
            Save();
        }
        else if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(hash), Encoding.ASCII.GetBytes(record.tokenHash)))
            throw new InvalidOperationException("La identidad local no corresponde a ese personaje.");
        var session = new Session(++nextSession, hello.characterId, record);
        sessions.Add(session.Id, session);
        RefreshPlayer(session, hello.player, JsonNode.Parse(record.snapshot)!.AsObject());
        Map(session.State.map);
        return session;
    }
    public AOCoopMessage Welcome(Session s) => new() { type = "welcome", id = s.Id, version = AOCoopMessage.Protocol,
        snapshot = s.Record.snapshot, ack = s.Record.ack, events = s.Record.events.ToArray() };
    public int[] Recipients(int map) => sessions.Values.Where(s=>s.State.map==map).Select(s=>s.Id).ToArray();
    public void Leave(Session s) { sessions.Remove(s.Id); Save(); }
    public AOCoopMessage Process(Session s, AOCoopMessage m)
    {
        if (m.type == "position")
        {
            if (m.player != null && ValidPosition(m.player.map, m.player.x, m.player.y))
            { s.State.map = m.player.map; s.State.x = m.player.x; s.State.y = m.player.y; s.State.heading = Math.Clamp(m.player.heading, 1, 4); }
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
                s.LastAction = Now;
                var map = Map(s.State.map);
                switch (m.type)
                {
                    case "attack": Attack(s, map, m); break;
                    case "cast": Cast(s, map, m); break;
                    case "petHit": PetHit(s, map, m); break;
                    case "pickup": Pickup(s, map, m); break;
                    case "drop": Drop(s, map, m); break;
                    case "door": Door(s, map, m); break;
                    case "buy": case "sell": Trade(s,map,m); break;
                    default: throw new InvalidOperationException("Acción desconocida.");
                }
                Save();
            }
        }
        catch (InvalidOperationException e) { reply.ok = false; reply.text = e.Message; }
        reply.events = s.Record.events.ToArray();
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
        s.Record.snapshot = m.snapshot; s.Record.ack = m.ack;
        s.Record.events.RemoveAll(e => e.seq <= m.ack);
        RefreshPlayer(s, m.player, data); dirty = true;
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
            p.maxMana = Math.Clamp(reported.maxMana,0,100000);
            p.pets = (reported.pets ?? Array.Empty<AOCoopPet>()).Where(t => t != null && summons.ContainsKey(t.npc) &&
                ValidPosition(p.map,t.x,t.y) && Distance(p.x,p.y,t.x,t.y) < 20).Take(3).ToArray();
        }
        foreach(var pending in s.Record.events) if(pending.type=="hurt") p.hp=Math.Max(0,p.hp-pending.damage);
        p.dead |= p.hp==0;
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
        foreach(int id in sessions.Values.Select(s=>s.State.map).Distinct().ToArray()) TickMap(Map(id));
        if(dirty && Now-lastSave>1000) Save();
        foreach(var s in sessions.Values)
        {
            var map=Map(s.State.map);
            yield return (s,new AOCoopMessage { type="state", map=map.id, players=sessions.Values.Select(t=>JsonSerializer.Deserialize<AOCoopPlayer>(JsonSerializer.Serialize(t.State,Json),Json)!).ToArray(),
                npcs=map.npcs.Select(n=>new AOCoopNpc{id=n.state.id,npc=n.state.npc,x=n.state.x,y=n.state.y,heading=n.state.heading,hp=n.state.hp,maxHp=n.state.maxHp,dead=n.state.dead,target=n.state.target}).ToArray(), loot=map.loot.Select(l=>new AOCoopLoot{id=l.id,item=l.item,amount=l.amount,x=l.x,y=l.y,name=l.name}).ToArray(),
                doors=map.doors.Select(d=>new AOCoopDoor{x=d.x,y=d.y,open=d.open}).ToArray(), stocks=store.stocks.Select(k=>new AOCoopStock{npc=int.Parse(k.Key.Split(':')[0]),item=int.Parse(k.Key.Split(':')[1]),amount=k.Value}).ToArray(), events=s.Record.events.ToArray() });
        }
    }
    void Attack(Session s, MapRecord map, AOCoopMessage message)
    {
        Alive(s); if(Now<s.NextAttack) throw new InvalidOperationException("Todavía no podés atacar.");
        var n=Target(map,message.target); if(Distance(s.State.x,s.State.y,n.state.x,n.state.y)>1) throw new InvalidOperationException("Enemigo fuera de alcance.");
        s.NextAttack=Now+700; n.provoked=s.Id;
        if(random.Next(1,101)>Math.Clamp(50+(s.State.attack-Int(n.source,"evasionPower"))*.4,5,95)) return;
        int weaponMin=0,weaponMax=0;
        if(items.TryGetValue(s.State.weapon,out var weapon)){weaponMin=Int(weapon,"minHitToNpc")>0?Int(weapon,"minHitToNpc"):Int(weapon,"minHit");weaponMax=Int(weapon,"maxHitToNpc")>0?Int(weapon,"maxHitToNpc"):Int(weapon,"maxHit");}
        int raw=(int)Math.Round((3f*Roll(weaponMin,weaponMax)+weaponMax*.2f*Math.Max(0,s.State.strength-15)+Roll(s.State.minHit,s.State.maxHit))*s.State.damageModifier);
        Hit(s,map,n,Math.Max(0,raw-Int(n.source,"defense")));
    }
    void PetHit(Session s, MapRecord map, AOCoopMessage m)
    {
        Alive(s); var pet=s.State.pets?.FirstOrDefault(p=>p.id==m.item);
        if(pet==null || !summons.TryGetValue(pet.npc,out var def)) throw new InvalidOperationException("Mascota inexistente.");
        if(s.PetCooldown.TryGetValue(pet.id,out long next) && Now<next) return;
        var n=Target(map,m.target);
        if(Distance(pet.x,pet.y,n.state.x,n.state.y)>2) return;
        s.PetCooldown[pet.id]=Now+400; Hit(s,map,n,Math.Max(1,Roll(Int(def,"minHit"),Int(def,"maxHit"))-Int(n.source,"defense")));
    }
    void Cast(Session s, MapRecord map, AOCoopMessage m)
    {
        Alive(s);
        if(!spells.TryGetValue(m.spell,out var spell)) throw new InvalidOperationException("Hechizo inexistente.");
        var save=JsonNode.Parse(s.Record.snapshot)!;
        if(save["magic"]?["learnedSpells"] is not JsonArray known || !known.Any(k=>k?.GetValue<int>()==m.spell))
            throw new InvalidOperationException("No conocés ese hechizo.");
        if(Now<s.NextCast || (s.SpellCooldown.TryGetValue(m.spell,out long next)&&Now<next)) throw new InvalidOperationException("Hechizo en recuperación.");
        if(Distance(s.State.x,s.State.y,m.x,m.y)>12) throw new InvalidOperationException("Objetivo fuera de alcance.");
        if(m.id>0)
        {
            if(!sessions.TryGetValue(m.id,out var friend) || friend.State.map!=map.id || Distance(s.State.x,s.State.y,friend.State.x,friend.State.y)>12)
                throw new InvalidOperationException("Compañero fuera de alcance.");
            if(Harmful(spell)) throw new InvalidOperationException("Esta sala cooperativa no permite daño entre amigos.");
            Event(friend,new AOCoopEvent {type="spell",spell=m.spell,text=s.State.name});
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
        s.NextCast=Now+1100; s.SpellCooldown[m.spell]=Now+(long)(Float(spell,"cooldown")*1000);
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
        var def=lootDefs.GetValueOrDefault(n.state.npc)??n.source;
        n.respawnAt=Bool(def,"respawnDisabled")?long.MaxValue:Now+Math.Max(350,Roll(Int(def,"respawnMinSeconds"),Int(def,"respawnMaxSeconds"))*1000L);
        var group=sessions.Values.Where(p=>p.Record.events.Count<240&&p.State.map==map.id&&!p.State.dead&&Distance(p.State.x,p.State.y,n.state.x,n.state.y)<=12).ToArray();
        int total=Math.Max(0,Int(def,"giveExp"));
        for(int i=0;i<group.Length;i++)
        {
            var extras=new List<AOCoopItem>();
            if(def["questDrops"] is JsonArray questDrops)foreach(var d in questDrops)
                if(d!=null&&Roll(1,Math.Max(1,Int(d,"probabilityDenominator")))==1)
                    extras.Add(new AOCoopItem {item=Int(d,"itemIndex"),amount=Math.Max(1,Int(d,"amount")),quest=Int(d,"questId")});
            Event(group[i],new AOCoopEvent {type="kill",npc=n.state.npc,exp=total/group.Length+(i<total%group.Length?1:0),items=extras.ToArray()});
        }
        if(def["inventoryDrops"] is JsonArray drops)foreach(var d in drops)if(d!=null)AddLoot(map,Int(d,"itemIndex"),Int(d,"amount"),n.state.x,n.state.y);
        if(Int(def,"giveGold")>0)AddLoot(map,Int(catalog["loot"],"goldItemIndex"),Int(def,"giveGold"),n.state.x,n.state.y);
        if(def["randomDrops"] is JsonArray choices&&choices.Count>0&&Roll(1,Math.Max(1,Int(def,"randomDropDenominator")))==1)
        {var d=choices[random.Next(choices.Count)]!;AddLoot(map,Int(d,"itemIndex"),Int(d,"amount"),n.state.x,n.state.y);}
        Save();
    }
    void Pickup(Session s,MapRecord map,AOCoopMessage m)
    {
        Alive(s); var item=map.loot.FirstOrDefault(l=>l.id==m.target)??throw new InvalidOperationException("Otro jugador ya recogió ese objeto.");
        if(Distance(s.State.x,s.State.y,item.x,item.y)>1)throw new InvalidOperationException("Acercate al objeto.");
        if(!CanReceive(s,item.item))throw new InvalidOperationException("Inventario lleno; liberá un espacio.");
        Event(s,new AOCoopEvent {type="item",item=item.item,amount=item.amount});
        map.loot.Remove(item);
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
            if((data["combat"]?["gold"]?.GetValue<long>()??0)<cost)throw new InvalidOperationException("No tenés suficiente oro.");
            if(!CanReceive(s,m.item))throw new InvalidOperationException("Inventario lleno.");
            Event(s,new AOCoopEvent{type="buy",item=m.item,amount=m.amount,gold=-cost});
            if(!Bool(entry,"infinite"))store.stocks[key]=stock-m.amount;
        }
        else
        {
            if(Bool(item,"newbie")||Bool(item,"untransferable")||Bool(item,"destroyOnSell")||(Int(item,"objType")>=38&&Int(item,"objType")<=43))throw new InvalidOperationException("Ese objeto no se vende.");
            if(Int(merchant,"itemType")!=100&&Int(merchant,"itemType")!=Int(item,"objType")&&!(merchant["stock"]?.AsArray().Any(e=>Int(e,"itemIndex")==m.item)??false))throw new InvalidOperationException("El comerciante no compra ese objeto.");
            if(new[]{"weapon","armor","helmet","shield","amulet","magicAccessory"}.Any(k=>Int(inv,k)==m.item))throw new InvalidOperationException("Desequipá el objeto antes de venderlo.");
            if(CountItem(inv,m.item)<m.amount)throw new InvalidOperationException("No tenés esa cantidad.");
            long price=(long)(Int(item,"value")/3)*m.amount;if(price<=0)throw new InvalidOperationException("Objeto sin valor de venta.");
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
        else map.loot.Add(new AOCoopLoot{id=++store.nextLoot,item=item,amount=amount,x=x,y=y,name=Text(def,"name")});
        dirty=true;
    }
    void TickMap(MapRecord map)
    {
        var present=sessions.Values.Where(p=>p.Record.events.Count<240&&p.State.map==map.id&&!p.State.dead).ToArray();
        foreach(var n in map.npcs)
        {
            if(n.state.dead)
            {
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
            int vision=Math.Clamp(Int(n.source,"visionRange"),4,15);
            var target=hostile?present.Where(p=>Distance(p.State.x,p.State.y,n.state.x,n.state.y)<=vision)
                .OrderBy(p=>Distance(p.State.x,p.State.y,n.state.x,n.state.y)).FirstOrDefault():null;
            n.state.target=target?.Id??0;
            if(Now<n.paralyzedUntil)continue;
            if(target!=null)
            {
                int dist=Distance(target.State.x,target.State.y,n.state.x,n.state.y),range=Math.Max(1,Int(n.source,"attackRange"));
                if(dist<=range&&Now>=n.nextAttack)
                {
                    n.nextAttack=Now+Math.Clamp(Int(n.source,"attackIntervalMs"),500,10000);
                    n.state.heading=Heading(target.State.x-n.state.x,target.State.y-n.state.y);
                    if(random.Next(1,101)<=Math.Clamp(50+(Int(n.source,"attackPower")-target.State.evasion)*.4,10,90))
                    {
                        int damage=Math.Max(0,Roll(Int(n.source,"minHit"),Int(n.source,"maxHit"))-target.State.defense-EquipmentDefense(target.State));
                        Event(target,new AOCoopEvent{type="hurt",damage=damage,text=Text(n.source,"name")});
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
        var template=templates[id];map=new MapRecord{id=id};
        int index=0;foreach(var def in template["npcs"]!.AsArray())
        {
            index++;if(def==null)continue;
            map.npcs.Add(new NpcRecord{state=new AOCoopNpc{id=index,npc=Int(def,"npcIndex"),x=Int(def,"x"),y=Int(def,"y"),
                heading=Math.Clamp(Int(def,"heading"),1,4),hp=Math.Max(1,Int(def,"maxHp")),maxHp=Math.Max(1,Int(def,"maxHp"))}});
        }
        BindMap(map);store.maps.Add(id,map);dirty=true;return map;
    }
    void BindMap(MapRecord map)
    {
        map.source=templates[map.id];
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
    void Event(Session s,AOCoopEvent e)
    { if(s.Record.events.Count>=256)throw new InvalidOperationException("Demasiadas recompensas pendientes; liberá inventario y reconectá.");e.seq=++s.Record.nextEvent;s.Record.events.Add(e);dirty=true; }
    public bool ValidPosition(int map,int x,int y)=>templates.TryGetValue(map,out var t)&&x>=Int(t,"xmin")&&x<=Int(t,"xmax")&&y>=Int(t,"ymin")&&y<=Int(t,"ymax");
    static void Alive(Session s){if(s.State.dead)throw new InvalidOperationException("No podés hacer eso muerto.");}
    static bool Harmful(JsonNode s)=>Int(s,"hostileEffect")!=0||Int(s,"raiseHp")==2||Int(s,"paralyze")!=0||Int(s,"immobilize")!=0||Int(s,"poison")!=0||Int(s,"incinerate")!=0||Int(s,"curse")!=0||Int(s,"blindness")!=0||Int(s,"dumb")!=0||Int(s,"raiseMana")==2||Int(s,"raiseStamina")==2||Int(s,"raiseStrength")==2||Int(s,"raiseAgility")==2||Int(s,"stealBuff")!=0;
    static int CountItem(JsonNode inv,int item){long total=0;var ids=inv["itemIndices"]!.AsArray();var amounts=inv["amounts"]!.AsArray();for(int i=0;i<ids.Count;i++)if(ids[i]?.GetValue<int>()==item)total+=amounts[i]?.GetValue<int>()??0;return (int)Math.Min(int.MaxValue,total);}
    static int Heading(int dx,int dy)=>Math.Abs(dx)>=Math.Abs(dy)&&dx!=0?(dx>0?2:4):(dy>0?3:1);
    static int Distance(int x,int y,int tx,int ty)=>Math.Max(Math.Abs(x-tx),Math.Abs(y-ty));
    int Roll(int min,int max)=>random.Next(Math.Min(min,max),Math.Max(min,max)+1);
    static long Now=>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    internal static int Int(JsonNode? n,string k)=>n?[k]?.GetValue<int>()??0;
    static float Float(JsonNode? n,string k)=>n?[k]?.GetValue<float>()??0;
    static bool Bool(JsonNode? n,string k)=>n?[k]?.GetValue<bool>()??false;
    static string Text(JsonNode? n,string k)=>n?[k]?.GetValue<string>()??"";
    static Dictionary<int,JsonObject> Index(JsonNode? node,string key)=>(node?.AsArray()??new JsonArray()).OfType<JsonObject>().ToDictionary(n=>Int(n,key));
}
sealed class Session(int id,string characterId,CharacterRecord record)
{
    public readonly int Id=id; public readonly string CharacterId=characterId; public readonly CharacterRecord Record=record;
    public AOCoopPlayer State=new(); public long LastAction,NextAttack,NextCast;
    public Dictionary<int,long> SpellCooldown=new(),PetCooldown=new();
    public Dictionary<string,AOCoopMessage> Replies=new();
}
sealed class RoomSave { public Dictionary<string,int> stocks=new(); public int nextLoot; public Dictionary<string,CharacterRecord> characters=new(); public Dictionary<int,MapRecord> maps=new(); }
sealed class CharacterRecord { public string name="",tokenHash="",snapshot=""; public long ack,nextEvent; public List<AOCoopEvent> events=new(); }
sealed class MapRecord
{
    public int id; public List<NpcRecord> npcs=new(); public List<AOCoopLoot> loot=new(); public List<AOCoopDoor> doors=new();
    [JsonIgnore] public JsonObject source=new();
    [JsonIgnore] public Dictionary<int,int> flags=new(),triggers=new();
    [JsonIgnore] public HashSet<int> exits=new();
}
sealed class NpcRecord
{
    public AOCoopNpc state=new(); public long respawnAt,poisonUntil,fireUntil,paralyzedUntil,immobileUntil,nextPoison,nextFire;
    public int provoked,route;
    [JsonIgnore] public long nextMove,nextAttack;
    [JsonIgnore] public JsonObject source=new();
}
