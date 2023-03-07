using System.Buffers.Binary;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RplaceServer.CaptchaGeneration;
using RplaceServer.Enums;
using RplaceServer.Events;
using RplaceServer.Types;
using WatsonWebsocket;

namespace RplaceServer;

public sealed class SocketServer
{
    private readonly HttpClient httpClient = new();
    private readonly WatsonWsServer app;
    private readonly GameData gameData;
    private readonly string origin;
    private readonly JsonSerializerOptions defaultJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    public Action<string>? Logger;
    public event EventHandler<ChatMessageEventArgs> ChatMessageReceived;
    public event EventHandler<PixelPlacementEventArgs> PixelPlacementReceived;
    public event EventHandler<PlayerConnectedEventArgs> PlayerConnected;
    public event EventHandler<PlayerDisconnectedEventArgs> PlayerDisconnected;

    public SocketServer(GameData data, string certPath, string keyPath, string origin, bool ssl, int port)
    {
        app = new WatsonWsServer(port, ssl, certPath, keyPath, LogLevel.None, "localhost");
        gameData = data;
        this.origin = origin;
        
        ChatMessageReceived += DistributeChatMessage;
        PixelPlacementReceived += DistributePixelPlacement;
        PlayerConnected = (_, _) => { };
        PlayerDisconnected = (_, _) => { };
        
        gameData.PlayerCount = 0;
        gameData.PendingCaptchas = new Dictionary<string, string>();
        gameData.Clients = new Dictionary<ClientMetadata, ClientData>();
    }

    public async Task StartAsync()
    {
        app.ClientConnected += ClientConnected;
        app.MessageReceived += MessageReceived;
        app.ClientDisconnected += ClientDisconnected;

        await app.StartAsync();
    }
    
    private void ClientConnected(object? sender, ClientConnectedEventArgs args)
    {
        var address = args.Client.IpPort.Split(":")[0];

        // Reject
        if ((!string.IsNullOrEmpty(origin) && args.HttpRequest.Cookies["origin" ] != origin) || gameData.Bans.Contains(address))
        {
            Logger?.Invoke($"Client {args.Client.IpPort} disconnected for violating ban or initial headers checks");
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
                Logger?.Invoke($"Client {args.Client.IpPort} disconnected for null cloudflare clearance cookie");
                app.DisconnectClient(args.Client);
                return;
            }
            
            foreach (var metadata in gameData.Clients.Keys
                .Where(metadata => metadata.HttpContext.Request.Cookies["cf_clearance"] == clearance))
            {
                Logger?.Invoke($"Client {args.Client.IpPort} disconnected for new connection from the same clearance cookie");
                app.DisconnectClient(metadata);
            }
        }
        
        // Accept
        // Create player client instance
        var playerSocketClient = new ClientData(args.Client.IpPort, DateTimeOffset.Now);
        gameData.Clients.Add(args.Client, playerSocketClient);
        gameData.PlayerCount++;
        
        if (gameData.CaptchaEnabled)
        {
            var result = CaptchaGenerator.Generate(CaptchaType.Emoji);
            if (gameData.PendingCaptchas.ContainsKey(address))
            {
                gameData.PendingCaptchas[address] = result.Answer;
            }
            else
            {
                gameData.PendingCaptchas.Add(address, result.Answer);
            }

            var captchaBuffer = new byte[2 + result.ImageData.Length];
            captchaBuffer[0] = (byte) ServerPacket.Captcha;
            captchaBuffer[1] = (byte) CaptchaType.Emoji;
            Encoding.UTF8.GetBytes(result.Dummies!).CopyTo(captchaBuffer, 2); // Length = 10
            result.ImageData.CopyTo(captchaBuffer, 13);
            app.SendAsync(args.Client, captchaBuffer);
        }

        // Send player palette data (if using a custom palette)
        if (gameData.Palette is not null)
        {
            var palette = gameData.Palette.ToArray();
            var paletteBuffer = new byte[1 + palette.Length * 4];
            paletteBuffer[0] = (byte) ServerPacket.Palette;
            Buffer.BlockCopy(palette, 0, paletteBuffer, 1, palette.Length * 4);
            app.SendAsync(args.Client, paletteBuffer);
        }
        
        // Send player cooldown + other data
        var canvasInfo = (Span<byte>) stackalloc byte[17];
        canvasInfo[0] = (byte) ServerPacket.CanvasInfo;
        // TODO: Previous cooldown that they may have had before disconnect
        BinaryPrimitives.WriteUInt32BigEndian(canvasInfo[1..], 1);
        BinaryPrimitives.WriteUInt32BigEndian(canvasInfo[5..], (uint) gameData.Cooldown);
        BinaryPrimitives.WriteUInt32BigEndian(canvasInfo[9..], (uint) gameData.BoardWidth);
        BinaryPrimitives.WriteUInt32BigEndian(canvasInfo[13..], (uint) gameData.BoardHeight);
        app.SendAsync(args.Client, canvasInfo.ToArray());

        DistributePlayerCount();
        PlayerConnected.Invoke(this, new PlayerConnectedEventArgs(args.Client));
    }
    
    private void MessageReceived(object? sender, MessageReceivedEventArgs args)
    {
        var address = args.Client.IpPort.Split(":")[0];
        var data = new Span<byte>(args.Data.ToArray());
        
        switch ((ClientPacket) args.Data[0])
        {
            case ClientPacket.PixelPlace:
            {
                //Reject
                if (data.Length < 6)
                {
                    Logger?.Invoke($"Pixel from client {args.Client.IpPort} rejected for invalid packet length ({data.Length})");
                    return;
                }

                var index = BinaryPrimitives.ReadUInt32BigEndian(data[1..]);
                var colour = args.Data[5];

                // Reject
                if (index >= gameData.Board.Length || colour >= (gameData.Palette?.Count ?? 32))
                {
                    Logger?.Invoke($"Pixel from client {args.Client.IpPort} rejected for exceeding canvas size or palette ({index}, {colour})");
                    return;
                }
                var clientCooldown = gameData.Clients[args.Client].Cooldown;

                if (clientCooldown > DateTimeOffset.Now)
                {
                    // Reject
                    Logger?.Invoke($"Pixel from client {args.Client.IpPort} rejected for breaching cooldown ({clientCooldown})");
                    var buffer = (Span<byte>) stackalloc byte[10];
                    buffer[0] = (byte) ServerPacket.RejectPixel;
                    BinaryPrimitives.WriteInt32BigEndian(buffer[1..], (int) clientCooldown.ToUnixTimeMilliseconds());
                    BinaryPrimitives.WriteInt32BigEndian(buffer[5..], (int) index);
                    buffer[9] = gameData.Board[index];
                    app.SendAsync(args.Client, buffer.ToArray());
                    return;
                }

                // Accept
                PixelPlacementReceived.Invoke
                (
                    this,
                    new PixelPlacementEventArgs(colour, (int) (index % gameData.BoardWidth),
                        (int) index / gameData.BoardHeight, (int) index, args.Client, data.ToArray())
                );

                break;
            }
            case ClientPacket.ChatMessage:
            {
                // Reject
                if (gameData.Clients[args.Client].LastChat.AddMilliseconds(gameData.ChatCooldown) > DateTimeOffset.Now || args.Data.Count > 400)
                {
                    Logger?.Invoke($"Chat from client {args.Client.IpPort} rejected for breaching length/cooldown rules");
                    return;
                }

                gameData.Clients[args.Client].LastChat = DateTimeOffset.Now;

                var rawMessage = Encoding.UTF8.GetString(data.ToArray(), 1, data.Length - 1);
                var text = rawMessage.Split("\n").ElementAtOrDefault(0);
                var name = rawMessage.Split("\n").ElementAtOrDefault(1) ?? "anon";
                var msgChannel = rawMessage.Split("\n").ElementAtOrDefault(2);

                // Reject
                if (text is null || msgChannel is null)
                {
                    return;
                }

                var type = rawMessage.Split("\n").ElementAtOrDefault(3) switch
                {
                    "live" => ChatMessageType.LiveChat,
                    "place" => ChatMessageType.PlaceChat,
                    _ => ChatMessageType.LiveChat
                };

                var x = rawMessage.Split("\n").ElementAtOrDefault(4);
                var y = rawMessage.Split("\n").ElementAtOrDefault(5);
 
                // Accept
                ChatMessageReceived.Invoke
                (
                    this, 
                    new ChatMessageEventArgs(args.Client,text, msgChannel, name, type, 
                    data.ToArray(), x is not null ? int.Parse(x) : null, y is not null ? int.Parse(y) : null)
                );
                break;
            }
            case ClientPacket.CaptchaSubmit:
            {
                var response = Encoding.UTF8.GetString(data[1..]);
                
                if (gameData.PendingCaptchas.TryGetValue(address, out var answer) || !response.Equals(answer))
                {
                    Logger?.Invoke($"Client {args.Client.IpPort} disconnected for invalid captcha response");
                    app.DisconnectClient(args.Client);
                    return;
                }
                
                // Accept
                var buffer = new byte[2];
                buffer[1] = (byte) ServerPacket.Captcha;
                buffer[2] = (byte) CaptchaType.Success;
                app.SendAsync(args.Client, buffer);
                break;
            }
        }
    }

    private void ClientDisconnected(object? sender, ClientDisconnectedEventArgs args)
    {
        gameData.Clients.Remove(args.Client);
        gameData.PlayerCount--;

        DistributePlayerCount();
        PlayerDisconnected.Invoke(this, new PlayerDisconnectedEventArgs(args.Client));
    }

    /// <summary>
    /// Method to send a player count update packet to all connected clients
    /// </summary>
    private void DistributePlayerCount()
    {
        var gameInfo = (Span<byte>) stackalloc byte[5];
        gameInfo[0] = (byte) ServerPacket.PlayerCount;
        BinaryPrimitives.TryWriteUInt16BigEndian(gameInfo[1..], (ushort) gameData.PlayerCount);

        foreach (var client in app.Clients)
        {
            app.SendAsync(client, gameInfo.ToArray());
        }
    }
    
    /// <summary>
    /// Internal event handler to distribute a pixel placement to all other clients, that can be inhibited
    /// </summary>
    private void DistributePixelPlacement(object? sender, PixelPlacementEventArgs args)
    {
        gameData.Board[args.Index] = (byte) args.Colour;
        gameData.Clients[args.Player].Cooldown = DateTimeOffset.Now.AddMilliseconds(gameData.Cooldown);

        var serverPixel = args.Packet;
        serverPixel[0] = (byte) ServerPacket.PixelPlace;

        foreach (var client in app.Clients)
        {
            app.SendAsync(client, serverPixel);
        }
    }

    /// <summary>
    /// Internal event handler to distribute a chat message to all other clients, that can be inhibited
    /// </summary>
    private void DistributeChatMessage(object? sender, ChatMessageEventArgs args)
    {
        foreach (var client in app.Clients)
        {
            app.SendAsync(client, args.Packet);
        }

        if (string.IsNullOrEmpty(gameData.WebhookUrl)) return;
        var hookBody = new WebhookBody($"[{args.Channel}] {args.Name}@rplace.tk", args.Message);
        httpClient.PostAsJsonAsync(gameData.WebhookUrl + "?wait=true", hookBody, defaultJsonOptions);
    }
    
    /// <summary>
    /// Increases the size of a canvas/board, by a given width and height.
    /// </summary>
    /// <param name="widthIncrease">The increase in pixels on the X axis.</param>
    /// <param name="heightIncrease">The increase in pixels on the Y axis.</param>
    /// <param name="expandColour">The colour which the new expanded area of the board will be filled with.</param>
    /// <returns>The new width and new height of the board after it has been increased.</returns>
    public (int NewWidth, int NewHeight) ExpandCanvas(int widthIncrease, int heightIncrease, int expandColour)
    {
        var newHeight = gameData.BoardHeight + heightIncrease;
        var newWidth = gameData.BoardWidth + widthIncrease;

        var newBoard = new byte[newHeight * newWidth];
        for (var y = 0; y < gameData.BoardHeight; y++)
        {
            newBoard[y * newWidth] = gameData.Board[y * gameData.BoardWidth];
        }

        gameData.Board = newBoard;
        gameData.BoardHeight = newHeight;
        gameData.BoardWidth = newWidth;

        return (newWidth, newHeight);
    }

    /// <summary>
    /// Sends a message inside of the game live chat to a specific client, or all connected clients.
    /// </summary>
    /// <param name="message">The message being sent.</param>
    /// <param name="channel">The channel that the message will be broadcast to.</param>
    /// <param name="client">The player that this chat message will be sent to, if no client provided, then it is sent to all.</param>
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
    /// <param name="startX">Starting X coordinate of pixel fill.</param>
    /// <param name="startY">Starting Y coordinate of pixel fill.</param>
    /// <param name="endX">Ending X coordinate of pixel fill.</param>
    /// <param name="endY">Ending Y coordinate of pixel fill.</param>
    /// <param name="colour">The integer colour code that we want to set.</param>
    /// <returns>Area in pixels filled by this method.</returns>
    public int Fill(int startX, int startY, int endX, int endY, byte colour = 27)
    {
        while (startY < endY && startX < endX)
        {
            gameData.Board[startX++ + startY++ * gameData.BoardWidth] = colour;
        }

        return (endX - startX) * (endY - startY);
    }
    
    /// <summary>
    /// Bans a player from the current server instance, kicking them and preventing them from reconnecting for the duration
    /// of this instance running (is not persistent between server restarts unless implemented by a server software).
    /// </summary>
    /// <param name="client">The client who is to be banned from reconnecting to the game</param>
    public void BanPlayer(ClientMetadata client)
    {
        var address = client.IpPort.Split(":")[0];
        gameData.Bans.Add(address);
        app.DisconnectClient(client);
    }

    public void KickPlayer(ClientMetadata client)
    {
        app.DisconnectClient(client);
    }

    public async Task StopAsync()
    {
        foreach (var client in app.Clients)
        {
            app.DisconnectClient(client);
        }
        
        await app.StopAsync();
    }
}
