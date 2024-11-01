using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using RplaceServer.Events;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using RplaceServer.TimelapseGeneration;
using RplaceServer.Types;

namespace RplaceServer;

public sealed class WebServer
{
    private readonly WebApplication app;
    private readonly ServerInstance instance;
    private readonly GameData gameData;
    private readonly RateLimiter timelapseLimiter;
    public Action<string>? Logger;

    private string pagesRoot;
    private double cpuUsagePercentage;
    private long backupsSize;
    private int backupsCount;
    
    public event EventHandler<CanvasBackupCreatedEventArgs>? CanvasBackupCreated;

    public WebServer(ServerInstance parentInstance, GameData data, string? certPath, string? keyPath, string origin, bool ssl, int port)
    {
        instance = parentInstance;
        gameData = data;
        timelapseLimiter = new RateLimiter(TimeSpan.FromMilliseconds(gameData.TimelapseLimitPeriodS));

        pagesRoot = Path.Join(gameData.StaticResourcesFolder, "Pages");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            WebRootPath = pagesRoot
        });

        builder.Services.AddCors(options =>
        {
            // We allow CORS on any endpoint except place, which will respect only from the origin so that we can ensure
            // legitimate clients.
            options.AddDefaultPolicy(policy => policy
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader());
            options.AddPolicy("PlacePolicy", policy => policy
                .WithOrigins(origin)
                .AllowAnyMethod()
                .AllowAnyHeader());
        });
        
        builder.WebHost.UseKestrel(options =>
        {
            options.ListenAnyIP(port, listenOptions =>
            {
                if (ssl && certPath != null && keyPath != null)
                {
                    var certificate = new X509Certificate2(certPath, keyPath);
                    listenOptions.UseHttps(certificate);
                }
            });
        });
        
        app = builder.Build();
        
        // Static page hosting
        app.UseDefaultFiles(new DefaultFilesOptions());
        app.UseStaticFiles(new StaticFileOptions
        {
            ServeUnknownFileTypes = true,
            FileProvider = new PhysicalFileProvider(Path.GetFullPath(pagesRoot)),
            RequestPath = ""
        });
        
        // More fine grained CORS policy than global server
        app.UseCors(policy => policy
            .WithOrigins(origin)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
        
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
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
            cpuUsagePercentage = cpuUsageTotal * 100;

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
            Logger?.Invoke($"Could not find 'static resources' webserver pages files at {pagesRoot}.");
            throw new FileNotFoundException(pagesRoot);
        }
        
        // Load backups from file
        var backups = new DirectoryInfo(gameData.CanvasFolder).GetFiles();
        backupsCount = backups.Length;
        backupsSize = backups.Sum(file => file.Length);
        
        //Serve absolute latest board from memory.
        app.MapGet("/place",() =>
        {
            var board = BoardPacker.RunLengthCompressBoard(instance.Board);
            return Results.Bytes(board);
        }).RequireCors("PlacePolicy");
        
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
            if (!gameData.TimelapseEnabled)
            {
                Logger?.Invoke($"Timelapse generation is disabled on this instance");
                return Results.Unauthorized();
            }
            
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
        await File.WriteAllBytesAsync(Path.Join(gameData.CanvasFolder, "place"), instance.Board);

        // Save a dated backup of the canvas to timestamp the place file at this point in time
        var backupName = "place " + DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss");
        await using var backupList = new StreamWriter(Path.Join(gameData.CanvasFolder, "backuplist.txt"), append: true);
        await backupList.WriteLineAsync(backupName);
        await backupList.FlushAsync();

        var boardPath = Path.Join(gameData.CanvasFolder, backupName);
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