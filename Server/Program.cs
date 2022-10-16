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

namespace Server;

public static class Program
{
    private const string ProgramConfigPath = "server_config.json";
    private const string SocketConfigPath = "game_server_config.json";
    private const string WebConfigPath = "canvas_server_config.json";

    private static readonly JsonSerializerOptions JsonOptions = new() {WriteIndented = true};

    private static SocketServer? socketServer;
    private static WebServer? webServer;
    private static List<string> replPrevious = new();
    private static int replPreviousIndex;

    public static void Main(string[] args)
    {
        var missing = CheckFilesMissing(new[] {ProgramConfigPath, SocketConfigPath, WebConfigPath});
        if (missing.Count != 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("[Warning]: Could not find config files: ");
            while (missing.Count > 0)
            {
                var missingPath = missing.Pop();
                Console.Write(missingPath + ", ");

                dynamic config = missingPath switch
                {
                    ProgramConfigPath => new ProgramConfig(true, 443, 8080, "", "", "https://rplace.tk", false, "Backups"),
                    SocketConfigPath => new SocketServerConfig(1000, 1000, 31, 10, true, new List<string>(), new List<string>(), ""),
                    WebConfigPath => new WebServerConfig(6000),
                    _ => throw new ArgumentOutOfRangeException()
                };

                File.WriteAllText(missingPath, JsonSerializer.Serialize(config, JsonOptions));
            }
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n[INFO]: Config files recreated. Please check {Directory.GetCurrentDirectory()} and run this program again.");
            Console.ResetColor();
            Environment.Exit(0);
        }

        var programConfig = JsonSerializer.Deserialize<ProgramConfig>(File.ReadAllText(ProgramConfigPath)) ??
                            throw new NullReferenceException();
        var socketConfig = JsonSerializer.Deserialize<SocketServerConfig>(File.ReadAllText(SocketConfigPath)) ??
                           throw new NullReferenceException();
        var webConfig = JsonSerializer.Deserialize<WebServerConfig>(File.ReadAllText(WebConfigPath)) ??
                        throw new NullReferenceException();
        
        socketServer = new SocketServer(programConfig, socketConfig);
        webServer = new WebServer(programConfig, webConfig);

        //socketServer.Start();
        //webServer.Start();
        StartNephriteRepl();
    }

    private static async void StartNephriteRepl()
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
            
            if (key.Key == ConsoleKey.Backspace)
            {
                Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                input.Remove(input.Length - 1);
                Console.Write("\b \b");
                continue;
            }

            if (key.Key == ConsoleKey.UpArrow)
            {
                input = replPrevious.ElementAtOrDefault(^replPreviousIndex);
                replPreviousIndex++;
                continue;
            }
            
            input += key.KeyChar;
            if (key.Key != ConsoleKey.Enter) continue;
            
            Console.WriteLine();
            Console.Write(">> ");

            if (!string.IsNullOrEmpty(input))
                await runner.Execute(input);

            replPrevious.Add(input);
            input = "";
        }
    }

    private static Stack<string> CheckFilesMissing(IEnumerable<string> files)
    {
        var targets = new Stack<string>();

        foreach (var file in files)
            if (!File.Exists(file))
                targets.Push(file);

        return targets;
    }

    public static void SendBoardToWebServer(byte[] canvas)
    {
        webServer?.IncomingBoard(canvas);
    }
}