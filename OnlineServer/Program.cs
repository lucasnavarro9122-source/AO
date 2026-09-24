using System.Collections.Concurrent;
using System.Net;
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
var room=new CoopRoom(catalog,data);
var listener=new TcpListener(IPAddress.Any,port);
var clients=new ConcurrentDictionary<int,Connection>();
using var permits=new SemaphoreSlim(32);
using var stop=new CancellationTokenSource();
listener.Start(32);
Console.WriteLine($"AO cooperativo v2: TCP {port}; anfitrión + 10 amigos. Guardados: {data}");
Console.WriteLine("Clave de acceso disponible en room-key.txt.");
Console.CancelKeyPress+=(_,e)=>{e.Cancel=true;stop.Cancel();listener.Stop();};
var ticker=Task.Run(async()=>
{
    try
    {
        while(!stop.IsCancellationRequested)
        {
            await Task.Delay(150,stop.Token);
            (Session,AOCoopMessage)[] updates;
            lock(room.Gate)updates=room.Tick().ToArray();
            foreach(var (s,message) in updates)if(clients.TryGetValue(s.Id,out var c))c.Send(message);
        }
    }
    catch(OperationCanceledException) { }
    catch(Exception e){Console.Error.WriteLine("No se pudo mantener el mundo: "+e.Message);stop.Cancel();listener.Stop();}
});
try
{
    while(!stop.IsCancellationRequested)
    {
        TcpClient socket=await listener.AcceptTcpClientAsync(stop.Token);
        if(!permits.Wait(0)){socket.Close();continue;}
        _=Task.Run(()=>Handle(socket));
    }
}
catch(Exception e) when(e is OperationCanceledException or SocketException) { }
finally
{
    stop.Cancel();listener.Stop();foreach(var c in clients.Values)c.Close();
    lock(room.Gate)room.Save();
}
void Handle(TcpClient socket)
{
    Session? session=null;var connection=new Connection(socket);
    try
    {
        socket.NoDelay=true;socket.ReceiveTimeout=60000;socket.SendTimeout=1500;
        var hello=connection.Read();
        if(hello?.type!="hello"||!SameKey(hello.key??"",key))throw new InvalidOperationException("Clave de sala inválida.");
        lock(room.Gate){session=room.Join(hello);connection.Send(room.Welcome(session));clients[session.Id]=connection;}
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
            AOCoopMessage result;lock(room.Gate)result=room.Process(session,m);
            if(result.type!="noop")connection.Send(result);
        }
    }
    catch(Exception e) when(e is IOException or SocketException or JsonException or InvalidOperationException or ArgumentException or FormatException or OverflowException)
    {connection.Send(new AOCoopMessage{type="error",text=e is InvalidOperationException?e.Message:"Conexión cerrada por datos inválidos."});}
    finally
    {
        if(session!=null){clients.TryRemove(session.Id,out _);lock(room.Gate)room.Leave(session);Console.WriteLine("Salió "+session.State.name);}
        connection.Close();permits.Release();
    }
}
static bool SameKey(string a,string b){var x=Encoding.UTF8.GetBytes(a);var y=Encoding.UTF8.GetBytes(b);return x.Length==y.Length&&CryptographicOperations.FixedTimeEquals(x,y);}
sealed class Connection(TcpClient socket)
{
    readonly object gate=new();
    public AOCoopMessage? Read()
    {
        var stream=socket.GetStream();using var bytes=new MemoryStream();
        while(bytes.Length<131072){int b=stream.ReadByte();if(b<0)return null;if(b=='\n')return JsonSerializer.Deserialize<AOCoopMessage>(bytes.ToArray(),CoopRoom.Json);bytes.WriteByte((byte)b);}
        throw new IOException("Paquete demasiado grande.");
    }
    public void Send(AOCoopMessage message)
    {
        try{byte[] bytes=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message,CoopRoom.Json)+"\n");lock(gate)socket.GetStream().Write(bytes);}
        catch(Exception e) when(e is IOException or SocketException or ObjectDisposedException or InvalidOperationException){Close();}
    }
    public void Close()=>socket.Close();
}
