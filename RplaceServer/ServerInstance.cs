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

using System.Net;
using RplaceServer;
using RplaceServer.Exceptions;

public sealed class ServerInstance
{
    public GameData GameData;
    public SocketServer SocketServer { get; set; }
    public WebServer WebServer { get; set; }
    public Action<string>? Logger;

    public ServerInstance(GameData gameData, string certPath, string keyPath, string origin, int socketPort, int webPort, bool ssl)
    {
        GameData = gameData;
        SocketServer = new SocketServer(gameData, certPath, keyPath, origin, ssl, socketPort);
        WebServer = new WebServer(gameData, certPath, keyPath, origin, ssl, webPort);
        
        try
        {
            var boardBytes = File.ReadAllBytes(Path.Join(gameData.CanvasFolder, "place"));
            if (boardBytes.Length == 0)
            {
                throw new NoCanvasFileFoundException(
                    "Could not read canvas file at", Path.Join(gameData.CanvasFolder, "place"));
            }
            
            gameData.Board = boardBytes;
        }
        catch (Exception exception)
        {
            Logger?.Invoke(exception.Message);
            gameData.Board = new byte[gameData.BoardWidth * gameData.BoardHeight];

            if (!Directory.Exists(gameData.CanvasFolder))
            {
                Directory.CreateDirectory(gameData.CanvasFolder);
                Logger?.Invoke("Created new canvas folder.");
            }
            
            File.WriteAllBytes(Path.Join(gameData.CanvasFolder, "place"), gameData.Board);
        }

        if (File.Exists("bans.txt"))
        {
            GameData.Bans.AddRange(File.ReadAllLines("bans.txt"));
        }
        else
        {
            Logger?.Invoke($"Could not find bans file at {Path.Join(gameData.CanvasFolder, "bans.txt")}. Will be regenerated when a player is banned");
        }
    }
    
    public async Task StartAsync()
    {
        await Task.WhenAll(SocketServer.StartAsync(), WebServer.StartAsync());
    }

    public async Task StopAsync()
    {
        await SocketServer.StopAsync();
        await WebServer.StopAsync();
    }
}