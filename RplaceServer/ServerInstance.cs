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

    public ServerInstance(GameData gameData, string certPath, string keyPath, string origin, int socketPort, int webPort, bool ssl)
    {
        GameData = gameData;
        SocketServer = new SocketServer(gameData, certPath, keyPath, origin, ssl, socketPort);
        WebServer = new WebServer(gameData, certPath, keyPath, origin, ssl, webPort);
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