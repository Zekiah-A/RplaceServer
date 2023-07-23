using RplaceServer;
using RplaceServer.Types;
using Terminal.Gui;
using WatsonWebsocket;

namespace TKOfficial;

public class ConsoleWindow : Window
{
    public Action<string>? Logger;

    public ConsoleWindow()
    {
        Initialise();
    }

    private void Initialise()
    {
        Title = "TKOfficial CLI Environment -> ©Zekiah-A (Ctrl+Q to quit)";

        // Server actions stack panel container
        var serverActionsContainer = new View
        {
            Width = 32,
            Height = 16
        };
        var expandCanvasButton = new Button
        {
            Text = "Expand canvas",
            Y = Pos.Top(serverActionsContainer) + 1
        };
        expandCanvasButton.Clicked += () =>
        {
            var expandCanvasWizard = new Wizard("")
            {
                Modal = false,
                Width = 32,
                Height = 9
            };

            var firstStep = new Wizard.WizardStep("Edit chat message cooldown");
            var expandXField = new TextField("0")
            {
                Width = Dim.Fill(),
                Y = 1
            };
            var expandYField = new TextField("0")
            {
                Width = Dim.Fill(),
                Y = 3
            };
            var colourIndexField = new TextField("0")
            {
                Width = Dim.Fill(),
                Y = 5
            };

            firstStep.Add(new Label { Text = "Expand X (Width)" }, expandXField,
                new Label { Y = 2, Text = "Expand Y (Height)" }, expandYField,
                new Label { Y = 4, Text = "Expand colour index" }, colourIndexField);

            expandCanvasWizard.AddStep(firstStep);
            expandCanvasWizard.Finished += _ =>
            {
                if (!int.TryParse(expandXField.Text.ToString(), out var expandWidth))
                {
                    Logger?.Invoke("Failed to expand, invalid Expand X Parameter");
                    goto closeWizard;
                }
                if (!int.TryParse(expandYField.Text.ToString(), out var expandHeight))
                {
                    Logger?.Invoke("Failed to expand, invalid Expand Y Parameter");
                    goto closeWizard;
                }
                if (!int.TryParse(colourIndexField.Text.ToString(), out var colourIndex) ||
                    colourIndex > (Program.Server.GameData.Palette?.Count ?? 31) || colourIndex < 0)
                {
                    Logger?.Invoke("Failed to expand, invalid Colour Index Parameter");
                    goto closeWizard;
                }
                
                var formattedColour = colourIndex.ToString();
                if (Program.Server.GameData.Palette?.ElementAtOrDefault(colourIndex) is not null)
                {
                    formattedColour = RgbFormatColour(Program.Server.GameData.Palette[colourIndex]);
                }
                
                var originalDimensions = (Program.Server.GameData.BoardWidth, Program.Server.GameData.BoardHeight);
                var newDimensions = Program.Server.SocketServer.ExpandCanvas((uint) expandWidth, (uint) expandHeight, colourIndex);
                var difference = newDimensions.NewWidth * newDimensions.NewHeight - originalDimensions.BoardWidth * originalDimensions.BoardHeight;

                Logger?.Invoke($"Expanded canvas size to ({newDimensions.NewWidth}, {newDimensions.NewHeight}), with colour {formattedColour}, ({difference} pixels)");
closeWizard:
                Application.Top.Remove(expandCanvasWizard);
                Application.RequestStop();
                Application.Run(Application.Top);
            };
            
            Application.Top.Add(expandCanvasWizard);
            Application.Run(Application.Top);
        };

        var fillCanvasButton = new Button
        {
            Text = "Fill canvas area",
            Y = Pos.Top(serverActionsContainer) + 2
        };
        fillCanvasButton.Clicked += () =>
        {
            var fillCanvasWizard = new Wizard("")
            {
                Modal = false,
                Width = 32,
                Height = 13,
            };

            var firstStep = new Wizard.WizardStep("Fill canvas area");
            var xStartField = new TextField("0")
            {
                Width = Dim.Fill(),
                Y = 1
            };
            var yStartField = new TextField("0")
            {
                Width = Dim.Fill(),
                Y = 3
            };
            var xEndField = new TextField("0")
            {
                Width = Dim.Fill(),
                Y = 5
            };
            var yEndField = new TextField("0")
            {
                Width = Dim.Fill(),
                Y = 7
            };
            var colourIndexField = new TextField("0")
            {
                Width = Dim.Fill(),
                Y = 9
            };
            firstStep.Add(xStartField, new Label { Text = "Pixel X start" }, yStartField,
                new Label { Y = 2, Text = "Pixel Y start" }, xEndField, new Label { Y = 4, Text = "Pixel X end" },
                yEndField, new Label { Y = 6, Text = "Pixel Y end" },
                new Label { Y = 8, Text = $"Palette colour index (0 - {Program.Server.GameData.Palette?.Count ?? 31})" },
                colourIndexField);

            fillCanvasWizard.AddStep(firstStep);
            fillCanvasWizard.Finished += _ =>
            {
                if (!int.TryParse(xStartField.Text.ToString(), out var startX) || startX < 0)
                {
                    Logger?.Invoke("Failed to fill, invalid Start X Parameter");
                    goto closeWizard;
                }
                if (!int.TryParse(yStartField.Text.ToString(), out var startY) || startY < 0)
                {
                    Logger?.Invoke("Failed to fill, invalid Start Y Parameter");
                    goto closeWizard;
                }
                if (!int.TryParse(xEndField.Text.ToString(), out var endX) || endX > Program.Server.GameData.BoardWidth)
                {
                    Logger?.Invoke("Failed to fill, invalid End X Parameter");
                    goto closeWizard;
                }
                if (!int.TryParse(yEndField.Text.ToString(), out var endY) || endX > Program.Server.GameData.BoardHeight)
                {
                    Logger?.Invoke("Failed to fill, invalid End Y Parameter");
                    goto closeWizard;
                }
                if (!int.TryParse(colourIndexField.Text.ToString(), out var colourIndex) ||
                    colourIndex > (Program.Server.GameData.Palette?.Count ?? 31) || colourIndex < 0)
                {
                    Logger?.Invoke("Failed to fill, invalid Colour Index Parameter");
                    goto closeWizard;
                }
                
                var formattedColour = colourIndex.ToString();
                if (Program.Server.GameData.Palette?.ElementAtOrDefault(colourIndex) is not null)
                {
                    formattedColour = RgbFormatColour(Program.Server.GameData.Palette[colourIndex]);
                }
                var areaFilled = Program.Server.SocketServer.Fill(startX, startY, endX, endY, (byte) colourIndex);
                
                Logger?.Invoke($"Filled canvas area ({startX}, {startY}) to ({endX}, {endY}) with colour {formattedColour} ({areaFilled} pixels filled)");
closeWizard:
                Application.Top.Remove(fillCanvasWizard);
                Application.RequestStop();
                Application.Run(Application.Top);
            };
            
            Application.Top.Add(fillCanvasWizard);
            Application.Run(Application.Top);
        };

        var chatCooldownButton = new Button
        {
            Text = "Edit chat cooldown",
            Y = Pos.Top(serverActionsContainer) + 3
        };
        chatCooldownButton.Clicked += () =>
        {
            var cooldownWizard = new Wizard("")
            {
                Modal = false,
                Width = 32,
                Height = 4,
            };

            var firstStep = new Wizard.WizardStep("Edit chat message cooldown");
            var cooldownField = new TextField(Program.Server.GameData.ChatCooldown.ToString())
            {
                Width = Dim.Fill(),
            };
            firstStep.Add(cooldownField);

            cooldownWizard.AddStep(firstStep);
            cooldownWizard.Finished += _ =>
            {
                if (int.TryParse(cooldownField.Text.ToString(), out var cooldown))
                {
                    Program.Server.GameData.ChatCooldown = cooldown;
                }
                
                Logger?.Invoke($"Updated game chat message cooldown to {Program.Server.GameData.ChatCooldown}ms");
                Application.Top.Remove(cooldownWizard);
                Application.RequestStop();
                Application.Run(Application.Top);
            };
            
            Application.Top.Add(cooldownWizard);
            Application.Run(Application.Top);
        };

        var broadcastChatButton = new Button
        {
            Text = "Broadcast chat message",
            Y = Pos.Top(serverActionsContainer) + 4
        };
        broadcastChatButton.Clicked += () =>
        {
            var chatWizard = new Wizard("")
            {
                Modal = false,
                Width = 32,
                Height = 7,
            };
            
            var firstStep = new Wizard.WizardStep("Broadcast chat message");
            var textInput = new TextField("Message from the server")
            {
                Width = Dim.Fill(),
                Y = 1
            };
            var channelInput = new TextField("en")
            {
                Width = Dim.Fill(),
                Y = 3
            };
            firstStep.Add(new Label { Text = "Message:" }, textInput, new Label { Text = "Channel:", Y = 2 }, channelInput);

            chatWizard.AddStep(firstStep);
            chatWizard.Finished += _ =>
            {
                Program.Server.SocketServer.BroadcastChatMessage(textInput.Text.ToString(), channelInput.Text.ToString());
                Logger?.Invoke($"Sent chat message '{textInput.Text}' in channel '{channelInput.Text}'");
                Application.Top.Remove(chatWizard);
                Application.RequestStop();
                Application.Run(Application.Top);
            };
            
            Application.Top.Add(chatWizard);
            Application.Run(Application.Top);
        };

        var changeGameCooldownButton = new Button
        {
            Text = "Edit place cooldown",
            Y = Pos.Top(serverActionsContainer) + 5
        };
        changeGameCooldownButton.Clicked += () =>
        {
            var cooldownWizard = new Wizard("")
            {
                Modal = false,
                Width = 32,
                Height = 4,
            };
            
            var firstStep = new Wizard.WizardStep("Edit pixel place cooldown");
            var cooldownField = new TextField(Program.Server.GameData.Cooldown.ToString())
            {
                Width = Dim.Fill(),
            };
            firstStep.Add(cooldownField);

            cooldownWizard.AddStep(firstStep);
            cooldownWizard.Finished += _ =>
            {
                if (int.TryParse(cooldownField.Text.ToString(), out var cooldown))
                {
                    Program.Server.GameData.Cooldown = (uint) cooldown;
                }

                Logger?.Invoke($"Updated game pixel place cooldown to {Program.Server.GameData.Cooldown} ms");
                Application.Top.Remove(cooldownWizard);
                Application.RequestStop();
                Application.Run(Application.Top);
            };
            
            Application.Top.Add(cooldownWizard);
            Application.Run(Application.Top);
        };

        var editPaletteButton = new Button
        {
            Text = "Edit colour palette",
            Y = Pos.Top(serverActionsContainer) + 6
        };
        editPaletteButton.Clicked += () =>
        {
            var paletteWizard = new Wizard("")
            {
                Modal = false,
                Width = 64,
                Height = 5,
            };
            
            var firstStep = new Wizard.WizardStep("Edit colour palette"); 
            var paletteField = new TextField(string.Join(", ", Program.Server.GameData.Palette))
            {
                Width = Dim.Fill(),
                Y = 1
            };
            firstStep.Add(new Label { Text = "Comma separated integer palette colours" }, paletteField);

            paletteWizard.AddStep(firstStep);
            paletteWizard.Finished += _ =>
            {
                var newPalette = new List<uint>();
                var split = paletteField.Text.ToString().Split(',');

                if (split.Length == 0)
                {
                    Program.Server.GameData.Palette = null;
                    Logger?.Invoke("Cleared colour palette, server will use default game palette");
                    goto closeWizard;
                }
                
                for (var index = 0; index < split.Length; index++)
                {
                    if (uint.TryParse(split[index].Trim(), out var uintValue))
                    {
                        newPalette.Add(uintValue);
                        continue;
                    }
                    
                    Logger?.Invoke($"Could not add beyond the {GetOrdinalSuffix(index + 1)} element due to it not being a correctly formatted number.");
                    break;
                }

                Program.Server.GameData.Palette = newPalette;
                Logger?.Invoke("Updated colour palette to: " +
                              string.Join(", ", Program.Server.GameData.Palette ?? new List<uint>()) +
                              " with a length of " + (Program.Server.GameData.Palette?.Count ?? 0));
                
closeWizard:
                Application.Top.Remove(paletteWizard);
                Application.RequestStop();
                Application.Run(Application.Top);
            };
            
            Application.Top.Add(paletteWizard);
            Application.Run(Application.Top);
        };
        
        var restoreBackupButton = new Button
        {
            Text = "Restore canvas from backup",
            Y = Pos.Top(serverActionsContainer) + 7
        };
        restoreBackupButton.Clicked += () =>
        {
            var restoreWizard = new Wizard("")
            {
                Modal = false,
                Width = 64,
                Height = 5,
            };
            
            var firstStep = new Wizard.WizardStep("Restore canvas from backup"); 
            var restoreField = new TextField(Program.Server.GameData.CanvasFolder)
            {
                Width = Dim.Fill(),
                Y = 1
            };
            firstStep.Add(new Label { Text = "Enter the path to the canvas backup to restore from" }, restoreField);

            restoreWizard.AddStep(firstStep);
            restoreWizard.Finished += _ =>
            {
                if (RestoreFromBackup(restoreField.Text.ToString()) is { } unpackedInfo)
                {
                    Logger?.Invoke($"Successfully restored canvas from provided backup. Restored canvas length: : " +
                        $"{unpackedInfo.Board.Length}, restored palette length: {string.Join(", ", unpackedInfo.Palette)}, " +
                        $"restored canvas dimensions {unpackedInfo.Width}x{unpackedInfo.Board.Length/unpackedInfo.Width}");
                }
                else
                {
                    Logger?.Invoke("Could not restore canvas from provided backup. Make sure the path is pointing to a valid canvas backup.");
                }

                Application.Top.Remove(restoreWizard);
                Application.RequestStop();
                Application.Run(Application.Top);
            };
            
            Application.Top.Add(restoreWizard);
            Application.Run(Application.Top);
        };

        var saveCanvasButton = new Button
        {
            Text = "Save canvas to disk",
            Y = Pos.Top(serverActionsContainer) + 8
        };
        saveCanvasButton.Clicked += async () =>
        {
            Logger?.Invoke("Canvas saved to disk");
            await Program.Server.WebServer.SaveCanvasBackupAsync();
        };

        var pruneBackupsButton = new Button
        {
            Text = "Prune backup list",
            Y = Pos.Top(serverActionsContainer) + 9
        };
        pruneBackupsButton.Clicked += () => Task.Run(PruneBackupList);

        var stopServerButton = new Button
        {
            Text = "Gracefully stop server",
            Y = Pos.Top(serverActionsContainer) + 10
        };
        stopServerButton.Clicked += async () =>
        {
            Logger?.Invoke("Server shutdown request received");
            await Program.Server.WebServer.SaveCanvasBackupAsync();
            Application.Shutdown();
            await Program.Server.StopAsync();
            Console.Clear();
        };
        
        serverActionsContainer.Add(expandCanvasButton, fillCanvasButton, chatCooldownButton,
            broadcastChatButton, changeGameCooldownButton, editPaletteButton, restoreBackupButton, saveCanvasButton,
            pruneBackupsButton, stopServerButton);
        // End server actions stack panel container
        
        // Server actions panel, provides nice border around container
        var serverActionsPanel = new PanelView
        {
            Border = new Border
            {
                BorderBrush = Color.White,
                BorderStyle = BorderStyle.Rounded,
                Title = "Server actions",
            },
            ColorScheme = Colors.Base,
            Child = serverActionsContainer
        };
        
        // Clients panel
        var clientsListView = new ListView(new Rect(0, 0, 64, 16),
            new List<string> { "No clients are connected yet... Invite some friends!" })
        {
            Width = Dim.Fill(),
        };
        clientsListView.SelectedItemChanged += (args) =>
        {
            var clientWizard = new Wizard("")
            {
                Modal = false,
                Width = 64,
                Height = 8,
                Border = new Border { Background = Color.White }
            };

            var selectedClientPair = Program.Server.GameData.Clients
                .FirstOrDefault(clientPair => clientPair.Value.IdIpPort.Equals(args.Value));

            if (selectedClientPair.Equals(default(KeyValuePair<ClientMetadata, ClientData>)))
            {
                return;
            }
            
            var firstStep = new Wizard.WizardStep("Player info");
            var ipLabel = new Label
            {
                Text = "Player IP/Port: " + args.Value,
                Y = 0
            };
            
            var vipLabel = new Label
            {
                Text = "Player VIP status:" + (selectedClientPair.Value.Vip ? "VIP" : selectedClientPair.Value.Admin ? "Admin" : "None"),
                Y = 1
            };
            var lastChatLabel = new Label
            {
                Text = "Player last chat: " + selectedClientPair.Value.LastChat,
                Y = 2
            };
            var kickButton = new Button
            {
                Text = "Kick player",
                Y = 3
            };
            kickButton.Clicked += async () =>
            {
                Logger?.Invoke($"Disconnected player {selectedClientPair.Value.IdIpPort}");
                await Program.Server.SocketServer.KickPlayer(selectedClientPair.Key);
            };
            var banButton = new Button
            {
                Text = "Ban player",
                Y = 4
            };
            banButton.Clicked += () =>
            {
                Logger?.Invoke($"Banned player {selectedClientPair.Value.IdIpPort}");
                Program.Server.SocketServer.BanPlayer(selectedClientPair.Key, 1000); //TODO: Add ban duration
            };
            firstStep.Add(ipLabel, vipLabel, lastChatLabel, kickButton, banButton);

            clientWizard.AddStep(firstStep);
            clientWizard.Finished += _ =>
            {
                Application.Top.Remove(clientWizard);
                Application.RequestStop();
                Application.Run(Application.Top);
            };
            
            Application.Top.Add(clientWizard);
            Application.Run(Application.Top);
        };
        var clientsPanel = new PanelView
        {
            X = Pos.Right(serverActionsPanel) + 2,
            Height = 16,
            Border = new Border
            {
                BorderBrush = Color.White,
                BorderStyle = BorderStyle.Rounded,
                Title = "Connected Clients"
            },
            ColorScheme = Colors.Base,
            Child = clientsListView
        };
        
        var statisticLogLabel = new Label
        {
            Text = "Server Statistics:",
            Y = Pos.Bottom(serverActionsPanel) + 2,
            X = Pos.Center()
        };
        var serverUptimeLabel = new Label
        {
            Text = "Server uptime: 00:00:00",
            Y = Pos.Bottom(statisticLogLabel),
            X = Pos.Center()
        };
        var serverIpPortLabel = new Label
        {
            Text = "Server IP Port: " + (Program.Config.Ssl ? "wss://" : "ws://") + "localhost:" +
                   Program.Config.SocketPort + " " + (Program.Config.Ssl ? "https://" : "http://") + "localhost:" +
                   Program.Config.HttpPort + "/place" + (Program.Config.UseCloudflare ? " (cloudflare)" : ""),
            Y = Pos.Bottom(serverUptimeLabel),
            X = Pos.Center()
        };
        var serverBackupPathLabel = new Label
        {
            Text = "Canvas backup path: " + new DirectoryInfo(Program.Config.CanvasFolder).FullName,
            Y = Pos.Bottom(serverIpPortLabel),
            X = Pos.Center()
        };
        var serverWebhookUrlLabel = new Label
        {
            Text = "Game chat webhook URL: " + (string.IsNullOrEmpty(Program.Config.WebhookUrl) ? "No webhook URL set" : Program.Config.WebhookUrl),
            Y = Pos.Bottom(serverBackupPathLabel),
            X = Pos.Center()
        };

        // Statistics log panel
        var serverLogs = new List<string>();
        var serverLogClear = new Button("Clear")
        {
            Y = Pos.AnchorEnd() - 11,
            X = Pos.AnchorEnd() - 10
        };
        serverLogClear.Clicked += () => serverLogs.Clear();
        var serverLogPanel = new PanelView
        {
            Y = Pos.AnchorEnd() - 10,
            Height = 8,
            Border = new Border
            {
                BorderBrush = Color.White,
                BorderStyle = BorderStyle.Rounded,
                Title = "Server logs"
            },
            ColorScheme = Colors.Base,
        };
        serverLogPanel.Child = new ListView(new Rect(0, 0, 128, 8), serverLogs)
        {
            Width = Dim.Fill()
        };

        Add(serverActionsPanel, clientsPanel, statisticLogLabel, serverUptimeLabel, serverIpPortLabel,
            serverBackupPathLabel, serverWebhookUrlLabel, serverLogPanel, serverLogClear);
        
        // Server uptime timer
        var elapsedSeconds = 0;
        var uptimeTimer = new System.Timers.Timer
        {
            AutoReset = true,
            Interval = TimeSpan.FromSeconds(1).TotalMilliseconds
        };
        uptimeTimer.Elapsed += (_, _) =>
        {
            serverUptimeLabel.Text = "Server uptime: " + TimeSpan.FromSeconds(elapsedSeconds);
            serverUptimeLabel.SetNeedsDisplay(serverUptimeLabel.Bounds);
            elapsedSeconds++;
        };
        uptimeTimer.Start();
        
        // Server logs panel
        if (Program.Config.LogToConsole)
        {
            Logger = message =>
            {
                serverLogs.Add("[TKOfficial " + DateTime.Now.ToString("hh:mm:ss") + "]: " + message);
            };
            
            Program.Server.Logger = message =>
            {
                serverLogs.Add("[ServerInstance " + DateTime.Now.ToString("hh:mm:ss") + "]: " + message);
            };

            Program.Server.SocketServer.Logger = message =>
            {
                serverLogs.Add("[SocketServer " + DateTime.Now.ToString("hh:mm:ss") + "]: " + message);
            };

            Program.Server.WebServer.Logger = message =>
            {
                serverLogs.Add("[WebServer " + DateTime.Now.ToString("hh:mm:ss") + "]: " + message);
            };
        }
        else
        {
            serverLogs.Add("[TKOfficial " + DateTime.Now.ToString("hh:mm:ss") + "]:" +
                           "Server logging to console has been disabled. Logs will not appear here.");
        }

        // Update clients panel with a list of all currently connected clients
        Program.Server.SocketServer.PlayerConnected += (_, _) =>
        {
            clientsListView.SetSource(Program.Server.GameData.Clients
                .Select(pair => pair.Value.IdIpPort)
                .ToList());
        };
        Program.Server.SocketServer.PlayerDisconnected += (_, _) =>
        {
            clientsListView.SetSource(Program.Server.GameData.Clients
                .Select(pair => pair.Value.IdIpPort)
                .ToList());
        };
        
        Logger?.Invoke("Server software started");
    }

    private UnpackedBoard? RestoreFromBackup(string path)
    {
        try
        {
            var rawData = File.ReadAllBytes(path);
            if (rawData.Length == 0)
            {
                return null;
            }

            var boardInfo = BoardPacker.UnpackBoard(rawData);
            Program.Server.GameData.Board = boardInfo.Board;
            Program.Server.GameData.Palette = boardInfo.Palette.Count == 0 ? null : boardInfo.Palette;
            Program.Server.GameData.BoardWidth = boardInfo.Width;
            Program.Server.GameData.BoardHeight = (uint) (boardInfo.Board.Length / boardInfo.Width);
            return boardInfo;
        }
        catch
        {
            return null;
        }
    }

    private async Task PruneBackupList()
    {
        var newListPath = Path.Join(Program.Config.CanvasFolder, "backuplist.txt." + DateTime.Now.ToFileTime());
        await using var newBackupList = new StreamWriter(newListPath);
        var listPath = Path.Join(Program.Config.CanvasFolder, "backuplist.txt");
        using (var reader = new StreamReader(listPath))
        {
            var line = await reader.ReadLineAsync();
            while (line is not null)
            {
                if (File.Exists(Path.Join(Program.Config.CanvasFolder, line)))
                {
                    await newBackupList.WriteLineAsync(line);
                }
                
                line = await reader.ReadLineAsync();
            }
        }
        
        await newBackupList.FlushAsync();
        await newBackupList.DisposeAsync();
        
        File.Move(newListPath, listPath, true);
    }
    
    private static string RgbFormatColour(uint colourValue)
    {
        var red = (byte) ((colourValue >> 16) & 0xFF);
        var green = (byte) ((colourValue >> 8) & 0xFF);
        var blue = (byte) (colourValue & 0xFF);

        return $"rgb({red}, {green}, {blue})";
    }
    
    private static string GetOrdinalSuffix(int number)
    {
        return (number % 100 is 11 or 12 or 13 ? 9 : number % 10) switch
        {
            1 => "st",
            2 => "nd",
            3 => "rd",
            _ => "th"
        };
    }
}
