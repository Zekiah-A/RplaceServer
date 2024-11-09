using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RplaceServer.DataModel;
using RplaceServer.Events;
using RplaceServer.Types;
using WatsonWebsocket;
using ZCaptcha;

namespace RplaceServer;

public sealed partial class SocketServer
{
    private readonly string dbPath;
    private readonly DatabaseContext database;
    private readonly HttpClient httpClient;
    private readonly WatsonWsServer app;
    private readonly ServerInstance instance;
    private readonly GameData gameData;
    private readonly string origins;

    public Action<string>? Logger;
    public ICaptchaGenerator? CaptchaGenerator;
    public event EventHandler<ChatMessageEventArgs>? ChatMessageReceived;
    public event EventHandler<PixelPlacementEventArgs>? PixelPlacementReceived;
    public event EventHandler<PlayerConnectedEventArgs>? PlayerConnected;
    public event EventHandler<PlayerDisconnectedEventArgs>? PlayerDisconnected;
    
    [GeneratedRegex(@"(?:https?:\/\/)([\da-z.-]+)\.([a-z.]{2,6})([/\w .-]*)(\/?[^\s]*)")]
    private static partial Regex DomainRegex();
    
    [GeneratedRegex(@"\W+")]
    private static partial Regex PlayerNameRegex();

    public SocketServer(ServerInstance parentInstance, GameData data, string? certPath, string? keyPath, string originsHeader, bool ssl, int port)
    {
        instance = parentInstance;
        gameData = data;
        app = new WatsonWsServer(port, ssl, certPath, keyPath, LogLevel.None, "localhost");
        origins = originsHeader;
        httpClient = new HttpClient();

        // Create and initialise database
        dbPath = Path.Combine(gameData.SaveDataFolder, "server.db");
        database = new DatabaseContext(dbPath);
    }
    
    public async Task StartAsync()
    {
        if (!await database.Database.CanConnectAsync())
        {
            Logger?.Invoke($"Couldn't find server database at {dbPath} creating...");
            await database.Database.MigrateAsync();
            Logger?.Invoke("Server database created successfully");
        }
        else
        {
            var pendingMigrationsAsync = await database.Database.GetPendingMigrationsAsync();
            var pendingMigrations = pendingMigrationsAsync as string[] ?? pendingMigrationsAsync.ToArray();
            if (pendingMigrations.Length != 0)
            {
                Logger?.Invoke($"Server database is outdated, applying {pendingMigrations.Length} pending migrations...");
                await database.Database.MigrateAsync();
                Logger?.Invoke("Server database updated successfully");
            }
        }

        var captchaResources = Path.Combine(gameData.StaticResourcesFolder, "CaptchaGeneration");
        if (!Directory.Exists(captchaResources))
        {
            Logger?.Invoke($"Could not find 'static resources' captcha generation files at {captchaResources}.");
            throw new FileNotFoundException(captchaResources);
        }
        CaptchaGenerator = new EmojiCaptchaGenerator(Path.Combine(gameData.StaticResourcesFolder, "CaptchaGeneration", "NotoColorEmoji-Regular.ttf"));

        var blacklistPath = Path.Combine(gameData.SaveDataFolder, "bans.txt");
        if (File.Exists(blacklistPath))
        {
            var blacklistText = await File.ReadAllLinesAsync(blacklistPath);
            await FileUtils.ReadUrlSheet(httpClient, blacklistText, instance.IpBlacklist);
        }
        else
        {
            Logger?.Invoke($"Could not find IP blacklist file at {Path.Combine(gameData.SaveDataFolder, "bans.txt")}");
        }
        
        var vipPath = Path.Combine(gameData.SaveDataFolder, "vip.txt");
        if (File.Exists(vipPath))
        {
            // TODO: Parse as list file, filtering out comments and empty lines
            var vipText = await File.ReadAllLinesAsync(vipPath);
            FileUtils.ReadListFile(vipText, instance.VipKeys);
        }
        else
        {
            Logger?.Invoke($"Could not find game VIPs file. Generating new VIP key file at '{vipPath}'");
            await File.WriteAllTextAsync(vipPath, """
                # VIP Key configuration file
                # Below is the correct format of a VIP key configuration:
                # MY_SHA256_HASHED_VIP_KEY { "perms": "canvasmod"|"chatmod"|"admin","vip", "cooldownMs": number, "enforceChatName": string|null }
                
                # Example VIP key configuration:
                # 7eb65b1afd96609903c54851eb71fbdfb0e3bb2889b808ef62659ed5faf09963 { "perms": "admin", "cooldownMs": 30, "enforceChatName": "<ADMIN> zekiah" }
                # Make sure all VIP keys stored here are sha256 hashes of the real keys you hand out
                """);
        }
        
        app.ClientConnected += ClientConnected;
        app.MessageReceived += MessageReceived;
        app.ClientDisconnected += ClientDisconnected;

        await app.StartAsync();
    }
    
    private void ClientConnected(object? sender, ClientConnectedEventArgs args)
    {
        // We make changes to the IP in case there is a reverse proxy acting and blocking us from accessing the true IP
        // of the client. This is only done for proxies running on localhost, as any other may be untrustworthy, and be
        // faking the information that they send to us.
        var realIpPort = args.Client.IpPort;
        var realIp = realIpPort.Split(":").FirstOrDefault() ?? realIpPort;
        
        // Resolve the real IPs from reverse-proxied addresses
        if ((args.Client.IpPort.StartsWith("::1") || args.Client.IpPort.StartsWith("localhost") ||
             args.Client.IpPort.StartsWith("127.0.0.1")) && args.HttpRequest.Headers.TryGetValue("X-Forwarded-For", out var forwardedForHeader))
        {
            var addresses = forwardedForHeader.ToString().Split(",", StringSplitOptions.RemoveEmptyEntries);
            realIpPort = addresses.FirstOrDefault() ?? args.Client.IpPort;
        }
        
        // Carry out ban check, if their ban time is expired we allow connect
        if (instance.IpBlacklist.Contains(realIp))
        {
            Logger?.Invoke($"Client {realIpPort} disconnected for violating initial headers checks.");
            _ = app.DisconnectClientAsync(args.Client, "Initial connection checks fail");
            return;
        }
        
        // Reject
        if (!string.IsNullOrEmpty(origins) && origins != "*" && 
            !args.HttpRequest.Headers.Origin.Any(origin => origin != null && origins.Contains(origin)))
        {
            Logger?.Invoke($"Client {realIpPort} disconnected for violating initial headers checks.");
            _ = app.DisconnectClientAsync(args.Client, "Initial connection checks fail");
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
                Logger?.Invoke($"Client {realIpPort} disconnected for null cloudflare clearance cookie");
                _ = app.DisconnectClientAsync(args.Client, "Invalid cloudflare clearance cookie");
                return;
            }
            
            foreach (var metadata in instance.Clients.Keys
                .Where(metadata => metadata.HttpContext.Request.Cookies["cf_clearance"] == clearance))
            {
                Logger?.Invoke($"Client {realIpPort} disconnected for new connection from the same clearance cookie");
                _ = app.DisconnectClientAsync(metadata, "Already connected with given clearance cookie");
                return;
            }
        }
        
        // Try resolve their user, otherwise we allocate client a new user
        string? token;
        if (args.HttpRequest.Query.TryGetValue("token", out var queryToken))
        {
            token = queryToken.ToString();
        }
        else if (!args.HttpRequest.Cookies.TryGetValue("token", out var cookieToken))
        {
            token = cookieToken;
        }
        else
        {
            token = args.HttpRequest.Headers.Authorization;
        }
        var user = database.Users.SingleOrDefault(u => u.Token == token);
        if (user is null || string.IsNullOrEmpty(token))
        {
            token = RandomNumberGenerator.GetHexString(64, true);
            user = new User
            {
                AccountId = null,
                ChatName = null,
                PixelsPlaced = 0,
                PlayTimeSeconds = 0,
                Token = token
            };
            database.Users.Add(user);
        }
        
        // Create user session entry
        var session = new Session()
        {
            UserId = user.Id,
            StartDate = DateTime.Now,
            FinishDate = default,
            Ip = realIp,
            UserAgent = args.HttpRequest.Headers.UserAgent.ToString()
        };
        database.Sessions.Add(session);
        
        // Append UidToken cookie to response
        args.Client.HttpContext.Response.Cookies.Append("token", token, new CookieOptions
        {
            Domain = args.HttpRequest.Host.ToString(),
            Expires = DateTimeOffset.MaxValue,
            HttpOnly = true, // Inaccessible from JS
            SameSite = SameSiteMode.None,
            Secure = true,
            Path = "/"
        });
        
        // Accept - Create a player instance
        var playerSocketClient = new ClientData(realIpPort, user.Id, session.Id, DateTimeOffset.Now);
        var uriKey = args.HttpRequest.Path.ToUriComponent().Split("/").FirstOrDefault();
        if (!string.IsNullOrEmpty(uriKey))
        {
            var codeHash = HashSha256String(uriKey[1..]);
            if (!instance.VipKeys.Contains(codeHash))
            {
                Logger?.Invoke($"Client with IP {GetRealIp(args.Client)} attempted to use an invalid VIP key {codeHash}");
                _ = app.DisconnectClientAsync(args.Client, "Invalid VIP key. Do not try again");
                return;
            }
            if (uriKey[0] == '!')
            {
                playerSocketClient.Permissions = Permissions.Admin;
            }
            else
            {
                playerSocketClient.Permissions = Permissions.Vip;
            }
        }
        instance.Clients.Add(args.Client, playerSocketClient);
        instance.PlayerCount++;
        
        if (gameData.CaptchaEnabled && CaptchaGenerator is not null)
        {
            var result = CaptchaGenerator.Generate();
            instance.PendingCaptchas[realIp] = result.Answer;

            var dummiesSize = Encoding.UTF8.GetByteCount(result.Dummies);
            var captchaBuffer = new byte[3 + dummiesSize + result.ImageData.Length];
            captchaBuffer[0] = (byte) ServerPacket.Captcha;
            captchaBuffer[1] = (byte) CaptchaType.Emoji;
            captchaBuffer[2] = (byte) dummiesSize;
            Encoding.UTF8.GetBytes(result.Dummies).CopyTo(captchaBuffer, 3);
            result.ImageData.CopyTo(captchaBuffer, 3 + dummiesSize);
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
        BinaryPrimitives.WriteUInt32BigEndian(canvasInfo[5..], gameData.CooldownMs);
        BinaryPrimitives.WriteUInt32BigEndian(canvasInfo[9..], gameData.BoardWidth);
        BinaryPrimitives.WriteUInt32BigEndian(canvasInfo[13..], gameData.BoardHeight);
        app.SendAsync(args.Client, canvasInfo.ToArray());
        
        DistributePlayerCount();
        PlayerConnected?.Invoke(this, new PlayerConnectedEventArgs(instance, args.Client));
    }
    
    private void MessageReceived(object? sender, MessageReceivedEventArgs args)
    {
        var realIp = GetRealIp(args.Client);
        var data = new Span<byte>(args.Data.ToArray());
        var client = instance.Clients[args.Client];

        switch ((ClientPacket) args.Data[0])
        {
            case ClientPacket.PixelPlace:
            {
                //Reject
                if (data.Length < 6)
                {
                    Logger?.Invoke($"Pixel from client {GetRealIpPort(args.Client)} rejected for invalid packet length ({data.Length})");
                    break;
                }

                var index = BinaryPrimitives.ReadUInt32BigEndian(data[1..]);
                var colour = data[5];

                // Reject
                if (index >= instance.Board.Length || colour >= (gameData.Palette?.Count ?? 32))
                {
                    Logger?.Invoke($"Pixel from client {GetRealIpPort(args.Client)} rejected for exceeding canvas size or palette ({index}, {colour})");
                    break;
                }
                var clientCooldown = instance.Clients[args.Client].Cooldown;

                if (clientCooldown > DateTimeOffset.Now)
                {
                    // Reject
                    Logger?.Invoke($"Pixel from client {GetRealIpPort(args.Client)} rejected for breaching cooldown ({clientCooldown})");
                    var buffer = (Span<byte>) stackalloc byte[10];
                    buffer[0] = (byte) ServerPacket.RejectPixel;
                    BinaryPrimitives.WriteInt32BigEndian(buffer[1..], (int) clientCooldown.ToUnixTimeMilliseconds());
                    BinaryPrimitives.WriteInt32BigEndian(buffer[5..], (int) index);
                    buffer[9] = instance.Board[index];
                    app.SendAsync(args.Client, buffer.ToArray());
                    break;
                }
                
                var inhibitor = new EventInhibitor();
                PixelPlacementReceived?.Invoke
                (
                    this,
                    new PixelPlacementEventArgs(instance, colour, (index % gameData.BoardWidth),
                        index / gameData.BoardHeight, index, args.Client, data.ToArray(), inhibitor)
                );

                if (inhibitor.Raised)
                {
                    Logger?.Invoke($"Pixel from client {GetRealIpPort(args.Client)} inhibited by event handler");
                    break;
                }

                // Accept
                instance.Board[index] = colour;
                instance.Clients[args.Client].Cooldown = DateTimeOffset.Now.AddMilliseconds(gameData.CooldownMs);
                var serverPixel = data[..6];
                serverPixel[0] = (byte) ServerPacket.PixelPlace;
                foreach (var wsClient in app.Clients)
                {
                    app.SendAsync(wsClient, serverPixel.ToArray());
                }
                break;
            }
            case ClientPacket.SetChatName:
            {
                // TODO: Make GameData configuration for chat name change cooldown
                if (client.LastNameChange + TimeSpan.FromSeconds(10) > DateTimeOffset.Now)
                {
                    break;
                }
                client.LastNameChange = DateTimeOffset.Now;

                // Validate name
                var name = Encoding.UTF8.GetString(data[1..]);
                if (string.IsNullOrWhiteSpace(name) || name.Length > 16)
                {
                    break;
                }

                // Update database
                var user = database.Users.Find(client.UserId);
                if (user is null)
                {
                    break;
                }
                user.ChatName = name;
                database.SaveChanges();

                // Distribute new chat name
                var nameInfoBuffer = CreateNamePacket(name, user.Id);
                SendToAll(nameInfoBuffer);
                break;
            }
            case ClientPacket.ChatMessage:
            {
                // Reject
                if (instance.Clients[args.Client].LastChat.AddMilliseconds(gameData.ChatCooldownMs) > DateTimeOffset.Now || data.Length > 400)
                {
                    Logger?.Invoke($"Chat from client {GetRealIpPort(args.Client)} rejected for breaching length/cooldown rules");
                    break;
                }

                instance.Clients[args.Client].LastChat = DateTimeOffset.Now;

                var rawText = Encoding.UTF8.GetString(data.ToArray(), 1, data.Length - 1);
                var splitText = rawText.Split("\n");
                var message = splitText.ElementAtOrDefault(0);
                var name = splitText.ElementAtOrDefault(1);
                var channel = splitText.ElementAtOrDefault(2);

                // Reject
                if (message is null || name is null || channel is null)
                {
                    break;
                }

                message = gameData.CensorChatMessages ? message : CensorText(message);
                name = gameData.CensorChatMessages ? CensorText(name) : name;
                name = PlayerNameRegex().Replace(name, "").ToLowerInvariant();
                
                var type = splitText.ElementAtOrDefault(3) switch
                {
                    "live" => ChatMessageType.LiveChat,
                    "place" => ChatMessageType.PlaceChat,
                    _ => ChatMessageType.LiveChat
                };

                var x = splitText.ElementAtOrDefault(4);
                var y = splitText.ElementAtOrDefault(5);

                var inhibitor = new EventInhibitor();
                ChatMessageReceived?.Invoke
                (
                    this, 
                    new ChatMessageEventArgs(instance, args.Client, message, channel, name, type, 
                        data.ToArray(), x is not null ? int.Parse(x) : null, y is not null ? int.Parse(y) : null, inhibitor)
                );

                if (inhibitor.Raised)
                {
                    Logger?.Invoke($"Chat message from client {GetRealIpPort(args.Client)} inhibited by event handler");
                    break;
                }
                
                // Accept
                var builder = new StringBuilder();
                builder.AppendLine(message);
                builder.AppendLine(name);
                builder.AppendLine(channel);
                builder.AppendLine(splitText.ElementAtOrDefault(3));
                builder.AppendLine(x ?? "0");
                builder.AppendLine(y ?? "0");
                var messageData = Encoding.UTF8.GetBytes(builder.ToString());
                var packet = new byte[messageData.Length + 1];
                packet[0] = (byte) ServerPacket.ChatMessage;
                messageData.CopyTo(packet, 1);
                
                foreach (var wsClient in app.Clients)
                {
                    app.SendAsync(wsClient, packet);
                }

                gameData.WebhookService?.SendWebhook(
                    new WebhookBody($"[{channel}] {name}@rplace.tk", message), httpClient);
                break;
            }
            case ClientPacket.CaptchaSubmit:
            {
                var response = Encoding.UTF8.GetString(data[1..]);
                
                if (instance.PendingCaptchas.TryGetValue(realIp, out var answer) || !response.Equals(answer))
                {
                    Logger?.Invoke($"Client {GetRealIpPort(args.Client)} disconnected for invalid captcha response");
                    _ = app.DisconnectClientAsync(args.Client, "Captcha fail");
                    break;
                }
                
                // Accept
                var buffer = new byte[2];
                buffer[1] = (byte) ServerPacket.Captcha;
                buffer[2] = (byte) CaptchaType.Success;
                app.SendAsync(args.Client, buffer);
                break;
            }
            case ClientPacket.ModAction:
            {
                if (!instance.Clients.TryGetValue(args.Client, out var clientData) || clientData.Permissions != Permissions.Admin)
                {
                    Logger?.Invoke($"Unauthenticated client {GetRealIpPort(args.Client)} attempted to initiate a mod action");
                    break;
                }

                // TODO: Mod action
                break;
            }
            case ClientPacket.Rollback:
            {
                if (!instance.Clients.TryGetValue(args.Client, out var clientData) || clientData.Permissions != Permissions.Admin)
                {
                    Logger?.Invoke($"Unauthenticated client {GetRealIpPort(args.Client)} attempted to initiate a rollback");
                    return;
                }
                if (data.Length < 7)
                {
                    Logger?.Invoke($"Rollback from client {GetRealIpPort(args.Client)} rejected for invalid packet length ({data.Length})");
                    break;
                }

                var regionWidth = data[1];
                var regionHeight = data[2];
                var boardPos = BinaryPrimitives.ReadInt32BigEndian(data[3..]);
                if (boardPos % gameData.BoardWidth + regionWidth >= gameData.BoardWidth
                    || boardPos + regionHeight * gameData.BoardHeight >= gameData.BoardWidth * gameData.BoardHeight)
                {
                    Logger?.Invoke($"Rollback from client {GetRealIpPort(args.Client)
                        } rejected for invalid parameters (w: {regionWidth}, h: {regionHeight}, pos: {boardPos})");
                    break;
                }

                var boardSpan = new Span<byte>(instance.Board);
                var regionI = 7; // We ignore initial data
                while (regionI < regionWidth * regionHeight + 7)
                {
                    data[regionI..(regionI + regionWidth)].CopyTo(boardSpan[boardPos..]);
                    boardPos += (int) gameData.BoardWidth;
                    regionI += regionWidth;
                }
                break;
            }
        }
    }

    private void ClientDisconnected(object? sender, ClientDisconnectedEventArgs args)
    {
        instance.Clients.Remove(args.Client);
        instance.PlayerCount--;
        DistributePlayerCount();
        
        PlayerDisconnected?.Invoke(this, new PlayerDisconnectedEventArgs(instance, args.Client));
    }

    /// <summary>
    /// Method to send a player count update packet to all connected clients
    /// </summary>
    private void DistributePlayerCount()
    {
        var gameInfo = (Span<byte>) stackalloc byte[3];
        gameInfo[0] = (byte) ServerPacket.PlayerCount;
        BinaryPrimitives.TryWriteUInt16BigEndian(gameInfo[1..], (ushort) instance.PlayerCount);
        SendToAll(gameInfo);
    }

    public void SendToAll(Span<byte> data)
    {
        var dataArray = data.ToArray();
        foreach (var client in app.Clients)
        {
            app.SendAsync(client, dataArray);
        }
    }
    
    public static byte[] CreateNamesPacket(Dictionary<int, string> names)
    {
        var size = 1;
        var encodedNames = new Dictionary<int, byte[]>();

        foreach (var (intId, name) in names)
        {
            var encName = Encoding.UTF8.GetBytes(name);
            encodedNames[intId] = encName;
            size += encName.Length + 5;
        }

        var infoBuffer = new byte[size];
        var span = infoBuffer.AsSpan();
        span[0] = 12;
        var i = 1;

        foreach (var (intId, encName) in encodedNames)
        {
            BinaryPrimitives.WriteInt32BigEndian(span.Slice(i, 4), intId);
            i += 4;
        
            span[i++] = (byte)encName.Length;
            encName.CopyTo(span[i..]);
            i += encName.Length;
        }

        return infoBuffer;
    }

    public static byte[] CreateNamePacket(string name, int intId)
    {
        var encName = Encoding.UTF8.GetBytes(name);
        var nmInfoBuf = new byte[6 + encName.Length];
        var span = nmInfoBuf.AsSpan();

        span[0] = 12;
        BinaryPrimitives.WriteInt32BigEndian(span.Slice(1, 4), intId);
        span[5] = (byte)encName.Length;
        encName.CopyTo(span[6..]);

        return nmInfoBuf;
    }
    
    /// <summary>
    /// Increases the size of a canvas/board, by a given width and height.
    /// </summary>
    /// <param name="widthIncrease">The increase in pixels on the X axis.</param>
    /// <param name="heightIncrease">The increase in pixels on the Y axis.</param>
    /// <param name="expandColour">The colour which the new expanded area of the board will be filled with.</param>
    /// <returns>The new width and new height of the board after it has been increased.</returns>
    public (uint NewWidth, uint NewHeight) ExpandCanvas(uint widthIncrease, uint heightIncrease, int expandColour)
    {
        var newHeight = gameData.BoardHeight + heightIncrease;
        var newWidth = gameData.BoardWidth + widthIncrease;

        var newBoard = new byte[newHeight * newWidth];
        for (var y = 0; y < gameData.BoardHeight; y++)
        {
            newBoard[y * newWidth] = instance.Board[y * gameData.BoardWidth];
        }

        instance.Board = newBoard;
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
        var messageBytes = Encoding.UTF8.GetBytes($"\x0f{message}\nSERVER@RPLACE✓\n{channel}");
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
        var width = endX - startX;
        var height = endY - startY; 
       
        while(startY < endY)
        { 
            while (startX < endX)
            {
                instance.Board[startX++ + startY * gameData.BoardWidth] = colour;
            }

            startY++;
            startX = endX - width;
        } 
        
        return width * height;
    }

    /// <summary>
    /// Bans a player from the current server instance, kicking them and preventing them from reconnecting for the duration
    /// defined in timeSpecifier.
    /// </summary>
    /// <param name="identifier">The client who is to be banned from reconnecting to the game, either ip or ClientMetadata</param>
    /// <param name="timeSpecifier">Either long unixTimeMs for ban end or a DateTimeOffset</param>
    public async Task BanPlayer<TPlayer, TDuration>(TPlayer identifier, TDuration timeSpecifier)
    {
        throw new NotImplementedException();

        var clientIp = identifier switch
        {
            string ip => ip,
            ClientMetadata client => GetRealIp(client),
            _ => null
        };
        if (clientIp is null)
        {
            return;
        }

        var endTime = timeSpecifier switch
        {
            long unixTimeMs => unixTimeMs,
            DateTimeOffset offset => (long?) offset.ToUnixTimeMilliseconds(),
            _ => null
        };
        if (endTime is null)
        {
            return;
        }

        await File.AppendAllTextAsync(Path.Combine(gameData.SaveDataFolder, "bans.txt"), "\n" + clientIp);
        foreach (var client in app.Clients.Where(client => GetRealIp(client) == clientIp))
        {
            await app.DisconnectClientAsync(client, "You have been banned from this instance");
        }
    }

    /// <summary>
    /// Mutes a player from the current server instance, preventing them from sending messages in chat for the duration
    /// defined in timeSpecifier.
    /// </summary>
    /// <param name="identifier">The client who is to be muted, either ip or ClientMetadata</param>
    /// <param name="timeSpecifier">Either long unixTimeMs for mute end or a DateTimeOffset</param>
    public async Task MutePlayer<TPlayer, TDuration>(TPlayer identifier, TDuration timeSpecifier)
    {
        throw new NotImplementedException();
        var clientIp = identifier switch
        {
            string ip => ip,
            ClientMetadata client => GetRealIp(client),
            _ => null
        };
        if (clientIp is null)
        {
            return;
        }

        var endTime = timeSpecifier switch
        {
            long unixTimeMs => unixTimeMs,
            DateTimeOffset offset => (long?) offset.ToUnixTimeMilliseconds(),
            _ => null
        };
        if (endTime is null)
        {
            return;
        }
        
        await File.AppendAllTextAsync(Path.Combine(gameData.SaveDataFolder, "mutes.txt"), "\n" + clientIp);
    }

    /// <summary>
    /// Kicks a player from the current server instance, disconnecting them from the game immediately.
    /// </summary>
    /// <param name="identifier">The client who is to be kicked, either ip or ClientMetadata</param>
    public async Task KickPlayer<T>(T identifier)
    {
        switch (identifier)
        {
            case ClientMetadata clientIdentifier:
            {
                await app.DisconnectClientAsync(clientIdentifier, "You have been kicked from this instance");
                break;
            }
            case string ip:
            {
                foreach (var client in app.Clients.Where(client => GetRealIp(client) == ip))
                {
                    await app.DisconnectClientAsync(client, "You have been kicked from this instance");
                }
                break;
            }
        }
    }

    private string CensorText(string text)
    {
        // Censored words regex
        foreach (var regex in gameData.ChatCensorRegexes)
        {
            text = regex.Replace(text, match => new string('*', match.Length));
        }
        
        text = DomainRegex().Replace(text, match =>
        {
            // Will include host & path (protocol is not captured)
            var url = match.ToString();
            return gameData.ChatAllowedDomainsRegexes.Any(allowed => allowed.IsMatch(url))
                ? url
                : new string('*', url.Length);
        });

        return text.Trim();
    }

    private static string HashSha256String(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return bytes.Aggregate("", (current, b) => current + b.ToString("x2"));
    }
    
    public async Task StopAsync()
    {
        foreach (var client in app.Clients)
        {
            await app.DisconnectClientAsync(client, "Server shutdown");
        }
        
        await app.StopAsync();
    }

    private string GetRealIpPort(ClientMetadata client)
    {
        return instance.Clients.GetValueOrDefault(client)?.IpPort ?? client.IpPort;
    }

    private string GetRealIp(ClientMetadata client)
    {
        var readAddress = GetRealIpPort(client);
        return readAddress.Split(":").FirstOrDefault() ?? readAddress;
    }
}
