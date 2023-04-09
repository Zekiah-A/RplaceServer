// An rplace server software that is intended to be used completely remotely, being accessable fully through a web interface
using System.Buffers.Binary;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    Console.Write("[Warning]: Could not find game config file, at " + configPath);

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


var config = await JsonSerializer.DeserializeAsync<Configuration>(File.OpenRead(configPath));
var server = new WatsonWsServer(config.Port, config.UseHttps, config.CertPath, config.KeyPath);
var emailAttributes = new EmailAddressAttribute();

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
    var data = new Span<byte>(args.Data.ToArray()[1..]);

    switch (args.Data.ToArray()[0])
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
            var code = string.Join("", codeChars);
            var accountData = new AccountData(username, HashSha256String(password), email, 0, new List<int>());
            
            toAuthenticate.TryAdd(args.Client, code);
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
                           "<h1 style=\"background-color: #13131314;display: inline;padding: 4px;border-radius: 4px;\">" + code + "</h1>" +
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
        case (byte) WorkerPackets.AuthenticateCreate:
        {
            if (!workerClients.Contains(args.Client) || data.Length != 50)
            {
                return;
            }
            
            var responseBuffer = new byte[6];
            responseBuffer[0] = (byte) ServerPackets.AuthorisedCreateInstance; // Sign the packet with the correct auth
            Buffer.BlockCopy(data.ToArray(), 42, responseBuffer, 1, 4); // Copy over the request ID
            
            if (!Authenticate(ref data, out var accountData)
                || accountData.AccountTier == 0 && accountData.Instances.Count >= 5)
            {
                responseBuffer[5] = 0; // Failed to auth
                server.SendAsync(args.Client, responseBuffer);
                return;
            }

            var instanceId = BinaryPrimitives.ReadInt32BigEndian(data);
            
            // Accept -  We add this instance to their account data, save the account data and send back the response
            accountData.Instances.Add(instanceId);
            responseBuffer[5] = 1; // Successfully authenticated
            server.SendAsync(args.Client, responseBuffer);
            File.WriteAllText(Path.Join(dataPath,
                HashSha256String(accountData.Username + accountData.Password)),
                JsonSerializer.Serialize(accountData));
            break;
        }
        case (byte) WorkerPackets.AuthenticateDelete:
        {
            if (!workerClients.Contains(args.Client) || data.Length != 50)
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
            
            // Accept -  We remove this instance from their account data, save the account data and send back the response
            accountData.Instances.Remove(instanceId);
            responseBuffer[5] = 1; // Failed to authenticate
            server.SendAsync(args.Client, responseBuffer);
            File.WriteAllText(Path.Join(dataPath,
                HashSha256String(accountData.Username + accountData.Password)),
                JsonSerializer.Serialize(accountData));
            break;
        }
        case (byte) WorkerPackets.AuthenticateManage:
        {
            if (!workerClients.Contains(args.Client) || data.Length != 50)
            {
                return;
            }
            
            var responseBuffer = new byte[6];
            responseBuffer[0] = (byte) ServerPackets.Authorised; // Sign the packet with the correct auth
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
                return;
            }
            
            // Accept - this is a general manage server authentication, so we don't need to touch account data
            responseBuffer[5] = 1; // Failed to authenticate
            server.SendAsync(args.Client, responseBuffer);
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