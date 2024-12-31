// AuthOfficial
// Copyright (C) 2024 Zekiah-A (https://github.com/Zekiah-A)
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
using CensorCore;
using WatsonWebsocket;
using FluentValidation;
using FluentValidation.AspNetCore;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AuthOfficial.ApiModel;
using AuthOfficial.Configuration;
using AuthOfficial.DataModel;
using AuthOfficial.Middlewares;
using AuthOfficial.Services;
using AuthOfficial.Validation;
using AuthOfficial.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace AuthOfficial;

/// <summary>
/// Central rplace global auth server, intended to act as a backbone for global accounts, instance creation and posts.
/// Test with:
/// ASPNETCORE_ENVIRONMENT=development; dotnet run
/// </summary>
internal static partial class Program
{
    private static ILogger logger;
    private static HttpClient httpClient;
    private static JsonSerializerOptions defaultJsonOptions;
    private static AIService nudeNetAiService;
    private static CancellationTokenSource serverShutdownToken;

    [GeneratedRegex(@"^.{3,32}#[0-9]{4}$")]
    private static partial Regex TwitterHandleRegex();

    [GeneratedRegex(@"^(/ua/)?[A-Za-z0-9_-]+$")]
    private static partial Regex RedditHandleRegex();

    public static async Task Main(string[] args)
    {
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "server_config.json");
        var instancesPath = Path.Combine(Directory.GetCurrentDirectory(), "Instances");
        var logsPath = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
        if (!Directory.Exists(logsPath))
        {
            Directory.CreateDirectory(logsPath);
        }

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.AddFile(options =>
            {
                options.RootPath = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
            });
        });
        logger = loggerFactory.CreateLogger("AuthOfficial");

        void CreateNewConfig()
        {
            // Create config
            logger.LogWarning("Could not find server config file, at {configPath}", configPath);
            var defaultConfiguration = new Configuration.Config
            {
                Version = Configuration.Config.CurrentVersion,
                // TODO: Consider migrating instances to authenticate themselves with JWTs
                InstanceKey = RandomNumberGenerator.GetHexString(64, true),
                ServerConfiguration = new ServerConfiguration
                {
                    Origin = "https://rplace.live",
                    Port = 8080,
                    SocketPort = 450,
                    UseHttps = false,
                    CertPath = "PATH_TO_CA_CERT",
                    KeyPath = "PATH_TO_CA_KEY",
                },
                DatabaseConfiguration = new DatabaseConfiguration
                {
                    DefaultInstances =
                    [
                        new Instance("server.rplace.live", true)
                        {
                            Id = 1,
                            VanityName = "canvas1",
                            FileServerLocation = "raw.githubusercontent.com/rplacetk/canvas1/main",
                            Legacy = true
                        },
                        new Instance("server.rplace.live/testws", true)
                        {
                            Id = 2,
                            VanityName = "placetest",
                            FileServerLocation = "raw.githubusercontent.com/rplacetk/canvas1/main",
                            Legacy = true
                        }
                    ],
                    DefaultForums =
                    [
                        new Forum()
                        {
                            Id = 1,
                            VanityName = "canvas1",
                            Title = "Canvas 1",
                            Description = "The forum community for rplace.live's main canvas!",
                            AssociatedInstanceId = 1
                        },
                        new Forum()
                        {
                            Id = 2,
                            VanityName = "placetest",
                            Title = "Test canvas",
                            Description = "The forum community for rplace.live's test canvas!",
                            AssociatedInstanceId = 2
                        }
                    ]
                },
                PostsConfiguration = new PostsConfiguration
                {
                    PostsFolder = "Posts",
                    PostLimitSeconds = 60,
                },
                AccountConfiguration = new AccountConfiguration
                {
                    AccountTierInstanceLimits = new Dictionary<AccountTier, int>
                    {
                        { AccountTier.Free, 2 },
                        { AccountTier.Bronze, 5 },
                        { AccountTier.Silver, 10 },
                        { AccountTier.Gold, 25 },
                        { AccountTier.Administrator, 50 }
                    }
                },
                EmailConfiguration = new EmailConfiguration
                {
                    SmtpHost = "SMTP_HOST",
                    SmtpPort = 587,
                    Username = "EMAIL_USERNAME",
                    Password = "EMAIL_PASSWORD",
                    FromEmail = "EMAIL_FROM",
                    FromName = "admin",
                    UseStartTls = true,
                    TimeoutSeconds = 30,
                    WebsiteUrl = "https://rplace.live",
                    SupportEmail = "admin@rplace.live"
                },
                AuthConfiguration = new AuthConfiguration
                {
                    JwtSecret = RandomNumberGenerator.GetHexString(64, true),
                    JwtIssuer = "JWT_ISSUER", // e.g https://server.rplace.live/auth
                    JwtAudience = "WT_AUDIENCE", // e.g https://server.rplace.live/auth
                    JwtExpirationMinutes = 60,
                    RefreshTokenExpirationDays = 30,
                    VerificationCodeExpirationMinutes = 15,
                    MaxFailedVerificationAttempts = 5,
                    SignupRateLimitSeconds = 300,
                    FailedVerificationAttemptResetMinutes = 5
                },
                CensorConfiguration = new CensorConfiguration
                {
                    DefaultFilterAlllowedDomains = [ 
                        "bilibili.com", "canv.tk", "chit.cf", "count.land", "discordapp.com", "discord.com",
                        "discord.gg", "douban.com", "fandom.com", "github.com", "hippo.casino", "instagram.com",
                        "kookapp.cn", "line.me", "medium.com", "openmc.pages.dev", "pinterest.com", "quora.com",
                        "reddit.com", "rplace.live", "rplace.tk", "snapchat.com", "stackexchange.com",
                        "stackoverflow.com", "t.me", "tiktok.com", "tumblr.com", "twitch.tv", "twitter.com",
                        "vk.com", "wechat.com", "weibo.com", "wikipedia.org", "x.com", "youtube.com"
                    ],
                    DefaultFilterBannedWords = [],
                    DefaultFilterMinPerceptualPercent = 80,
                    DefaultProcessMaxGifFrames = 100
                }
            };
            var newConfigText = JsonSerializer.Serialize(defaultConfiguration, new JsonSerializerOptions()
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

        var configData = JsonSerializer.Deserialize<Configuration.Config>(await File.ReadAllTextAsync(configPath));
        if (configData is null || configData.Version < Configuration.Config.CurrentVersion)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var oldConfigPath = $"{configPath}.{timestamp}.old";
            logger.LogWarning("Current config file is invalid or outdated, moving to {oldConfigDirectory}. Config files recreated. Check {currentDirectory} and run this program again.",
                oldConfigPath, Directory.GetCurrentDirectory());
            File.Move(configPath, oldConfigPath);
            CreateNewConfig();
            Environment.Exit(0);
        }

        // Create server builder
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(Program).Assembly.FullName,
            ContentRootPath = Path.GetFullPath(Directory.GetCurrentDirectory()),
            WebRootPath = "/",
            Args = args
        });

        // Add config file to server configuration & DI sections
        builder.Configuration.AddJsonFile(configPath, optional: false, reloadOnChange: true);
        builder.Services.Configure<Config>(builder.Configuration);
        builder.Services.Configure<ServerConfiguration>(builder.Configuration.GetSection("ServerConfiguration"));
        builder.Services.Configure<PostsConfiguration>(builder.Configuration.GetSection("PostsConfiguration"));
        builder.Services.Configure<AccountConfiguration>(builder.Configuration.GetSection("AccountConfiguration"));
        builder.Services.Configure<AuthConfiguration>(builder.Configuration.GetSection("AuthConfiguration"));
        builder.Services.Configure<EmailConfiguration>(builder.Configuration.GetSection("EmailConfiguration"));
        builder.Services.Configure<CensorConfiguration>(builder.Configuration.GetSection("CensorConfiguration"));
        var config = builder.Configuration.Get<Config>()!;

        // Configure webserver
        builder.WebHost.ConfigureKestrel(options =>
        {
            var certPath = config.ServerConfiguration.CertPath;
            var keyPath = config.ServerConfiguration.KeyPath;
            if (!string.IsNullOrEmpty(certPath) && !string.IsNullOrEmpty(keyPath) && config.ServerConfiguration.UseHttps)
            {
                var certificate = LoadCertificate(certPath, keyPath);
                options.ConfigureHttpsDefaults(httpsOptions =>
                {
                    httpsOptions.ServerCertificate = certificate;
                });
            }

            options.ListenAnyIP(config.ServerConfiguration.Port, listenOptions =>
            {
                if (config.ServerConfiguration.UseHttps)
                {
                    listenOptions.UseHttps();
                }
            });
        });

        // Swagger service
        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddSwaggerGen();
            builder.Services.AddEndpointsApiExplorer();
        }

        // Logger service
        builder.Services.AddSingleton<ILogger>(logger);

        // SMTP email sending service
        builder.Services.AddSingleton<EmailService>();
        
        // Token scoped service
        builder.Services.AddScoped<TokenService>();
        
        // Content filters / censors scoped service
        builder.Services.AddScoped<CensorService>();
        
        // Account scoped service
        builder.Services.AddScoped<AccountService>();

        // Account background hosted service
        builder.Services.AddHostedService<AccountBackgroundService>();

        // EFCore database service
        builder.Services.AddDbContext<DatabaseContext>(options =>
        {
            options.UseSqlite("Data Source=server.db");
        });
        
        // Context accessor service
        builder.Services.AddHttpContextAccessor();

        // Configure JSON options
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });

        // CORS service
        builder.Services.AddCors(cors =>
        {
            cors.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin();
                policy.AllowAnyHeader();
                policy.AllowAnyMethod();
            });
        });

        // Configure cookia auth
        builder.Services.Configure<CookieAuthenticationOptions>(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });

        // Authorization service
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("PostAuthorPolicy", policy =>
                policy.Requirements.Add(new PostAuthorRequirement()));
        });

        // Authorization services
        builder.Services.AddSingleton<IAuthorizationHandler, PostAuthorizationHandler>();


        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = config.AuthConfiguration.JwtIssuer,
                ValidAudience = config.AuthConfiguration.JwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.AuthConfiguration.JwtSecret))
            };
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    context.Token = context.Request.Cookies["AccessToken"];
                    return Task.CompletedTask;
                }
            };
        });
        
        // Validation service
        builder.Services.AddScoped<IValidator<ProfileUpdateRequest>, ProfileUpdateRequestValidator>();
        builder.Services.AddScoped<IValidator<PostUpdateRequest>, PostUpdateRequestValidator>();
        builder.Services.AddScoped<IValidator<PostUploadRequest>, PostUploadRequestValidator>();
        builder.Services.AddFluentValidationAutoValidation();

        var app = builder.Build();
        
        // Static files middlewares
        app.UseStaticFiles();

        // CORS middleware
        app.UseCors(policy =>
        {
            policy.AllowAnyMethod()
                .AllowAnyHeader()
                .SetIsOriginAllowed(_ => true)
                .AllowCredentials();
        });

        // Swagger and dev middleware
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // Authentication middleware
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<AuthTypeMiddleware>();
        app.UseMiddleware<ClaimsMiddleware>();

        // Forwarded headers middleware
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        });

        var wsServer = new WatsonWsServer(config.ServerConfiguration.SocketPort, config.ServerConfiguration.UseHttps, config.ServerConfiguration.CertPath, config.ServerConfiguration.KeyPath);

        // Vanity -> URL of actual socket server & board, done by worker clients on startup
        httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "web:Rplace.Live AuthServer v1.0 (by Zekiah-A)");

        // Used by worker servers + async communication
        var registeredVanities = new Dictionary<string, string>();
        var workerClients = new Dictionary<ClientMetadata, WorkerInfo>();
        var workerRequestId = 0;
        var workerRequestQueue = new ConcurrentDictionary<int, TaskCompletionSource<byte[]>>(); // ID, Data callback

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

        // Update database with static / default records
        await InsertDatabaseDefaultsAsync(app, config.DatabaseConfiguration);

        // Shutdown and exceptions
        serverShutdownToken = new CancellationTokenSource();

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

        // Map all AuthOfficial endopoints
        app.MapAuthEndpoints();
        app.MapAccountEndpoints();
        app.MapForumEndpoints();
        app.MapInstanceEndpoints();
        app.MapPostEndpoints();
        app.MapOverlayEndpoints();

        await Task.WhenAll(app.RunAsync(), wsServer.StartAsync(serverShutdownToken.Token));
        await Task.Delay(-1, serverShutdownToken.Token);
    }

    private static async Task InsertDatabaseDefaultsAsync(WebApplication app, DatabaseConfiguration config)
    {
        using var scope = app.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        if (database is null)
        {
            throw new Exception("Couldn't insert database defaults: Database was null");
        }

        foreach (var defaultInstance in config.DefaultInstances)
        {
            if (!await database.Instances.AnyAsync(instance => instance.VanityName == defaultInstance.VanityName))
            {
                database.Instances.Add(defaultInstance);
            }
        }

        foreach (var defaultForum in config.DefaultForums)
        {
            if (!await database.Instances.AnyAsync(forum => forum.VanityName == defaultForum.VanityName))
            {
                database.Forums.Add(defaultForum);
            }
        }

        await database.SaveChangesAsync();
    }


    public static X509Certificate2 LoadCertificate(string certPath, string keyPath)
    {
        var cert = X509Certificate2.CreateFromPemFile(certPath, keyPath);
        return cert;
    }

    private static List<string> ReadTxtListFile(string path)
    {
        return File.ReadAllLines(path)
            .Where(entry => !string.IsNullOrWhiteSpace(entry) && entry.TrimStart().First() != '#').ToList();
    }
}
