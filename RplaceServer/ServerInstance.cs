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
using RplaceServer;

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
    }

    private async Task CreateNewBoardAsync()
    {
        GameData.Board = new byte[GameData.BoardWidth * GameData.BoardHeight];

        if (!Directory.Exists(GameData.CanvasFolder))
        {
            Directory.CreateDirectory(GameData.CanvasFolder);
            Logger?.Invoke("Could not find canvas folder. Regenerating");
        }
            
        await File.WriteAllBytesAsync(Path.Join(GameData.CanvasFolder, "place"), GameData.Board);
    }
    
    public async Task StartAsync()
    {
        var boardPath = Path.Join(GameData.CanvasFolder, "place");
        if (!File.Exists(boardPath))
        {
            Logger?.Invoke($"Could not find board. Regenerating with width: {GameData.BoardWidth}, height: {GameData.BoardHeight}");
            await CreateNewBoardAsync();
        }
        else
        {
            var boardBytes = await File.ReadAllBytesAsync(boardPath);
            if (boardBytes.Length != GameData.BoardWidth * GameData.BoardHeight)
            {
                Logger?.Invoke("Board had invalid length (0). Regenerating.");
                await CreateNewBoardAsync();
            }
            else
            {
                GameData.Board = boardBytes;
            }
        }

        if (!Directory.Exists(GameData.StaticResourcesFolder))
        {
            Directory.CreateDirectory(GameData.StaticResourcesFolder);
            Logger?.Invoke($"Could not find Static resources folder at {GameData.StaticResourcesFolder}. Regenerating");
        }
        
        if (!Directory.Exists(GameData.SaveDataFolder))
        {
            Directory.CreateDirectory(GameData.SaveDataFolder);
            Logger?.Invoke($"Could not find Save Data folder at {GameData.SaveDataFolder}. Regenerating.");
        }

        await Task.WhenAll(SocketServer.StartAsync(), WebServer.StartAsync());
    }

    public async Task StopAsync()
    {
        await Task.WhenAll(SocketServer.StopAsync(), WebServer.StopAsync());
    }
}