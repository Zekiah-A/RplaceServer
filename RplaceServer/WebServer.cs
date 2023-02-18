using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using PlaceHttpsServer;
using RplaceServer.Events;
using RplaceServer.Types;
using UnbloatDB;
using UnbloatDB.Serialisers;

namespace RplaceServer;

public sealed class WebServer
{
    private readonly ServerInstance instance;
    private readonly WebApplication app;
    private readonly WebApplicationBuilder builder;
    private readonly GameData gameData;
    private readonly Database postsDB;
    private readonly RateLimiter timelapseLimiter;
    private readonly RateLimiter postLimiter;
    public Action<string>? Logger;
    
    
    public event EventHandler<CanvasBackupCreatedEventArgs> CanvasBackupCreated = (_, _) => { };

    public WebServer(GameData data, string certPath, string keyPath, string origin, bool ssl, int port)
    {
        gameData = data;
        postsDB = new Database(new Config(gameData.PostsFolder, new JsonSerialiser()));
        timelapseLimiter = new RateLimiter(TimeSpan.FromMilliseconds(gameData.TimelapseLimitPeriod));
        postLimiter = new RateLimiter(TimeSpan.FromMilliseconds(gameData.PostLimitPeriod));
        
        var pagesRoot = Path.Join(Directory.GetCurrentDirectory(), @"Pages");

        builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            WebRootPath = pagesRoot
        });

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy => policy.WithOrigins(origin, "*"));
        });

        builder.Configuration["Kestrel:Certificates:Default:Path"] = certPath;
        builder.Configuration["Kestrel:Certificates:Default:KeyPath"] = keyPath;
        
        app = builder.Build();
        app.Urls.Add($"{(ssl ? "https" : "http")}://*:{port}");
        app.UseCors(policy =>
        {
            policy.AllowAnyMethod().AllowAnyHeader().SetIsOriginAllowed(_ => true).AllowCredentials();
        });

        app.UseStaticFiles(new StaticFileOptions
        {
            ServeUnknownFileTypes = true,
            FileProvider = new PhysicalFileProvider(pagesRoot)
        });
    }

    public async Task StartAsync()
    {
        //Serve absolute latest board from memory.
        app.MapGet("/place", () => Results.Bytes(gameData.Board));
        
        // To download a specific backup.
        app.MapGet("/backups/{placeFile}", (string placeFile) =>
        {
            var path = Path.Join(gameData.CanvasFolder, placeFile);
            path = path.Replace("..", "");

            if (!File.Exists(path))
            {
                return Results.NotFound();
            }
            
            var stream = new FileStream(path, FileMode.Open);
            return Results.File(stream);
        });

        app.MapGet("/backuplist", async () =>
            await File.ReadAllTextAsync(Path.Join(gameData.CanvasFolder, "backuplist.txt"))
        );
        
        app.MapPost("/timelapse", async (TimelapseInformation timelapseInfo, HttpContext context) =>
        {
            var address = context.Connection.RemoteIpAddress;
            
            if (address is null || !timelapseLimiter.IsAuthorised(address))
            {
                Logger?.Invoke($"Timelapse generation rejected from client {address} due to exceeding rate limit");
                return Results.Unauthorized();
            }
            
            var stream = await TimelapseGenerator.GenerateTimelapseAsync(timelapseInfo, gameData);
            return Results.File(stream);
        });
        
        app.MapGet("/posts", () =>
            Results.Json(postsDB.FindRecordsBefore<Post, DateTime>(nameof(Post.CreationDate), DateTime.Now, false)));

        app.MapGet("/posts/{masterKey}", (string masterKey) =>
            Results.Json(postsDB.GetRecord<Post>(masterKey)));
        
        app.MapPost("/posts/upload", async (Post submission, HttpContext context) =>
        {
            var address = context.Connection.RemoteIpAddress;
            
            if (address is null || !postLimiter.IsAuthorised(address))
            {
                Logger?.Invoke($"Client {address} denied post upload for breaching rate limit, or null address.");
                return Results.Unauthorized();
            }

            var sanitised = submission with
            {
                Upvotes = 0,
                Downvotes = 0,
                CreationDate = DateTime.Now,
                ContentPath = null,
                
            };

            // If client also wanted to upload content with this post, we grant them the post key, which gives them
            // temporary permission to upload the content to the CDN.
            var postKey = await postsDB.CreateRecord(sanitised);
            return Results.Text(postKey);
        });

        app.MapGet("/content/{contentPath}", (string contentPath) =>
        {
            var path = Path.Join(gameData.PostsFolder, "Content", contentPath);
            path = path.Replace("..", "");
            
            if (!File.Exists(path))
            {
                return Results.NotFound();
            }
            
            var stream = new FileStream(path, FileMode.Open);
            return Results.File(stream);
        });

        app.MapPost("/content/upload/{masterKey}", async (HttpRequest request, string masterKey) =>
        {
            var address = request.HttpContext.Connection.RemoteIpAddress;
            var pendingPost = await postsDB.GetRecord<Post>(masterKey);
            
            if (pendingPost is null || !pendingPost.MasterKey.Equals(masterKey) || pendingPost.Data.ContentPath is not null)
            {
                Logger?.Invoke($"Client {address} denied content upload for invalid master key or post not found.");
                return Results.Unauthorized();
            }
            
            // Limit stream length to 5MB to prevent excessively large uploads
            if (request.Body.Length > 5_000_000)
            {
                Logger?.Invoke($"Client {address} denied content upload for too large stream file size.");
                return Results.UnprocessableEntity();
            }

            // Save data to CDN folder
            var contentPath = Guid.NewGuid().ToString();
            pendingPost.Data.ContentPath = contentPath;

            await using var fileStream = File.Create(Path.Join(gameData.PostsFolder, "Content", contentPath));
            request.Body.Seek(0, SeekOrigin.Begin);
            await request.Body.CopyToAsync(fileStream);
            
            return Results.Ok();
        })
        .Accepts<IFormFile>("image/gif", "image/jpeg","image/png", "image/webp");
        
        if (gameData.CreateBackups)
        {
            HandleBoardBackups();
        }
        
        await app.RunAsync();
    }

    private void HandleBoardBackups()
    {
        var timer = new System.Timers.Timer
        {
            AutoReset = true,
            Enabled = true,
            Interval = gameData.BackupFrequency
        };

        timer.Elapsed += async (_, _) =>
        {
            if (!gameData.CreateBackups)
            {
                return;
            }
            
            timer.Interval = gameData.BackupFrequency;
            await SaveCanvasBackup();
        };
    }

    public async Task SaveCanvasBackup()
    {
        // Save the place file so that we can recover after a server restart
        await File.WriteAllBytesAsync(Path.Join(gameData.CanvasFolder, "place"), gameData.Board);

        // Save a dated backup of the canvas to timestamp the place file at this point in time
        var backupName = "place " + DateTime.Now.ToString("yyyy.MM.dd HH.mm.ss");
        await using var file = new StreamWriter(Path.Join(gameData.CanvasFolder, "backuplist.txt"), append: true);
        await file.WriteLineAsync(backupName);

        var boardPath = Path.Join(gameData.CanvasFolder, backupName);
        await File.WriteAllBytesAsync(boardPath, BoardPacker.PackBoard(gameData.Board, gameData.Palette, gameData.BoardWidth));
            
        CanvasBackupCreated.Invoke(this, new CanvasBackupCreatedEventArgs(backupName, DateTime.Now, boardPath));
    }

    public async Task StopAsync()
    {
        await SaveCanvasBackup();
        await app.StopAsync();
    }
}