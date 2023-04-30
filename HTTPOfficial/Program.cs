// An rplace server software that is intended to be used completely remotely, being accessible fully through a web interface

using System.Buffers.Binary;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HTTPOfficial;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using WatsonWebsocket;

const string alphanumerics = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
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
            Guid.NewGuid().ToString(),
            "MY_REDDIT_API_APPLICATION_CLIENT_ID",
            "MY_REDDIT_API_APPLICATION_CLIENT_SECRET",
            true);
    await JsonSerializer.SerializeAsync(configFile, defaultConfiguration, new JsonSerializerOptions { WriteIndented = true });
    await configFile.FlushAsync();
    
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"\n[INFO]: Config files recreated. Please check {Directory.GetCurrentDirectory()} and run this program again.");
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

// Vanity -> URL of  actual socket server & board, done by worker clients on startup
var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "web:Rplace.Tk AuthServer v1.0 (by zekiahepic)");
// Used by worker servers
var registeredVanities = new Dictionary<string, string>();
var workerClients = new Dictionary<ClientMetadata, WorkerInfo>();
var clientAccountDatas = new Dictionary<ClientMetadata, AccountData>();

// Used by reddit auth
var refreshTokenAuthDates = new Dictionary<string, DateTime>();
var refreshTokenAccessTokens = new Dictionary<string, string>();

// Used by normal normal accounts
var accountTokenAccountNames = new Dictionary<string, string>();
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

void InvokeLogger(string message)
{
    if (config.Logger)
    {
        Console.WriteLine("[WebServer " + DateTime.Now.ToString("hh:mm:ss") + "]: " + message);
    }
}

var emailAuthCompletions = new Dictionary<string, EmailAuthCompletion>();
// Either accountToken is valid, which allows immediate authentication, or if accountToken is null/invalid, while name and email
// are valid, the user will be prompted to use slower email authentication with an email code.
async Task<AccountData?> Authenticate(string? accountToken, string? name, string? email)
{
    // If they already have a valid token, we can do a quick and simple auth
    if (accountToken is not null && accountTokenAccountNames.TryGetValue(accountToken, out var accountName) && File.Exists(Path.Join(dataPath, accountName)))
    {
        return JsonSerializer.Deserialize<AccountData>(File.ReadAllText(Path.Join(dataPath, accountName)));
    }
    
    // We pre-deserialise their account data using their name to prove that the email they provided is even for this account
    var accountPath = Path.Join(dataPath, name);
    var accountData = File.Exists(accountPath)
        ? JsonSerializer.Deserialize<AccountData>(File.ReadAllText(accountPath))
        : null;
    if (accountData is null || !accountData.Email.Equals(email))
    {
        InvokeLogger("Account data was null or email provided was invalid, could not authenticate account login.");
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
    message.Subject = "rplace.tk Account Code";
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
                    <a href="https://rplace.tk" style="text-decoration: none;">https://rplace.tk</a></p>
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
        InvokeLogger("Could not send email message: " + exception);
        return null;
    }

    // This task completes when the client provides the correct email account code, see ClientPackets.AccountCode,
    // once a correct email auth code is provided, we will finally allow them to acess their account data.
    return await emailCompletionSource.Task ? accountData : null;
}

async Task<AccountData?> RedditAuthenticate(string refreshToken)
{
    var accessToken = await GetOrUpdateRedditAccessToken(refreshToken);
    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    var meResponse = await httpClient.GetAsync("https://oauth.reddit.com/api/v1/me");
    var meData = await meResponse.Content.ReadFromJsonAsync<RedditMeResponse>(redditSerialiserOptions);
    httpClient.DefaultRequestHeaders.Authorization = null;
    if (!meResponse.IsSuccessStatusCode || meData is null)
    {
        InvokeLogger("Could not request me data for authentication, reason: " + meResponse.ReasonPhrase);
        return null;
    }
    
    var accountPath = Path.Join(dataPath, meData.Id);
    return File.Exists(accountPath)
        ? JsonSerializer.Deserialize<AccountData>(File.ReadAllText(accountPath))
        : null;
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
        InvokeLogger("Could not get or update access token, token response was non-positive: " + tokenResponse.ReasonPhrase);
        return null;
    }
    
    refreshTokenAuthDates.Add(refreshToken, DateTime.Now);
    refreshTokenAccessTokens.Add(refreshToken, tokenData.AccessToken);
    return tokenData.AccessToken;
}

server.MessageReceived += (_, args) =>
{
    var packetCode = args.Data.ToArray()[0];
    var data = new Span<byte>(args.Data.ToArray()[1..]);

    switch (packetCode)
    {
        case (byte) ClientPackets.CreateAccount:
        {
            if (data.Length < 352)
            {
                InvokeLogger($"Rejecting account creation from client {args.Client} due to invalid packet length");
                return;
            }
            
            var stringData = Encoding.UTF8.GetString(data);
            var username = stringData[..32].TrimEnd();
            var email = stringData[32..352].TrimEnd();
            if (username.Length <= 4 || !emailAttributes.IsValid(email))
            {
                var response = "XCould not create account. Invalid information provided!"u8.ToArray();
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
            var emailCompletionSource = new TaskCompletionSource<bool>();
            emailAuthCompletions.Add(authCode, new EmailAuthCompletion(emailCompletionSource, DateTime.Now));
            var accountData = new AccountData(username, email, 0, new List<int>(),
                "", "", "", 0, DateTime.Now, new List<Badge>(), false, "");
            clientAccountDatas.TryAdd(args.Client, accountData);

            async Task SendCodeEmailAsync()
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(config.EmailUsername, config.EmailUsername));
                message.To.Add(new MailboxAddress(email, email));
                message.Subject = "rplace.tk Account Code";
                message.Body = new TextPart("html")
                {
                    Text = $"""
                        <div style="background-color: #f0f0f0;font-family: 'IBM Plex Sans', sans-serif;">
                            <h1 style="background: orangered;color: white;">Hello </h1>
                            <div style="margin: 8px;">
                                <p>Someone used your email to register a new rplace account.</p>
                                <p>If that's you, then cool, your code is:</p>
                                <h1 style="background-color: #13131314;display: inline;padding: 4px;border-radius: 4px;"> {authCode} </h1>
                                <p>Otherwise, you can ignore this email, who cares anyway??</p>
                                <img src="https://raw.githubusercontent.com/rslashplace2/rslashplace2.github.io/main/images/rplace.png">
                                <p style="opacity: 0.6;">Email sent at {DateTime.Now} | Feel free to reply |
                                <a href="https://rplace.tk" style="text-decoration: none;">https://rplace.tk</a></p>
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
                    InvokeLogger("Could not send email message: " + exception);
                }

                // This task completes when the client provides the correct email account code, see ClientPackets.AccountCode
                // once they have given the correct email authentication code, we finally commit saving and creating the account to disk.
                if (await emailCompletionSource.Task)
                {
                    File.WriteAllText(Path.Join(dataPath, accountData.Username), JsonSerializer.Serialize(accountData));
                }
            }
            
            Task.Run(SendCodeEmailAsync);
            break;
        }
        case (byte) ClientPackets.AccountCode:
        {
            var code = Encoding.UTF8.GetString(data);
            if (emailAuthCompletions.TryGetValue(code, out var completion))
            {
                completion.TaskSource.SetResult(completion.StartDate - DateTime.Now <= TimeSpan.FromMinutes(10));
            }
            break;
        }
        case (byte) ClientPackets.DeleteAccount:
        {
            if (clientAccountDatas.TryGetValue(args.Client, out var accountData))
            {
                // TODO: Tell worker servers to delete all their instances
                clientAccountDatas.Remove(args.Client);
                File.Delete(Path.Join(dataPath, accountData.UsesRedditAuthentication
                    ? accountData.RedditId
                    : accountData.Username));
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
            var text = Encoding.UTF8.GetString(data);
            var name = text.Length >= 352 ? text[..32].Trim() : null;
            var email = text.Length >= 352 ? text[32..352].Trim() : null;

            async Task AuthenticateClientAsync()
            {
                // Either text will be invalid, or name and email
                var accountData = await Authenticate(text, name, email);
                if (accountData is not null)
                {
                    clientAccountDatas.TryAdd(args.Client, accountData);
                    var accountToken = RandomNumberGenerator.GetHexString(64);
                    accountTokenAccountNames.Add(accountToken, accountData.Username);
                    
                    var tokenBuffer = Encoding.UTF8.GetBytes("X" + accountToken);
                    tokenBuffer[0] = (byte) ServerPackets.AccountToken;
                    await server.SendAsync(args.Client, tokenBuffer);
                }
            }

            Task.Run(AuthenticateClientAsync);
            break;
        }
        case (byte) ClientPackets.LocateVanity:
        {
            var vanity = Encoding.UTF8.GetString(data[1..]);
            if (registeredVanities.TryGetValue(vanity, out var urlResult))
            {
                var buffer = Encoding.UTF8.GetBytes("X" + urlResult);
                buffer[0] = (byte) ServerPackets.VanityLocation;
                server.SendAsync(args.Client, urlResult);
            }
            break;
        }
        case (byte) ClientPackets.LocateWorkers:
        {
            var encoded = Encoding.UTF8.GetBytes("X" + JsonSerializer.Serialize(workerClients.Values));
            encoded[0] = (byte) ServerPackets.WorkerLocations;
            server.SendAsync(args.Client, encoded);
            break;
        }
        case (byte) ClientPackets.VanityAvailable:
        {
            var buffer = new[]
            {
                (byte) ServerPackets.AvailableVanity,
                (byte) (registeredVanities.ContainsKey(Encoding.UTF8.GetString(data)) ? 0 : 1)
            };
            server.SendAsync(args.Client, buffer);
            break;
        }
        // Will create an account if doesn't exist, or allow a user to get the refresh token of their account & authenticate
        // if they already had an account, but were not OAuthed on that specific device (did not have RefreshToken in localStorage).
        case (byte) ClientPackets.RedditCreateAccount:
        {
            var accountCode = Encoding.UTF8.GetString(data);
            
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
                    { "redirect_uri", "https://rplace.tk/" }
                });
                var tokenResponse = await httpClient.PostAsync("https://www.reddit.com/api/v1/access_token", contentPayload);
                var tokenData = await tokenResponse.Content.ReadFromJsonAsync<RedditTokenResponse>(redditSerialiserOptions);
                // We need to make ultra sure this auth will never be sent to someone else
                httpClient.DefaultRequestHeaders.Authorization = null;
                if (!tokenResponse.IsSuccessStatusCode || tokenData is null )
                {
                    InvokeLogger("Client create account rejected for failed access taken retrieval, reason: " + tokenResponse.ReasonPhrase);
                    return;
                }
                
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenData.AccessToken);
                var meResponse = await httpClient.GetAsync("https://oauth.reddit.com/api/v1/me");
                var meData = await meResponse.Content.ReadFromJsonAsync<RedditMeResponse>(redditSerialiserOptions);
                httpClient.DefaultRequestHeaders.Authorization = null;
                if (!meResponse.IsSuccessStatusCode || meData is null)
                {
                    InvokeLogger("Client create account rejected for null me API response, reason: " + tokenResponse.ReasonPhrase);
                    return;
                }
                
                var accountPath = Path.Join(dataPath, meData.Id);
                if (!File.Exists(accountPath))
                {
                    // Create new accountData for this client
                    var accountData = new AccountData(meData.Name, "", 0, new List<int>(),
                        "", "", meData.Name, 0, DateTime.Now, new List<Badge>(), true, meData.Id);
                    File.WriteAllText(accountPath, JsonSerializer.Serialize(accountData));
                    refreshTokenAuthDates.Add(tokenData.RefreshToken, DateTime.Now);
                    refreshTokenAccessTokens.Add(tokenData.RefreshToken, tokenData.AccessToken);
                }
                
                {
                    // If they already have an account, we can simply authenticate them
                    var accountData = await RedditAuthenticate(tokenData.RefreshToken);
                    if (accountData is not null)
                    {
                        clientAccountDatas.TryAdd(args.Client, accountData);
                        
                        var tokenBuffer = Encoding.UTF8.GetBytes("X" + tokenData.RefreshToken);
                        tokenBuffer[0] = (byte) ServerPackets.AccountToken;
                        await server.SendAsync(args.Client, tokenBuffer);
                        InvokeLogger($"Successfully updated refresh token for {meData.Name} (client {args.Client.IpPort})");
                    }
                    
                    InvokeLogger($"Client create account {meData.Name} succeeded for client: {args.Client.IpPort}");
                }
            }

            Task.Run(ExchangeAccessTokenAsync);
            break;
        }
        // If they already have a RefreshToken in localstorage, then they can simply authenticate and login
        case (byte) ClientPackets.RedditAuthenticate:
        {
            var refreshToken = Encoding.UTF8.GetString(data);

            async Task AuthenticateClientAsync()
            {
                var accountData = await RedditAuthenticate(refreshToken);
                if (accountData is not null)
                {
                    clientAccountDatas.TryAdd(args.Client, accountData);
                }
            }

            Task.Run(AuthenticateClientAsync);
            break;
        }
        case (byte) ClientPackets.UpdateProfile:
        {
            if (!clientAccountDatas.TryGetValue(args.Client, out var accountData))
            {
                return;
            }
            
            switch (data[0])
            {
                case (byte) PublicEditableData.Username:
                {
                    var input = Encoding.UTF8.GetString(data[1..]);
                    if (input.Length is < 0 or > 32)
                    {
                        return;
                    }

                    accountData.Username = input;
                    break;
                }
                case (byte) PublicEditableData.DiscordId:
                {
                    //TODO: Validate
                    var snowflake = BinaryPrimitives.ReadInt64BigEndian(data[1..]);
                    accountData.DiscordId = snowflake.ToString();
                    break;
                }
                case (byte) PublicEditableData.TwitterHandle:
                {
                    var input = Encoding.UTF8.GetString(data[1..]);
                    if (!TwitterHandleRegex().IsMatch(input))
                    {
                        return;
                    }

                    accountData.TwitterHandle = input;
                    break;
                }
                case (byte) PublicEditableData.RedditHandle:
                {
                    var input = Encoding.UTF8.GetString(data[1..]);
                    if (!RedditHandleRegex().IsMatch(input) || accountData.UsesRedditAuthentication)
                    {
                        return;
                    }

                    accountData.RedditHandle = input;
                    break;
                }
                case (byte) PublicEditableData.Badges:
                {
                    if ((Badge) data[1] is not Badge.EthicalBotter or Badge.Gay or Badge.ScriptKiddie)
                    {
                        return;
                    }
                    
                    if (data[0] == 1)
                    {
                        accountData.Badges.Add((Badge) data[1]);
                    }
                    else
                    {
                        accountData.Badges.Add((Badge) data[1]);
                    }
                    break;
                }
            }
            
            var accountPath = Path.Join(dataPath, accountData.UsesRedditAuthentication
                    ? accountData.RedditId
                    : accountData.Username);
            File.WriteAllText(accountPath, JsonSerializer.Serialize(accountData));
            break;
        }
        case (byte) ClientPackets.ProfileInfo:
        {
            var id = Encoding.UTF8.GetString(data);
            var accountPath = Path.Join(dataPath, id);
            if (File.Exists(accountPath))
            {
                var buffer = Encoding.UTF8.GetBytes("X" + JsonSerializer.Deserialize<AccountProfile>(File.ReadAllText(accountPath)));
                buffer[0] = (byte) ServerPackets.AccountProfile;
                server.SendAsync(args.Client, buffer);
            }
            break;
        }
        
        //TODO: BREAKING - In order to authenticate with different methods, such as via reddit oauth, standard login, or whatever is added
        //TODO: in the future, the packets should be structured as |(byte) authLength|(byte) authType (standard|reddit)|(n) authPayload|....
        //TODO: This is all broken, both reddit and normal accounts now use tokens. Switch to this eventually.
        // A worker server has joined the network. It now has to tell the auth server it exists, and prove that it is
        // a legitimate worker using the network instance key so that it will be allowed to carry out actions. 
        /*
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
                Task.Run(UpdateConfigAsync);
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
            if (!workerClients.ContainsKey(args.Client) || data.Length < 46)
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
            }

            switch (packetCode)
            {
                // A client has just asked the worker server to create an instance, the worker server then checks with the auth server
                // whether they are actually allowed to delete this instance, if so, the auth server must also change the account data
                // of the client, removing this instance ID from the client's instances list to ensure it is synchronised with the worker.
                case (byte) WorkerPackets.AuthenticateCreate:
                {
                    // Reject - Client is not allowed more than 2 canvases on free plan
                    if (accountData.AccountTier == 0 && accountData.Instances.Count >= 2)
                    {
                        responseBuffer[5] = 0; // Failed to authenticate
                        server.SendAsync(args.Client, responseBuffer);
                        return;
                    }
                    
                    // Accept -  We add this instance to their account data, save the account data and send back the response
                    accountData.Instances.Add(instanceId);
                    responseBuffer[5] = 1; // Successfully authenticated
                    server.SendAsync(args.Client, responseBuffer);
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
                    server.SendAsync(args.Client, responseBuffer);
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
        */
    }
};
server.ClientDisconnected += (_, args) =>
{
    clientAccountDatas.Remove(args.Client);
};

Console.CancelKeyPress += async (_, _) =>
{
    await server.StopAsync();
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

partial class Program
{
    [GeneratedRegex(@"^.{3,32}#[0-9]{4}$")]
    private static partial Regex TwitterHandleRegex();
}

partial class Program
{
    [GeneratedRegex(@"^(/ua/)?[A-Za-z0-9_-]+$")]
    private static partial Regex RedditHandleRegex();
}