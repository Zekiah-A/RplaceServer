using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AuthWorkerShared;
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
                InstanceKey = RandomNumberGenerator.GetHexString(96),
                DefaultInstances =
                [
                    new Instance("server.rplace.live", true)
                    {
                        VanityName = "canvas1",
                        Legacy = true,
                        FileServerLocation = "raw.githubusercontent.com/rplacetk/canvas1/main" 
                    },
                    new Instance("server.rplace.live/testws", true)
                    {
                        VanityName = "placetest",
                        Legacy = true
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

        builder.Services.AddDbContext<DatabaseContext>(options =>
        {
            options.UseSqlite("Data Source=Server.db");
        });

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
        var modelPath = Path.Combine("Resources", modelName);
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
            var packet = new ReadablePacket(data);
            var code = packet.ReadByte();

            switch (code)
            {
                case (byte) WorkerPackets.AnnounceExistence:
                {
                    var instanceKey = packet.ReadString();
                    var instanceUri = packet.ReadString();
                    var workerInstanceCount = packet.ReadInt();
                    var workerMaxInstances = packet.ReadInt();
                    logger.LogInformation($"{instanceKey}, {instanceUri}, {workerInstanceCount}, {workerMaxInstances}");

                    break;
                }
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