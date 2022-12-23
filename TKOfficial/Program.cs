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

using System.Reflection;
using System.Text.Json;
using RplaceServer;
using WatsonWebsocket;

namespace TKOfficial;

public static class Program
{
    private const string ConfigPath = "server_config.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static List<string> replPrevious = new();
    private static int replPreviousIndex;
    private static ServerInstance server;

    public static async Task Main(string[] args)
    {
        if (!File.Exists(ConfigPath))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("[Warning]: Could not game config file, at " + ConfigPath);

            var defaultConfig = new Config(5, true, true, new List<string>(), new List<string>(), 1000, 1000, 600,
                false, "Canvases", 300, true, "", "", "https://rplace.tk", 443, 80, false);
            await File.WriteAllTextAsync(ConfigPath, JsonSerializer.Serialize(defaultConfig, JsonOptions));

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n[INFO]: Config files recreated. Please check {Directory.GetCurrentDirectory()} and run this program again.");
            Console.ResetColor();
            Environment.Exit(0);
        }

        var config = JsonSerializer.Deserialize<Config>(await File.ReadAllTextAsync(ConfigPath)) ?? throw new NullReferenceException();
        server = new ServerInstance(config, config.CertPath, config.KeyPath, config.Origin, config.SocketPort, config.HttpPort, config.Ssl);

        await Task.WhenAll(server.Start(), StartNephriteRepl());

        if (config.LogToConsole)
        {
            server.SocketServer.Logger = message =>
            {
                Console.WriteLine("[SocketServer]: " + message);
            };

            server.WebServer.Logger = message =>
            {
                Console.WriteLine("[WebServer]: " + message);
            };
        }
    }
    
    private static async Task StartNephriteRepl()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"You have entered the rPlace server Nephrite REPL. Enter a command to run it.\n");
        Console.ResetColor();

        object? variable = null;
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
                input = input.Replace("\n", "").Replace("\r", "");
                var sections = input.Split(" ");

                switch (sections.ElementAtOrDefault(0))
                {
                    case "server":
                        var args = sections[2..];
                        
                        switch (sections.ElementAtOrDefault(1))
                        {
                            case "expand_canvas":
                                server.SocketServer.ExpandCanvas(
                                    int.Parse(args[0]),
                                    int.Parse(args[1])
                                );
                                break;
                            case "broadcast_chat_message":
                                if (variable is null)
                                {
                                    server.SocketServer.BroadcastChatMessage(
                                        args[0],
                                        args[1]
                                    );
                                }
                                else
                                {
                                    server.SocketServer.BroadcastChatMessage(
                                        args[0],
                                        args[1],
                                         (ClientMetadata) variable
                                    );
                                }
                                break;
                            case "fill":
                                server.SocketServer.Fill(
                                    int.Parse(args[0]),
                                    int.Parse(args[1]),
                                    int.Parse(args[2]),
                                    int.Parse(args[3]),
                                    byte.Parse(args[4])
                                );
                                break;
                            default:
                                Console.WriteLine("Commands: fill [startX, startY, endX, endY, colour]," +
                                                  "expand_canvas [widthIncrease, heightIncrease]," +
                                                  "broadcast_chat_message [message, channel, client]");
                                break;
                        }
                        break;
                    case "data":
                        var property = sections.ElementAtOrDefault(1);
                        if (property is null)
                        {
                            Console.WriteLine("Put the name of a GameData variable after this command to display " +
                                              "that variable and store it in the 'variable' variable");
                            break;
                        }
                        
                        var data = server.GameData.GetType().GetProperty(property);
                        variable = data?.GetValue(server.GameData);
                        
                        Console.WriteLine(variable + " loaded into 'variable'");
                        break;
                    case "clients":
                        var command = sections.ElementAtOrDefault(1);
                        
                        switch (command)
                        {
                            case "list":
                                foreach (var client in server.GameData.Clients.Keys)
                                {
                                    Console.Write(client.IpPort + ", ");
                                }
                                break;
                            case "data":
                                var clientPair = server.GameData.Clients.FirstOrDefault(key =>
                                        key.Key.IpPort == sections.ElementAtOrDefault(2));
                                
                                Console.WriteLine(clientPair.Key + " loaded into 'variable'");
                                variable = clientPair.Key;
                                break;
                            default:
                                Console.WriteLine("Commands: list (list ip:port of all players), " +
                                                  "data [ip:port] (stores that player's instance into 'variable')");
                                break;
                        }
                        break;
                }
            }

            replPrevious.Add(input);
            input = "";
        }
    }
}