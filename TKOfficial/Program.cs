//rplace.tk
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
using Nephrite;
using RplaceServer;

namespace TKOfficial;

public static class Program
{
    private const string ConfigPath = "server_config.json";

    private static readonly JsonSerializerOptions JsonOptions = new() {WriteIndented = true};

    private static List<string> replPrevious = new();
    private static int replPreviousIndex;

    public static async Task Main(string[] args)
    {
        if (!File.Exists(ConfigPath))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("[Warning]: Could not game config file, at " + ConfigPath);

            var defaultConfig = new Config(5, true, true, new List<string>(), new List<string>(), 1000, 1000, 600,
                false, "Canvases", "", "", "https://rplace.tk", 443, 80, false);
            await File.WriteAllTextAsync(ConfigPath, JsonSerializer.Serialize(defaultConfig, JsonOptions));

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n[INFO]: Config files recreated. Please check {Directory.GetCurrentDirectory()} and run this program again.");
            Console.ResetColor();
            Environment.Exit(0);
        }

        var config = JsonSerializer.Deserialize<Config>(await File.ReadAllTextAsync(ConfigPath)) ?? throw new NullReferenceException();
        var server = new ServerInstance(config, config.CertPath, config.KeyPath, config.Origin, config.SocketPort, config.HttpPort, config.Ssl);

        await Task.WhenAll(server.Start(), StartNephriteRepl());
    }
    
    private static async Task StartNephriteRepl()
    {
        var runner = new NephriteRunner();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"You have entered the rPlace server Nephrite REPL. Enter a command to run it.\n");
        Console.ResetColor();

        var input = "";
        Console.Write(">> ");

        while (true)
        {
            var key = Console.ReadKey();
            
            switch (key.Key)
            {
                case ConsoleKey.Backspace:
                    Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                    if (input?.Length < 1) continue;
                    input = input?[..^1];
                    Console.Write("\b \b");
                    continue;
                case ConsoleKey.UpArrow:
                    input = replPrevious.ElementAtOrDefault(^replPreviousIndex);
                    replPreviousIndex++;
                    continue;
            }

            input += key.KeyChar.ToString();
            if (key.Key != ConsoleKey.Enter) continue;
            
            Console.WriteLine();
            Console.Write(">> ");

            if (!string.IsNullOrEmpty(input))
            {
                await runner.Execute(input);
            }

            replPrevious.Add(input);
            input = "";
        }
    }
}