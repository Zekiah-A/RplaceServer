using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using RplaceServer;
using WatsonWebsocket;
using WorkerOfficial;

const string configPath = "server_config.json";
const string dataPath = "ServerData";
var dataFilePath = Path.Join(dataPath, "server_data.json");

async Task CreateConfig()
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write($"[Warning]: Could not find game config file, at {configPath}.");

    await using var configFile = File.OpenWrite(configPath);
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
            "Auth server GUID instance key");
    await JsonSerializer.SerializeAsync(configFile, defaultConfiguration, new JsonSerializerOptions { WriteIndented = true });
    await configFile.FlushAsync();
    
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"\n[INFO]: Config files recreated. Please check {Directory.GetCurrentDirectory()} and run this program again.");
    Console.ResetColor();
}

if (!File.Exists(configPath))
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

if (!File.Exists(dataFilePath))
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write("[Warning]: Could not find data file, at " + dataFilePath);
    var defaultDataFile = new WorkerData(new List<int>(), new List<int>(), new List<int>());
    File.WriteAllText(dataFilePath, JsonSerializer.Serialize(defaultDataFile));
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"\n[INFO]: Data file recreated successfully, program will continue to run.");
    Console.ResetColor();
}

var defaultGameData = new GameData(
    5000, 2500, false, true, new List<string>(), new List<string>(),
    new List<string>(), 1000, 1000, 600000,  false, "Canvases",
    "Posts", 60, 300000, true, 100, "", new List<uint>());
var config = await JsonSerializer.DeserializeAsync<Configuration>(File.OpenRead(configPath));
var workerData = await JsonSerializer.DeserializeAsync<WorkerData>(File.OpenRead(dataFilePath));
var instances = new Dictionary<int, ServerInstance>();
var client = new WatsonWsClient(new Uri(config.AuthServerUri));
var server = new WatsonWsServer(27277, config.UseHttps, config.CertPath, config.KeyPath);
var createAuthQueue = new Dictionary<int, TaskCompletionSource<bool>>();
var deleteAuthQueue = new Dictionary<int, TaskCompletionSource<bool>>();
var modifyAuthQueue = new Dictionary<int, TaskCompletionSource<bool>>();
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
    File.WriteAllText(dataFilePath, JsonSerializer.Serialize(workerData));
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
    File.WriteAllText(dataFilePath, JsonSerializer.Serialize(workerData));
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
    File.WriteAllText(dataFilePath, JsonSerializer.Serialize(workerData));
    return next;
}

// TODO: TEMP: We clear the WorkerData used web and socket ports as a temporary fix for port leakage.
workerData.SocketPorts = new List<int>();
workerData.WebPorts = new List<int>();

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

    var gameData = await JsonSerializer.DeserializeAsync<GameData>(File.OpenRead(Path.Join(serverPath, "game_data.json")));
    if (gameData is null)
    {
        Console.WriteLine($"Could not find game data for server with id: {id}, will attempt to use default");
        gameData = defaultGameData;
    }
    
    instances.Add(id, new ServerInstance(gameData, config.KeyPath, config.CertPath, "", NextSocketPort(), NextWebPort(), config.UseHttps));
    await JsonSerializer.SerializeAsync(File.OpenWrite(dataFilePath), workerData);
}

// We announce ourselves to the auth server so that we can be advertised to clients
client.ServerConnected += async (_, _) =>
{
    var instanceKeyBytes = Encoding.UTF8.GetBytes(config.InstanceKey);
    var announceBuffer = new byte[1 + instanceKeyBytes.Length];
    announceBuffer[0] = (byte) WorkerPackets.AnnounceExistence;
    instanceKeyBytes.CopyTo(announceBuffer.AsSpan()[1..]);
    await client.SendAsync(announceBuffer);
};

// Comes from auth server
client.MessageReceived += (_, args) =>
{
    Console.WriteLine("Incoming response from auth server");
    
    var data = args.Data.ToArray()[1..];
    if (data.Length != 5)
    {
        return;
    }
    
    var id = BinaryPrimitives.ReadInt32BigEndian(data); // 0, 1, 2, 3
    var result = data[4] == 1; // 4
    
    if (args.Data.ToArray()[0] == (byte) ServerPackets.AuthorisedCreateInstance && createAuthQueue.TryGetValue(id, out var createSource))
    {
        createSource.SetResult(result);
    }
    else if (args.Data.ToArray()[0] == (byte) ServerPackets.AuthorisedDeleteInstance && deleteAuthQueue.TryGetValue(id, out var deleteSource))
    {
        deleteSource.SetResult(result);
    }
    else if (args.Data.ToArray()[0] == (byte) ServerPackets.Authorised && modifyAuthQueue.TryGetValue(id, out var source))
    {
        source.SetResult(result);
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
            if (data.Length != 42)
            {
                return;
            }

            Console.WriteLine("Client created server instance successfully");
                
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
            createAuthQueue.Add(requestHandle, authoriseCompletion);

            var authBuffer = new byte[51];
            authBuffer[0] = (byte) WorkerPackets.AuthenticateCreate; // 1 byte - Packet code
            data.CopyTo(authBuffer.AsSpan()[1..]); // 42 bytes - Client auth
            BinaryPrimitives.TryWriteInt32BigEndian(authBuffer.AsSpan()[43..], requestHandle); // 4 bytes - request ID
            BinaryPrimitives.TryWriteInt32BigEndian(authBuffer.AsSpan()[47..], id); // 4 bytes - instance ID
            await client.SendAsync(authBuffer);
            
            if (!await authoriseCompletion.Task)
            {
                workerData.Ids.Remove(id);
                workerData.SocketPorts.Remove(socketPort);
                workerData.WebPorts.Remove(webPort);
                await JsonSerializer.SerializeAsync(File.OpenWrite(dataFilePath), workerData);
                
                // Remove from the queue now that we are done with it
                createAuthQueue.Remove(requestHandle);
                return;
            }
            
            // Remove from the queue now that we are done with it
            createAuthQueue.Remove(requestHandle);

            // Set up directory that will be used by the new instance server software + it's configuration
            var instanceDirectory = Path.Join(dataPath, id.ToString());
            Directory.CreateDirectory(instanceDirectory);

            // Set up the new instance server software
            var gameData = defaultGameData with { CanvasFolder = Path.Join(instanceDirectory, "Canvases"), PostsFolder = Path.Join(instanceDirectory, "Posts") };
            var instance = new ServerInstance(gameData, config.CertPath, config.KeyPath, "", socketPort, webPort, config.UseHttps);
            await JsonSerializer.SerializeAsync(File.OpenWrite(Path.Join(instanceDirectory, "gamedata.json")), gameData);

            // Start the new instance
            instances.Add(id, instance);
            _ = Task.Run(instance.StartAsync);
            break;
        }
        case (byte) ClientPackets.DeleteInstance:
        {
            if (data.Length != 46)
            {
                return;
            }
            
            var instanceId = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan()[42..]);
            if (instances.TryGetValue(instanceId, out var instance))
            {
                await instance.StopAsync();
                instances.Remove(instanceId);
            }

            // Check with auth server that they are allowed to do this
            var authoriseDeletion = new TaskCompletionSource<bool>();
            var requestHandle = requestId++;
            deleteAuthQueue.Add(requestHandle, authoriseDeletion);

            var authBuffer = new byte[51];
            authBuffer[0] = (byte) WorkerPackets.AuthenticateDelete; // 1 byte - Packet code
            data.CopyTo(authBuffer.AsSpan()[1..]); // 42 bytes - Client auth
            BinaryPrimitives.TryWriteInt32BigEndian(authBuffer.AsSpan()[43..], requestHandle); // 4 bytes - request ID
            BinaryPrimitives.TryWriteInt32BigEndian(authBuffer.AsSpan()[47..], instanceId); // 4 bytes - instance ID
            await client.SendAsync(authBuffer);

            if (!await authoriseDeletion.Task)
            {
                deleteAuthQueue.Remove(requestHandle);
                return;
            }

            // TODO: Find a fix for the port leakage which will occur due to ports not being released.
            workerData.Ids.Remove(instanceId);
            await JsonSerializer.SerializeAsync(File.OpenWrite(dataFilePath), workerData);
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