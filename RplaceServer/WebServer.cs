using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using RplaceServer.Events;
using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using RplaceServer.TimelapseGeneration;
using RplaceServer.Types;

namespace RplaceServer;

public sealed class WebServer
{
    private readonly WebApplication app;
    private readonly ServerInstance instance;
    private readonly GameData gameData;
    private readonly RateLimiter timelapseLimiter;
    private readonly string pagesRoot;

    public Action<string>? Logger;

    private double cpuUsagePercentage;
    private long backupsSize;
    private int backupsCount;
    
    public event EventHandler<CanvasBackupCreatedEventArgs>? CanvasBackupCreated;

    public WebServer(ServerInstance parentInstance, GameData data, string? certPath, string? keyPath, string origins, bool ssl, int port)
    {
        instance = parentInstance;
        gameData = data;
        timelapseLimiter = new RateLimiter(TimeSpan.FromMilliseconds(gameData.TimelapseLimitPeriodS));
        var staticResourcesDirectory = new DirectoryInfo(gameData.StaticResourcesFolder);
        pagesRoot = Path.Combine(staticResourcesDirectory.FullName, "Pages");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            WebRootPath = pagesRoot
        });

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy => policy
                .WithOrigins(origins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials());
        });

        builder.WebHost.UseKestrel(options =>
        {
            options.ListenAnyIP(port, listenOptions =>
            {
                if (ssl && certPath != null && keyPath != null)
                {
                    listenOptions.UseHttps(certPath, keyPath);
                }
            });
        });

        app = builder.Build();

        // Static file serving with improved security options
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(pagesRoot),
            ServeUnknownFileTypes = false,
            OnPrepareResponse = ctx =>
            {
                ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=600");
            }
        });
        app.UseCors();

        var cpuUsageTimer = new System.Timers.Timer(TimeSpan.FromSeconds(2))
        {
            Enabled = true,
            AutoReset = true
        };

        var startTime = DateTime.UtcNow;
        var startCpuTime = Process.GetCurrentProcess().TotalProcessorTime;
        cpuUsageTimer.Elapsed += (_, _) =>
        {
            var cpuUsedMs = (Process.GetCurrentProcess().TotalProcessorTime - startCpuTime).TotalMilliseconds;
            var totalMsPassed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            cpuUsagePercentage = (cpuUsedMs / (Environment.ProcessorCount * totalMsPassed)) * 100;
            startTime = DateTime.UtcNow;
            startCpuTime = Process.GetCurrentProcess().TotalProcessorTime;
        };

        backupsCount = 0;
        backupsSize = 0;
    }

    public async Task StartAsync()
    {
        if (!Directory.Exists(pagesRoot))
        {
            Logger?.Invoke($"Static resources folder not found at {pagesRoot}.");
            throw new FileNotFoundException(pagesRoot);
        }
        
        // Load backups from file
        var backups = new DirectoryInfo(gameData.CanvasFolder).GetFiles();
        backupsCount = backups.Length;
        backupsSize = backups.Sum(file => file.Length);
        
        // Serve current board state from memory.
        app.MapGet("/place", () =>
        {
            var board = BoardPacker.RunLengthCompressBoard(instance.Board);
            return Results.Bytes(board);
        });
    
        // To download a specific backup.
        app.MapGet("/backups/{placeFile}", (string placeFile) =>
        {
            var safeFileName = Path.GetFileName(placeFile);
            var path = Path.Combine(gameData.CanvasFolder, safeFileName);

            if (!File.Exists(path))
            {
                return Results.NotFound();
            }

            return Results.File(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), contentType: "application/octet-stream");
        });

        app.MapGet("/backuplist", async () =>
        {
            var backupListPath = Path.Combine(gameData.CanvasFolder, "backuplist.txt");
            return File.Exists(backupListPath) ? Results.Ok(await File.ReadAllTextAsync(backupListPath)) : Results.NotFound();
        });

        app.MapPost("/timelapses/create", async ([FromBody] TimelapseInformation timelapseInfo, HttpContext context) =>
        {
            if (!gameData.TimelapseEnabled)
            {
                context.Response.StatusCode = StatusCodes.Status501NotImplemented;
                await context.Response.WriteAsync("Timelapse generation is disabled on this instance");
                return;
            }

            var address = context.Connection.RemoteIpAddress;
            if (address is null || !timelapseLimiter.IsAuthorised(address))
            {
                Logger?.Invoke($"Timelapse generation rejected for {address}");
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                return;
            }

            await using var stream = await TimelapseGenerator.GenerateTimelapseAsync(timelapseInfo, gameData);
            context.Response.ContentType = "video/webm";
            await stream.CopyToAsync(context.Response.Body);
        });

        app.MapGet("/statistics", () =>
        {
            var performanceStats = new PerformanceStatistics(
                GC.GetTotalMemory(false),
                cpuUsagePercentage,
                backupsCount,
                backupsSize);
            return Results.Ok(performanceStats);
        });

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
            Interval = gameData.BackupFrequencyS * 1000
        };

        timer.Elapsed += async (_, _) =>
        {
            if (!gameData.CreateBackups)
            {
                return;
            }
            
            timer.Interval = gameData.BackupFrequencyS * 1000;
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
        await File.WriteAllBytesAsync(Path.Combine(gameData.CanvasFolder, "place"), instance.Board);

        // Save a dated backup of the canvas to timestamp the place file at this point in time
        var backupName = "place " + DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss");
        await using var backupList = new StreamWriter(Path.Combine(gameData.CanvasFolder, "backuplist.txt"), append: true);
        await backupList.WriteLineAsync(backupName);
        await backupList.FlushAsync();

        var boardPath = Path.Combine(gameData.CanvasFolder, backupName);
        var boardData = BoardPacker.PackBoard(instance.Board, gameData.Palette, gameData.BoardWidth, gameData.BoardHeight);
        await File.WriteAllBytesAsync(boardPath, boardData);
        backupsCount++;
        backupsSize += boardData.Length;

        CanvasBackupCreated?.Invoke(this, new CanvasBackupCreatedEventArgs(instance, backupName, DateTime.Now, boardPath));
    }

    public async Task StopAsync()
    {
        await SaveCanvasBackupAsync();
        await app.StopAsync();
    }
}