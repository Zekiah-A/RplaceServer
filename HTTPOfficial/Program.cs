// An rplace server software that is intended to be used completely remotely, being accessible fully through a web interface
using System.Buffers.Binary;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HTTPOfficial;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using WatsonWebsocket;

const string configPath = "server_config.json";
const string dataPath = "ServerData";

async Task CreateConfig()
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write("[Warning]: Could not find server config file, at " + configPath);

    await using var configFile = File.OpenWrite(configPath);
    var defaultConfiguration =
        new Configuration(8080,
            false,
            "",
            "",
            "smtp.gmail.com",
            587,
            "myUsername@email.com",
            "myEmailPassword",
            new List<string>(),
            Guid.NewGuid().ToString());
    await JsonSerializer.SerializeAsync(configFile, defaultConfiguration, new JsonSerializerOptions { WriteIndented = true });
    await configFile.FlushAsync();
    
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"\n[INFO]: Config files recreated. Please check {Directory.GetCurrentDirectory()} and run this program again.");
    Console.ResetColor();
    Environment.Exit(0);
}

static string HashSha256String(string rawData)
{
    using var sha256Hash = SHA256.Create();
    var bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
    var builder = new StringBuilder();  
    foreach (var @byte in bytes)
    {
        builder.Append(@byte.ToString("x2"));
    }
    
    return builder.ToString();
}

if (!File.Exists(configPath))
{
    await CreateConfig();
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

if (!File.Exists(Path.Join(dataPath, "vanities.txt")))
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write("[Warning]: Could not find server vanities file, at " + configPath);
    await File.WriteAllTextAsync(Path.Join(dataPath, "vanities.txt"), ""); 
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"\n[INFO]: Data file recreated successfully, program will continue to run.");
    Console.ResetColor();
}

var config = await JsonSerializer.DeserializeAsync<Configuration>(File.OpenRead(configPath));
var server = new WatsonWsServer(config.Port, config.UseHttps, config.CertPath, config.KeyPath);
var emailAttributes = new EmailAddressAttribute();

// Vanity -> URL of  actual socket server & board, done by worker clients on startup
var registeredVanities = new Dictionary<string, string>();
var workerClients = new List<ClientMetadata>();
var toAuthenticate = new Dictionary<ClientMetadata, string>();
var clientAccountDatas = new Dictionary<ClientMetadata, AccountData>();
var random = new Random();
var emojis = new[]
{
    "😀", "😁", "😂", "🤣", "😃", "😄", "😅", "😆", "😉", "😊", "😋", "😎", "😍", "😘", "😗", "😙", "😚", "☺️", "🙂",
    "🤗", "🤔", "😐", "😑", "😶", "🙄", "😏", "😣", "😥", "😮", "🤐", "😯", "😪", "😫", "😴", "😌", "😛", "😜", "😝",
    "🤤", "😒", "😓", "😔", "😕", "🙃", "🤑", "😲", "☹️", "🙁", "😖", "😞", "😟", "😤", "😢", "😭", "😦", "😧", "😨",
    "😩", "🤯", "😬", "😰", "😱", "🥵", "🥶", "😳", "🤪", "😵", "🥴", "😷", "🤕", "🤒", "🤮", "🤢", "🥳", "🥺", "👋",
    "🤚", "🖐️", "✋", "🖖", "👌", "🤏", "✌️", "🤞", "🤟", "🤘", "🤙", "👈", "👉", "👆", "👇", "☝️", "👍", "👎", "✊", "👊",
    "🤛", "🤜", "👏", "🙌", "👐", "🤲", "🤝", "🙏", "💪", "🦾", "🦿", "🦵", "🦶", "👂", "🦻", "👃", "🧠", "🦷", "🦴",
    "👀", "👁️", "👅", "👄", "💋", "🩸", "😎", "🤖", "🗣", "🔥", "🏠", "🤡", "👾", "👋", "💩", "⚽", "👅", "🧠", "🕶",
    "🌳", "🌍", "🌈", "🎅", "👶", "👼", "🥖", "🍆", "🎮", "🎳", "🚢", "🗿", "ඞ", "📱", "🔑", "❤", "👺", "🤯", "🤬",
    "🦩", "🍔", "🎬", "🚨", "⚡️", "🍪", "🕋", "🎉", "📋", "🚦", "🔇", "🥶", "💼", "🎩", "🎒", "🦅", "🧊", "★", "✅",
    "😂", "😍", "🚀", "😈", "👟", "🍷", "🚜", "🐥", "🔍", "🎹", "🚻", "🚗", "🏁", "🥚", "🔪", "🍕", "🐑", "🖱", "😷",
    "🌱", "🏀", "🛠", "🤮", "💂", "📎", "🎄", "🕯️", "🔔", "⛪", "☃", "🍷", "❄", "🎁", "🩸"
};

async Task UpdateConfigAsync()
{
    await using var configFile = File.OpenWrite(configPath);
    await JsonSerializer.SerializeAsync(configFile, config, new JsonSerializerOptions { WriteIndented = true });
    await configFile.FlushAsync();
}

// This method will GOBBLE the first 42 bytes of the input array, be warned
bool Authenticate(ref Span<byte> data, out AccountData accountData)
{
    if (data.Length < 42)
    {
        accountData = null!;
        return false;
    }
    
    var stringData = Encoding.UTF8.GetString(data);
    var username = stringData[..10].TrimEnd();
    var password = stringData[10..42].TrimEnd();
    var accountPath = Path.Join(dataPath, HashSha256String(username + HashSha256String(password)));
    data = data[42..];

    if (!File.Exists((accountPath)))
    {
        accountData = null!;
        return false;
    }

    var account = JsonSerializer.Deserialize<AccountData>(File.ReadAllText(accountPath));
    if (account is null)
    {
        accountData = null!;
        return false;
    }
    
    accountData = account;
    return true;
}

server.MessageReceived += (_, args) =>
{
    var packetCode = args.Data.ToArray()[0];
    var data = new Span<byte>(args.Data.ToArray()[1..]);

    switch (packetCode)
    {
        case (byte) ClientPackets.CreateAccount:
        {
            var stringData = Encoding.UTF8.GetString(data);
            var username = stringData[..10].TrimEnd();
            var password = stringData[10..42].TrimEnd();
            var email = stringData[42..362].TrimEnd();
            if (username.Length <= 4 || password.Length <= 6 || !emailAttributes.IsValid(email))
            {
                var response = Encoding.UTF8.GetBytes("XCould not create account. Invalid information provided!");
                response[0] = (byte) ServerPackets.Fail;
                server.SendAsync(args.Client, response);
                return;
            }

            var codeChars = new string[10];
            for (var i = 0; i < 10; i++)
            {
                codeChars[i] = emojis[random.Next(0, emojis.Length - 1)];
            }
            var authCode = string.Join("", codeChars);
            var accountData = new AccountData(username, HashSha256String(password), email, 0, new List<int>());
            
            toAuthenticate.TryAdd(args.Client, authCode);
            clientAccountDatas.TryAdd(args.Client, accountData);

            async Task SendCodeEmailAsync()
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(config.EmailUsername, config.EmailUsername));
                message.To.Add(new MailboxAddress(email, email));
                message.Subject = "rplace.tk Instance Manager Account Code";
                message.Body = new TextPart("html")
                {
                    Text = "<h1>Hello</h1>" +
                           "<p>Someone used your email to register a new rplace instance manager account.</p>" +
                           "<p>If that's you, then cool, your code is:</p>" +
                           "<h1 style=\"background-color: #13131314;display: inline;padding: 4px;border-radius: 4px;\">" + authCode + "</h1>" +
                           "<p>Otherwise, you can ignore this email, who cares anyway??</p>" +
                           "<img src=\"https://raw.githubusercontent.com/rslashplace2/rslashplace2.github.io/main/favicon.png\">" +
                           "<p style=\"opacity: 0.6;\">Email sent at " + DateTime.Now + " | Feel free to reply |" +
                           "<a href=\"https://rplace.tk\" style=\"text-decoration: none;\">https://rplace.tk</a></p>"
                };
                
                try
                {
                    using var smtpClient = new SmtpClient();
                    await smtpClient.ConnectAsync(config.SmtpHost, config.SmtpPort, SecureSocketOptions.StartTlsWhenAvailable);
                    await smtpClient.AuthenticateAsync(config.EmailUsername, config.EmailPassword);
                    await smtpClient.SendAsync(message);
                    await smtpClient.DisconnectAsync(true);
                }
                catch (Exception exception)
                {
                    Console.WriteLine("Could not send error message" + exception);
                }
            }
            
            Task.Run(SendCodeEmailAsync);
            break;
        }
        case (byte) ClientPackets.AccountCode:
        {
            if (!toAuthenticate.TryGetValue(args.Client, out var realCode))
            {
                var response = Encoding.UTF8.GetBytes("XCould not create account. No pending account found!");
                response[0] = (byte) ServerPackets.Fail;
                server.SendAsync(args.Client, response);
                return;
            }

            var code = Encoding.UTF8.GetString(data);
            if (!realCode.Equals(code.Trim().Replace(" ", "")))
            {
                var response = Encoding.UTF8.GetBytes("XCould not create account. Code was invalid!");
                response[0] = (byte) ServerPackets.Fail;
                server.SendAsync(args.Client, response);
                
                toAuthenticate.Remove(args.Client);
                return;
            }

            var accountData = clientAccountDatas[args.Client];
            File.WriteAllText(Path.Join(dataPath,
                HashSha256String(accountData.Username + accountData.Password)),
                JsonSerializer.Serialize(accountData));
            
            toAuthenticate.Remove(args.Client);
            Console.WriteLine("Client created account successfully");
            break;
        }
        case (byte) ClientPackets.DeleteAccount:
        {
            if (clientAccountDatas.TryGetValue(args.Client, out var accountData))
            {
                File.Delete(Path.Join(dataPath, HashSha256String(accountData.Username + accountData.Password)));
                // TODO: Tell worker servers to delete all their instances
            }
            break;
        }
        case (byte) ClientPackets.AccountInfo:
        {
            if (clientAccountDatas.TryGetValue(args.Client, out var accountData))
            {
                var dataBlob = Encoding.UTF8.GetBytes("X" + JsonSerializer.Serialize(accountData));
                dataBlob[0] = (byte) ServerPackets.AccountInfo;
                server.SendAsync(args.Client, dataBlob);
            }
            break;
        }
        case (byte) ClientPackets.Authenticate:
        {
            if (Authenticate(ref data, out var accountData))
            {
                clientAccountDatas.TryAdd(args.Client, accountData);
            }
            break;
        }
        
        // A worker server has joined the network. It now has to tell the auth server it exists, and prove that it is
        // a legitimate worker using the network instance key so that it will be allowed to carry out actions. 
        case (byte) WorkerPackets.AnnounceExistence:
        {
            // If it is a legitimate worker wanting to join the network, we include it so that it can be announced to clients
            if (Encoding.UTF8.GetString(data).Equals(config.InstanceKey))
            {
                config.KnownWorkers.Add(args.Client.IpPort);
                Task.Run(UpdateConfigAsync);
                workerClients.Add(args.Client);
            }
            break;
        }
        // Worker server has just started up and booted it's instances, it sees that some of it's instances have
        // previously registered vanities, and now needs to announce them onto the auth server with whatever new
        // URLS those instances have.
        case (byte) WorkerPackets.AnnounceVanity:
        {
            if (!workerClients.Contains(args.Client))
            {
                return;
            }
                    
            // Should be in the format "myvanityname\nserver=https://server.com:2304/place&board=wss://server.com:21314/ws"
            var text = Encoding.UTF8.GetString(data).Split("\n");
            registeredVanities.Add(text[0], text[1]);
            break;
        }
        // All of these methods have overlapping authentication methods, with little variation on auth success, so we can merge
        // their cases until we do actually need to finally branch for the specifics of each.
        case (byte) WorkerPackets.AuthenticateCreate or (byte) WorkerPackets.AuthenticateDelete
            or (byte) WorkerPackets.AuthenticateManage or (byte) WorkerPackets.AuthenticateVanity:
        {
            if (!workerClients.Contains(args.Client) || data.Length < 46)
            {
                return;
            }
            
            var responseBuffer = new byte[6];
            responseBuffer[0] = (byte) ServerPackets.AuthorisedDeleteInstance; // Sign the packet with the correct auth
            Buffer.BlockCopy(data.ToArray(), 42, responseBuffer, 1, 4); // Copy over the request ID

            if (!Authenticate(ref data, out var accountData))
            {
                responseBuffer[5] = 0; // Failed to authenticate
                server.SendAsync(args.Client, responseBuffer);
                return;
            }

            var instanceId = BinaryPrimitives.ReadInt32BigEndian(data);

            if (!accountData.Instances.Contains(instanceId))
            {
                responseBuffer[5] = 0; // Failed to authenticate
                server.SendAsync(args.Client, responseBuffer);
            }

            switch (packetCode)
            {
                // A client has just asked the worker server to create an instance, the worker server then checks with the auth server
                // whether they are actually allowed to delete this instance, if so, the auth server must also change the account data
                // of the client, removing this instance ID from the client's instances list to ensure it is synchronised with the worker.
                case (byte) WorkerPackets.AuthenticateCreate:
                {
                    // Accept -  We add this instance to their account data, save the account data and send back the response
                    accountData.Instances.Add(instanceId);
                    responseBuffer[5] = 1; // Successfully authenticated
                    server.SendAsync(args.Client, responseBuffer);
                    File.WriteAllText(Path.Join(dataPath,
                        HashSha256String(accountData.Username + accountData.Password)),
                        JsonSerializer.Serialize(accountData));
                    break;
                }
                // A client has just asked the worker server to delete an instance, the worker server then checks with the auth server
                // whether they are actually allowed to delete this instance, if so, the auth server must also change the account data
                // of the client, removing this instance ID from the client's instances list to ensure it is synchronised with the worker.
                case (byte) WorkerPackets.AuthenticateDelete:
                {
                    // Accept -  We remove this instance from their account data, save the account data and send back the response
                    accountData.Instances.Remove(instanceId);
                    responseBuffer[5] = 1; // Failed to authenticate
                    server.SendAsync(args.Client, responseBuffer);
                    File.WriteAllText(Path.Join(dataPath,
                        HashSha256String(accountData.Username + accountData.Password)),
                        JsonSerializer.Serialize(accountData));
                    break;
                }
                // A client has just asked the worker server to modify, subscribe to, or have some other kind of access to a
                // private part of an instance, however, it does not involve modifying the client's data unlike AuthenticateDelete
                // or AuthenticateCreate, the auth server only ensures that the client owns this instance that they claim they want to do something with.
                case (byte) WorkerPackets.AuthenticateManage:
                {
                    // Accept - this is a general manage server authentication, so we don't need to touch account data
                    responseBuffer[5] = 1; // Failed to authenticate
                    server.SendAsync(args.Client, responseBuffer);
                    break;
                }
                // A client has requested to apply a new vanity to an instance. The auth server must now prove that the client
                // in fact owns that vanity, that this vanity name has not already been registered, and if so, we register this
                // vanity to the URL of the instance.
                case (byte) WorkerPackets.AuthenticateVanity:
                {
                    var text = Encoding.UTF8.GetString(data[4..]).Split("\n");

                    if (text.Length != 2 || registeredVanities.TryGetValue(text[0], out var _))
                    {
                        responseBuffer[5] = 0; // vanity with specified name already exists
                        server.SendAsync(args.Client, responseBuffer);
                    }

                    // Accept - Register vanity
                    registeredVanities.Add(text[0], text[1]);
                    responseBuffer[5] = 1; 
                    server.SendAsync(args.Client, responseBuffer);
                    break;
                }
            }
            break;
        }
    }
};
server.ClientDisconnected += (_, args) =>
{
    clientAccountDatas.Remove(args.Client);
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

Console.WriteLine("Server listening on port " + config.Port);
server.Logger = Console.WriteLine;
await server.StartAsync();
await Task.Delay(-1);