using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WatsonWebsocket;

namespace Server;

public partial class SocketServer
{
    private readonly HttpClient httpClient = new();
    private readonly Dictionary<string, SocketClient> clients = new();
    private readonly WatsonWsServer app;
    private readonly ProgramConfig programConfig;
    private readonly SocketServerConfig serverConfig;

    private int players;
    private byte[] board;

    public SocketServer(ProgramConfig programConfig, SocketServerConfig serverConfig)
    {
        app = new WatsonWsServer(new List<string> {"127.0.0.1", "localhost"}, programConfig.SocketPort,
            programConfig.Ssl);
        this.programConfig = programConfig;
        this.serverConfig = serverConfig;
        
        try
        {
            var boardFile = File.ReadAllBytes(Path.Join(programConfig.CanvasFolder, "place"));
            if (boardFile.Length == 0) throw new Exception("Could not find canvas save file! Creating new.");
            board = boardFile;
        }
        catch (Exception exception)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[WARNING]: " + exception.Message);
            Console.ResetColor();

            board = new byte[serverConfig.Width * serverConfig.Height];

            if (!Directory.Exists(programConfig.CanvasFolder))
            {
                Directory.CreateDirectory(programConfig.CanvasFolder);
                
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[INFO]: Created new canvas folder.");
                Console.ResetColor();
            }
            
            File.WriteAllBytes(Path.Join(programConfig.CanvasFolder, "place"), board);
        }
        
        //Make a canvas save file just before the program exits.
        AppDomain.CurrentDomain.ProcessExit += (object sender, EventArgs e) =>
        {
            File.WriteAllBytes(Path.Join(programConfig.CanvasFolder, "place"), board);
        };
        
        Program.SendBoardToWebServer(ref board);
    }

    public async Task Start()
    {
        app.ClientConnected += (sender, args) =>
        {
            var idIpPort = programConfig.UseCloudflare ? args.HttpRequest.Headers.Get(Array.IndexOf(args.HttpRequest.Headers.AllKeys, "x-forwarded-for")) : args.IpPort;

            if (programConfig.UseCloudflare &&
                 args.HttpRequest.Headers.Get(Array.IndexOf(args.HttpRequest.Headers.AllKeys, "origin")) !=
                 programConfig.Origin || serverConfig.Bans.Contains(args.IpPort.ToString()) || idIpPort.StartsWith("%") ||
                string.IsNullOrEmpty(idIpPort))
            {
                serverConfig.Bans.Add(args.IpPort.ToString());
                app.DisconnectClient(args.IpPort);
                return;
            }
            
            clients.Add(args.IpPort, new SocketClient(idIpPort));
            var buffer = new byte[9];
            buffer[0] = 1;
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan()[1..],  1);
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan()[5..], (uint) serverConfig.Cooldown);
            app.SendAsync(args.IpPort, buffer);
            players++;
        };

        app.MessageReceived += async (sender, args) =>
        {
            switch (args.Data[0])
            {
                case 15:
                    if (clients[args.IpPort].LastChat + 2500 > new DateTimeOffset().Millisecond ||
                        args.Data.Count > 400) return;
                    clients[args.IpPort].LastChat = new DateTimeOffset().Millisecond;
                    
                    foreach (var ipPort in app.ListClients())
                        await app.SendAsync(ipPort, args.Data);
                    
                    if (string.IsNullOrEmpty(serverConfig.WebhookUrl) || args.Data.Array is null) return;
                    
                    var rawMessage = Encoding.UTF8.GetString(args.Data.Array, 1, args.Data.Array.Length - 1).Replace("@", "");
                    var text = rawMessage.Split("\n")[0];
                    var name = AlphabetOnlyRegex().Replace(rawMessage.Split("\n")[1], "");
                    var msgChannel = rawMessage.Split("\n")[2];

                    var hook = $"{{'username': [{msgChannel}] {name}@rplace.tk`, 'content': {text}}}";
                    await httpClient.PostAsJsonAsync(serverConfig.WebhookUrl + "?wait=true", hook, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                    break;
                case 16:
                    var buffer = new byte[2];
                    buffer[1] = 16;
                    buffer[2] = 255;
                    await app.SendAsync(args.IpPort, buffer);
                    break;
                case 99:
                    break;
                case 20:
                    break;
            }
            
            if (args.Data.Array?.Length < 6) return;
            var index = BinaryPrimitives.ReadUInt32BigEndian(args.Data.Array?[1..]);
            var colour = args.Data[5];

            if (index >= board.Length || colour >= serverConfig.PaletteSize) return;
            var cd = clients[args.IpPort].Cooldown;

            if (cd > new DateTimeOffset().Millisecond)
            {
                //reject
                var buffer = new byte[10];
                buffer[0] = 7;
                BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan()[1..],  cd);
                BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan()[5..], (int) index);
                buffer[9] = board[index];
                await app.SendAsync(args.IpPort, buffer);
                return;
            }
            
            //accept
            board[index] = colour;
            clients[args.IpPort].Cooldown = new DateTimeOffset().Millisecond + serverConfig.Cooldown - 500;
            
            //Board instantly to http server, new clients always with the latest (no changes).
            Program.SendBoardToWebServer(ref board);
        };

        app.ClientDisconnected += (sender, args) =>
        {
            players--;
        };

        await app.StartAsync();
    }

    [RegexGenerator("/\\W+/g")]
    private static partial Regex AlphabetOnlyRegex();
}