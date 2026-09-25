using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

string Option(string name,string fallback){int i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
int port=int.Parse(Option("--port","7777"));
string data=Path.GetFullPath(Option("--data",Path.Combine(AppContext.BaseDirectory,"Saves")));
string catalog=Path.GetFullPath(Option("--catalog",Path.Combine(AppContext.BaseDirectory,"Data","catalog.json.gz")));
string keyPath=Path.GetFullPath(Option("--key-file",Path.Combine(AppContext.BaseDirectory,"room-key.txt")));
if(!File.Exists(keyPath))File.WriteAllText(keyPath,Convert.ToHexString(RandomNumberGenerator.GetBytes(18)));
string key=File.ReadAllText(keyPath).Trim();
if(key.Length<24)throw new InvalidOperationException("Clave de sala inválida.");
// --test: loopback only + testLedger. --test-time-scale speeds up duel timers (only with --test). --demo: demo rules.
bool test=args.Contains("--test");
double timeScale=double.Parse(Option("--test-time-scale","1"),System.Globalization.CultureInfo.InvariantCulture);
if(!test&&timeScale!=1)throw new InvalidOperationException("--test-time-scale solo se permite junto con --test.");
var options=new RoomOptions(test,Math.Clamp(timeScale,.01,1),args.Contains("--demo"));
var room=new CoopRoom(catalog,data,options);
var binds=BindAddresses(Option("--bind","auto"));
var listeners=binds.Select(ip=>new TcpListener(ip,port)).ToList();
var clients=new ConcurrentDictionary<int,Connection>();
// Review #2: at most 4 connections per address still saying hello (and 32 in total), so nobody can hold every slot.
var greeting=new ConcurrentDictionary<IPAddress,int>();
using var permits=new SemaphoreSlim(32);
using var stop=new CancellationTokenSource();
foreach(var l in listeners)l.Start(32);
Console.WriteLine($"AO cooperativo v{AOCoopMessage.Protocol}: TCP {port} en {string.Join(", ",binds.Select(b=>b.ToString()))}; anfitrión + 10 amigos. Guardados: {data}"+(options.Demo?" · DEMO":"")+(test?$" · MODO PRUEBA (tiempos ×{options.TimeScale})":""));
Console.WriteLine("Clave de acceso disponible en room-key.txt.");
Console.CancelKeyPress+=(_,e)=>{e.Cancel=true;stop.Cancel();foreach(var l in listeners)l.Stop();};
long lastTickError=0;
var ticker=Task.Run(async()=>
{
    try
    {
        while(!stop.IsCancellationRequested)
        {
            await Task.Delay(150,stop.Token);
            (Session,AOCoopMessage)[] updates;(int,AOCoopMessage)[] pushes;
            // An error in one tick never stops the room for everybody: it is logged (at most every 10 s) and the world goes on.
            try{lock(room.Gate){updates=room.Tick().ToArray();pushes=room.DrainOutbox();}}
            catch(Exception e) when(e is not OperationCanceledException)
            {
                if(Environment.TickCount64-lastTickError>10000){lastTickError=Environment.TickCount64;Console.Error.WriteLine("Error en el mundo (la sala sigue): "+e);}
                continue;
            }
            foreach(var (s,message) in updates)if(clients.TryGetValue(s.Id,out var c))c.Send(message);
            Deliver(pushes);
        }
    }
    catch(OperationCanceledException) { }
    catch(Exception e){Console.Error.WriteLine("No se pudo mantener el mundo: "+e.Message);stop.Cancel();foreach(var l in listeners)l.Stop();}
});
try{await Task.WhenAll(listeners.Select(l=>Task.Run(()=>AcceptLoop(l))));}
finally
{
    stop.Cancel();foreach(var l in listeners)l.Stop();foreach(var c in clients.Values)c.Close();
    lock(room.Gate){room.Save();room.Close();}
}

// --bind (review #2): "auto" (default) = 127.0.0.1 + this PC's Tailscale addresses (100.64.0.0/10); without Tailscale it
// falls back to every interface and says so. "any" = every interface. Or a list "ip,ip". --test is always 127.0.0.1.
IPAddress[] BindAddresses(string option)
{
    if(test)return new[]{IPAddress.Loopback};
    if(option=="any")return new[]{IPAddress.Any};
    if(option!="auto")return option.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Select(IPAddress.Parse).ToArray();
    var tailscale=NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up)
        .SelectMany(n=>n.GetIPProperties().UnicastAddresses).Select(a=>a.Address)
        .Where(a=>a.AddressFamily==AddressFamily.InterNetwork&&a.GetAddressBytes() is var b&&b[0]==100&&(b[1]&0xC0)==64).Distinct().ToArray();
    if(tailscale.Length>0)return new[]{IPAddress.Loopback}.Concat(tailscale).ToArray();
    Console.WriteLine("Aviso: no encontré una IP de Tailscale; escucho en todas las interfaces. Dejá el firewall abierto solo para Tailscale (100.64.0.0/10).");
    return new[]{IPAddress.Any};
}
async Task AcceptLoop(TcpListener listener)
{
    while(!stop.IsCancellationRequested)
    {
        TcpClient socket;
        try{socket=await listener.AcceptTcpClientAsync(stop.Token);}
        catch(Exception e) when(e is OperationCanceledException or ObjectDisposedException){break;}
        catch(SocketException e)
        {   // Review #3: one failed accept must not end the loop.
            if(stop.IsCancellationRequested)break;
            Console.Error.WriteLine("No se pudo aceptar una conexión: "+e.Message);await Task.Delay(100);continue;
        }
        var ip=(socket.Client.RemoteEndPoint as IPEndPoint)?.Address??IPAddress.None;
        if(greeting.AddOrUpdate(ip,1,(_,n)=>n+1)>4||!permits.Wait(0)){Greeted(ip);socket.Close();continue;}
        _=Task.Run(()=>Handle(socket,ip));
    }
}
void Greeted(IPAddress ip)=>greeting.AddOrUpdate(ip,0,(_,n)=>Math.Max(0,n-1));
void Handle(TcpClient socket,IPAddress ip)
{
    Session? session=null;var connection=new Connection(socket);bool greetingOpen=true;
    try
    {
        // Review #2: the hello has 5 s in total (not 60 s per byte); after that, the usual 60 s.
        socket.NoDelay=true;socket.ReceiveTimeout=5000;socket.SendTimeout=1500;
        var hello=connection.Read(Environment.TickCount64+5000);
        greetingOpen=false;Greeted(ip);
        if(hello?.type!="hello"||!SameKey(hello.key??"",key))throw new InvalidOperationException("Clave de sala inválida.");
        socket.ReceiveTimeout=60000;
        (int,AOCoopMessage)[] joined;
        lock(room.Gate){session=room.Join(hello);connection.Send(room.Welcome(session));clients[session.Id]=connection;joined=room.DrainOutbox();}
        Deliver(joined);
        Console.WriteLine("Entró "+session.State.name);
        long chatAt=0,windowAt=Environment.TickCount64;int received=0;
        while(!stop.IsCancellationRequested)
        {
            var m=connection.Read();if(m==null)break;
            long now=Environment.TickCount64;
            if(now-windowAt>1000){received=0;windowAt=now;}
            if(++received>45)throw new InvalidOperationException("Demasiados mensajes.");
            if(m.type=="chat")
            {
                if(now-chatAt<700)continue;chatAt=now;
                string text=new string((m.text??"").Where(c=>!char.IsControl(c)).Take(160).ToArray()).Trim();
                if(text.Length==0)continue;
                int[] recipients;lock(room.Gate)recipients=room.Recipients(session.State.map);
                foreach(int id in recipients)if(clients.TryGetValue(id,out var c))c.Send(new AOCoopMessage{type="chat",id=session.Id,name=session.State.name,text=text});
                continue;
            }
            AOCoopMessage result;(int,AOCoopMessage)[] pushes;
            lock(room.Gate){result=room.Process(session,m);pushes=room.DrainOutbox();}
            // Pushes first, then the reply: the reply carries the journal (duelEnd…), which must come after the pushes it
            // follows (duelRoundEnd), the same order everybody else gets (QA R-2: the last duelRoundEnd hid "¡VICTORIA!").
            Deliver(pushes);
            if(result.type!="noop")connection.Send(result);
        }
    }
    catch(Exception e) when(e is IOException or SocketException or JsonException or InvalidOperationException or ArgumentException or FormatException or OverflowException)
    {connection.Send(new AOCoopMessage{type="error",text=e is InvalidOperationException?e.Message:"Conexión cerrada por datos inválidos."});}
    catch(Exception e)
    {
        // A server bug must not vanish silently: log it and tell the player.
        Console.Error.WriteLine("Error inesperado con "+(session?.State.name??"un cliente")+": "+e);
        connection.Send(new AOCoopMessage{type="error",text="Error del servidor; reconectá."});
    }
    finally
    {
        // Review #3: whatever happens while leaving, the connection and its permit are always released.
        try
        {
            if(session!=null)
            {
                clients.TryRemove(session.Id,out _);(int,AOCoopMessage)[] left;
                lock(room.Gate){room.Leave(session);left=room.DrainOutbox();}
                Deliver(left);Console.WriteLine("Salió "+session.State.name);
            }
        }
        catch(Exception e){Console.Error.WriteLine("Error al cerrar la sesión de "+(session?.State.name??"un cliente")+": "+e.Message);}
        finally{if(greetingOpen)Greeted(ip);connection.Close();permits.Release();}
    }
}
void Deliver((int,AOCoopMessage)[] pushes){foreach(var (id,message) in pushes)if(clients.TryGetValue(id,out var c))c.Send(message);}
static bool SameKey(string a,string b){var x=Encoding.UTF8.GetBytes(a);var y=Encoding.UTF8.GetBytes(b);return x.Length==y.Length&&CryptographicOperations.FixedTimeEquals(x,y);}
sealed class Connection(TcpClient socket)
{
    readonly object gate=new();
    // deadline (Environment.TickCount64) limits the whole message, not each byte.
    public AOCoopMessage? Read(long deadline=0)
    {
        var stream=socket.GetStream();using var bytes=new MemoryStream();
        while(bytes.Length<131072)
        {
            if(deadline>0&&Environment.TickCount64>deadline)throw new IOException("El saludo tardó demasiado.");
            int b=stream.ReadByte();if(b<0)return null;
            if(b=='\n')return JsonSerializer.Deserialize<AOCoopMessage>(bytes.ToArray(),CoopRoom.Json);
            bytes.WriteByte((byte)b);
        }
        throw new IOException("Paquete demasiado grande.");
    }
    public void Send(AOCoopMessage message)
    {
        try{byte[] bytes=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message,CoopRoom.Json)+"\n");lock(gate)socket.GetStream().Write(bytes);}
        catch(Exception e) when(e is IOException or SocketException or ObjectDisposedException or InvalidOperationException){Close();}
    }
    public void Close()=>socket.Close();
}
