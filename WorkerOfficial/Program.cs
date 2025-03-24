using System.Text.Json;
using AuthWorkerShared;
using DataProto;
using Microsoft.Extensions.Logging;
using RplaceServer;
using RplaceServer.Events;
using WatsonWebsocket;
using WorkerOfficial;

var factory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = factory.CreateLogger("WorkerOfficial");

const string defaultOrigin = "https://rplace.live";
const string configFilePath = "server_config.json";
const string dataPath = "ServerData";

async Task CreateConfig()
{
    logger.LogWarning("Could not find game config file, at {configFilePath}", configFilePath);

    await using var configFile = File.OpenWrite(configFilePath);
    var defaultConfiguration =
        new Configuration(
            Configuration.CurrentVersion,
            27277,
            false,
            "",
            "",
            150,
            new IntRange(3000, 4000),
            new IntRange(4000, 5000),
            "ws://localhost:1234",
            "Auth server GUID instance key",
            "server.rplace.live");
    await JsonSerializer.SerializeAsync(configFile, defaultConfiguration, new JsonSerializerOptions { WriteIndented = true });
    await configFile.FlushAsync();
    
    logger.LogWarning("Config files recreated. Please check {currentDirectory} and run this program again", Directory.GetCurrentDirectory());
}

if (!File.Exists(configFilePath))
{
    await CreateConfig();
    Environment.Exit(0);
}

if (!Directory.Exists(dataPath))
{
    logger.LogWarning("Could not find data path, at {dataPath}", dataPath);
    Directory.CreateDirectory(dataPath);
    logger.LogInformation("Data path recreated successfully, server will continue running");
}

var workerData = new WorkerData
{
    Ids = new List<int>(),
    SocketPorts = new List<int>(),
    WebPorts = new List<int>()
};
var configFileText = File.ReadAllText(configFilePath);
var config = JsonSerializer.Deserialize<Configuration>(configFileText);
if (config is null)
{
    logger.LogError("Could not parse config file at {configFilePath}", configFilePath);
    Environment.Exit(1);
}

if (config.Version != Configuration.CurrentVersion)
{
    logger.LogWarning("Current config at {configFilePath} is outdated and cannot be used, please update config to the " +
        "latest version ({currentVersion}) before trying again", configFilePath, Configuration.CurrentVersion);
    Environment.Exit(0);
}

var instances = new Dictionary<int, ServerInstance>();
var authReconnectTimeout = TimeSpan.FromSeconds(1);
WatsonWsClient? authServer = null;
authServer = CreateAuthServerConnection();

WatsonWsClient CreateAuthServerConnection()
{
    var client = new WatsonWsClient(new Uri(config.AuthServerUri));
    client.Logger = OnAuthServerSocketLog;
    client.ServerConnected += OnAuthServerConnected;
    client.ServerDisconnected += OnAuthServerDisconnected;
    client.MessageReceived += OnAuthServerMessageReceived;
    return client;
}

int NextId()
{
    var next = 0;
    if (workerData.Ids.Count != 0)
    {
        next = workerData.Ids.Max() + 1;
    }
    if (next >= config.MaxInstances)
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

// Wake up and add all existing instances
foreach (var id in workerData.Ids.ToList())
{
    var serverPath = Path.Combine(dataPath, id.ToString());
    if (!File.Exists(serverPath))
    {
        logger.LogError("Could not find server with id {id}, deleting from worker data.", id);
        workerData.Ids.Remove(id);
        continue;
    }

    await using var gameDataStream = File.OpenRead(Path.Combine(serverPath, "game_data.json"));
    var gameData =  JsonSerializer.Deserialize<GameData>(gameDataStream);
    if (gameData is null)
    {
        logger.LogError("Could not find game data for server with id: {id}", id);
        workerData.Ids.Remove(id);
        return;
    }

    await using var serverDataStream = File.OpenRead(Path.Combine(serverPath, "server_data.json"));
    var serverData = JsonSerializer.Deserialize<ServerData>(serverDataStream);
    if (serverData is null)
    {
        logger.LogError("Could not find server data for server with id: {id}, will attempt to generate new. Vanity name will be lost", id);
        serverData = new ServerData(id, NextSocketPort(), NextWebPort());
    }

    instances.Add(id, new ServerInstance(gameData, config.KeyPath, config.CertPath, defaultOrigin, serverData.SocketPort, serverData.WebPort, config.UseHttps));
}


void ForwardServerLog(string message)
{
    var packet = new WriteablePacket();
    packet.WriteByte((byte) WorkerPackets.LoggerEntry);
    packet.WriteString(message);
    authServer.SendAsync(packet);
}

void ForwardPlayerConnected(object? sender, PlayerConnectedEventArgs args)
{
    if (sender is not ServerInstance instance)
    {
        return;
    }

    var packet = new WriteablePacket();
    var clientData = instance.Clients[args.Player];
    packet.WriteByte((byte) WorkerPackets.PlayerConnected);
    packet.Write(clientData);
    authServer.SendAsync(packet);
}

void ForwardPlayerDisconnected(object? _, PlayerDisconnectedEventArgs args)
{
    var packet = new WriteablePacket();
    var clientData = args.Instance.Clients[args.Player];
    packet.WriteByte((byte)WorkerPackets.PlayerDisconnected);
    packet.Write(clientData);
    authServer.SendAsync(packet);
}

void ForwardCanvasBackupCreated(object? _, CanvasBackupCreatedEventArgs args)
{
    var packet = new WriteablePacket();
    packet.WriteByte((byte) WorkerPackets.BackupCreated);
    packet.Write(args.Name);
    packet.Write(args.Path);
    packet.Write(new DateTimeOffset(args.Created).ToUnixTimeMilliseconds());
    authServer.SendAsync(packet);
}

void OnAuthServerSocketLog(string message)
{
    logger.LogInformation("{message}", message);
}

// We announce ourselves to the auth server so that we can be advertised to clients
void OnAuthServerConnected(object? sender, EventArgs args)
{
    logger.LogInformation("Connected to auth server");
    var authPacket = new WriteablePacket();
    authPacket.WriteByte((byte) WorkerPackets.AnnounceExistence);
    var instanceKey = config.InstanceKey;
    authPacket.WriteString(instanceKey);
    var scheme = config.UseHttps ? "wss" : "ws";
    var builder = new UriBuilder
    {
        Scheme = scheme,
        Host = config.PublicHostname,
        Port = config.Port
    };
    var instanceUri = builder.ToString();
    authPacket.WriteString(instanceUri);
    authPacket.WriteInt(workerData.Ids.Count);
    authPacket.WriteInt(config.MaxInstances);
    authServer.SendAsync(authPacket);
}

async void OnAuthServerDisconnected(object? sender, EventArgs args)
{
    logger.LogInformation("Disconnected from auth server, attempting to reconnect in {reconnectTime}", authReconnectTimeout);
    await authServer.StopAsync();
    await Task.Delay(authReconnectTimeout);
    await authServer.StartAsync();
}

// Comes from auth server
void OnAuthServerMessageReceived(object? sender, MessageReceivedEventArgs args)
{
    var packet = new ReadablePacket(args.Data.ToArray());
    var code = packet.ReadByte();
    switch (code)
    {
        // Create new server instance
        case (byte) AuthPackets.CreateInstance:
        {
            var requestId = packet.ReadUInt();
            var responsePacket = new WriteablePacket();
            responsePacket.WriteByte((byte) WorkerPackets.InstanceCreateStatus);
            responsePacket.WriteUInt(requestId);
            
            var id = NextId();
            var socketPort = NextSocketPort();
            var webPort = NextWebPort();
            if (id == -1 || socketPort == -1 || webPort == -1)
            {
                responsePacket.WriteBool(false);
                authServer.SendAsync(responsePacket);
                return;
            }
            
            var instanceDirectory = Path.Combine(dataPath, id.ToString());
            if (Directory.Exists(instanceDirectory))
            {
                responsePacket.WriteBool(false);
                authServer.SendAsync(responsePacket);
                return;
            }
            // Set up directory that will be used by the new instance server software + it's configuration
            Directory.CreateDirectory(instanceDirectory);

            var width = packet.ReadUInt();
            var height = packet.ReadUInt();
            var cooldownMs = packet.ReadUInt();
            
            var gameData = IGameDataBuilder<GameData>.CreateBuilder()
                .ConfigureCanvas(options =>
                {
                    options.BoardWidth = width;
                    options.BoardHeight = height;
                    options.CooldownMs = cooldownMs * 1000;
                })
                .ConfigureModeration(options =>
                {
                    options.UseCloudflare = true;
                    options.CaptchaEnabled = false;
                    options.ChatCooldownMs = 2500;
                    options.CensorChatMessages = true;
                })
                .ConfigureStorage(options =>
                {
                    options.CreateBackups = true;
                    options.CanvasFolder = Path.Combine(instanceDirectory, "Canvases");
                    options.BackupFrequencyS = TimeSpan.FromMinutes(15).Seconds;
                    options.StaticResourcesFolder = Path.Combine(instanceDirectory, "StaticData");
                    options.SaveDataFolder = Path.Combine(instanceDirectory, "SaveData");
                    options.TimelapseLimitPeriodS = 900;
                    options.TimelapseEnabled = false;
                })
                .Build();
            var serverData = new ServerData(id, socketPort, webPort);

            // Set up the new instance server software data files
            var gameDataText = JsonSerializer.Serialize(gameData);
            File.WriteAllText(Path.Combine(instanceDirectory, "game_data.json"), gameDataText);
            var serverDataText = JsonSerializer.Serialize(serverData);
            File.WriteAllText(Path.Combine(instanceDirectory, "server_data.json"), serverDataText);
            var instance = new ServerInstance(gameData, config.CertPath, config.KeyPath, defaultOrigin, socketPort, webPort, config.UseHttps);

            // Start the new instance
            instances.Add(id, instance);
            Task.Run(instance.StartAsync);

            // Notify server of success
            responsePacket.WriteBool(true);
            authServer.SendAsync(responsePacket);
            break;
        }
        // Completely nuke ann instance (irreversable)
        case (byte) AuthPackets.DeleteInstance:
        {
            var requestId = packet.ReadUInt();
            var responsePacket = new WriteablePacket();
            responsePacket.WriteByte((byte) WorkerPackets.InstanceDeleteStatus);
            responsePacket.WriteUInt(requestId);
        
            var instanceId = packet.ReadInt();
            if (!instances.TryGetValue(NextId(), out var instanceInfo))
            {
                responsePacket.WriteBool(false);
                authServer.SendAsync(responsePacket);
                return;
            }
        
            var instanceDirectory = Path.Combine(dataPath, instanceId.ToString());
            if (Directory.Exists(instanceDirectory))
            {
                Directory.Delete(instanceDirectory, true);
            }

            instanceInfo.StopAsync();
            instances.Remove(instanceId);
            responsePacket.WriteBool(true);
            authServer.SendAsync(responsePacket);
            break;
        }
        // Checks if server is currently hosting an instance + status of the instance.
        case (byte)AuthPackets.QueryInstance:
        {
            break;
        }
        // Client is listening to their instance's updates from console, we let auth server proxy them.
        case (byte)AuthPackets.Subscribe:
        {
            var instanceId = packet.ReadInt();
            if (!instances.TryGetValue(instanceId, out var instance))
            {
                return;
            }
    
            instance.SocketServer.Logger = ForwardServerLog;
            instance.WebServer.Logger = ForwardServerLog;
            instance.Logger = ForwardServerLog;
            instance.SocketServer.PlayerConnected += ForwardPlayerConnected;
            instance.SocketServer.PlayerDisconnected += ForwardPlayerDisconnected;
            instance.WebServer.CanvasBackupCreated += ForwardCanvasBackupCreated;
            break;
        }
        // We don't need to send instance updates when nobody is listening in.
        case (byte)AuthPackets.Unsubscribe:
        {
            var instanceId = packet.ReadInt();
            if (!instances.TryGetValue(instanceId, out var instance))
            {
                return;
            }

            instance.SocketServer.Logger = null;
            instance.WebServer.Logger = null;
            instance.Logger = null;
            instance.SocketServer.PlayerConnected -= ForwardPlayerConnected;
            instance.SocketServer.PlayerDisconnected -= ForwardPlayerDisconnected;
            instance.WebServer.CanvasBackupCreated -= ForwardCanvasBackupCreated;
            break;
        }
    }
};

Console.CancelKeyPress += async (_, _) =>
{
    await authServer.StopAsync();
    Environment.Exit(0);
};
AppDomain.CurrentDomain.UnhandledException += (_, exceptionEventArgs) =>
{
    logger.LogError("Unhandled exception: {exceptionObject}", exceptionEventArgs.ExceptionObject);
};

logger.LogInformation("Server started, connecting websockets.");
await authServer.StartAsync();
await Task.Delay(-1);
