using System.Buffers.Binary;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RplaceServer.Exceptions;
using WatsonWebsocket;

namespace RplaceServer;

public class SocketServer
{
    private readonly HttpClient httpClient = new();
    private readonly WatsonWsServer app;
    private readonly GameData gameData;
    
    public SocketServer(GameData data, int socketPort, bool ssl)
    {
        //TODO: Make my own watson fork, that has a mentally sane certificate implementation, and a proper unique way to identify clients.
        app = new WatsonWsServer("localhost", socketPort, ssl);
        gameData = data;
        
        try
        {
            var boardFile = File.ReadAllBytes(Path.Join(gameData.CanvasFolder, "place"));
            if (boardFile.Length == 0) throw new NoCanvasFileFoundException("Could not locate canvas file at", Path.Join(gameData.CanvasFolder, "place"));
            gameData.Board = boardFile;
        }
        catch (Exception exception)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[WARNING]: " + exception.Message);
            Console.ResetColor();

            gameData.Board = new byte[gameData.BoardWidth * gameData.BoardHeight];

            if (!Directory.Exists(gameData.CanvasFolder))
            {
                Directory.CreateDirectory(gameData.CanvasFolder);
                
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[INFO]: Created new canvas folder.");
                Console.ResetColor();
            }
            
            File.WriteAllBytes(Path.Join(gameData.CanvasFolder, "place"), gameData.Board);
        }
        
        //Make a canvas save file just before the program exits.
        AppDomain.CurrentDomain.ProcessExit += (sender, e) => { File.WriteAllBytes(Path.Join(gameData.CanvasFolder, "place"), gameData.Board); };
    }

    public async Task Start()
    {
        app.ClientConnected += ClientConnected;
        app.MessageReceived += MessageReceived;
        app.ClientDisconnected += ClientDisconnected;

        await app.StartAsync();
    }

    public void ClientConnected(object? sender, ClientConnectedEventArgs args)
    {
        var idIpPort = GetIdIpPort(args.IpPort);
        
        if (gameData.UseCloudflare &&
            args.HttpRequest.Headers.Get(Array.IndexOf(args.HttpRequest.Headers.AllKeys, "origin")) !=
            gameData.Origin || gameData.Bans.Contains(args.IpPort) || idIpPort.StartsWith("%"))
        {
            gameData.Bans.Add(idIpPort);
            app.DisconnectClient(idIpPort);
            return;
        }
            
        gameData.Clients.Add(idIpPort, new SocketClient(idIpPort));
        var buffer = new byte[9];
        buffer[0] = 1;
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan()[1..], 1);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan()[5..], gameData.Cooldown);
        app.SendAsync(idIpPort, buffer);
        gameData.Players++;
    }

    public void MessageReceived(object? sender, MessageReceivedEventArgs args)
    {
        var idIpPort = GetIdIpPort(args.IpPort);
        
        switch (args.Data[0])
        {
            case 15:                                                //Static Datetime.Now exists... :skull:
                if (gameData.Clients[idIpPort].LastChat + 2500 > DateTime.Now || args.Data.Count > 400) return;
                gameData.Clients[idIpPort].LastChat = DateTime.Now;
                
                foreach (var ipPort in app.ListClients())
                    app.SendAsync(ipPort, args.Data);
                
                if (string.IsNullOrEmpty(gameData.WebhookUrl) || args.Data.Array is null) return;
                
                var rawMessage = Encoding.UTF8.GetString(args.Data.Array, 1, args.Data.Array.Length - 1).Replace("@", "");
                var text = rawMessage.Split("\n")[0];
                var name = new Regex("/\\W+/g").Replace(rawMessage.Split("\n")[1], "");
                var msgChannel = rawMessage.Split("\n")[2];

                var hook = $"{{'username': [{msgChannel}] {name}@rplace.tk`, 'content': {text}}}";
                httpClient.PostAsJsonAsync(gameData.WebhookUrl + "?wait=true", hook, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                break;
            case 16:
                var buffer = new byte[2];
                buffer[1] = 16;
                buffer[2] = 255;
                app.SendAsync(idIpPort, buffer);
                break;
            case 99:
                break;
            case 20:
                break;
        }
        
        if (args.Data.Array?.Length < 6) return;
        var index = BinaryPrimitives.ReadUInt32BigEndian(args.Data.Array?[1..]);
        var colour = args.Data[5];

        if (index >= gameData.Board.Length || colour >= gameData.PaletteSize) return;
        var cd = gameData.Clients.GetValueOrDefault(idIpPort)?.Cooldown;
        if (cd is null) return;
        
        if (cd > DateTime.Now)
        {
            //reject
            var buffer = new byte[10];
            buffer[0] = 7;
            BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan()[1..], (int) cd);
            BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan()[5..], (int) index);
            buffer[9] = gameData.Board[index];
            app.SendAsync(idIpPort, buffer);
            return;
        }
        
        //accept
        gameData.Board[index] = colour;
        gameData.Clients[idIpPort].Cooldown = DateTime.Now + gameData.Cooldown - 500;
    }

    public virtual void ClientDisconnected(object? sender, ClientDisconnectedEventArgs args)
    {
        var idIpPort = GetIdIpPort(args.IpPort);
        gameData.Players--;
        gameData.Clients.Remove(idIpPort);
    }

    private string GetIdIpPort(string ipPort)
    {
        return ipPort;
        //return gameData.UseCloudflare
        //    ? args.HttpRequest.Headers.Get(Array.IndexOf(args.HttpRequest.Headers.AllKeys, "x-forwarded-for"))
        //    : args.IpPort;
    }
}