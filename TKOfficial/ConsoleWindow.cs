using RplaceServer;
using RplaceServer.Types;
using Terminal.Gui;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using WatsonWebsocket;

namespace TKOfficial;

public class ConsoleWindow : Window
{
    private readonly List<string> serverLogs = [];
    private Action<string>? logger;
    private FrameView? serverLogPanel;
    private FrameView? serverReplPanel;
    private FrameView? serverBottomPrimary;
    private Button? bottomActionButton;
    private ListView? clientsListView;
    private TextView? replTextField;
    private Label? serverUptimeLabel;

    // Used as a portal to allow the repl to access and interact with the server
    public ServerInstance Server => Program.Server;
    public Action<string> Logger => this.logger!;

    public ConsoleWindow()
    {
        InitialiseWindow();
        InitialiseServerActions();
        InitialiseClientsPanel();
        InitializeStatisticsPanel();
        InitialiseBottomPanels();
        InitialiseServerLogging();
        InitialiseTimers();

        logger?.Invoke("Server software started");
        _ = Task.Run(Server.StartAsync);
    }

    private void InitialiseWindow()
    {
        Title = "TKOfficial CLI Environment -> ©Zekiah-A (Ctrl+Q to quit)";
        BorderStyle = LineStyle.Rounded;
    }

    // Used as an alias to make invoking logger from the repl easier
    public void Print(object data)
    {
        var formatted = ObjectDumper.Dump(data);
        logger?.Invoke(formatted);
    }

    private void InitialiseServerActions()
    {
        var actionButtons = new[]
        {
            CreateActionButton("Expand canvas", ShowExpandCanvasDialog),
            CreateActionButton("Fill canvas area", ShowFillCanvasDialog),
            CreateActionButton("Edit chat cooldown", ShowChatCooldownDialog),
            CreateActionButton("Broadcast chat message", () => ShowBroadcastChatDialog()),
            CreateActionButton("Edit place cooldown", ShowPlaceCooldownDialog),
            CreateActionButton("Edit colour palette", ShowPaletteDialog),
            CreateActionButton("Restore canvas from backup", ShowRestoreBackupDialog),
            CreateActionButton("Save canvas to disk", async () => {
                await Server.WebServer.SaveCanvasBackupAsync();
                logger?.Invoke("Canvas saved to disk");
            }),
            CreateActionButton("Prune backup list", () => Task.Run(PruneBackupList)),
            CreateActionButton("Stop server", async () => {
                logger?.Invoke("Server shutdown request received");
                await Server.WebServer.SaveCanvasBackupAsync();
                Application.Shutdown();
                await Server.StopAsync();
                Console.Clear();
            })
        };

        var panel = new FrameView
        {
            Title = "Server actions",
            Width = 32,
            Height = 16,
            BorderStyle = LineStyle.Rounded
        };
        for (var i = 0; i < actionButtons.Length; i++)
        {
            actionButtons[i].Y = i;
            panel.Add(actionButtons[i]);
        }
        Add(panel);
    }

    private static Button CreateActionButton(string text, Action clickHandler)
    {
        var button = new Button(text);
        button.Clicked += (sender, args) => clickHandler();
        return button;
    }

    private void InitialiseClientsPanel()
    {
        clientsListView = new ListView(new Rect(0, 0, 64, 16),
            new List<string> { "No clients are connected yet... Invite some friends!" })
        {
            Width = Dim.Fill(),
        };
        clientsListView.OpenSelectedItem += ShowClientInfoDialog;

        var clientsPanel = new FrameView
        {
            X = Pos.X(this) + 33,
            Height = 16,
            Width = Dim.Fill(),
            Title = "Connected clients",
            BorderStyle = LineStyle.Rounded
        };
        clientsPanel.Add(clientsListView);
        Add(clientsPanel);

        // Wire up client connection events
        Server.SocketServer.PlayerConnected += UpdateClientsList;
        Server.SocketServer.PlayerDisconnected += UpdateClientsList;
    }

    private void UpdateClientsList(object? sender, EventArgs e)
    {
        clientsListView?.SetSource(Server.Clients
            .Select(pair => pair.Value.IdIpPort)
            .ToList());
    }

    private void InitializeStatisticsPanel()
    {
        Add(new Label
            {
                Text = "Server Statistics:",
                X = Pos.Center(),
                Y = Pos.Center()
            },
            serverUptimeLabel = new Label
            {
                Text = "Server uptime: 00:00:00",
                X = Pos.Center(),
                Y = Pos.Center() + 1
            },
            new Label
            {
                Text = $"Server Address: {(Program.Config.Ssl ? "wss://" : "ws://")}localhost:{Program.Config.SocketPort} " +
                       $"{(Program.Config.Ssl ? "https://" : "http://")}localhost:{Program.Config.HttpPort}/place" +
                       $"{(Program.Config.UseCloudflare ? " (cloudflare)" : "")}",
                X = Pos.Center(),
                Y = Pos.Center() + 2
            },
            new Label {
                Text = "Canvas backup path: " + new DirectoryInfo(Program.Config.CanvasFolder).FullName,
                X = Pos.Center(),
                Y = Pos.Center() + 3
            },
            new Label {
                Text = "Game chat webhook URL: " +
                       (string.IsNullOrEmpty(Program.Config.WebhookService?.Url) ?
                           "No webhook URL set" : Program.Config.WebhookService.Url),
                X = Pos.Center(),
                Y = Pos.Center() + 4
            }
        );
    }

    private void InitialiseBottomPanels()
    {
        InitializeLogPanel();
        InitializeReplPanel();

        var swapButton = new Button("Swap primary")
        {
            Y = Pos.Bottom(this) - 14,
            X = Pos.Right(this) - 28
        };
        swapButton.Clicked += (sender, args) => SwapPrimaryPanel();

        bottomActionButton = new Button("Clear")
        {
            Y = Pos.Bottom(this) - 14,
            X = Pos.Right(this) - 12
        };
        bottomActionButton.Clicked += (sender, args) =>
        {
            if (serverBottomPrimary == serverLogPanel)
            {
                serverLogs.Clear();
            }
            else
            {
                ExecuteServerRepl();
            }
        };

        Add(swapButton, bottomActionButton);
    }

    private void InitializeLogPanel()
    {
        serverLogPanel = new FrameView
        {
            Y = Pos.Bottom(this) - 14,
            Height = 12,
            Width = Dim.Fill(),
            Title = "Server logs",
            ColorScheme = Colors.Base,
            BorderStyle = LineStyle.Rounded
        };

        serverLogPanel.Add(new ListView(new Rect(0, 0, 128, 8), serverLogs)
        {
            Width = Dim.Fill()
        });

        Add(serverLogPanel);
        serverBottomPrimary = serverLogPanel;
    }

    private void InitializeReplPanel()
    {
        serverReplPanel = new FrameView
        {
            Y = Pos.Bottom(this) - 5,
            Height = 3,
            Width = Dim.Fill(),
            Title = "Command line repl",
            ColorScheme = Colors.Base,
            BorderStyle = LineStyle.Rounded
        };

        replTextField = new TextView
        {
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        replTextField.KeyDown += HandleReplKeyDown;

        serverReplPanel.Add(replTextField);
    }

    private void HandleReplKeyDown(object? sender, KeyEventEventArgs? args)
    {
        if (serverBottomPrimary == serverLogPanel && args?.KeyEvent.Key == Key.Enter)
        {
            args.Handled = true;
            ExecuteServerRepl();
        }
    }

    private void InitialiseServerLogging()
    {
        if (!Program.Config.LogToConsole)
        {
            serverLogs.Add($"[TKOfficial {DateTime.Now:hh:mm:ss}]: Server logging to console has been disabled. Logs will not appear here.");
            return;
        }

        logger = message => serverLogs.Add($"[TKOfficial {DateTime.Now:hh:mm:ss}]: {message}");
        Server.Logger = message => serverLogs.Add($"[ServerInstance {DateTime.Now:hh:mm:ss}]: {message}");
        Server.SocketServer.Logger = message => serverLogs.Add($"[SocketServer {DateTime.Now:hh:mm:ss}]: {message}");
        Server.WebServer.Logger = message => serverLogs.Add($"[WebServer {DateTime.Now:hh:mm:ss}]: {message}");
    }

    private void InitialiseTimers()
    {
        var elapsedSeconds = 0;
        var uptimeTimer = new System.Timers.Timer
        {
            AutoReset = true,
            Interval = TimeSpan.FromSeconds(1).TotalMilliseconds
        };

        uptimeTimer.Elapsed += (_, _) =>
        {
            if (serverUptimeLabel == null)
            {
                return;
            }

            serverUptimeLabel.Text = $"Server uptime: {TimeSpan.FromSeconds(elapsedSeconds)}";
            serverUptimeLabel.SetNeedsDisplay(serverUptimeLabel.Bounds);
            elapsedSeconds++;
        };

        uptimeTimer.Start();
    }

    private async void ExecuteServerRepl()
    {
        if (replTextField == null)
        {
            return;
        }
        
        var codeText = replTextField.Text;
        replTextField.Text = "";

        try
        {
            var state = await CSharpScript.RunAsync(codeText, ScriptOptions.Default, this, typeof(ConsoleWindow));
            if (state.ReturnValue != null)
            {
                logger?.Invoke(state.ReturnValue.ToString()!);
            }
        }
        catch (Exception e)
        {
            logger?.Invoke(e.ToString());
        }
    }

    private void SwapPrimaryPanel()
    {
        if (bottomActionButton == null)
        {
            return;
        }
        
        Remove(serverBottomPrimary);
        if (serverBottomPrimary == serverLogPanel)
        {
            bottomActionButton.Text = "Exec-";
            serverBottomPrimary = serverReplPanel;
        }
        else
        {
            bottomActionButton.Text = "Clear";
            serverBottomPrimary = serverLogPanel;
        }
        Add(serverBottomPrimary);
    }

    private void ShowExpandCanvasDialog()
    {
        var wizard = new Wizard
        {
            Title = "Expand canvas",
            Modal = true,
            Width = 32,
            Height = 12,
            BorderStyle = LineStyle.Rounded
        };

        var expandXField = new TextField("0") { Width = Dim.Fill(), Y = 1 };
        var expandYField = new TextField("0") { Width = Dim.Fill(), Y = 3 };
        var colourIndexField = new TextField("0") { Width = Dim.Fill(), Y = 5 };
        wizard.Add(
            new Label { Text = "Expand X (Width)" },
            expandXField,
            new Label { Y = 2, Text = "Expand Y (Height)" },
            expandYField,
            new Label { Y = 4, Text = "Expand colour index" },
            colourIndexField
        );
        wizard.BackButton.Clicked += (sender, args) =>
        {
            CloseWizard(wizard);
        };
        wizard.Finished += (sender, args) =>
        {
            if (!ValidateExpandCanvas(expandXField.Text, expandYField.Text, colourIndexField.Text,
                    out var expandWidth, out var expandHeight, out var colourIndex))
            {
                return;
            }

            var formattedColour = RgbFormatColour((Server.GameData.Palette ?? GameData.DefaultPalette)
                .ElementAtOrDefault(colourIndex));
            var originalDimensions = (Server.GameData.BoardWidth, Server.GameData.BoardHeight);
            var newDimensions = Server.SocketServer.ExpandCanvas((uint)expandWidth, (uint)expandHeight, colourIndex);
            var difference = newDimensions.NewWidth * newDimensions.NewHeight -
                           originalDimensions.BoardWidth * originalDimensions.BoardHeight;

            logger?.Invoke($"Expanded canvas size to ({newDimensions.NewWidth}, {newDimensions.NewHeight}), " +
                          $"with colour {formattedColour}, ({difference} pixels)");
            CloseWizard(wizard);
        };

        OpenWizard(wizard);
    }

    private bool ValidateExpandCanvas(string xText, string yText, string colorText,
        out int expandWidth, out int expandHeight, out int colourIndex)
    {
        expandWidth = 0;
        expandHeight = 0;
        colourIndex = 0;

        if (!int.TryParse(xText, out expandWidth))
        {
            logger?.Invoke("Failed to expand, invalid Expand X Parameter");
            return false;
        }
        if (!int.TryParse(yText, out expandHeight))
        {
            logger?.Invoke("Failed to expand, invalid Expand Y Parameter");
            return false;
        }
        if (!int.TryParse(colorText, out colourIndex) ||
            colourIndex > (Server.GameData.Palette?.Count ?? 31) || colourIndex < 0)
        {
            logger?.Invoke("Failed to expand, invalid Colour Index Parameter");
            return false;
        }

        return true;
    }

    private void ShowFillCanvasDialog()
    {
        var wizard = new Wizard()
        {
            Title = "Fill canvas area",
            Modal = true,
            Width = 32,
            Height = 13,
            BorderStyle = LineStyle.Rounded
        };

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
        wizard.Add(xStartField, new Label { Text = "Pixel X start" }, yStartField,
            new Label { Y = 2, Text = "Pixel Y start" }, xEndField, new Label { Y = 4, Text = "Pixel X end" },
            yEndField, new Label { Y = 6, Text = "Pixel Y end" },
            new Label { Y = 8, Text = $"Palette colour index (0 - {Server.GameData.Palette?.Count ?? 31})" },
            colourIndexField);
        wizard.BackButton.Clicked += (sender, args) =>
        {
            CloseWizard(wizard);
        };
        wizard.Finished += (_, _) =>
        {
            if (!ValidateFillCanvas(xStartField.Text, yStartField.Text, xEndField.Text, yEndField.Text, colourIndexField.Text,
                out var startX, out var startY, out var endX, out var endY, out var colourIndex))
            {
                return;
            }

            var formattedColour = colourIndex.ToString();
            if (Server.GameData.Palette?.ElementAtOrDefault(colourIndex) is not null)
            {
                formattedColour = RgbFormatColour(Server.GameData.Palette[colourIndex]);
            }
            var areaFilled = Server.SocketServer.Fill(startX, startY, endX, endY, (byte) colourIndex);

            logger?.Invoke($"Filled canvas area ({startX}, {startY}) to ({endX}, {endY}) with colour {formattedColour} ({areaFilled} pixels filled)");
            CloseWizard(wizard);
        };

        OpenWizard(wizard);
    }

    private bool ValidateFillCanvas(string xStartText, string yStartText, string xEndText, string yEndText,
        string colourText, out int startX, out int startY, out int endX, out int endY, out int colourIndex)
    {
        startX = 0;
        startY = 0;
        endX = 0;
        endY = 0;
        colourIndex = 0;

        if (!int.TryParse(xStartText, out startX) || startX < 0)
        {
            logger?.Invoke("Failed to fill, invalid Start X Parameter");
            return false;
        }
        if (!int.TryParse(yStartText, out startY) || startY < 0)
        {
            logger?.Invoke("Failed to fill, invalid Start Y Parameter");
            return false;
        }
        if (!int.TryParse(xEndText, out endX) || endX > Server.GameData.BoardWidth)
        {
            logger?.Invoke("Failed to fill, invalid End X Parameter");
            return false;
        }
        if (!int.TryParse(yEndText, out endY) || endX > Server.GameData.BoardHeight)
        {
            logger?.Invoke("Failed to fill, invalid End Y Parameter");
            return false;
        }
        if (!int.TryParse(colourText, out colourIndex) ||
            colourIndex > (Server.GameData.Palette?.Count ?? 31) || colourIndex < 0)
        {
            logger?.Invoke("Failed to fill, invalid Colour Index Parameter");
            return false;
        }

        return true;
    }

    private void ShowChatCooldownDialog()
    {
        var wizard = new Wizard
        {
            Title = "Edit chat message cooldown",
            Modal = true,
            Width = 32,
            Height = 4,
            BorderStyle = LineStyle.Rounded
        };

        var cooldownField = new TextField(Server.GameData.ChatCooldownMs.ToString())
        {
            Width = Dim.Fill()
        };
        wizard.Add(cooldownField);
        wizard.BackButton.Clicked += (sender, args) =>
        {
            CloseWizard(wizard);
        };
        wizard.Finished += (_, _) =>
        {
            if (int.TryParse(cooldownField.Text, out var cooldown))
            {
                Server.GameData.ChatCooldownMs = cooldown;
                logger?.Invoke($"Updated game chat message cooldown to {Server.GameData.ChatCooldownMs}ms");
                CloseWizard(wizard);
            }
            else
            {
                logger?.Invoke("Failed to update chat cooldown, invalid Cooldown Parameter");
            }
        };

        OpenWizard(wizard);
    }

    private void ShowBroadcastChatDialog(ClientMetadata? targetClient = null)
    {
        var title = "Broadcast chat message";
        if (targetClient is not null && Server.Clients.TryGetValue(targetClient, out var clientData))
        {
            title = "Message player " + clientData.IdIpPort;
        }
        var wizard = new Wizard
        {
            Title = title,
            Modal = true,
            Width = 32,
            Height = 7,
            BorderStyle = LineStyle.Rounded
        };

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
        wizard.Add(new Label { Text = "Message:" }, textInput, new Label { Text = "Channel:", Y = 2 }, channelInput);
        wizard.BackButton.Clicked += (sender, args) =>
        {
            CloseWizard(wizard);
        };
        wizard.Finished += (_, _) =>
        {
            Server.SocketServer.BroadcastChatMessage(textInput.Text, channelInput.Text);
            logger?.Invoke($"Sent chat message '{textInput.Text}' in channel '{channelInput.Text}'");
            CloseWizard(wizard);
        };

        OpenWizard(wizard);
    }

    private void ShowPlaceCooldownDialog()
    {
        var wizard = new Wizard
        {
            Title = "Edit place cooldown",
            Modal = true,
            Width = 32,
            Height = 4,
            BorderStyle = LineStyle.Rounded
        };

        var cooldownField = new TextField(Server.GameData.CooldownMs.ToString())
        {
            Width = Dim.Fill()
        };
        wizard.Add(cooldownField);
        wizard.BackButton.Clicked += (sender, args) =>
        {
            CloseWizard(wizard);
        };
        wizard.Finished += (_, _) =>
        {
            if (uint.TryParse(cooldownField.Text, out var cooldown))
            {
                Server.GameData.CooldownMs = cooldown;
                logger?.Invoke($"Updated place cooldown to {Server.GameData.CooldownMs}ms");
                CloseWizard(wizard);
            }
            else
            {
                logger?.Invoke("Failed to update chat cooldown, invalid CooldownMs Parameter");
            }
        };

        OpenWizard(wizard);
    }

    private void ShowPaletteDialog()
    {
        var wizard = new Wizard()
        {
            Title = "Edit colour palette",
            Modal = true,
            Width = 64,
            Height = 5,
            BorderStyle = LineStyle.Rounded
        };

        var paletteField = new TextField(string.Join(", ", Server.GameData.Palette ?? GameData.DefaultPalette))
        {
            Width = Dim.Fill(),
            Y = 1
        };
        wizard.Add(new Label { Text = "Comma separated integer palette colours" }, paletteField);
        wizard.BackButton.Clicked += (sender, args) =>
        {
            CloseWizard(wizard);
        };
        wizard.Finished += (_, _) =>
        {
            var newPalette = new List<uint>();
            var split = paletteField.Text.Split(',');

            if (split.Length == 0)
            {
                Server.GameData.Palette = null;
                logger?.Invoke("Cleared colour palette, server will use default game palette");
                CloseWizard(wizard);
            }

            for (var index = 0; index < split.Length; index++)
            {
                if (uint.TryParse(split[index].Trim(), out var uintValue))
                {
                    newPalette.Add(uintValue);
                    continue;
                }

                logger?.Invoke($"Could not add beyond the {GetOrdinalSuffix(index + 1)} element due to it not being a correctly formatted number.");
                break;
            }

            Program.Server.GameData.Palette = newPalette;
            logger?.Invoke("Updated colour palette to: " +
                string.Join(", ", Program.Server.GameData.Palette ?? new List<uint>()) +
                " with a length of " + (Program.Server.GameData.Palette?.Count ?? 0));
            CloseWizard(wizard);
        };

        OpenWizard(wizard);
    }

    private void ShowRestoreBackupDialog()
    {
        var wizard = new Wizard()
        {
            Title = "Restore canvas from backup",
            Modal = true,
            Width = 64,
            Height = 5,
            BorderStyle = LineStyle.Rounded
        };

        var restoreField = new TextField(Program.Server.GameData.CanvasFolder)
        {
            Width = Dim.Fill(),
            Y = 1
        };
        wizard.Add(new Label { Text = "Enter the path to the canvas backup to restore from" }, restoreField);
        wizard.BackButton.Clicked += (sender, args) =>
        {
            CloseWizard(wizard);
        };
        wizard.Finished += (_, _) =>
        {
            if (RestoreFromBackup(restoreField.Text) is { } unpackedInfo)
            {
                logger?.Invoke($"Successfully restored canvas from provided backup. Restored canvas length: : " +
                    $"{unpackedInfo.Board.Length}, restored palette length: {string.Join(", ", unpackedInfo.Palette)}, " +
                    $"restored canvas dimensions {unpackedInfo.Width}x{unpackedInfo.Board.Length/unpackedInfo.Width}");
                CloseWizard(wizard);
            }
            else
            {
                logger?.Invoke("Could not restore canvas from provided backup. Make sure the path is pointing to a valid canvas backup.");
            }
        };

        OpenWizard(wizard);
    }

    private void ShowClientInfoDialog(object? sender, ListViewItemEventArgs args)
    {
        // TODO: This is scuffed - perhaps use args.value to get the selected item in a more solid way
        var selectedClient = Server.Clients.ElementAtOrDefault(args.Item);
        if (selectedClient.Key == null || selectedClient.Value == null)
        {
            return;
        }
        
        var wizard = new Wizard()
        {
            Modal = true,
            Width = 64,
            Height = 10,
            Title = "Client info",
            BorderStyle = LineStyle.Rounded
        };
        
        var ipLabel = new Label
        {
            Text = "Player IP/Port: " + selectedClient.Value.IdIpPort,
            Y = 0
        };
        
        var vipLabel = new Label
        {
            Text = "Player permissions: " + selectedClient.Value.Permissions,
            Y = 1
        };
        var lastChatLabel = new Label
        {
            Text = "Player last chat: " + (selectedClient.Value.LastChat == DateTimeOffset.MinValue
                ? "Never"
                : selectedClient.Value.LastChat),
            Y = 2
        };
        var kickButton = new Button
        {
            Text = "Kick player",
            Y = 3
        };
        kickButton.Clicked += async (_, _) =>
        {
            logger?.Invoke($"Disconnected player {selectedClient.Value.IdIpPort}");
            await Program.Server.SocketServer.KickPlayer(selectedClient.Key);
        };
        var banButton = new Button
        {
            Text = "Ban player",
            Y = 4
        };
        banButton.Clicked += (_, _) =>
        {
            logger?.Invoke($"Banned player {selectedClient.Value.IdIpPort}");
            Program.Server.SocketServer.BanPlayer(selectedClient.Key, 1000); //TODO: Add ban duration
        };
        var messageButton = new Button()
        {
            Text = "Message player",
            Y = 5
        };
        messageButton.Clicked += (_, _) =>
        {
            CloseWizard(wizard);
            ShowBroadcastChatDialog(selectedClient.Key);
        };
        wizard.Add(ipLabel, vipLabel, lastChatLabel, kickButton, banButton, messageButton);
        wizard.BackButton.Clicked += (_, _) =>
        {
            CloseWizard(wizard);
        };
        wizard.Finished += (_, _) =>
        {
            CloseWizard(wizard);
        };
        
        OpenWizard(wizard);
    }

    private void OpenWizard(Wizard wizard)
    {
        Application.Top.Add(wizard);
    }

    private void CloseWizard(Wizard wizard)
    {
        Application.Top.Remove(wizard);
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
            Server.Board = boardInfo.Board;
            Server.GameData.Palette = boardInfo.Palette.Count == 0 ? null : boardInfo.Palette;
            Server.GameData.BoardWidth = boardInfo.Width;
            Server.GameData.BoardHeight = (uint) (boardInfo.Board.Length / boardInfo.Width);
            return boardInfo;
        }
        catch
        {
            return null;
        }
    }

    private async Task PruneBackupList()
    {
        logger.Invoke("Started pruning backup list task...");
        var beforeLines = 0;
        var afterLines = 0;
        var newListPath = Path.Combine(Program.Config.CanvasFolder, "backuplist.txt." + DateTime.Now.ToFileTime());
        await using var newBackupList = new StreamWriter(newListPath);
        var listPath = Path.Combine(Program.Config.CanvasFolder, "backuplist.txt");
        using (var reader = new StreamReader(listPath))
        {
            var line = await reader.ReadLineAsync();
            while (line is not null)
            {
                beforeLines++;
                if (File.Exists(Path.Combine(Program.Config.CanvasFolder, line)))
                {
                    afterLines++;
                    await newBackupList.WriteLineAsync(line);
                }

                line = await reader.ReadLineAsync();
            }
        }

        await newBackupList.FlushAsync();

        File.Move(newListPath, listPath, true);
        logger.Invoke($"Backup list pruned successfully! Line count changed from {beforeLines} to {afterLines}.");
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
