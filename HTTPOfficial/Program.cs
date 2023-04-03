// An rplace server software that is intended to be used completely remotely, being accessable fully through a web interface

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
    Console.Write("[Warning]: Could not game config file, at " + configPath);

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
            });
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


if (!File.Exists(configPath))
{
    await CreateConfig();
}

if (!File.Exists(dataPath))
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write("[Warning]: Could not find data path, at " + dataPath);
    Directory.CreateDirectory(dataPath);
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"\n[INFO]: Data path recreated successfully, program will continue to run.");
    Console.ResetColor();
}

var configNullable = await JsonSerializer.DeserializeAsync<Configuration>(File.OpenRead(configPath));
if (configNullable is null)
{
    await CreateConfig();
}

var config = configNullable!;
var server = new WatsonWsServer(config.Port, config.UseHttps, config.CertPath, config.KeyPath);
var emailAttributes = new EmailAddressAttribute();

var toAuthenticate = new Dictionary<ClientMetadata, string>();
var pendingDatas = new Dictionary<ClientMetadata, AccountData>();
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

server.MessageReceived += (sender, args) =>
{
    var data = new Span<byte>(args.Data.ToArray()[1..]);

    switch (args.Data.ToArray()[0])
    {
        case (byte) PacketCodes.CreateAccount:
        {
            var stringData = Encoding.UTF8.GetString(data);
            var username = stringData[..10].TrimEnd();
            var password = stringData[10..42].TrimEnd();
            var email = stringData[42..362].TrimEnd();
            if (username.Length > 4 || password.Length > 6 || emailAttributes.IsValid(email))
            {
                server.SendAsync(args.Client, new[] {(byte) PacketCodes.Fail});
                return;
            }

            var code = new string[10];
            for (var i = 0; i < 10; i++)
            {
                code[i] = emojis[random.Next(0, emojis.Length - 1)];
            }
            var accountData = new AccountData(username, HashSha256String(password), email, 0, new int[] { });
            
            toAuthenticate.Add(args.Client, string.Join("", code));
            pendingDatas.Add(args.Client, accountData);
            
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
        case (byte) PacketCodes.AuthenticateCreate:
        {
            if (!toAuthenticate.TryGetValue(args.Client, out var realCode))
            {
                server.SendAsync(args.Client, new[] {(byte) PacketCodes.Fail});
                return;
            }

            var code = Encoding.UTF8.GetString(data[..10]);
            if (!realCode.Equals(code))
            {
                server.SendAsync(args.Client, new[] {(byte) PacketCodes.Fail});
                return;
            }

            var accountData = pendingDatas[args.Client];
            File.WriteAllText(Path.Join(dataPath,
                HashSha256String(accountData.Username + accountData.Password)),
                JsonSerializer.Serialize(accountData));
            
            toAuthenticate.Remove(args.Client);
            pendingDatas.Remove(args.Client);
            Console.WriteLine("Client created account successfully");
            break;
        }
        case (byte) PacketCodes.DeleteAccount:
        {
            if (Authenticate(ref data, out var accountData))
            {
                File.Delete(Path.Join(dataPath, HashSha256String(accountData.Username + accountData.Email)));
                // TODO: Tell worker servers to delete all their instances
            }
            break;
        }
        case (byte) PacketCodes.CreateInstance:
        {
            break;
        }
    }
};

await server.StartAsync();