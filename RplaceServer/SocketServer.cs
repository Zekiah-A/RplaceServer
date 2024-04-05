using System.Buffers.Binary;
using System.Collections;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RplaceServer.Events;
using RplaceServer.Types;
using WatsonWebsocket;
using LiteDB;
using ZCaptcha;

namespace RplaceServer;

public sealed partial class SocketServer
{
    private readonly ServerDbService? serverDb;
    private readonly HttpClient httpClient;
    private readonly EmojiCaptchaGenerator emojiCaptchaGenerator;
    private readonly WatsonWsServer app;
    public readonly ServerInstance instance;
    private readonly GameData gameData;
    private readonly string origin;
    private readonly List<string> allowedDomains =
    [
        "https://rplace.tk", "https://discord.com",
        "https://twitter.com", "https://google.com",
        "https://reddit.com", "https://github.com",
        "https://wikipedia.org", "https://rplace.live"
    ];

    public Action<string>? Logger;
    public event EventHandler<ChatMessageEventArgs>? ChatMessageReceived;
    public event EventHandler<PixelPlacementEventArgs>? PixelPlacementReceived;
    public event EventHandler<PlayerConnectedEventArgs>? PlayerConnected;
    public event EventHandler<PlayerDisconnectedEventArgs>? PlayerDisconnected;

    private string blockedWordsPattern;
    private Regex blockedWordsRegex;
    public string BlockedWordsPattern
    {
        get => blockedWordsPattern;
        set
        {
            blockedWordsPattern = value;
            blockedWordsRegex = new Regex(value, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
        }
    }

    private string blockedDomainsPattern;
    private Regex blockedDomainsRegex;
    public string BlockedDomainsPattern
    {
        get => blockedDomainsPattern;
        set
        {
            blockedDomainsPattern = value;
            blockedDomainsRegex = new Regex(value, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
        }
    }
    
    [GeneratedRegex("\\W+")]
    private static partial Regex PlayerNameRegex();

    public SocketServer(ServerInstance parentInstance, GameData data, string? certPath, string? keyPath, string originHeader, bool ssl, int port)
    {
        instance = parentInstance;
        gameData = data;
        app = new WatsonWsServer(port, ssl, certPath, keyPath, LogLevel.None, "localhost");
        origin = originHeader;
        httpClient = new HttpClient();
        emojiCaptchaGenerator = new EmojiCaptchaGenerator(Path.Join(gameData.SaveDataFolder, "CaptchaGeneration", "NotoColorEmoji-Regular.ttf"));
        blockedWordsPattern = @"\\b(sik[ey]rim|orospu|piç|yavşak|kevaşe|ıçmak|kavat|kaltak|götveren|amcık|@everyone|@here|amcık|[fF][uU][ckr]{1,3}(\\b|ing\\b|ed\\b)?|shi[t]|c[u]nt|nigg[ae]r?|bastard|bitch|blowjob|clit|cock|cum|cunt|dick|fag|tranny|faggot|fuck|jizz|kike|dyke|masturbat(e|ion)|nazi|nigga|whore|porn|pussy|queer|rape|r[a4]pe|slut|suck|tit)\\b";
        blockedWordsRegex = new Regex(blockedWordsPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(50));
        blockedDomainsPattern = @"(https?:\/\/)?([\\da-z.-]+)\.([a-z.]{2,6})([/\\w .-]*)(\/?[^\s]*)";
        blockedDomainsRegex = new Regex(blockedDomainsPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(50));
        
        if (gameData.UseDatabase)
        {
            serverDb = new ServerDbService(Path.Join(gameData.SaveDataFolder, "Server.db"));
        }
    }
    
    // These are hybrid files, made up of a key value dictionary separated by a whitespace, but also can contain
    // links to other hosted plaintext files, which will also be evaluated and added to the dictionary
    private async Task ReadMuteBanLinkSheet<T>(IEnumerable<string> text, T targetDictionary) where T : IDictionary
    {
        foreach (var line in text)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var sections = line.Split(" ");
            switch (sections.Length)
            {
                case 0:
                {
                    continue;
                }
                case 1:
                {
                    if (Uri.TryCreate(sections[0], UriKind.Absolute, out var banSheetUri) &&
                        (banSheetUri.Scheme == Uri.UriSchemeHttp || banSheetUri.Scheme == Uri.UriSchemeHttps))
                    {
                        var response = await httpClient.GetAsync(sections[0]);
                        if (response.IsSuccessStatusCode)
                        {
                            var linkSheet = await response.Content.ReadAsStringAsync();
                            await ReadMuteBanLinkSheet(linkSheet.Split("\n"), targetDictionary);
                        }
                    }
                    else
                    {
                        targetDictionary.Add(sections[0], long.MaxValue);
                    }
                    break;
                }
                case 2:
                {
                    if (long.TryParse(sections[1], out var dateEnd))
                    {
                        targetDictionary.Add(sections[0], dateEnd);
                    }
                    break;
                }
            }
        }
    }
    
    public async Task StartAsync()
    {
        var captchaResources = Path.Join(gameData.StaticResourcesFolder, @"CaptchaGeneration");
        if (!Directory.Exists(captchaResources))
        {
            Logger?.Invoke($"Could not find Resources Captcha Generation at {captchaResources}. Regenerating.");
            Directory.CreateDirectory(captchaResources);
            FileUtils.RecursiveCopy(Path.Join(FileUtils.BuildContentPath, @"CaptchaGeneration"), captchaResources);
        }
        
        var bansPath = Path.Join(gameData.SaveDataFolder, "bans.txt");
        if (File.Exists(bansPath))
        {
            ReadMuteBanLinkSheet(await File.ReadAllLinesAsync(bansPath), instance.Blacklist).GetAwaiter().GetResult();
        }
        else
        {
            Logger?.Invoke($"Could not find bans file at {Path.Join(gameData.SaveDataFolder, "bans.txt")}. Will be regenerated when a player is banned.");
        }
        
        var mutesPath = Path.Join(gameData.SaveDataFolder, "mutes.txt");
        if (File.Exists(mutesPath))
        {
            ReadMuteBanLinkSheet(await File.ReadAllLinesAsync(mutesPath), instance.Mutes).GetAwaiter().GetResult();
        }
        else
        {
            Logger?.Invoke($"Could not find mutes file at {Path.Join(gameData.SaveDataFolder, "mutes.txt")}. Will be regenerated when a player is muted.");
        }

        var vipPath = Path.Join(gameData.SaveDataFolder, "vip.txt");
        if (File.Exists(vipPath))
        {
            instance.VipKeys.AddRange(await File.ReadAllLinesAsync(vipPath));
        }
        else
        {
            Logger?.Invoke($"Could not find game VIPs file. Generating new VIP key file at '{vipPath}'");
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
        if (instance.Blacklist.TryGetValue(realIp, out var banUnixMsEnd))
        {
            if (banUnixMsEnd < DateTimeOffset.Now.ToUnixTimeMilliseconds())
            {
                Logger?.Invoke($"Client {realIpPort} disconnected for violating initial headers checks");
                return;
            }

            instance.Blacklist.Remove(realIp);
            // TODO: Send discord and console message, update bans in file
        }
        
        // Reject
        if (!string.IsNullOrEmpty(origin) && args.HttpRequest.Headers.Origin.First() != origin)
        {
            Logger?.Invoke($"Client {realIpPort} disconnected for violating initial headers checks");
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
        
        // Accept - Create a player instance
        var playerSocketClient = new ClientData(realIpPort, DateTimeOffset.Now);
        var uriKey = args.HttpRequest.Path.ToUriComponent().Split("/").FirstOrDefault();
        if (uriKey is not null && uriKey.Length != 0)
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
        
        if (gameData.CaptchaEnabled)
        {
            var result = emojiCaptchaGenerator.Generate();
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
        PlayerConnected?.Invoke(this, new PlayerConnectedEventArgs(args.Client));
    }
    
    private void MessageReceived(object? sender, MessageReceivedEventArgs args)
    {
        var realIp = GetRealIp(args.Client);
        var data = new Span<byte>(args.Data.ToArray());

        switch ((ClientPacket) args.Data[0])
        {
            case ClientPacket.PixelPlace:
            {
                //Reject
                if (data.Length < 6)
                {
                    Logger?.Invoke($"Pixel from client {GetRealIpPort(args.Client)} rejected for invalid packet length ({data.Length})");
                    return;
                }

                var index = BinaryPrimitives.ReadUInt32BigEndian(data[1..]);
                var colour = data[5];

                // Reject
                if (index >= instance.Board.Length || colour >= (gameData.Palette?.Count ?? 32))
                {
                    Logger?.Invoke($"Pixel from client {GetRealIpPort(args.Client)} rejected for exceeding canvas size or palette ({index}, {colour})");
                    return;
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
                    return;
                }
                
                var inhibitor = new EventInhibitor();
                PixelPlacementReceived?.Invoke
                (
                    this,
                    new PixelPlacementEventArgs(colour, (index % gameData.BoardWidth),
                        index / gameData.BoardHeight, index, args.Client, data.ToArray(), inhibitor)
                );

                if (inhibitor.Raised)
                {
                    Logger?.Invoke($"Pixel from client {GetRealIpPort(args.Client)} inhibited by event handler");
                    return;
                }

                // Accept
                instance.Board[index] = colour;
                instance.Clients[args.Client].Cooldown = DateTimeOffset.Now.AddMilliseconds(gameData.CooldownMs);
                var serverPixel = data[..6];
                serverPixel[0] = (byte) ServerPacket.PixelPlace;
                foreach (var client in app.Clients)
                {
                    app.SendAsync(client, serverPixel.ToArray());
                }
                break;
            }
            case ClientPacket.ChatMessage:
            {
                // Reject
                if (instance.Clients[args.Client].LastChat.AddMilliseconds(gameData.ChatCooldownMs) > DateTimeOffset.Now || data.Length > 400)
                {
                    Logger?.Invoke($"Chat from client {GetRealIpPort(args.Client)} rejected for breaching length/cooldown rules");
                    return;
                }
                
                if (instance.Mutes.TryGetValue(GetRealIp(args.Client), out var muteUnixMsEnd))
                {
                    if (muteUnixMsEnd < DateTimeOffset.Now.ToUnixTimeMilliseconds())
                    {
                        Logger?.Invoke($"Chat message from client {GetRealIpPort(args.Client)} rejected for being muted");
                        return;
                    }

                    instance.Mutes.Remove(realIp);
                    // TODO: Send discord and console message & save to file
                    return;
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
                    return;
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
                    new ChatMessageEventArgs(args.Client, message, channel, name, type, 
                        data.ToArray(), x is not null ? int.Parse(x) : null, y is not null ? int.Parse(y) : null, inhibitor)
                );

                if (inhibitor.Raised)
                {
                    Logger?.Invoke($"Chat message from client {GetRealIpPort(args.Client)} inhibited by event handler");
                    return;
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
                
                foreach (var client in app.Clients)
                {
                    app.SendAsync(client, packet);
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
                    return;
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
                    return;
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
                    return;
                }

                var regionWidth = data[1];
                var regionHeight = data[2];
                var boardPos = BinaryPrimitives.ReadInt32BigEndian(data[3..]);
                if (boardPos % gameData.BoardWidth + regionWidth >= gameData.BoardWidth
                    || boardPos + regionHeight * gameData.BoardHeight >= gameData.BoardWidth * gameData.BoardHeight)
                {
                    Logger?.Invoke($"Rollback from client {GetRealIpPort(args.Client)
                        } rejected for invalid parameters (w: {regionWidth}, h: {regionHeight}, pos: {boardPos})");
                    return;
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
        
        PlayerDisconnected?.Invoke(this, new PlayerDisconnectedEventArgs(args.Client));
    }

    /// <summary>
    /// Method to send a player count update packet to all connected clients
    /// </summary>
    private void DistributePlayerCount()
    {
        var gameInfo = (Span<byte>) stackalloc byte[3];
        gameInfo[0] = (byte) ServerPacket.PlayerCount;
        BinaryPrimitives.TryWriteUInt16BigEndian(gameInfo[1..], (ushort) instance.PlayerCount);

        foreach (var client in app.Clients)
        {
            app.SendAsync(client, gameInfo.ToArray());
        }
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

        instance.Blacklist.Add(clientIp, (long) endTime);
        await File.AppendAllTextAsync(Path.Join(gameData.SaveDataFolder, "bans.txt"), "\n" + clientIp);
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
        
        instance.Mutes.Add(clientIp, (long) endTime);
        await File.AppendAllTextAsync(Path.Join(gameData.SaveDataFolder, "mutes.txt"), "\n" + clientIp);
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
        var censoredText = blockedWordsRegex
            .Replace(text, match => new string('*', match.Length));
        
        censoredText = blockedDomainsRegex.Replace(censoredText, match =>
        {
            var url = match.ToString();
            var domain = url.Replace("http://", "").Replace("https://", "");
            return allowedDomains.Any(allowed => allowed.StartsWith(domain)) ? url : new string('*', url.Length);
        });

        return censoredText.Trim();
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetRealIpPort(ClientMetadata client)
    {
        return instance.Clients.GetValueOrDefault(client)?.IdIpPort ?? client.IpPort;
    }

    private string GetRealIp(ClientMetadata client)
    {
        var readAddress = GetRealIpPort(client);
        return readAddress.Split(":").FirstOrDefault() ?? readAddress;
    }
}
