using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using RplaceServer;
using WatsonWebsocket;
using WorkerOfficial;

const string configFilePath = "server_config.json";
const string dataPath = "ServerData";

async Task CreateConfig()
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write($"[Warning]: Could not find game config file, at {configFilePath}.");

    await using var configFile = File.OpenWrite(configFilePath);
    var defaultConfiguration =
        new Configuration(
            27277,
            false,
            "",
            "",
            new IntRange(0, 100),
            new IntRange(3000, 4000),
            new IntRange(4000, 5000),
            "ws://localhost:1234",
            "Auth server GUID instance key",
            "server.poemanthology.org");
    await JsonSerializer.SerializeAsync(configFile, defaultConfiguration, new JsonSerializerOptions { WriteIndented = true });
    await configFile.FlushAsync();
    
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"\n[INFO]: Config files recreated. Please check {Directory.GetCurrentDirectory()} and run this program again.");
    Console.ResetColor();
}

if (!File.Exists(configFilePath))
{
    await CreateConfig();
    Environment.Exit(0);
}

if (!Directory.Exists(dataPath))
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write($"[Warning]: Could not find data path, at {dataPath}.");
    Directory.CreateDirectory(dataPath);
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"\n[INFO]: Data path recreated successfully, program will continue to run.");
    Console.ResetColor();
}

var defaultGameData = new GameData(
    5000, 2500, false, true, new List<string>(), new List<string>(),
    new List<string>(), 1000, 1000, 600000,  false, "Canvases",
    "Posts", 60, 300000, true, 100, "", null);
var workerData = new WorkerData
{
    Ids = new List<int>(),
    SocketPorts = new List<int>(),
    WebPorts = new List<int>()
};
Configuration config;
await using (var configFileStream = File.OpenRead(configFilePath))
{
    config = await JsonSerializer.DeserializeAsync<Configuration>(configFileStream);
}
var instances = new Dictionary<int, ServerInstance>();
var client = new WatsonWsClient(new Uri(config.AuthServerUri));
var server = new WatsonWsServer(27277, config.UseHttps, config.CertPath, config.KeyPath);
var authQueue = new Dictionary<int, TaskCompletionSource<bool>>();
var subscriberGroups = new Dictionary<int, List<ClientMetadata>>();
var requestId = 0;

client.Logger = Console.WriteLine;
server.Logger = Console.WriteLine;
    
int NextId()
{
    var next = config.IdRange.Start;
    
    if (workerData.Ids.Count != 0)
    {
        next = workerData.Ids.Max() + 1;
    }

    if (next > config.IdRange.End)
    {
        return -1;
    }
    
    workerData.Ids.Add(next);
    return next;
}

int NextSocketPort()
{
    var next = config.SocketPortRange.Start;
    
    if (workerData.SocketPorts.Count != 0)
    {
        next = workerData.SocketPorts.Max() + 1;
    }

    if (next > config.SocketPortRange.End)
    {
        return -1;
    }
    
    workerData.SocketPorts.Add(next);
    return next;
}

int NextWebPort()
{
    var next = config.WebPortRange.Start;
    
    if (workerData.WebPorts.Count != 0)
    {
        next = workerData.WebPorts.Max() + 1;
    }

    if (next > config.WebPortRange.End)
    {
        return -1;
    }
    
    workerData.WebPorts.Add(next);
    return next;
}

void AttachSubscribers(int instanceId)
{
    if (!instances.TryGetValue(instanceId, out var instance))
    {
        return;
    }
    
    instance.SocketServer.Logger = ForwardServerLog;
    instance.WebServer.Logger = ForwardServerLog;
    instance.Logger = ForwardServerLog;
    void ForwardServerLog(string message)
    {
        if (!subscriberGroups.TryGetValue(instanceId, out var subscribers))
        {
            return;
        }

        var encoded = Encoding.UTF8.GetBytes("X" + message);
        encoded[0] = (byte) WorkerPackets.Logger;

        foreach (var subscriber in subscribers)
        {
            server.SendAsync(subscriber, encoded);
        }
    }

    instance.SocketServer.PlayerConnected += (_, args) =>
    {
        if (!subscriberGroups.TryGetValue(instanceId, out var subscribers))
        {
            return;
        }

        var encoded = Encoding.UTF8.GetBytes("X" + JsonSerializer.Serialize(instance.GameData.Clients[args.Player]));
        encoded[0] = (byte) WorkerPackets.PlayerConnected;

        foreach (var subscriber in subscribers)
        {
            server.SendAsync(subscriber, encoded);
        }
    };

    instance.SocketServer.PlayerDisconnected += (_, args) =>
    {
        if (!subscriberGroups.TryGetValue(instanceId, out var subscribers))
        {
            return;
        }

        // TODO: We need an BeforePlayerDisconnected event too, else we can't catch the real IPPort of this client
        // TODO: (may cause) issues on reverse-proxied severs.
        var encoded = Encoding.UTF8.GetBytes("X" + args.Player.IpPort);
        encoded[0] = (byte) WorkerPackets.PlayerDisconnected;
        
        foreach (var subscriber in subscribers)
        {
            server.SendAsync(subscriber, encoded);
        }
    };

    instance.WebServer.CanvasBackupCreated += (_, args) =>
    {
        if (!subscriberGroups.TryGetValue(instanceId, out var subscribers))
        {
            return;
        }

        var encoded = Encoding.UTF8.GetBytes("X" + args);
        encoded[0] = (byte) WorkerPackets.BackupCreated;
        
        foreach (var subscriber in subscribers)
        {
            server.SendAsync(subscriber, encoded);
        }
    };
}

// Wake up and add all existing instances
foreach (var id in workerData.Ids.ToList())
{
    var serverPath = Path.Join(dataPath, id.ToString());
    if (!File.Exists(serverPath))
    {
        Console.WriteLine($"Could not find server with id {id}, deleting from worker data.");
        workerData.Ids.Remove(id);
        continue;
    }

    await using var gameDataStream = File.OpenRead(Path.Join(serverPath, "game_data.json"));
    var gameData = await JsonSerializer.DeserializeAsync<GameData>(gameDataStream);
    if (gameData is null)
    {
        Console.WriteLine($"Could not find game data for server with id: {id}, will attempt to use default");
        gameData = defaultGameData;
    }

    await using var serverDataStream = File.OpenRead(Path.Join(serverPath, "server_data.json"));
    var serverData = await JsonSerializer.DeserializeAsync<ServerData>(serverDataStream);
    if (serverData is null)
    {
        Console.WriteLine($"Could not find server data for server with id: {id}, will attempt to generate new. Vanity name will be lost.");
        serverData = new ServerData(id, null, NextSocketPort(), NextWebPort());
    }

    if (serverData.VanityName != null)
    {
        var vanityBuffer = Encoding.UTF8.GetBytes("X" + serverData.VanityName
            + $"\nserver={(config.UseHttps ? "wss" : "ws")}://{config.PublicHostname}:{serverData.SocketPort}"
            + $"&board={(config.UseHttps ? "https" : "http")}://{config.PublicHostname}:{serverData.WebPort}/place");
        vanityBuffer[0] = (byte) WorkerPackets.AnnounceVanity;
        await client.SendAsync(vanityBuffer);
    }

    instances.Add(id, new ServerInstance(gameData, config.KeyPath, config.CertPath, "", serverData.SocketPort, serverData.WebPort, config.UseHttps));
    AttachSubscribers(id);
}

// We announce ourselves to the auth server so that we can be advertised to clients
client.ServerConnected += async (_, _) =>
{
    var instanceKeyHostnameBytes = Encoding.UTF8.GetBytes(
        config.InstanceKey + "\n" + ((config.UseHttps ? "wss://" : "ws://") + config.PublicHostname + ":" + config.Port));
    var announceBuffer = new byte[9 + instanceKeyHostnameBytes.Length];
    announceBuffer[0] = (byte) WorkerPackets.AnnounceExistence;
    BinaryPrimitives.WriteInt32BigEndian(announceBuffer.AsSpan()[1..], config.IdRange.Start);
    BinaryPrimitives.WriteInt32BigEndian(announceBuffer.AsSpan()[5..], config.IdRange.End);
    instanceKeyHostnameBytes.CopyTo(announceBuffer.AsSpan()[9..]);
    await client.SendAsync(announceBuffer);
};

// Comes from auth server
client.MessageReceived += (_, args) =>
{
    var data = args.Data.ToArray()[1..];
    if (data.Length != 5)
    {
        return;
    }
    
    var requestHandle = BinaryPrimitives.ReadInt32BigEndian(data);
    if (args.Data.ToArray()[0] == (byte) ServerPackets.Authorised && authQueue.TryGetValue(requestHandle, out var completionSource))
    {
        completionSource.SetResult(data[4] == 1);
    }
};

// Comes from clients
server.MessageReceived += async (_, args) =>
{
    if (args.Data.ToArray().Length == 0)
    {
        return;
    }
    
    var data = args.Data.ToArray()[1..];

    switch (args.Data.ToArray()[0])
    {
        case (byte) ClientPackets.CreateInstance:
        {
            if (data.Length != 46)
            {
                return;
            }
            
            // Accept - start making new server instance
            var id = NextId();
            var socketPort = NextSocketPort();
            var webPort = NextWebPort();
            if (id == -1 || socketPort == -1 || webPort == -1)
            {
                return;
            }

            // Check with auth server that they are allowed to do this
            var authoriseCompletion = new TaskCompletionSource<bool>();
            var requestHandle = requestId++;
            authQueue.Add(requestHandle, authoriseCompletion);

            var authBuffer = new byte[51];
            authBuffer[0] = (byte) WorkerPackets.AuthenticateCreate; // 1 byte - Packet code
            data.CopyTo(authBuffer.AsSpan()[1..]); // 42 bytes - Client auth
            BinaryPrimitives.WriteInt32BigEndian(authBuffer.AsSpan()[43..], requestHandle); // 4 bytes - request ID
            BinaryPrimitives.WriteInt32BigEndian(authBuffer.AsSpan()[47..], id); // 4 bytes - instance ID
            await client.SendAsync(authBuffer);
            
            if (!await authoriseCompletion.Task)
            {
                workerData.Ids.Remove(id);
                workerData.SocketPorts.Remove(socketPort);
                workerData.WebPorts.Remove(webPort);
                
                // Remove from the queue now that we are done with it
                authQueue.Remove(requestHandle);
                return;
            }
            
            // Remove from the queue now that we are done with it
            authQueue.Remove(requestHandle);

            // Set up directory that will be used by the new instance server software + it's configuration
            var instanceDirectory = Path.Join(dataPath, id.ToString());
            Directory.CreateDirectory(instanceDirectory);

            // Set up the new instance server software data files
            var gameData = defaultGameData with { CanvasFolder = Path.Join(instanceDirectory, "Canvases"), PostsFolder = Path.Join(instanceDirectory, "Posts") };
            var instance = new ServerInstance(gameData, config.CertPath, config.KeyPath, "", socketPort, webPort, config.UseHttps);
            await using var gameDataStream = File.Create(Path.Join(instanceDirectory, "game_data.json"));
            await JsonSerializer.SerializeAsync(gameDataStream, gameData);
            await using var serverDataStream = File.Create(Path.Join(instanceDirectory, "server_data.json"));
            await JsonSerializer.SerializeAsync(serverDataStream, new ServerData(id, null, socketPort, webPort));

            // Start the new instance
            instances.Add(id, instance);
            AttachSubscribers(id);
            _ = Task.Run(instance.StartAsync);
            
            // data 42...46 is a requestID used by the client so that it can confirm completion, along with the instance ID of the new instance
            var completionBuffer = new byte[9];
            completionBuffer[0] = (byte) WorkerPackets.InstanceCreated;
            data[42..46].CopyTo(completionBuffer, 1);
            BinaryPrimitives.WriteInt32BigEndian(completionBuffer.AsSpan()[5..], id);
            await server.SendAsync(args.Client, completionBuffer);
            break;
        }
        case (byte) ClientPackets.DeleteInstance:
        {
            if (data.Length != 46)
            {
                return;
            }
            
            var instanceId = BinaryPrimitives.ReadInt32BigEndian(data[42..]);

            // Check with auth server that they are allowed to do this
            var authoriseDeletion = new TaskCompletionSource<bool>();
            var requestHandle = requestId++;
            authQueue.Add(requestHandle, authoriseDeletion);

            var authBuffer = new byte[51];
            authBuffer[0] = (byte) WorkerPackets.AuthenticateDelete; // 1 byte - Packet code
            data.CopyTo(authBuffer.AsSpan()[1..]); // 42 bytes - Client auth
            BinaryPrimitives.WriteInt32BigEndian(authBuffer.AsSpan()[43..], requestHandle); // 4 bytes - request ID
            BinaryPrimitives.WriteInt32BigEndian(authBuffer.AsSpan()[47..], instanceId); // 4 bytes - instance ID
            await client.SendAsync(authBuffer);

            if (!await authoriseDeletion.Task)
            {
                authQueue.Remove(requestHandle);
                return;
            }

            if (instances.TryGetValue(instanceId, out var instance))
            {
                await instance.StopAsync();
                instances.Remove(instanceId);
            }
            
            // TODO: Find a fix for the port leakage which will occur due to ports not being released.
            workerData.Ids.Remove(instanceId);
            break;
        }
        case (byte) ClientPackets.Subscribe:
        {
            if (data.Length != 51)
            {
                return;
            }

            var instanceId = BinaryPrimitives.ReadInt32BigEndian(data[42..]);

            // TODO: Make some kind of method for modify-based authentication, as the code below this will be repeated
            // TODO: many times throught this codebase.
            // Check with auth server that they are allowed to do this
            var authoriseModify = new TaskCompletionSource<bool>();
            var requestHandle = requestId++;
            authQueue.Add(requestHandle, authoriseModify);

            var authBuffer = new byte[51];
            authBuffer[0] = (byte) WorkerPackets.AuthenticateManage; // 1 byte - Packet code
            data.CopyTo(authBuffer.AsSpan()[1..]); // 42 bytes - Client auth
            BinaryPrimitives.WriteInt32BigEndian(authBuffer.AsSpan()[43..], requestHandle); // 4 bytes - request ID
            BinaryPrimitives.WriteInt32BigEndian(authBuffer.AsSpan()[47..], instanceId); // 4 bytes - instance ID
            await client.SendAsync(authBuffer);

            if (!await authoriseModify.Task)
            {
                return;
            }
            // TODO: END

            subscriberGroups.TryAdd(instanceId, new List<ClientMetadata>());
            subscriberGroups[instanceId].Add(args.Client);
            break;
        }
        case (byte) ClientPackets.QueryInstance:
        {
            if (data.Length != 4)
            {
                return;
            }
            
            var instanceId = BinaryPrimitives.ReadInt32BigEndian(data);
            
            await using var dataStream = File.OpenRead(Path.Join(dataPath, instanceId.ToString(), "server_data.json"));
            var instanceData = await JsonSerializer.DeserializeAsync<ServerData>(dataStream);
            if (instanceData is null)
            {
                return;
            }

            var info = JsonSerializer.Serialize(new InstanceInfo(
                instances.ContainsKey(instanceId),
                $"\nserver={(config.UseHttps ? "wss" : "ws")}://{config.PublicHostname}:{instanceData.SocketPort}" 
                + $"&board={(config.UseHttps ? "https" : "http")}://{config.PublicHostname}:{instanceData.WebPort}/place",
                instanceData.VanityName));
            
            var encoded = Encoding.UTF8.GetBytes("X" + info);
            encoded[0] = (byte) WorkerPackets.InstanceQuery;
            await server.SendAsync(args.Client, encoded);
            break;
        }
        case (byte) ClientPackets.CreateVanity:
        {
            var instanceId = BinaryPrimitives.ReadInt32BigEndian(data[42..]);

            // Check with auth server that they are allowed to do this
            var authoriseVanity = new TaskCompletionSource<bool>();
            var requestHandle = requestId++;
            authQueue.Add(requestHandle, authoriseVanity);

            await using var dataStream = File.OpenRead(Path.Join(dataPath, instanceId.ToString(), "server_data.json"));
            var instanceData = await JsonSerializer.DeserializeAsync<ServerData>(dataStream);
            if (instanceData is null)
            {
                return;
            }
            
            var vanityLinkBuffer = Encoding.UTF8.GetBytes(
                $"\nserver={(config.UseHttps ? "wss" : "ws")}://{config.PublicHostname}:{instanceData.SocketPort}" 
                + $"&board={(config.UseHttps ? "https" : "http")}://{config.PublicHostname}:{instanceData.WebPort}/place");
            var authBuffer = new byte[51 + data[46..].Length + vanityLinkBuffer.Length];
            authBuffer[0] = (byte) WorkerPackets.AuthenticateVanity; // 1 byte - Packet code
            data.CopyTo(authBuffer.AsSpan()[1..]); // 42 bytes - Client auth
            BinaryPrimitives.WriteInt32BigEndian(authBuffer.AsSpan()[43..], requestHandle); // 4 bytes - request ID
            BinaryPrimitives.WriteInt32BigEndian(authBuffer.AsSpan()[47..], instanceId); // 4 bytes - instance ID
            data[46..].CopyTo(authBuffer, 51); // Copy over vanity name (variable length)
            vanityLinkBuffer.CopyTo(authBuffer, 51 + data[46..].Length); // Append real canvas link (variable length)
            await client.SendAsync(authBuffer);

            if (!await authoriseVanity.Task)
            {
                return;
            }

            // Accept - Apply vanity to instance's data so that it can be retained on worker restart
            instanceData.VanityName = Encoding.UTF8.GetString(data[46..]);
            await JsonSerializer.SerializeAsync(dataStream, instanceData);
            break;
        }
    }
};

Console.CancelKeyPress += (_, _) =>
{
    server.StopAsync();
    Environment.Exit(0);
};
AppDomain.CurrentDomain.UnhandledException += (_, exceptionEventArgs) =>
{
    Console.WriteLine("Unhandled server exception: " + exceptionEventArgs.ExceptionObject);
};

Console.WriteLine("Server started, connecting websockets.");
await Task.WhenAll(client.StartAsync(), server.StartAsync());
await Task.Delay(-1);