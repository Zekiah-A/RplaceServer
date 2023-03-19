// TKOfficial/RplaceServer
//Copyright (C) 2022 Zekiah-A (https://github.com/Zekiah-A)

// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.

// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System.Text.Json;
using Terminal.Gui;

namespace TKOfficial;

public static class Program
{
    private const string ConfigPath = "server_config.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public static ServerInstance Server { get; set; } = null!;
    public static Config Config { get; set; } = null!;

    public static async Task Main(string[] args)
    {
        if (!File.Exists(ConfigPath))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("[Warning]: Could not game config file, at " + ConfigPath);

            var defaultConfig = new Config(5000, 2500, true, true, new List<string>(),
            new List<string>(), 1000, 1000, 600000,  false, 
        "Canvases", "Posts", 60, 300, true, "", "",
            "", 8080, 8081, false);
            
            await File.WriteAllTextAsync(ConfigPath, JsonSerializer.Serialize(defaultConfig, JsonOptions));

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n[INFO]: Config files recreated. Please check {Directory.GetCurrentDirectory()} and run this program again.");
            Console.ResetColor();
            Environment.Exit(0);
        }

        Config = JsonSerializer.Deserialize<Config>(await File.ReadAllTextAsync(ConfigPath)) ?? throw new NullReferenceException();
        Server = new ServerInstance(Config, Config.CertPath, Config.KeyPath, Config.Origin, Config.SocketPort, Config.HttpPort, Config.Ssl);
        
        AppDomain.CurrentDomain.ProcessExit += async (_, _) =>
        {
            Application.Shutdown();
            await Server.StopAsync();
        };
        
        AppDomain.CurrentDomain.UnhandledException += async (sender, exceptionEventArgs) =>
        {
            Application.Shutdown();
            await Server.StopAsync();
            Console.WriteLine("Unhandled server exception: " + exceptionEventArgs.ExceptionObject);
        };
        
        try
        {
            var serverTask = Task.Run(async () => await Server.StartAsync());
            Application.Run<ConsoleWindow>();
            await serverTask;
        }
        catch (Exception exception)
        {
            Application.Shutdown();
            await Server.StopAsync();
            Console.WriteLine("Unexpected server exception: " + exception);
        }
    }
}