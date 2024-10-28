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
using System;
using System.Reflection;
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
    private static string baseConfigPath = null!;

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

    private static async Task<string> WriteDefaultConfig(ConfigFormat format)
    {
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
            TimelapseLimitPeriodS = defaultData.TimelapseLimitPeriodS,
            CanvasFolder = defaultData.CanvasFolder,
            CreateBackups = defaultData.CreateBackups,
            UseCloudflare = defaultData.UseCloudflare,
            ChatCooldownMs = defaultData.ChatCooldownMs,
            CaptchaEnabled = defaultData.CaptchaEnabled,
            CensorChatMessages = defaultData.CensorChatMessages,
            ChatCensorRegexes = defaultData.ChatCensorRegexes,
            WebhookService = defaultData.WebhookService,
            TurnstileService = defaultData.TurnstileService,
        };
        switch (format)
        {
            case ConfigFormat.Json:
            {
                var configPath = Path.Combine(baseConfigPath, "server_config.json");
                var config = JsonSerializer.Serialize(defaultConfig, JsonOptions);
                await File.WriteAllTextAsync(configPath, config);
                return configPath;
            }
            case ConfigFormat.Yaml:
            {
                var configPath = Path.Combine(baseConfigPath, "server_config.yaml");
                var serializer = new SerializerBuilder()
                    .WithNamingConvention(UnderscoredNamingConvention.Instance)
                    .Build();
                await File.WriteAllTextAsync(configPath, serializer.Serialize(defaultConfig));
                return configPath;
            }
            case ConfigFormat.Toml:
            {
                var configPath = Path.Combine(baseConfigPath, "server_config.toml");
                var config = TomletMain.DocumentFrom(defaultConfig).SerializedValue;
                await File.WriteAllTextAsync(configPath, config);
                return configPath;
            }
            default:
            {
                throw new ArgumentOutOfRangeException(nameof(format));
            }
        }
    }

    private static async Task RunWithConfig(string path)
    {
        ConfigFormat format;
        if (path.EndsWith(".json"))
        {
            Config = JsonSerializer.Deserialize<Config>(await File.ReadAllTextAsync(path))
                     ?? throw new NullReferenceException();
            format = ConfigFormat.Json;
        }
        else if (path.EndsWith(".yaml"))
        {
            var deserialiser = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();
            Config = deserialiser.Deserialize<Config>(await File.ReadAllTextAsync(path))
                     ?? throw new NullReferenceException();
            format = ConfigFormat.Yaml;
        }
        else if (path.EndsWith(".toml"))
        {
            Config = TomletMain.To<Config>(await File.ReadAllTextAsync(path));
            format = ConfigFormat.Toml;
        }
        else
        {
            throw new ArgumentException("Unsupported config file format", nameof(path));
        }

        // If config is outdated, move old config, generate new and bail
        if (Config.Version < Config.LatestVersion)
        {
            var moveLocation = $"{path}.v{Config.Version}.old";
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[Warning]: Config is outdated, moving config from {0} to {1} and generating new config.",
                path, moveLocation);
            Console.ResetColor();
            File.Move(path, moveLocation);
            var configPath = await WriteDefaultConfig(format);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(
                $"\n[INFO]: Config file recreated. Please check {configPath} and run this program again.");
            Console.ResetColor();
            Environment.Exit(0);
        }

        Server = new ServerInstance(Config, Config.CertPath, Config.KeyPath, Config.Origin, Config.SocketPort,
            Config.HttpPort, Config.Ssl);
        
        // Ensure server has created SaveData directory, etc
        await Server.CreateRequiredFilesAsync();
        
        // Copy build resources into server software's StaticResources folder.
        var buildContentPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? Directory.GetCurrentDirectory(), "Resources");
        FileUtils.RecursiveCopy(buildContentPath, Config.StaticResourcesFolder);

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
            Application.Run<ConsoleWindow>();
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
        baseConfigPath = Directory.GetCurrentDirectory();
        var foundConfigs = Directory.GetFiles(baseConfigPath, "server_config.*").ToList();
        foundConfigs = foundConfigs.Where(config => !config.EndsWith(".old")).ToList();
    FindConfigs:
        switch (foundConfigs.Count)
        {
            case 0:
            {
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[Warning]: Could not find a server config file in " + baseConfigPath);
                Console.ResetColor();

                Console.WriteLine("Creating a new server config file. Please select a file format for TKOfficial to use first...");
                var selected = CreateOptions("JSON (.json)", "YAML (.yaml)", "TOML (.toml)", "Exit");
                if (selected == 3)
                {
                    Environment.Exit(0);
                }
                var configPath = await WriteDefaultConfig((ConfigFormat) selected);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n[INFO]: Config file created. Please check {configPath} and run this program again.");
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
