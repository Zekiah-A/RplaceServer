// TKOfficial/RplaceServer
//Copyright (C) 2022 Zekiah-A (https://github.com/Zekiah-A)
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

using System.Text.Json;
using RplaceServer;
using Terminal.Gui;
using Tomlet;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TKOfficial;

public static class Program
{

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public static ServerInstance Server { get; set; } = null!;
    public static Config Config { get; set; } = null!;

    private static void PrintOptions(IReadOnlyList<string> options, int selectedOption, int optionsTop)
    {
        Console.SetCursorPosition(0, optionsTop);
        for (var i = 0; i < options.Count; i++)
        {
            if (i == selectedOption)
            {
                Console.BackgroundColor = ConsoleColor.Gray;
                Console.ForegroundColor = ConsoleColor.Black;
            }
            Console.WriteLine($"{i}) {options[i]}");
            Console.ResetColor();
        }
    }

    private static int CreateOptions(params string[] options)
    {
        var selectedOption = 0;
        var optionsTop = Console.CursorTop;
        PrintOptions(options, selectedOption, optionsTop);

        while (true)
        {
            var keyInfo = Console.ReadKey(true);

            if (keyInfo.Key == ConsoleKey.UpArrow)
            {
                selectedOption = Math.Max(0, selectedOption - 1);
                PrintOptions(options, selectedOption, optionsTop);
            }
            else if (keyInfo.Key == ConsoleKey.DownArrow)
            {
                selectedOption = Math.Min(options.Length - 1, selectedOption + 1);
                PrintOptions(options, selectedOption, optionsTop);
            }
            else if (keyInfo.Key == ConsoleKey.Enter)
            {
                Console.SetCursorPosition(0, options.Length + 1);
                Console.WriteLine("Selected option: " + options[selectedOption]);
                break;
            }
        }
        return selectedOption;
    }

    private static async Task RunWithConfig(string path)
    {
        if (path.EndsWith(".json"))
        {
            Config = JsonSerializer.Deserialize<Config>(await File.ReadAllTextAsync(path))
                ?? throw new NullReferenceException();
        }
        else if (path.EndsWith(".yaml"))
        {
            var deserialiser = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();
            Config = deserialiser.Deserialize<Config>(await File.ReadAllTextAsync(path))
                ?? throw new NullReferenceException();
        }
        else if (path.EndsWith(".toml"))
        {
            Config = TomletMain.To<Config>(await File.ReadAllTextAsync(path));
        }
        else
        {
            throw new ArgumentException("Unsupported config file format", nameof(path));
        }
        Server = new ServerInstance(Config, Config.CertPath, Config.KeyPath, Config.Origin, Config.SocketPort, Config.HttpPort, Config.Ssl);

        AppDomain.CurrentDomain.ProcessExit += async (_, _) =>
        {
            await Server.WebServer.SaveCanvasBackupAsync();
            Application.Shutdown();
            await Server.StopAsync();
        };
        AppDomain.CurrentDomain.UnhandledException += async (_, exceptionEventArgs) =>
        {
            await Server.WebServer.SaveCanvasBackupAsync();
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
    
    public static async Task Main(string[] args)
    {
        var baseConfigPath = Directory.GetCurrentDirectory();
        var foundConfigs = Directory.GetFiles(baseConfigPath, "server_config.*").ToList();
    FindConfigs:
        switch (foundConfigs.Count)
        {
            case 0:
            {
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[Warning]: Could not find a game config file in " + baseConfigPath);
                Console.ResetColor();
            
                Console.WriteLine("Creating a new server config file. Please select a file format for TKOfficial to use first...");
                var selected = CreateOptions("JSON (.json)", "YAML (.yaml)", "TOML (.toml)", "Exit");
                if (selected == 3)
                {
                    Environment.Exit(0);
                }
                var defaultData = GameData.CreateGameData()
                    .ConfigureCanvas()
                    .ConfigureModeration()
                    .ConfigureServices()
                    .ConfigureStorage();
                var defaultConfig = new Config
                {
                    CooldownMs = defaultData.CooldownMs,
                    BoardWidth = defaultData.BoardWidth,
                    BoardHeight = defaultData.BoardHeight,
                    Palette = defaultData.Palette,
                    BackupFrequencyS = defaultData.BackupFrequencyS,
                    StaticResourcesFolder = defaultData.StaticResourcesFolder,
                    SaveDataFolder = defaultData.SaveDataFolder,
                    UseDatabase = defaultData.UseDatabase,
                    TimelapseLimitPeriodS = defaultData.TimelapseLimitPeriodS,
                    CanvasFolder = defaultData.CanvasFolder,
                    CreateBackups = defaultData.CreateBackups,
                    UseCloudflare = defaultData.UseCloudflare,
                    ChatCooldownMs = defaultData.ChatCooldownMs,
                    CaptchaEnabled = defaultData.CaptchaEnabled,
                    CensorChatMessages = defaultData.CensorChatMessages,
                    WebhookService = defaultData.WebhookService,
                    TurnstileService = defaultData.TurnstileService,
                };
                string configPath;
                switch (selected)
                {
                    case 0:
                    {
                        configPath = Path.Join(baseConfigPath, "server_config.json");
                        var config = JsonSerializer.Serialize(defaultConfig, JsonOptions);
                        await File.WriteAllTextAsync(configPath, config);
                        break;
                    }
                    case 1:
                    {
                        configPath = Path.Join(baseConfigPath, "server_config.yaml");
                        var serializer = new SerializerBuilder()
                            .WithNamingConvention(UnderscoredNamingConvention.Instance)
                            .Build();
                        await File.WriteAllTextAsync(configPath, serializer.Serialize(defaultConfig));
                        break;
                    }
                    case 2:
                    {
                        configPath = Path.Join(baseConfigPath, "server_config.toml");
                        var config = TomletMain.DocumentFrom(defaultConfig).SerializedValue;
                        await File.WriteAllTextAsync(configPath, config);
                        break;
                    }
                    default:
                    {
                        throw new ArgumentOutOfRangeException(nameof(selected));
                    }
                }
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n[INFO]: Config files recreated. Please check {configPath} and run this program again.");
                Console.ResetColor();
                Environment.Exit(0);
                break;
            }
            case 1:
            {
                var config = foundConfigs[0];
                if (config.EndsWith(".json") || config.EndsWith(".yaml") || config.EndsWith(".toml"))
                {
                    await RunWithConfig(config);
                }
                else
                {
                    foundConfigs.Remove(config);
                    Console.WriteLine($"Unknown config {config}, ignoring");
                    goto FindConfigs;
                }

                break;
            }
            case > 1:
            {
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Multiple server config files found!");
                Console.ResetColor();
                Console.WriteLine("Please select which one you want TKOfficial to use:");
                var selected = CreateOptions(foundConfigs.ToArray());
                await RunWithConfig(foundConfigs[selected]);
                break;
            }
        }
    }
}
