using System.Buffers.Binary;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RplaceServer.Enums;
using RplaceServer.Events;
using RplaceServer.Exceptions;
using RplaceServer.Types;
using WatsonWebsocket;

namespace RplaceServer;

internal sealed class SocketServer
{
    private readonly JsonSerializerOptions defaultJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly HttpClient httpClient = new();
    private readonly WatsonWsServer app;
    private readonly GameData gameData;
    private readonly string origin;

    public event EventHandler<ChatMessageEventArgs> ChatMessageReceived;
    public event EventHandler<PixelPlacementEventArgs> PixelPlacementReceived;
    public event EventHandler<PlayerConnectedEventArgs> PlayerConnected;
    public event EventHandler<PlayerDisconnectedEventArgs> PlayerDisconnected;

    public SocketServer(GameData data, string certPath, string keyPath, string origin, bool ssl, int port)
    {
        //TODO: Make my own watson fork, that has a mentally sane certificate implementation, and a proper unique way to identify clients.
        app = new WatsonWsServer(port, "localhost");
        gameData = data;
        this.origin = origin;
        
        ChatMessageReceived = DistributeChatMessage;
        PixelPlacementReceived = DistributePixelPlacement;
        PlayerConnected = (_, _) => { };
        PlayerDisconnected = (_, _) => { };

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

    private void ClientConnected(object? sender, ClientConnectedEventArgs args)
    {
        var idIpPort = GetIdIpPort(args.Client.IpPort);

        // Reject
        if (args.HttpRequest.Cookies["origin" ] != origin || gameData.Bans.Contains(args.Client.IpPort))
        {
            Console.WriteLine($"Client {args.Client.IpPort} disconnected for violating ban or initial headers checks");
            app.DisconnectClient(args.Client);
            return;
        }
        
        // Reject
        // CF clearance cookie is made per device, per browser, as only 1 WS connection per browser/user/device is permitted,
        // to prevent bots, we disconnect the last client with the same cookie, only allowing current to be connected to server
        if (gameData.UseCloudflare)
        {
            var clearance = args.HttpRequest.Cookies["cf_clearance"];

            if (clearance is null)
            {
                Console.WriteLine($"Client {args.Client.IpPort} disconnected for null cloudflare clearance cookie");
                app.DisconnectClient(args.Client);
                return;
            }
            
            foreach (var metadata in gameData.Clients.Keys
                .Where(metadata => metadata.HttpContext.Request.Cookies["cf_clearance"] == clearance))
            {
                Console.WriteLine($"Client {args.Client.IpPort} disconnected for new connection from the same clearance cookie");
                app.DisconnectClient(metadata);
            }
        }
        
        // Accept
        // Create player client instance
        var playerSocketClient = new SocketClient(idIpPort, DateTimeOffset.Now);
        gameData.Clients.Add(args.Client, playerSocketClient);
        gameData.PlayerCount++;
        
        PlayerConnected.Invoke(this, new PlayerConnectedEventArgs(playerSocketClient));
    }
    
    private void MessageReceived(object? sender, MessageReceivedEventArgs args)
    {
        if (args.Data.Array is null)
        {
            Console.WriteLine($"Received null message Data.Array from {args.Client}");
            return;
        }
        
        switch ((ClientPacket) args.Data[0])
        {
            case ClientPacket.ChatMessage:
            {
                // Reject
                if (gameData.Clients[args.Client].LastChat.AddMilliseconds(2500) > DateTimeOffset.Now || args.Data.Count > 400) return;
                gameData.Clients[args.Client].LastChat = DateTimeOffset.Now;

                var rawMessage = Encoding.UTF8.GetString(args.Data.Array, 1, args.Data.Array.Length - 1);
                var text = rawMessage.Split("\n").ElementAtOrDefault(0);
                var name = new Regex("/\\W+/g").Replace(rawMessage.Split("\n").ElementAtOrDefault(1) ?? "anon", "");
                var msgChannel = rawMessage.Split("\n").ElementAtOrDefault(2);
                
                // Reject
                if (text is null || msgChannel is null) return;

                var type = rawMessage.Split("\n").ElementAtOrDefault(3) switch
                {
                    "live" => ChatMessageType.LiveChat,
                    "place" => ChatMessageType.PlaceChat,
                    _ => ChatMessageType.LiveChat
                };
                
                var x = rawMessage.Split("\n").ElementAtOrDefault(4);
                var y = rawMessage.Split("\n").ElementAtOrDefault(5);
                
                // Send player cooldown + other data
                var buffer = new byte[9];
                buffer[0] = (byte) ServerPacket.InitialInfo;
                BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan()[1..], 1);
                BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan()[5..], (uint) gameData.Cooldown * 1000);
                app.SendAsync(args.Client, buffer);
        
                // Send player palette data (if using a custom palette)
                if (gameData.Palette is not null)
                {
                    var palette = gameData.Palette.Select(Convert.ToUInt32).ToArray();
                    var paletteBuffer = new byte[1 + palette.Length * 4];
                    paletteBuffer[0] = 0;
                    Buffer.BlockCopy(palette, 0, paletteBuffer, 1, palette.Length * 4);
                    app.SendAsync(args.Client, paletteBuffer);
                }

                // Accept
                ChatMessageReceived.Invoke
                (
                    this, 
                    new ChatMessageEventArgs(gameData.Clients[args.Client],text, msgChannel, name, type, args.Data.Array!, x is not null ? int.Parse(x) : null, y is not null ? int.Parse(y) : null)
                );
                
                break;
            }
            case ClientPacket.CaptchaSubmit:
            {
                var buffer = new byte[2];
                buffer[1] = (byte) ServerPacket.CaptchaSuccess;
                buffer[2] = 255;
                app.SendAsync(args.Client, buffer);
                break;
            }
            /*case 99: break; case 20: break;*/
            default: // Pixel placement
            {
                //Reject
                if (args.Data.Array.Length < 6)
                {
                    return;
                }
                
                var index = BinaryPrimitives.ReadUInt32BigEndian(args.Data.Array?[1..]);
                var colour = args.Data[5];
                    
                // Reject
                if (index >= gameData.Board.Length || colour >= (gameData.Palette?.Count ?? 31)) return;
                var cd = gameData.Clients[args.Client].Cooldown;
        
                if (cd > DateTimeOffset.Now)
                {
                    // Reject
                    var buffer = new byte[10];
                    buffer[0] = (byte) ServerPacket.RejectPixel;
                    BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan()[1..], (int) cd.ToUnixTimeMilliseconds());
                    BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan()[5..], (int) index);
                    buffer[9] = gameData.Board[index];
                    app.SendAsync(args.Client, buffer);
                    return;
                }
        
                // Accept
                PixelPlacementReceived.Invoke
                (
                    this,
                    new PixelPlacementEventArgs(colour, (int) (index % gameData.BoardWidth), (int) index / gameData.BoardHeight, (int) index, args.Client, args.Data.Array!)
                );
                break;
            }
        }
    }

    private void ClientDisconnected(object? sender, ClientDisconnectedEventArgs args)
    {
        gameData.Clients.Remove(args.Client);
        gameData.PlayerCount--;
        
        PlayerDisconnected.Invoke(this, new PlayerDisconnectedEventArgs(gameData.Clients[args.Client]));
    }

    /// <summary>
    /// Internal event handler to distribute a chat message to all other clients, that can be inhibited
    /// </summary>
    private void DistributeChatMessage(object? sender, ChatMessageEventArgs args)
    {
        foreach (var client in app.Clients)
        {
            app.SendAsync(client, args.Message);
        }

        if (string.IsNullOrEmpty(gameData.WebhookUrl)) return;
        var hookBody = new WebhookBody($"[{args.Channel}] {args.Name}@rplace.tk", args.Message);
        httpClient.PostAsJsonAsync(gameData.WebhookUrl + "?wait=true", hookBody, defaultJsonOptions);
    }

    /// <summary>
    /// Internal event handler to distribute a pixel placement to all other clients, that can be inhibited
    /// </summary>
    private void DistributePixelPlacement(object? sender, PixelPlacementEventArgs args)
    {
        gameData.Board[args.Index] = (byte) args.Colour;
        gameData.Clients[args.Player].Cooldown = DateTimeOffset.Now.AddSeconds(gameData.Cooldown);
        
        foreach (var client in app.Clients)
        {
            app.SendAsync(client, args.Packet);
        }
    }

    /// <summary>
    /// Increases the size of a canvas/board, by a given width and height.
    /// </summary>
    /// <param name="widthIncrease">The increase in pixels on the X axis.</param>
    /// <param name="heightIncrease">The increase in pixels on the Y axis.</param>
    public void ExpandCanvas(int widthIncrease, int heightIncrease)
    {
        var newHeight = gameData.BoardHeight + heightIncrease;
        var newWidth = gameData.BoardWidth + widthIncrease;
        //Array.Copy(array, offset, result, 0, length);
        var newBoard = new byte[newHeight * newWidth];
        for (var y = 0; y < gameData.BoardHeight; y++)
        {
            newBoard[y * newWidth] = gameData.Board[y * gameData.BoardWidth];
        }

        gameData.Board = newBoard;
        gameData.BoardHeight = newHeight;
        gameData.BoardWidth = newWidth;
    }

    /// <summary>
    /// Sends a message inside of the game live chat to a specific client, or all connected clients.
    /// </summary>
    /// <param name="message">The message being sent.</param>
    /// <param name="channel">The channel that the message will be broadcast to.</param>
    /// <param name="client">The player that this chat message will be sent to, if no client provided, then it is sent to all</param>
    public void BroadcastChatMessage(string message, string channel, ClientMetadata? client = null)
    {
        var messageBytes = Encoding.UTF8.GetBytes($"\x0f{message}\nserver\n{channel}");
        messageBytes[0] = (byte) ServerPacket.ChatMessage;

        if (client is null)
        {
            foreach (var c in app.Clients)
            {
                app.SendAsync(c, messageBytes);
            }
            
            return;
        }

        app.SendAsync(client, messageBytes);
    }

    /// <summary>
    /// Sets an area of the canvas to a specific colour.
    /// </summary>
    /// <param name="startX"></param>
    /// <param name="startY"></param>
    /// <param name="endX"></param>
    /// <param name="endY"></param>
    /// <param name="colour"></param>
    public void Fill(int startX, int startY, int endX, int endY, byte colour = 27)
    {
        while (startY < endY && startX < endX)
        {
            gameData.Board[startX++ + startY++ * gameData.BoardWidth] = colour;
        }
    }

    public void BanPlayer(SocketClient player)
    {
        throw new NotImplementedException();
    }

    public void KickPlayer(SocketClient player)
    {
        throw new NotImplementedException();
    }
    
    private string GetIdIpPort(string ipPort)
    {
        return ipPort;
        //return gameData.UseCloudflare
        //    ? args.HttpRequest.Headers.Get(Array.IndexOf(args.HttpRequest.Headers.AllKeys, "x-forwarded-for"))
        //    : args.IpPort;
    }
}
