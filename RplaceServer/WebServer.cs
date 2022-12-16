using System.Buffers.Binary;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using PlaceHttpsServer;
using RplaceServer.Events;
using Timer = System.Timers.Timer;

namespace RplaceServer;

internal sealed class WebServer
{
    private readonly ServerInstance instance;
    private readonly WebApplication app;
    private readonly WebApplicationBuilder builder;
    private readonly GameData gameData;
    private readonly RateLimiter rateLimiter;
    
    public event EventHandler<CanvasBackupCreatedEventArgs> CanvasBackupCreated = (_, _) => { };

    public WebServer(GameData data, string certPath, string keyPath, string origin, bool ssl, int port)
    {
        gameData = data;
        var pagesRoot = Path.Join(Directory.GetCurrentDirectory(), @"Pages");

        builder = WebApplication.CreateBuilder(new WebApplicationOptions { WebRootPath = pagesRoot });
        builder.Configuration["Kestrel:Certificates:Default:Path"] = certPath;
        builder.Configuration["Kestrel:Certificates:Default:KeyPath"] = keyPath;
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy => policy.WithOrigins(origin, "*"));
        });

        app = builder.Build();
        app.Urls.Add($"{(ssl ? "https" : "http")}://*:{port}");
        app.UseCors(policy => policy.AllowAnyMethod().AllowAnyHeader().SetIsOriginAllowed(_ => true).AllowCredentials());
        app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(pagesRoot) }); //TODO: Fix 404s, files not appearing to be served

        rateLimiter = new RateLimiter(TimeSpan.FromSeconds(gameData.TimelapseLimitPeriod));
    }

    public async Task Start()
    {
        //Serve absolute latest board from memory.
        app.MapGet("/place", () => Results.Bytes(gameData.Board));
        
        // To download a specific backup.
        app.MapGet("/backups/{placeFile}", (string placeFile) =>
        {
            var stream = new FileStream(Path.Join(gameData.CanvasFolder, placeFile), FileMode.Open);
            return Results.File(stream);
        });

        app.MapGet("/backuplist", async () =>
            await File.ReadAllTextAsync(Path.Join(gameData.CanvasFolder, "backuplist.txt"))
        );
        
        app.MapPost("/timelapse", async (TimelapseInformation timelapseInfo, HttpContext context) =>
        {
            var address = context.Connection.RemoteIpAddress;
            
            if (address is null || !rateLimiter.IsAuthorised(address))
            {
                return Results.Unauthorized();
            }
            
            var stream = await TimelapseGenerator.GenerateTimelapseAsync(timelapseInfo, gameData);
            return Results.File(stream);
        });
        

        if (gameData.CreateBackups)
        {
            HandleBoardBackups();
        }
        
        await app.RunAsync();
    }

    private void HandleBoardBackups()
    {
        var timer = new Timer
        {
            AutoReset = true,
            Enabled = true,
            Interval = TimeSpan.FromSeconds(gameData.BackupFrequency).TotalMilliseconds
        };

        timer.Elapsed += async (_, _) =>
        {
            if (!gameData.CreateBackups)
            {
                return;
            }
            
            timer.Interval = TimeSpan.FromSeconds(gameData.BackupFrequency).TotalMilliseconds;
            
            var backupName = "place." + DateTime.Now.ToString("dd.MM.yyyy.HH:mm:ss");
            await using var file = new StreamWriter(Path.Join(gameData.CanvasFolder, "backuplist.txt"), append: true);
            await file.WriteLineAsync(backupName);

            var boardPath = Path.Join(gameData.CanvasFolder, backupName);
            await File.WriteAllBytesAsync(boardPath, BoardPacker.PackBoard(gameData.Board, gameData.Palette, gameData.BoardWidth));
            
            CanvasBackupCreated.Invoke(this, new CanvasBackupCreatedEventArgs(backupName, DateTime.Now, boardPath));
        };
    }
}