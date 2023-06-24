using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using PlaceHttpsServer;
using RplaceServer.Events;
using RplaceServer.Types;
using UnbloatDB;
using UnbloatDB.Serialisers;
using System.Diagnostics;
using System.Runtime.InteropServices.ComTypes;
using System.Timers;

namespace RplaceServer;

public sealed class WebServer
{
    private readonly WebApplication app;
    private readonly GameData gameData;
    private readonly RateLimiter timelapseLimiter;
    public Action<string>? Logger;
    
    private double cpuUsagePercentage;
    private long backupsSize;
    public int backupsCount;
    
    public event EventHandler<CanvasBackupCreatedEventArgs>? CanvasBackupCreated;

    public WebServer(GameData data, string certPath, string keyPath, string origin, bool ssl, int port)
    {
        gameData = data;
        timelapseLimiter = new RateLimiter(TimeSpan.FromMilliseconds(gameData.TimelapseLimitPeriod));

        var pagesRoot = Path.Join(Directory.GetCurrentDirectory(), @"Pages");
        if (!Directory.Exists(pagesRoot))
        {
            Logger?.Invoke("Could not find Pages root in current working directory. Regenerating.");
            Directory.CreateDirectory(pagesRoot);
            FileUtils.RecursiveCopy(Path.Join(FileUtils.BuildContentPath, @"Pages"), pagesRoot);
        }
        
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
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
        app.UseStaticFiles(new StaticFileOptions
        {
            ServeUnknownFileTypes = true,
            FileProvider = new PhysicalFileProvider(pagesRoot),
            RequestPath = ""
        });
        
        app.UseDirectoryBrowser(new DirectoryBrowserOptions
        {
            FileProvider = new PhysicalFileProvider(pagesRoot),
            RequestPath = ""
        });
        app.Urls.Add($"{(ssl ? "https" : "http")}://*:{port}");
        app.UseCors(policy =>
        {
            policy.AllowAnyMethod()
                .AllowAnyHeader()
                .SetIsOriginAllowed(_ => true) // TODO: Add origin block/check
                .AllowCredentials();
        });

        var cpuUsageTimer = new System.Timers.Timer(TimeSpan.FromSeconds(2))
        {
            Enabled = true,
            AutoReset = true
        };
        cpuUsageTimer.Elapsed += (_, _) =>
        {
            var startTime = DateTime.UtcNow;
            var startCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
            // As this method is asynchronous, the total processor time will increase even though we wait on this thread
            // due to all the rest of the server processes occuring in the background.
            Thread.Sleep(1000);
            
            var cpuUsedMs = (Process.GetCurrentProcess().TotalProcessorTime - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
            cpuUsagePercentage = cpuUsageTotal * 100;
        };
        
        var backups = new DirectoryInfo(gameData.CanvasFolder).GetFiles();
        backupsCount = backups.Length;
        backupsSize = backups.Sum(file => file.Length);
    }
    
    public async Task StartAsync()
    {
        //Serve absolute latest board from memory.
        app.MapGet("/place", () =>
        {
            var board = BoardPacker.RunLengthCompressBoard(gameData.Board);
            return Results.Bytes(board);
        });
        
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

        app.MapGet("/statistics", () => Results.Json(new PerformanceStatistics(
            GC.GetTotalMemory(false),
            cpuUsagePercentage,
            backupsCount,
            backupsSize)));
        
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
            await SaveCanvasBackupAsync();
        };
    }
    
    /// <summary>
    /// Writes a canvas backup to the disk, putting a new entry into the backuplist.txt log file, and saving the current
    /// canvas state with palette and width metadata so that the canvas state can be easily recovered.
    /// </summary>
    public async Task SaveCanvasBackupAsync()
    {
        // Save the place file so that we can recover after a server restart
        await File.WriteAllBytesAsync(Path.Join(gameData.CanvasFolder, "place"), gameData.Board);

        // Save a dated backup of the canvas to timestamp the place file at this point in time
        var backupName = "place " + DateTime.Now.ToString("yyyy.MM.dd HH.mm.ss");
        await using var backupList = new StreamWriter(Path.Join(gameData.CanvasFolder, "backuplist.txt"), append: true);
        await backupList.WriteLineAsync(backupName);
        await backupList.FlushAsync();

        var boardPath = Path.Join(gameData.CanvasFolder, backupName);
        var boardData = BoardPacker.PackBoard(gameData.Board, gameData.Palette, gameData.BoardWidth);
        await File.WriteAllBytesAsync(boardPath, boardData);
        backupsCount++;
        backupsSize += boardData.Length;

        CanvasBackupCreated?.Invoke(this, new CanvasBackupCreatedEventArgs(backupName, DateTime.Now, boardPath));
    }

    public async Task StopAsync()
    {
        await SaveCanvasBackupAsync();
        await app.StopAsync();
    }
}