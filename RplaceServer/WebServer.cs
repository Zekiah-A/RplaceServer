using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using RplaceServer.Events;

namespace RplaceServer;

internal class WebServer
{
    private readonly ServerInstance instance;
    private readonly WebApplication app;
    private readonly WebApplicationBuilder builder;
    private readonly GameData gameData;
    
    public event EventHandler CanvasBackupCreated;

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
        app.UseStaticFiles(new StaticFileOptions {FileProvider = new PhysicalFileProvider(pagesRoot) }); //TODO: Fix 404s, files not appearing to be served
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

        // TODO: Implement StarlkYT's timelapse generator
        /*
        app.MapPost("/timelapse", async (TimelapseInformation timelapseInfo) =>
        {
            var stream = await TimelapseGenerator.GenerateTimelapseAsync(timelapseInfo.BackupStart, timelapseInfo.BackupEnd, timelapseInfo.Fps, 750, timelapseInfo.StartX, timelapseInfo.StartY, timelapseInfo.EndX, timelapseInfo.EndY, timelapseInfo.Reverse);
            return Results.File(stream);
        });
        */

        if (gameData.CreateBackups)
        {
#pragma warning disable CS4014
            HandleBoardBackups();
#pragma warning restore CS4014
        }
        
        await app.RunAsync();
    }

    private async Task HandleBoardBackups()
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(gameData.BackupFrequency));

        while (await timer.WaitForNextTickAsync())
        {
            if (!gameData.CreateBackups)
            {
                return;
            }
            
            var backupName = "place." + DateTime.Now.ToString("dd.MM.yyyy.HH:mm:ss");
            await using var file = new StreamWriter(Path.Join(gameData.CanvasFolder, "backuplist.txt"), append: true);
            await file.WriteLineAsync(backupName);

            var boardPath = Path.Join(gameData.CanvasFolder, backupName);
            await File.WriteAllBytesAsync(boardPath, gameData.Board);
            
            CanvasBackupCreated.Invoke(this, new CanvasBackupEventArgs(backupName, DateTime.Now, boardPath));
        }
    }
}