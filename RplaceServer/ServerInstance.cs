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

using Microsoft.Extensions.Logging;
using RplaceServer;
using RplaceServer.Events;

public class ServerInstance
{
    private GameData data;
    private SocketServer socketServer;
    private WebServer webServer;
    
    public ServerInstance(GameData data, string certPath, string keyPath, string origin, int socketPort, int webPort, bool ssl)
    {
        this.data = data;
        socketServer = new SocketServer(data, certPath, keyPath, origin, ssl, socketPort);
        webServer = new WebServer(data, certPath, keyPath, origin, ssl, webPort);
    }

    public async Task Start()
    {
        await socketServer.Start();
        await webServer.Start();
    }
    
    public event EventHandler CanvasBackupCreated;
}