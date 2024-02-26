// An rplace server software that is intended to be used completely remotely, being accessible fully through a web interface
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DataProto;
using HTTPOfficial;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using RplaceServer;
using WatsonWebsocket;
using System.IO.Compression;
using Microsoft.EntityFrameworkCore;

var configPath = Path.Combine(Directory.GetCurrentDirectory(), "server_config.json");
var instancesPath = Path.Combine(Directory.GetCurrentDirectory(), "Instances");
var dataPath = Path.Combine(Directory.GetCurrentDirectory(), "SaveData");
var instanceInfoPath = Path.Combine(instancesPath, "instance_info.json");

using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
ILogger logger = factory.CreateLogger("Program");

async Task CreateConfig()
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("[Warning]: Could not find server config file, at " + configPath);

    await using var configFile = File.OpenWrite(configPath);
    var defaultConfiguration =
        new Configuration(
            1234,
            false,
            "",
            "",
            "smtp.gmail.com",
            587,
            "myUsername@email.com",
            "myEmailPassword",
            new List<string>(),
            Guid.NewGuid().ToString(),
            "MY_REDDIT_API_APPLICATION_CLIENT_ID",
            "MY_REDDIT_API_APPLICATION_CLIENT_SECRET",
            true,
            "Posts",
            60,
            "https://rplace.live",
            8080,
            new Dictionary<AccountTier, int>
            {
                { AccountTier.Free, 2 },
                { AccountTier.Bronze, 5 },
                { AccountTier.Silver, 10 },
                { AccountTier.Gold, 25 },
                { AccountTier.Administrator, 512 }
            });
    await JsonSerializer.SerializeAsync(configFile, defaultConfiguration, new JsonSerializerOptions
    {
        WriteIndented = true
    });
    await configFile.FlushAsync();
    
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"[INFO]: Config files recreated. Please check {Directory.GetCurrentDirectory()} and run this program again.");
    Console.ResetColor();
    Environment.Exit(0);
}

static string HashSha256String(string rawData)
{
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
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
if (!Directory.Exists(instancesPath))
{
    Directory.CreateDirectory(instancesPath);
}
if (!Directory.Exists(dataPath))
{
    Directory.CreateDirectory(dataPath);
}

var defaultGameData = new GameData(
    5000,
    2500,
    false,
    true,
    1000,
    1000,
    600000, 
    false,
    "Canvases",
    300000,
    true,
    "Resources",
    "SaveData",
    true,
    "",
    "",
    null);

InstancesInfo? instancesInfo;
if (!File.Exists(instanceInfoPath))
{
    instancesInfo = new InstancesInfo();
    File.WriteAllText(instanceInfoPath, JsonSerializer.Serialize<InstancesInfo>(instancesInfo));
}
else
{
    instancesInfo = JsonSerializer.Deserialize<InstancesInfo>(File.ReadAllText(instanceInfoPath));
}
if (instancesInfo == null)
{
    logger.LogError("Server fail - Error loading instances info");
    Environment.Exit(0);
}

// Email and trusted domains
var emailAttributes = new EmailAddressAttribute();
var trustedEmailDomains = File.ReadAllLines("trusted_domains.txt")
    .Where(entry => !string.IsNullOrWhiteSpace(entry) && entry.TrimStart().First() != '#').ToList();
var config = await JsonSerializer.DeserializeAsync<Configuration>(File.OpenRead(configPath));

// EFCore Sqlite3 Database
using var database = new DatabaseContext();        
database.Database.EnsureCreated();

// Main server
var wsServer = new WatsonWsServer(config.Port, config.UseHttps, config.CertPath, config.KeyPath);

// Post server
var postsServer = new PostsServer(config, database);

// Vanity -> URL of actual socket server & board, done by worker clients on startup
var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "web:Rplace.Tk AuthServer v1.0 (by Zekiah-A)");

// Used by worker servers + async communication
var registeredVanities = new Dictionary<string, string>();
var workerClients = new Dictionary<ClientMetadata, WorkerInfo>();
var workerRequestQueue = new ConcurrentDictionary<int, TaskCompletionSource<byte[]>>(); // ID, Data callback
var workerRequestId = 0;

// Auth
var authorisedClients = new Dictionary<ClientMetadata, string>();

// Used by reddit auth
var refreshTokenAuthDates = new Dictionary<string, DateTime>();
var refreshTokenAccessTokens = new Dictionary<string, string>();

// Used by normal accounts
var redditSerialiserOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true, 
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
};
var random = new Random();
var emojis = new[]
{
    "😀", "😁", "😂", "🤣", "😃", "😄", "😅", "😆", "😉", "😊", "😋", "😎", "😍", "😘", "😗", "😙", "😚", "☺️", "🙂",
    "🤗", "🤔", "😐", "😑", "😶", "🙄", "😏", "😣", "😥", "😮", "🤐", "😯", "😪", "😫", "😴", "😌", "😛", "😜", "😝",
    "🤤", "😒", "😓", "😔", "😕", "🙃", "🤑", "😲", "☹️", "🙁", "😖", "😞", "😟", "😤", "😢", "😭", "😦", "😧", "😨",
    "😩", "🤯", "😬", "😰", "😱", "🥵", "🥶", "😳", "🤪", "😵", "🥴", "😷", "🤕", "🤒", "🤮", "🤢", "🥳", "🥺", "👋",
    "🤚", "🖐️", "✋", "🖖", "👌", "🤏", "✌️", "🤞", "🤟", "🤘", "🤙", "👈", "👉", "👆", "👇", "☝️", "👍", "👎", "✊",
    "🤛", "🤜", "👏", "🙌", "👐", "🤲", "🤝", "🙏", "💪", "🦾", "🦿", "🦵", "🦶", "👂", "🦻", "👃", "🧠", "🦷", "🦴",
    "👀", "👁️", "👅", "👄", "💋", "🩸", "😎", "🤖", "🗣", "🔥", "🏠", "🤡", "👾", "👋", "💩", "⚽", "👅", "🧠", "🕶",
    "🌳", "🌍", "🌈", "🎅", "👶", "👼", "🥖", "🍆", "🎮", "🎳", "🚢", "🗿", "📱", "🔑", "❤", "👺", "🤯", "🤬", "📱",
    "🦩", "🍔", "🎬", "🚨", "⚡️", "🍪", "🕋", "🎉", "📋", "🚦", "🔇", "🥶", "💼", "🎩", "🎒", "🦅", "🧊", "✅", "🌱", 
    "😂", "😍", "🚀", "😈", "👟", "🍷", "🚜", "🐥", "🔍", "🎹", "🚻", "🚗", "🏁", "🥚", "🔪", "🍕", "🐑", "😷", "🏀",
    "🏀", "🤮", "💂", "📎", "🎄", "🕯️", "🔔", "⛪", "🍷", "🎁", "🩸", "👊", 
};
var emailAuthCompletions = new Dictionary<string, EmailAuthCompletion>();

async Task<AccountData?> AuthenticateNameEmail(string name, string email)
{
    // We get their account data using their name to prove that the email they provided is actually for this account
    var accountData = await database.Accounts.FirstOrDefaultAsync(account => account.Username == name && account.Email == email);
    if (accountData is null)
    {
        logger.LogWarning("Account data was null or email provided was invalid, could not authenticate account login.");
        return null;
    }
    
    // Otherwise, we have to send them an email so that they can reverify their account and get a new account token
    var codeChars = new string[10];
    for (var i = 0; i < 10; i++)
    {
        codeChars[i] = emojis[random.Next(0, emojis.Length - 1)];
    }
    var authCode = string.Join("", codeChars);
    var emailCompletionSource = new TaskCompletionSource<bool>();
    emailAuthCompletions.Add(authCode, new EmailAuthCompletion(emailCompletionSource, DateTime.Now));
    
    var message = new MimeMessage();
    message.From.Add(new MailboxAddress(config.EmailUsername, config.EmailUsername));
    message.To.Add(new MailboxAddress(email, email));
    message.Subject = "rplace.live Account Code";
    message.Body = new TextPart("html")
    {
        Text = $"""
            <div style="background-color: #f0f0f0;font-family: 'IBM Plex Sans', sans-serif;">
                <h1 style="background: orangered;color: white;">Hello </h1>
                <div style="margin: 8px;">
                    <p>Here's your rplace authentication code. Enter it on the site to finish logging in.</p>
                    <h1 style="background-color: #13131314;display: inline;padding: 4px;border-radius: 4px;">{authCode}</h1>
                    <p>Be quick, this code will expire in 10 minutes!</p>
                    <img src="https://raw.githubusercontent.com/rslashplace2/rslashplace2.github.io/main/images/rplace.png">
                    <p style="opacity: 0.6;">Email sent at {DateTime.Now} | Feel free to reply |
                    <a href="https://rplace.live" style="text-decoration: none;">https://rplace.live</a></p>
                </div>
            </div>
            """
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
        logger.LogWarning("Could not send email message: " + exception);
        return null;
    }

    // This task completes when the client provides the correct email account code, see ClientPackets.AccountCode,
    // once a correct email auth code is provided, we will finally allow them to access their account data.
    var success = await emailCompletionSource.Task;
    emailAuthCompletions.Remove(authCode);
    return success ? accountData : null;
}

async Task<AccountData?> AuthenticateReddit(string refreshToken)
{
    var accessToken = await GetOrUpdateRedditAccessToken(refreshToken);
    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    var meResponse = await httpClient.GetAsync("https://oauth.reddit.com/api/v1/me");
    var meData = await meResponse.Content.ReadFromJsonAsync<RedditMeResponse>(redditSerialiserOptions);
    httpClient.DefaultRequestHeaders.Authorization = null;
    if (!meResponse.IsSuccessStatusCode || meData is null)
    {
        logger.LogWarning("Could not request me data for authentication (reason " + meResponse.ReasonPhrase + ")");
        return null;
    }
    
    var accountData = database.FirstOrDefaultAsync(account => account.RedditId == meData.Id);
    return accountData;
}

async Task<string?> GetOrUpdateRedditAccessToken(string refreshToken)
{
    // If we already have their auth token cached,and it is within date, then we just return that
    if (refreshTokenAuthDates.TryGetValue(refreshToken, out var expiryDate) && expiryDate - DateTime.Now <= TimeSpan.FromHours(1))
    {
        return refreshTokenAccessTokens[refreshToken];
    }
    
    // Otherwise, we need to refresh their auth token and update our caches respectively
    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.RedditAuthClientId}:{config.RedditAuthClientSecret}")));
    var contentPayload = new FormUrlEncodedContent(new Dictionary<string, string>
    {
        { "grant_type", "refresh_token" },
        { "refresh_token", refreshToken }
    });
        
    var tokenResponse = await httpClient.PostAsync("https://www.reddit.com/api/v1/access_token", contentPayload);
    var tokenData = await tokenResponse.Content.ReadFromJsonAsync<RedditTokenResponse>(redditSerialiserOptions);
    httpClient.DefaultRequestHeaders.Authorization = null;
    if (!tokenResponse.IsSuccessStatusCode || tokenData is null)
    {
        logger.LogWarning("Could not get or update access token, token response was non-positive (reason " 
                          + tokenResponse.ReasonPhrase + ")");
        return null;
    }
    
    refreshTokenAuthDates.Add(refreshToken, DateTime.Now);
    refreshTokenAccessTokens.Add(refreshToken, tokenData.AccessToken);
    return tokenData.AccessToken;
}

async Task RunPostAuthentication(AccountData accountData)
{
    var badge = new AccountBadge(Badge.Newbie, DateTime.Now);

    // If they have been on the site for 20+ days, we remove their noob badge
    var noobBadge = await database.Badges.FirstOrDefaultAsync(badge => badge.OwnerId == accountData.Id);
    if (noobBadge is not null && DateTime.Now - accountData.JoinDate >= TimeSpan.FromDays(20))
    {
        database.Badges.Remove(noobBadge);
        await accountsDb.UpdateRecord(accountData);
    }
    if (!accountData.Data.Badges.Contains(Badge.Veteran) &&
        DateTime.Now - accountData.JoinDate >= TimeSpan.FromDays(365))
    {
        accountData.Badges.Add(Badge.Veteran);
        await accountsDb.UpdateRecord(accountData);
    }

    await database.SaveChangessAsync();
}

async Task<bool> CheckValidEmail(string email)
{
    if (!emailAttributes.IsValid(email) || trustedEmailDomains.BinarySearch(email.Split("@").Last()) < 0)
    {
        return false;
    }

    return (await accountsDb.FindRecords<AccountData, string>(nameof(AccountData.Email), email)).Length == 0;
}

wsServer.MessageReceived += (_, args) =>
{
    var data = args.Data.ToArray();
    var readableData = new ReadablePacket(data);

    switch ((ClientPackets) readableData.ReadByte())
    {
        case ClientPackets.CreateAccount:
        {
            var username = readableData.ReadString();
            var email = readableData.ReadString();
            if (username.Length > 32 || email.Length > 320)
            {
                logger.LogInformation($"Rejected client create account for too long packet length (client {args.Client.IpPort})");
                return;
            }

            async Task CreateAccountAsync()
            {
                if (username.Length <= 4 || !await CheckValidEmail(email))
                {
                    return;
                }

                var codeChars = new string[3];
                for (var i = 0; i < 3; i++)
                {
                    codeChars[i] = emojis[random.Next(0, emojis.Length - 1)];
                }
                var authCode = string.Join("", codeChars);
                var emailCompletionSource = new TaskCompletionSource<bool>();
                emailAuthCompletions.Add(authCode, new EmailAuthCompletion(emailCompletionSource, DateTime.Now));
                var accountData = new AccountData(username, email, 0, new List<int>(),
                    "", "", "", 0, DateTime.Now, new List<Badge>(),
                    false, "");

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(config.EmailUsername, config.EmailUsername));
                message.To.Add(new MailboxAddress(email, email));
                message.Subject = "rplace.live Account Creation Code";
                message.Body = new TextPart("html")
                {
                    Text = $"""
                        <div style="background-color: #f0f0f0;font-family: 'IBM Plex Sans', sans-serif;">
                            <h1 style="background: orangered;color: white;">Hello </h1>
                            <div style="margin: 8px;">
                                <p>Someone used your email to register a new rplace.live account.</p>
                                <p>If that's you, then cool, your code is:</p>
                                <h1 style="background-color: #13131314;display: inline;padding: 4px;border-radius: 4px;"> {authCode} </h1>
                                <p>Otherwise, you can ignore this email, who cares anyway??</p>
                                <img src="https://raw.githubusercontent.com/rslashplace2/rslashplace2.github.io/main/images/rplace.png">
                                <p style="opacity: 0.6;">Email sent at {DateTime.Now} | Feel free to reply |
                                <a href="https://rplace.live" style="text-decoration: none;">https://rplace.live</a></p>
                            <div>
                        </div>
                        """
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
                    logger.LogWarning("Could not send email message: " + exception);
                }

                // This task completes when the client provides the correct email account code, see ClientPackets.AccountCode
                // once they have given the correct email authentication code, we finally commit saving and creating the account to disk.
                if (await emailCompletionSource.Task)
                {
                    emailAuthCompletions.Remove(authCode);
                    
                    accountData.Badges.Add(Badge.Newbie);
                    var accountKey = await accountsDb.CreateRecord(accountData);
                    authorisedClients.TryAdd(args.Client, accountKey);

                    // Send them their token and sign them in
                    var accountToken = RandomNumberGenerator.GetHexString(64);
                    accountTokenAccountKeys.Add(accountToken, accountData.Username);
                    
                    await wsServer.SendAsync(args.Client, CreateTokenPacket(accountToken));
                }
            }

            byte[] CreateTokenPacket(string accountToken)
            {
                var tokenPacket = new WriteablePacket();
                tokenPacket.WriteByte((byte) ServerPackets.AccountToken);
                tokenPacket.WriteString(accountToken);
                wsServer.SendAsync(args.Client, tokenPacket);
                return tokenPacket.ToArray();
            }

            Task.Run(CreateAccountAsync);
            break;
        }
        case  ClientPackets.AccountCode:
        {
            var code = readableData.ReadString();
            if (emailAuthCompletions.TryGetValue(code, out var completion))
            {
                completion.TaskSource.SetResult(completion.StartDate - DateTime.Now <= TimeSpan.FromMinutes(10));
            }
            break;
        }
        case ClientPackets.DeleteAccount:
        {
            async Task DeleteAccountAsync()
            {
                if (authorisedClients.TryGetValue(args.Client, out var accountKey))
                {
                    // TODO: Tell worker servers to delete all their instances belonging to this account
                    authorisedClients.Remove(args.Client);
                    await accountsDb.DeleteRecord<AccountData>(accountKey);
                }
            }

            Task.Run(DeleteAccountAsync);
            break;
        }
        case ClientPackets.AccountInfo:
        {
            if (authorisedClients.TryGetValue(args.Client, out var accountData))
            {
                var dataPacket = new WriteablePacket();
                dataPacket.WriteByte((byte) ServerPackets.AccountInfo);
                dataPacket.WriteString(JsonSerializer.Serialize(accountData));
                wsServer.SendAsync(args.Client, dataPacket);
            }
            break;
        }
        case ClientPackets.Authenticate:
        {
            switch ((AuthType) readableData.ReadByte())
            {
                // Name email will cause them to have to email reauthenticate
                case AuthType.NameEmail:
                {
                    var name = readableData.ReadString();
                    var email = readableData.ReadString();
                
                    var accountData = AuthenticateNameEmail(name, email).GetAwaiter().GetResult();
                    if (accountData is not null)
                    {
                        _ = RunPostAuthentication(accountData);

                        // We give them a new token to use next time that they want to log in, so that they do not need to
                        // reauthenticate their email.
                        authorisedClients.TryAdd(args.Client, accountData.MasterKey);
                        var newToken = RandomNumberGenerator.GetHexString(64);
                        accountTokenAccountKeys.Add(newToken, accountData.MasterKey);

                        var tokenPacket = new WriteablePacket();
                        tokenPacket.WriteByte((byte) ServerPackets.AccountToken);
                        tokenPacket.WriteString(newToken);
                        wsServer.SendAsync(args.Client, tokenPacket);
                    }
                    break;
                }
                // If they have a normal account token in localstorage, then they can login without email revalidation
                case AuthType.NormalToken:
                {
                    var normalToken = readableData.ReadString();
                    // Either text will be invalid, or name and email
                    var accountData = AuthenticateToken(normalToken).GetAwaiter().GetResult();
                    if (accountData is not null)
                    {
                        _ = RunPostAuthentication(accountData);

                        // Regardless of if they are automatically logging in with account token, or logging in for the first
                        // time on that device with an email code, we make sure to invalidate their previous token and give 
                        // them a new one after every authentication. 
                        accountTokenAccountKeys.Remove(normalToken);
                        authorisedClients.TryAdd(args.Client, accountData.MasterKey);
                        var newToken = RandomNumberGenerator.GetHexString(64);
                        accountTokenAccountKeys.Add(newToken, accountData.MasterKey);
                        
                        var tokenPacket = new WriteablePacket();
                        tokenPacket.WriteByte((byte) ServerPackets.AccountToken);
                        tokenPacket.WriteString(newToken);
                        wsServer.SendAsync(args.Client, tokenPacket);
                    }
                    break;
                }
                // If they already have a RefreshToken in localstorage, then they can simply authenticate and login
                case AuthType.Reddit:
                {
                    var refreshToken = readableData.ReadString();
                    var accountData = AuthenticateReddit(refreshToken).GetAwaiter().GetResult();
                    if (accountData is not null)
                    {
                        _ = RunPostAuthentication(accountData);
                    
                        // Add them to server authenticated client memory so they do not have to authenticate each server API call
                        authorisedClients.TryAdd(args.Client, accountData.MasterKey);
                    }
                    break;
                }
            }
            break;
        }
        case ClientPackets.ResolveVanity:
        {
            var vanity = readableData.ReadString();
            if (registeredVanities.TryGetValue(vanity, out var urlResult))
            {
                var buffer = Encoding.UTF8.GetBytes("X" + urlResult);
                buffer[0] = (byte) ServerPackets.VanityLocation;
                wsServer.SendAsync(args.Client, urlResult);
            }
            break;
        }
        case ClientPackets.VanityAvailable:
        {
            var buffer = new[]
            {
                (byte) ServerPackets.AvailableVanity,
                (byte) (registeredVanities.ContainsKey(readableData.ReadString()) ? 0 : 1)
            };
            wsServer.SendAsync(args.Client, buffer);
            break;
        }
        // Will create an account if doesn't exist, or allow a user to get the refresh token of their account & authenticate
        // if they already had an account, but were not OAuthed on that specific device (did not have RefreshToken in localStorage).
        case ClientPackets.RedditCreateAccount:
        {
            var accountCode = readableData.ReadString();
            
            // We to exchange this with an access token so we can execute API calls with this user, such as fetching their
            // unique ID (/me) endpoint, anc checking if we already have it saved (then they already have an account,
            // and we can fetch account data, else, we create account data for this user with data we can scrape from the API).
            async Task ExchangeAccessTokenAsync()
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.RedditAuthClientId}:{config.RedditAuthClientSecret}")));
                
                var contentPayload = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "grant_type", "authorization_code" },
                    { "code", accountCode },
                    { "redirect_uri", "https://rplace.live/" }
                });
                var tokenResponse = await httpClient.PostAsync("https://www.reddit.com/api/v1/access_token", contentPayload);
                var tokenData = await tokenResponse.Content.ReadFromJsonAsync<RedditTokenResponse>(redditSerialiserOptions);
                // We need to make ultra sure this auth will never be sent to someone else
                httpClient.DefaultRequestHeaders.Authorization = null;
                if (!tokenResponse.IsSuccessStatusCode || tokenData is null )
                {
                    logger.LogWarning("Client create account rejected for failed access taken retrieval (reason" + tokenResponse.ReasonPhrase + ")");
                    return;
                }
                
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenData.AccessToken);
                var meResponse = await httpClient.GetAsync("https://oauth.reddit.com/api/v1/me");
                var meData = await meResponse.Content.ReadFromJsonAsync<RedditMeResponse>(redditSerialiserOptions);
                httpClient.DefaultRequestHeaders.Authorization = null;
                if (!meResponse.IsSuccessStatusCode || meData is null)
                {
                    logger.LogWarning("Client create account rejected for null me API response (reason " + tokenResponse.ReasonPhrase + ")");
                    return;
                }

                
                // Create their account, first check no other account with that ID is signed up
                if ((await accountsDb.FindRecords<AccountData, string>(nameof(AccountData.RedditId), meData.Id)).Length == 0)
                {
                    // Create new accountData for this client
                    var accountData = new AccountData(meData.Name, "", 0, new List<int>(),
                        "", "", meData.Name, 0, DateTime.Now, new List<Badge>(), true, meData.Id);
                    accountData.Badges.Add(Badge.Newbie);

                    var accountKey = await accountsDb.CreateRecord(accountData);
                    authorisedClients.TryAdd(args.Client, accountKey);
                    refreshTokenAuthDates.Add(tokenData.RefreshToken, DateTime.Now);
                    refreshTokenAccessTokens.Add(tokenData.RefreshToken, tokenData.AccessToken);
                }
                
                {
                    // If they already have an account, we can simply authenticate them
                    var accountData = await AuthenticateReddit(tokenData.RefreshToken);
                    if (accountData is not null)
                    {
                        await RunPostAuthentication(accountData);
                        authorisedClients.TryAdd(args.Client, accountData.MasterKey);
                        
                        // Send them their token so that they can quickly login again without having to reauthenticate
                        var tokenBuffer = Encoding.UTF8.GetBytes("X" + tokenData.RefreshToken);
                        tokenBuffer[0] = (byte) ServerPackets.RedditRefreshToken;
                        await wsServer.SendAsync(args.Client, tokenBuffer);
                        logger.LogInformation($"Successfully updated refresh token for {meData.Name} (client {args.Client.IpPort})");
                    }
                    
                    logger.LogTrace($"Client create account for {meData.Name} succeeded (client {args.Client.IpPort})");
                }
            }

            Task.Run(ExchangeAccessTokenAsync);
            break;
        }
        case ClientPackets.UpdateProfile:
        {
            async Task UpdateProfileAsync()
            {
                if (!authorisedClients.TryGetValue(args.Client, out var accountKey)
                    || await accountsDb.GetRecord<AccountData>(accountKey) is not { } accountData)
                {
                    return;
                }

                switch (data[0])
                {
                    case (byte)PublicEditableData.Username:
                    {
                        var input = Encoding.UTF8.GetString(data[1..]);
                        if (input.Length is < 0 or > 32)
                        {
                            return;
                        }

                        accountData.Data.Username = input;
                        break;
                    }
                    case (byte)PublicEditableData.DiscordId:
                    {
                        //TODO: Validate
                        var snowflake = BinaryPrimitives.ReadInt64BigEndian(data[1..]);
                        accountData.Data.DiscordId = snowflake.ToString();
                        break;
                    }
                    case (byte)PublicEditableData.TwitterHandle:
                    {
                        var input = Encoding.UTF8.GetString(data[1..]);
                        if (!TwitterHandleRegex().IsMatch(input))
                        {
                            return;
                        }

                        accountData.Data.TwitterHandle = input;
                        break;
                    }
                    case (byte)PublicEditableData.RedditHandle:
                    {
                        var input = Encoding.UTF8.GetString(data[1..]);
                        if (!RedditHandleRegex().IsMatch(input) || accountData.Data.UsesRedditAuthentication)
                        {
                            return;
                        }

                        accountData.Data.RedditHandle = input;
                        break;
                    }
                    case (byte)PublicEditableData.Badges:
                    {
                        if ((Badge)data[1] != Badge.EthicalBotter || (Badge)data[1] != Badge.Gay
                                                                  || (Badge)data[1] != Badge.ScriptKiddie)
                        {
                            return;
                        }

                        if (data[0] == 1)
                        {
                            accountData.Data.Badges.Add((Badge)data[1]);
                        }
                        else
                        {
                            accountData.Data.Badges.Remove((Badge)data[1]);
                        }

                        break;
                    }
                }

                await accountsDb.UpdateRecord(accountData);
            }

            Task.Run(UpdateProfileAsync);
            break;
        }
        case ClientPackets.ProfileInfo:
        {
            async Task<AccountData?> GetProfileInfoAsync(string accountKey)
            {
                return await accountsDb.GetRecord<AccountData>(accountKey);
            }
            
            if (GetProfileInfoAsync(readableData.ReadString()).GetAwaiter().GetResult() is {} accountData)
            {
                var responsePacket = new WriteablePacket();
                responsePacket.WriteByte((byte) ServerPackets.AccountProfile);
                responsePacket.WriteString(JsonSerializer.Serialize<AccountProfile>(accountData.Data));
            }
            break;
        }
        case ClientPackets.CreateInstance:
        {
            async Task<AccountData?> AuthenticateClientAsync()
            {
                if (!authorisedClients.TryGetValue(args.Client, out var accountKey))
                {
                    return null;
                }

                return await accountsDb.GetRecord<AccountData>(accountKey);
            }

            var accountData = AuthenticateClientAsync().GetAwaiter().GetResult();
            if (accountData is null)
            {
                return;
            }

            if (!config.AccountTierInstanceLimits.TryGetValue(accountData.Data.Tier, out var limit)
                || accountData.Data.Instances.Count > limit)
            {
                return;
            }

            // We hold onto this as it will be needed to be hooked up to an actual ip/location once a hoster is found
            var vanityName = readableData.ReadString();
            var cooldown = readableData.ReadUInt();
            var width = readableData.ReadUInt();
            var height = readableData.ReadUInt();
            var hasImage = readableData.ReadBool();
            if (hasImage)
            {
                var fromImage = readableData.ReadByteArray(); // TODO: Decode into default board
            }

            
            // Generate ID for this instance, save it so auth server knows about this instance
            var id = instancesInfo.Ids.Count == 0 ? 0 : instancesInfo.Ids.Max() + 1;
            instancesInfo.Ids.Add(id);
            //SaveInstancesInfo();

            // Set up directory that will be used to hold replication instance data + it's configuration
            var instanceDirectory = Path.Join(instancesPath, id.ToString());
            Directory.CreateDirectory(instanceDirectory);

            // Set up the new instance server software data files
            var instanceInfo = new InstanceInfo(id, DateTime.Now);
            var gameData = defaultGameData with { BoardWidth = width, BoardHeight = height, Cooldown = cooldown };
            File.WriteAllText(Path.Combine(instanceDirectory, "game_data.json"), JsonSerializer.Serialize(defaultGameData));
            File.WriteAllText(Path.Combine(instanceDirectory, "server_data.json"), JsonSerializer.Serialize(instanceInfo));
            Directory.CreateDirectory(Path.Combine(instanceDirectory, "Canvases"));
            File.Create(Path.Combine(instanceDirectory, "Canvases", "backuplist.txt"));
            
            // We need to find a worker server capable of hosting, then replicate to it, and tell it to start
            var responseSuccess = false;
            foreach (var workerPair in workerClients)
            {
                var queryReqId = workerRequestId++;
                var query = new WriteablePacket();
                var requestCompletionSource = new TaskCompletionSource<byte[]>();
                query.WriteByte((byte) ServerPackets.QueryCanCreate);
                query.WriteUInt((uint) queryReqId);
                
                workerRequestQueue.TryAdd(queryReqId, requestCompletionSource);
                wsServer.SendAsync(workerPair.Key, query);  // Blocking, v
                var queryResponse = new ReadablePacket(requestCompletionSource.Task.GetAwaiter().GetResult());
                workerRequestQueue.TryRemove(queryReqId, out var _);
                
                
                if (!responseSuccess && queryResponse.ReadBool())
                {
                    responseSuccess = true;
                    var packet = new WriteablePacket();
                    packet.WriteByte((byte) ServerPackets.SyncInstance);
                    packet.WriteInt(id);

                    var zipSyncDirectory = Path.Join(instancesPath, id.ToString(), ".tmp.zip");
                    ZipFile.CreateFromDirectory(instanceDirectory, zipSyncDirectory);
                    //packet.WriteBytes(File.ReadAllBytes(zipSyncDirectory));
                }
            }
            break;
        }

        /*
        // A worker server has joined the network. It now has to tell the auth server it exists, and prove that it is
        // a legitimate worker using the network instance key so that it will be allowed to carry out actions. 
        case (byte) WorkerPackets.AnnounceExistence:
        {
            if (data.Length < 8)
            {
                return;
            }
            
            var idRangeStart = BinaryPrimitives.ReadInt32BigEndian(data);
            var idRangeEnd = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
            var instanceKeyAddress = Encoding.UTF8.GetString(data[8..]).Split("\n");
            if (instanceKeyAddress.Length != 2) // 0 - Instance key, 1 - public address of worker socket
            {
                return;
            }
            
            // If it is a legitimate worker wanting to join the network, we include it so that it can be announced to clients
            if (instanceKeyAddress[0].Equals(config.InstanceKey))
            {
                config.KnownWorkers.Add(args.Client.IpPort);
                workerClients.Add(args.Client, new WorkerInfo(new IntRange(idRangeStart, idRangeEnd), instanceKeyAddress[1]));
            }
            break;
        }
        // Worker server has just started up and booted it's instances, it sees that some of it's instances have
        // previously registered vanities, and now needs to announce them onto the auth server with whatever new
        // URLS those instances have.
        case (byte) WorkerPackets.AnnounceVanity:
        {
            if (!workerClients.ContainsKey(args.Client))
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
            if (!workerClients.ContainsKey(args.Client))
            {
                return;
            }
            
            var responseBuffer = new byte[6];
            responseBuffer[0] = (byte) ServerPackets.Authorised; // Sign the packet with the correct auth

            var authLength = data[0];
            var marshalledData = data.ToArray();
            Buffer.BlockCopy(marshalledData, 2 + authLength, responseBuffer, 1, 4); // Copy over the request ID

            async Task CheckAuthAsync()
            {
                var token = Encoding.UTF8.GetString(marshalledData[2..(authLength + 2)]);

                var accountData = (AuthType)marshalledData[1] switch
                {
                    AuthType.Normal => await Authenticate(token, null, null),
                    AuthType.Reddit => await RedditAuthenticate(token),
                    _ => null
                };

                if (accountData is null)
                {
                    responseBuffer[5] = 0; // Failed to authenticate
                    await server.SendAsync(args.Client, responseBuffer);
                    return;
                }
                
                var instanceId = BinaryPrimitives.ReadInt32BigEndian(marshalledData.AsSpan()[(authLength + 6)..]); // RequestID start (int) + 4

                if (!accountData.Instances.Contains(instanceId))
                {
                    responseBuffer[5] = 0; // Failed to authenticate
                    await server.SendAsync(args.Client, responseBuffer);
                }

                switch (packetCode)
                {
                    // A client has just asked the worker server to create an instance, the worker server then checks with the auth server
                    // whether they are actually allowed to delete this instance, if so, the auth server must also change the account data
                    // of the client, removing this instance ID from the client's instances list to ensure it is synchronised with the worker.
                    case (byte) WorkerPackets.AuthenticateCreate:
                    {
                        // Reject - Client is not allowed more than 2 canvases on free plan
                        if (accountData is { AccountTier: 0, Instances.Count: >= 1 })
                        {
                            responseBuffer[5] = 0; // Failed to authenticate
                            await server.SendAsync(args.Client, responseBuffer);
                            return;
                        }
                        
                        // Accept -  We add this instance to their account data, save the account data and send back the response
                        accountData.Instances.Add(instanceId);
                        responseBuffer[5] = 1; // Successfully authenticated
                        await server.SendAsync(args.Client, responseBuffer);
                        File.WriteAllText(Path.Join(dataPath, accountData.Username), JsonSerializer.Serialize(accountData));
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
                        await server.SendAsync(args.Client, responseBuffer);
                        File.WriteAllText(Path.Join(dataPath, accountData.Username), JsonSerializer.Serialize(accountData));
                        break;
                    }
                    // A client has just asked the worker server to modify, subscribe to, or have some other kind of access to a
                    // private part of an instance, however, it does not involve modifying the client's data unlike AuthenticateDelete
                    // or AuthenticateCreate, the auth server only ensures that the client owns this instance that they claim they want to do something with.
                    case (byte) WorkerPackets.AuthenticateManage:
                    {
                        // Accept - this is a general manage server authentication, so we don't need to touch account data
                        responseBuffer[5] = 1; // Failed to authenticate
                        await server.SendAsync(args.Client, responseBuffer);
                        break;
                    }
                    // A client has requested to apply a new vanity to an instance. The auth server must now prove that the client
                    // in fact owns that vanity, that this vanity name has not already been registered, and if so, we register this
                    // vanity to the URL of the instance.
                    case (byte) WorkerPackets.AuthenticateVanity:
                    {
                        var text = Encoding.UTF8.GetString(marshalledData.AsSpan()[(authLength + 6)..]).Split("\n");

                        if (text.Length != 2 || registeredVanities.TryGetValue(text[0], out var _))
                        {
                            responseBuffer[5] = 0; // vanity with specified name already exists
                            await server.SendAsync(args.Client, responseBuffer);
                        }

                        // Accept - Register vanity
                        registeredVanities.Add(text[0], text[1]);
                        responseBuffer[5] = 1; 
                        await server.SendAsync(args.Client, responseBuffer);
                        break;
                    }
                }
            }

            Task.Run(CheckAuthAsync);
            break;
        }
        */
    }
};
wsServer.ClientDisconnected += (_, args) =>
{
    authorisedClients.Remove(args.Client);
};

Console.CancelKeyPress += async (_, _) =>
{
    await wsServer.StopAsync();
    Environment.Exit(0);
};
AppDomain.CurrentDomain.UnhandledException += (_, exceptionEventArgs) =>
{
    logger.LogError("Unhandled server exception: " + exceptionEventArgs.ExceptionObject);
};
var expiredAccountCodeTimer = new System.Timers.Timer(TimeSpan.FromMinutes(10))
{
    Enabled = true,
    AutoReset = true
};
expiredAccountCodeTimer.Elapsed += (_, _) =>
{
    foreach (var completion in emailAuthCompletions)
    {
        if (DateTime.Now - completion.Value.StartDate >= TimeSpan.FromMinutes(10))
        {
            emailAuthCompletions.Remove(completion.Key);
        }
    }
};
    
logger.LogInformation("Server listening on port {config}", config.Port);
wsServer.Logger = message => logger.LogInformation("[SocketServer]: {message}", message);
postsServer.Logger = message => logger.LogInformation("[PostsServer]: {message}", message);
await Task.WhenAll(postsServer.StartAsync(), wsServer.StartAsync());
await Task.Delay(-1);

internal partial class Program
{
    [GeneratedRegex(@"^.{3,32}#[0-9]{4}$")]
    private static partial Regex TwitterHandleRegex();
    [GeneratedRegex(@"^(/ua/)?[A-Za-z0-9_-]+$")]
    private static partial Regex RedditHandleRegex();
}
