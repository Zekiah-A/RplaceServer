using System.Runtime.InteropServices.ComTypes;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using RplaceServer;
using WorkerOfficial;

const string configPath = "server_config.json";
const string dataPath = "ServerData";
var dataFilePath = Path.Join(dataPath, "server_data.json");

async Task CreateConfig()
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write("[Warning]: Could not find game config file, at " + configPath);

    await using var configFile = File.OpenWrite(configPath);
    var defaultConfiguration =
        new Configuration(8080,
            false,
            "",
            "",
            new IntRange(0, 100),
            new IntRange(3000, 4000),
            new IntRange(4000, 5000),
            "secretInstanceControlKeyGoesHere");
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
    Console.Write("[Warning]: Could not find data path, at " + dataPath);
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
    1000, 1000, 600000,  false, "Canvases", "Posts",
    60, 300000, true, 100, "", new List<uint>());
var config = await JsonSerializer.DeserializeAsync<Configuration>(File.OpenRead(configPath));
var workerData = await JsonSerializer.DeserializeAsync<WorkerData>(File.OpenRead(dataFilePath));
var instances = new Dictionary<int, ServerInstance>();
var builder = WebApplication.CreateBuilder();
builder.Configuration["Kestrel:Certificates:Default:Path"] = config.CertPath;
builder.Configuration["Kestrel:Certificates:Default:KeyPath"] = config.KeyPath;
var app = builder.Build();
app.Urls.Add($"{(config.UseHttps ? "https" : "http")}://*:{config.Port}");

async Task<int> NextId()
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
    await JsonSerializer.SerializeAsync(File.OpenWrite(dataFilePath!), workerData);
    return next;
}

async Task<int> NextSocketPort()
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
    await JsonSerializer.SerializeAsync(File.OpenWrite(dataFilePath), workerData);
    return next;
}

async Task<int> NextWebPort()
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
    await JsonSerializer.SerializeAsync(File.OpenWrite(dataFilePath), workerData);
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
        Console.WriteLine("Could not find gamedata for server with if {id}, will attempt to use deafult");
        gameData = defaultGameData;
    }
    
    instances.Add(id, new ServerInstance(gameData, config.KeyPath, config.CertPath, "", await NextSocketPort(), await NextWebPort(), config.UseHttps));
    await JsonSerializer.SerializeAsync(File.OpenWrite(dataFilePath), workerData);
}

app.MapGet("/CreateInstance", async (context) =>
{
    if (!context.Request.Headers.TryGetValue("Authentication-Key", out var authKey) || authKey != config.InstanceKey)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }

    var id = await NextId();
    var socketPort = await NextSocketPort();
    var webPort = await NextWebPort();
    if (id == -1 || socketPort == -1 || webPort == -1)
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return;
    }
    
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
    await context.Response.WriteAsync(id.ToString());
    context.Response.StatusCode = StatusCodes.Status200OK;
});

app.MapGet("/DeleteInstance/{instanceId:int}", async (int instanceId, HttpContext context) =>
{
    if (!context.Request.Headers.TryGetValue("Authentication-Key", out var authKey) || authKey != config.InstanceKey)
    {
        return Results.Unauthorized();
    }

    if (instances.TryGetValue(instanceId, out var instance))
    {
        await instance.StopAsync();
        instances.Remove(instanceId);
    }

    // TODO: Find a fix for the port leakage which will occur due to ports not being released.
    workerData.Ids.Remove(instanceId);
    await JsonSerializer.SerializeAsync(File.OpenWrite(dataFilePath), workerData);
    return Results.Ok();
});

app.MapGet("/RestartInstance/{instanceId:int}", async (int instanceId, HttpContext context) =>
{
    if (!context.Request.Headers.TryGetValue("Authentication-Key", out var authKey) || authKey != config.InstanceKey)
    {
        return Results.Unauthorized();
    }

    if (!instances.TryGetValue(instanceId, out var instance))
    {
        return Results.Problem();
    }
    
    await instance.StopAsync();
    instances.Remove(instanceId);
    // TODO: Find a fix for the port leakage that will occur when we assign a new port.
    var newInstance = new ServerInstance(instance.GameData, config.CertPath, config.KeyPath, "",
        await NextSocketPort(), await NextWebPort(), config.UseHttps);
    instances.Add(instanceId, newInstance);
    return Results.Ok();
});

await app.StartAsync();
await Task.Delay(-1);