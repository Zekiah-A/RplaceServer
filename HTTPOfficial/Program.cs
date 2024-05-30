using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CensorCore;
using DataProto;
using HTTPOfficial.ApiModel;
using HTTPOfficial.DataModel;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using WatsonWebsocket;

namespace HTTPOfficial;

/// <summary>
/// Central rplace global auth server, intended to act as a backbone for global accounts, instance creation and posts.
/// Test with:
/// ASPNETCORE_ENVIRONMENT=development; dotnet run
/// </summary>
internal static partial class Program
{
    private static Configuration config;
    private static WebApplication app;
    private static ILogger logger;
    private static HttpClient httpClient;
    private static JsonSerializerOptions defaultJsonOptions;
    private static AIService nudeNetAiService;

    [GeneratedRegex(@"^.{3,32}#[0-9]{4}$")]
    private static partial Regex TwitterHandleRegex();

    [GeneratedRegex(@"^(/ua/)?[A-Za-z0-9_-]+$")]
    private static partial Regex RedditHandleRegex();
    
    public static async Task Main(string[] args)
    {
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "server_config.json");
        var instancesPath = Path.Combine(Directory.GetCurrentDirectory(), "Instances");

        using var factory = LoggerFactory.Create(builder => builder.AddConsole());
        logger = factory.CreateLogger("Program");

        void CreateNewConfig()
        {
            // Create config
            logger.LogWarning("Could not find server config file, at {configPath}", configPath);
            var defaultConfiguration = new Configuration
            {
                Version = Configuration.CurrentVersion,
                Port = 8080,
                UseHttps = false,
                CertPath = "PATH_TO_CA_CERT",
                KeyPath = "PATH_TO_CA_KEY",
                SmtpHost = "smtp.gmail.com",
                SmtpPort = 587,
                EmailUsername = "username@example.com",
                EmailPassword = "password",
                KnownWorkers = [],
                InstanceKey = Guid.NewGuid().ToString(),
                DefaultInstances =
                [
                    new Instance("server.rplace.live", true)
                    {
                        VanityName = "canvas1" 
                    },
                    new Instance("server.rplace.live/testws", true)
                    {
                        VanityName = "placetest"
                    }
                ],
                RedditAuthClientId = "MY_REDDIT_API_APPLICATION_CLIENT_ID",
                Logger = true,
                PostsFolder = "Posts",
                PostLimitSeconds = 60,
                SignupLimitSeconds = 60,
                VerifyLimitSeconds = 2,
                VerifyExpiryMinutes = 15,
                Origin = "https://rplace.live",
                SocketPort = 450,
                AccountTierInstanceLimits = new Dictionary<AccountTier, int>
                {
                    { AccountTier.Free, 2 },
                    { AccountTier.Bronze, 5 },
                    { AccountTier.Silver, 10 },
                    { AccountTier.Gold, 25 },
                    { AccountTier.Administrator, 50 }
                }
            };
            var newConfigText = JsonSerializer.Serialize(defaultConfiguration, new JsonSerializerOptions
            {
                WriteIndented = true,
                IndentSize = 1,
                IndentCharacter = '\t'
            });
            File.WriteAllText(configPath, newConfigText);
        }

        if (!File.Exists(configPath))
        {
            CreateNewConfig();
            logger.LogWarning("Config files recreated. Please check {currentDirectory} and run this program again.",
                Directory.GetCurrentDirectory());
            Environment.Exit(0);
        }
        if (!Directory.Exists(instancesPath))
        {
            Directory.CreateDirectory(instancesPath);
        }
        
        var configData = JsonSerializer.Deserialize<Configuration>(await File.ReadAllTextAsync(configPath));
        if (configData is null || configData.Version < Configuration.CurrentVersion)
        {
            var oldConfigPath = configPath + ".old";
            logger.LogWarning("Current config file is invalid or outdated, moving to {oldConfigDirectory}. Config files recreated. Cheeck {currentDirectory} and run this program again.",
                oldConfigPath, Directory.GetCurrentDirectory());
            File.Move(configPath, oldConfigPath);
            CreateNewConfig();
            Environment.Exit(0);
        }
        config = configData;
        
        // Main server
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(Program).Assembly.FullName,
            ContentRootPath = Path.GetFullPath(Directory.GetCurrentDirectory()),
            WebRootPath = "/",
            Args = args
        });

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddDbContext<DatabaseContext>(options => { options.UseSqlite("Data Source=Server.db"); });

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });

        builder.Services.AddCors(cors =>
        {
            cors.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin();
                policy.AllowAnyHeader();
                policy.AllowAnyMethod();
            });
        });

        builder.Configuration["Kestrel:Certificates:Default:Path"] = config.CertPath;
        builder.Configuration["Kestrel:Certificates:Default:KeyPath"] = config.KeyPath;

        app = builder.Build();
        app.Urls.Add($"{(config.UseHttps ? "https" : "http")}://*:{config.Port}");
        app.UseStaticFiles();

        app.UseCors(policy =>
        {
            policy.AllowAnyMethod()
                .AllowAnyHeader()
                .SetIsOriginAllowed(_ => true)
                .AllowCredentials();
        });

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        });

        var wsServer = new WatsonWsServer(config.SocketPort, config.UseHttps, config.CertPath, config.KeyPath);

        // Vanity -> URL of actual socket server & board, done by worker clients on startup
        httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "web:Rplace.Live AuthServer v1.0 (by Zekiah-A)");

        // Used by worker servers + async communication
        var registeredVanities = new Dictionary<string, string>();
        var workerClients = new Dictionary<ClientMetadata, WorkerInfo>();
        var workerRequestQueue = new ConcurrentDictionary<int, TaskCompletionSource<byte[]>>(); // ID, Data callback
        var workerRequestId = 0;

        // Auth - Used when transitioning client from open to message handlers, periodic routines, etc
        var authorisedClients = new Dictionary<ClientMetadata, int>();

        // Used by reddit auth
        var refreshTokenAuthDates = new Dictionary<string, DateTime>();
        var refreshTokenAccessTokens = new Dictionary<string, string>();

        // Used by normal accounts
        var redditSerialiserOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
        defaultJsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        
        // Sensitive content detection with NudeNet / CensorCore
        const string modelName = "detector_v2_default_checkpoint.onnx";
        var modelPath = Path.Combine("Resources/", modelName);
        var modelBytes = await File.ReadAllBytesAsync(modelPath);
        var imgSharp = new ImageSharpHandler(2048, 2048); // Max image size
        var handler = new BodyAreaImageHandler(imgSharp, OptimizationMode.Normal);
        nudeNetAiService = AIService.Create(modelBytes, handler, false);
        
        // Default canvas instances
        await InsertDefaultInstancesAsync();

        async Task<Account?> AuthenticateReddit(string refreshToken, DatabaseContext database)
        {
            throw new NotImplementedException();
            // TODO: Reimplement
            /*var accessToken = await GetOrUpdateRedditAccessToken(refreshToken);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var meResponse = await httpClient.GetAsync("https://oauth.reddit.com/api/v1/me");
            var meData = await meResponse.Content.ReadFromJsonAsync<RedditMeResponse>(redditSerialiserOptions);
            httpClient.DefaultRequestHeaders.Authorization = null;
            if (!meResponse.IsSuccessStatusCode || meData is null)
            {
                logger.LogWarning("Could not request me data for authentication (reason {ReasonPhrase})",
                    meResponse.ReasonPhrase);
                return null;
            }

            var accountData = await database.Accounts.FirstOrDefaultAsync(account => account.RedditId == meData.Id);
            return accountData;*/
        }

        async Task<string?> GetOrUpdateRedditAccessToken(string refreshToken)
        {
            // If we already have their auth token cached,and it is within date, then we just return that
            if (refreshTokenAuthDates.TryGetValue(refreshToken, out var expiryDate) &&
                expiryDate - DateTime.Now <= TimeSpan.FromHours(1))
            {
                return refreshTokenAccessTokens[refreshToken];
            }

            // Otherwise, we need to refresh their auth token and update our caches respectively
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{config.RedditAuthClientId}:{config.RedditAuthClientSecret}")));
            var contentPayload = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "refresh_token", refreshToken }
            });

            var tokenResponse =
                await httpClient.PostAsync("https://www.reddit.com/api/v1/access_token", contentPayload);
            var tokenData = await tokenResponse.Content.ReadFromJsonAsync<RedditTokenResponse>(redditSerialiserOptions);
            httpClient.DefaultRequestHeaders.Authorization = null;
            if (!tokenResponse.IsSuccessStatusCode || tokenData is null)
            {
                logger.LogWarning(
                    "Could not get or update access token, token response was non-positive ({ReasonPhrase}) ",
                    tokenResponse.ReasonPhrase);
                return null;
            }

            refreshTokenAuthDates.Add(refreshToken, DateTime.Now);
            refreshTokenAccessTokens.Add(refreshToken, tokenData.AccessToken);
            return tokenData.AccessToken;
        }

        wsServer.MessageReceived += (_, args) =>
        {
            var data = args.Data.ToArray();
            var readableData = new ReadablePacket(data);

            switch ((ClientPackets)readableData.ReadByte())
            {
                /*
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
                   // unique ID (/me) endpoint, anc checking if we already have it saved (then they already have an account,// and we can fetch account data, else, we create account data for this user with data we can scrape from the API).
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
                       var existingAccount = await database.Accounts.FirstOrDefaultAsync(account => account.RedditId == meData.Id);
                       if (existingAccount is null)
                       {
                           // Create new accountData for this client
                           var accountData = new AccountData()
                           //var accountData = new AccountData(meData.Name, "", 0, new List<int>(),
                           //    "", "", meData.Name, 0, DateTime.Now, new List<Badge>(), true, meData.Id);
                           //accountData.Badges.Add(Badge.Newbie);

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
                   var gameData = defaultGameData with { BoardWidth = width, BoardHeight = height, CooldownMs = cooldown };
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

        var serverShutdownToken = new CancellationTokenSource();

        Console.CancelKeyPress += async (_, _) =>
        {
            await wsServer.StopAsync(serverShutdownToken.Token);
            await serverShutdownToken.CancelAsync();
            Environment.Exit(0);
        };

        AppDomain.CurrentDomain.UnhandledException += async (_, exceptionEventArgs) =>
        {
            logger.LogError("Unhandled server exception: {exception}", exceptionEventArgs.ExceptionObject);
            await wsServer.StopAsync(serverShutdownToken.Token);
            await serverShutdownToken.CancelAsync();
            Environment.Exit(1);
        };
        
        ConfigureAccountEndpoints();
        ConfigurePostEndpoints();
        ConfigureInstanceEndpoints();
        
        logger.LogInformation("Server listening on port {config}", config.SocketPort);
        wsServer.Logger = message => logger.LogInformation("{message}", message);
        await Task.WhenAll(app.RunAsync(), wsServer.StartAsync(serverShutdownToken.Token));
        await Task.Delay(-1, serverShutdownToken.Token);
    }
    
    private static async Task InsertDefaultInstancesAsync()
    {
        using var scope = app.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        if (database is null)
        {
            throw new Exception("Couldn't insert default instances, db was null");
        }

        foreach (var defaultInstance in config.DefaultInstances)
        {
            if (!await database.Instances.AnyAsync(instance => instance.VanityName == defaultInstance.VanityName))
            {
                database.Instances.Add(defaultInstance);
            }
        }
        
        await database.SaveChangesAsync();
    }

    private static List<string> ReadTxtListFile(string path)
    {
        return File.ReadAllLines(path)
            .Where(entry => !string.IsNullOrWhiteSpace(entry) && entry.TrimStart().First() != '#').ToList();
    }
}