// An rplace server software that is intended to be used completely remotely, being accessable fully through a web interface

using System.Buffers.Binary;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HTTPOfficial;
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
            "myUsername@email.com",
            "myEmailPassword",
            new[]
            {
                new InstanceRange("192.128.1.253", new IntRange(0, 100))
            },
            "secretInstanceControlKeyGoesHere");
    await JsonSerializer.SerializeAsync(configFile, defaultConfiguration, new JsonSerializerOptions {WriteIndented = true });
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


var configNullable = await JsonSerializer.DeserializeAsync<Configuration>(File.OpenRead(configPath));
var config = configNullable!;
var server = new WatsonWsServer(config.Port, config.UseHttps, config.CertPath, config.KeyPath);
var emailAttributes = new EmailAddressAttribute();

var toAuthenticate = new Dictionary<ClientMetadata, string>();
var pendingData = new Dictionary<ClientMetadata, AccountData>();
var random = new Random();
var emojis = new[]
{
    "😀", "😁", "😂", "🤣", "😃", "😄", "😅", "😆", "😉", "😊", "😋", "😎", "😍", "😘", "😗", "😙", "😚", "☺️", "🙂",
    "🤗", "🤔", "😐", "😑", "😶", "🙄", "😏", "😣", "😥", "😮", "🤐", "😯", "😪", "😫", "😴", "😌", "😛", "😜", "😝",
    "🤤", "😒", "😓", "😔", "😕", "🙃", "🤑", "😲", "☹️", "🙁", "😖", "😞", "😟", "😤", "😢", "😭", "😦", "😧", "😨",
    "😩", "🤯", "😬", "😰", "😱", "🥵", "🥶", "😳", "🤪", "😵", "🥴", "😷", "🤕", "🤒", "🤮", "🤢", "🥳", "🥺", "👋",
    "🤚", "🖐️", "✋", "🖖", "👌", "🤏", "✌️", "🤞", "🤟", "🤘", "🤙", "👈", "👉", "👆", "🖕", "👇", "☝️", "👍", "👎",
    "✊", "👊", "🤛", "🤜", "👏", "🙌", "👐", "🤲", "🤝", "🙏", "💪", "🦾", "🦿", "🦵", "🦶", "👂", "🦻", "👃", "🧠",
    "🦷", "🦴", "👀", "👁️", "👅", "👄", "💋", "🩸", "👶", "🧒", "👦", "👧", "🧑", "👱‍️", "👱", "👩", "🧑‍",
    "👨‍", "👩‍🦰", "👨‍🦰", "👱‍♂️", "👩‍🦳", "👨‍🦳", "👩‍🦲", "👨‍🦲", "🧔", "👵", "🧓", "👴", "👲", "👳", "👳",
    "🧕", "👮"
};
var smtpClient = new SmtpClient(config.SmtpHost) 
{
    Port = 587,
    Credentials = new NetworkCredential(config.EmailUsername, config.EmailPassword),
    EnableSsl = true,
};

var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Add("Authentication-Key", config.InstanceKey);
var workerSocketClients = config.InstanceRanges
    .Select(instance => new WatsonWsClient(new Uri("ws://" + instance.InstanceIp + ":27277")))
    .ToList();

// Will consume first 42 bytes of data
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
    var accountPath = HashSha256String(Path.Join(dataPath, username + password));
    
    if (!File.Exists((accountPath)))
    {
        data = data[42..];
        accountData = null!;
        return false;
    }

    var account = JsonSerializer.Deserialize<AccountData>(File.ReadAllText(accountPath));
    if (account is null)
    {
        data = data[42..];
        accountData = null!;
        return false;
    }

    data = data[42..];
    accountData = account;
    return true;
}
bool ResolveInstanceIp(int instanceId, out string instanceIp)
{
    foreach (var range in config!.InstanceRanges)
    {
        if (range.Range.Start > instanceId || range.Range.End <= instanceId)
        {
            instanceIp = range.InstanceIp;
            return true;
        }
    }

    instanceIp = "";
    return false;
}

server.MessageReceived += (sender, args) =>
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
            if (username.Length > 4 || password.Length > 6 || emailAttributes.IsValid(email))
            {
                var response = Encoding.UTF8.GetBytes("XCould not create account. Invalid information provided!");
                response[0] = (byte) ServerPackets.Fail;
                server.SendAsync(args.Client, response);
                return;
            }

            var code = new string[10];
            for (var i = 0; i < 10; i++)
            {
                code[i] = emojis[random.Next(0, emojis.Length - 1)];
            }
            var accountData = new AccountData(username, HashSha256String(password), email, 0, new List<int>());
            
            toAuthenticate.Add(args.Client, string.Join("", code));
            pendingData.Add(args.Client, accountData);
            
            var mailMessage = new MailMessage
            {
                From = new MailAddress(config.EmailUsername),
                Subject = "Rplace.tk instance manager account code",
                Body = 
                    "<h1>Hello</h1>" +
                    "<p>Someone used your email to register a new rplace instance manager account.</p>" +
                    "<p>If that's you, then cool, your code is:</p>" +
                    "<h1>" + code + "</h1>" +
                    "<p>Otherwise, you can ignore this email, who cares anyway??</p>",
                IsBodyHtml = true,
            };
            mailMessage.To.Add(email);
            smtpClient.Send(mailMessage);
            Console.WriteLine("Client requested to create an account");
            break;
        }
        case (byte) ClientPackets.AuthenticateCreate:
        {
            if (!toAuthenticate.TryGetValue(args.Client, out var realCode))
            {
                var response = Encoding.UTF8.GetBytes("XCould not create account. No pending account found!");
                response[0] = (byte) ServerPackets.Fail;
                server.SendAsync(args.Client, response);
                return;
            }

            var code = Encoding.UTF8.GetString(data[..10]);
            if (!realCode.Equals(code))
            {
                var response = Encoding.UTF8.GetBytes("XCould not create account. Code was invalid!");
                response[0] = (byte) ServerPackets.Fail;
                server.SendAsync(args.Client, response);
                
                toAuthenticate.Remove(args.Client);
                pendingData.Remove(args.Client);
                return;
            }

            var accountData = pendingData[args.Client];
            File.WriteAllText(Path.Join(dataPath,
                HashSha256String(accountData.Username + accountData.Password)),
                JsonSerializer.Serialize(accountData));
            
            toAuthenticate.Remove(args.Client);
            pendingData.Remove(args.Client);
            Console.WriteLine("Client created account successfully");
            break;
        }
        case (byte) ClientPackets.DeleteAccount:
        {
            if (Authenticate(ref data, out var accountData))
            {
                File.Delete(Path.Join(dataPath, HashSha256String(accountData.Username + accountData.Email)));
                // TODO: Tell worker servers to delete all their instances
            }
            break;
        }
        case (byte) ClientPackets.CreateInstance:
        {
            if (!Authenticate(ref data, out var accountData))
            {
                var response = Encoding.UTF8.GetBytes("XCould not authenticate account!");
                response[0] = (byte) ServerPackets.Fail;
                server.SendAsync(args.Client, response);
                return;
            }

            if (accountData.AccountTier == 0 && accountData.Instances.Count >= 5)
            {
                var response = Encoding.UTF8.GetBytes("XCould not create instance! You have already registered too many!");
                response[0] = (byte) ServerPackets.Fail;
                server.SendAsync(args.Client, response);
                return;
            }

            async Task RequestCreationAsync()
            {
                var instanceId = -1;
                foreach (var instance in config.InstanceRanges)
                {
                    using var response = await httpClient.GetAsync("http://" + instance.InstanceIp + "/CreateInstance");
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        instanceId = int.Parse(await response.Content.ReadAsStringAsync());
                        accountData.Instances.Add(instanceId);
                    }
                }

                if (instanceId == -1)
                {
                    await server.SendAsync(args.Client, new [] { (byte) ServerPackets.Fail });
                }

                await File.WriteAllTextAsync(Path.Join(dataPath,
                    HashSha256String(accountData.Username + accountData.Password)),
                    JsonSerializer.Serialize(accountData));
            }

            Task.Run(RequestCreationAsync);
            break;
        }
        case (byte) ClientPackets.DeleteInstance:
        {
            if (!Authenticate(ref data, out var accountData))
            {
                var response = Encoding.UTF8.GetBytes("XCould not authenticate account!");
                response[0] = (byte) ServerPackets.Fail;
                server.SendAsync(args.Client, response);
                return;
            }

            var instanceId = BinaryPrimitives.ReadUInt32BigEndian(data);

            if (!accountData.Instances.Contains((int) instanceId))
            {
                var response = Encoding.UTF8.GetBytes("XCould not delete instance. You do not own it.");
                response[0] = (byte) ServerPackets.Fail;
                server.SendAsync(args.Client, response);
                return;
            }

            if (!ResolveInstanceIp((int) instanceId, out string instanceIp))
            {
                var response = Encoding.UTF8.GetBytes("XCould not delete instance. Instance with specified ID could not be found.");
                response[0] = (byte) ServerPackets.Fail;
                server.SendAsync(args.Client, response);
                return;
            }
            
            async Task RequestDeletionAsync()
            {
                using var response = await httpClient.GetAsync("http://" + instanceIp + "/DeleteInstance/" + instanceId);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    accountData.Instances.Remove((int) instanceId);
                }
                else
                {
                    var failure = Encoding.UTF8.GetBytes("XCould not delete instance. Instance with specified ID could not be found.");
                    failure[0] = (byte) ServerPackets.Fail;
                    await server.SendAsync(args.Client, failure);
                }

                accountData.Instances.Remove((int) instanceId);
                await File.WriteAllTextAsync(Path.Join(dataPath,
                    HashSha256String(accountData.Username + accountData.Password)),
                    JsonSerializer.Serialize(accountData));
            }

            Task.Run(RequestDeletionAsync);
            break;
        }
        case (byte) ClientPackets.RestartInstance:
        {
            if (!Authenticate(ref data, out var accountData))
            {
                var response = Encoding.UTF8.GetBytes("XCould not authenticate account!");
                response[0] = (byte) ServerPackets.Fail;
                server.SendAsync(args.Client, response);
                return;
            }

            var instanceId = BinaryPrimitives.ReadUInt32BigEndian(data);

            if (!accountData.Instances.Contains((int) instanceId))
            {
                var response = Encoding.UTF8.GetBytes("XCould not delete instance. You do not own it.");
                response[0] = (byte) ServerPackets.Fail;
                server.SendAsync(args.Client, response);
                return;
            }

            if (!ResolveInstanceIp((int) instanceId, out var instanceIp))
            {
                var response = Encoding.UTF8.GetBytes("XCould not restart instance. Instance with specified ID could not be found.");
                response[0] = (byte) ServerPackets.Fail;
                server.SendAsync(args.Client, response);
                return;
            }
            
            async Task RequestRestartAsync()
            {
                using var response = await httpClient.GetAsync("http://" + instanceIp + "/RestartInstance/" + instanceId);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    var failure = Encoding.UTF8.GetBytes("XCould not restart instance. Instance with specified ID could not be found.");
                    failure[0] = (byte) ServerPackets.Fail;
                    await server.SendAsync(args.Client, failure);
                }
            }

            Task.Run(RequestRestartAsync);
            break;
        }
        case (byte) ClientPackets.AccountInfo:
        {
            if (Authenticate(ref data, out var accountData))
            {
                var dataBlob = Encoding.UTF8.GetBytes("X" + JsonSerializer.Serialize(accountData));
                dataBlob[0] = (byte) ServerPackets.AccountInfo;
                server.SendAsync(args.Client, dataBlob);
            }
            break;
        }
    }
};

await server.StartAsync();