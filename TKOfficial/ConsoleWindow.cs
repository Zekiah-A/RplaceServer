using System.Collections;
using System.Net.Mime;
using Terminal.Gui;

namespace TKOfficial;

public class ConsoleWindow : Window
{
    public ConsoleWindow()
    {
        Initialise();
    }

    public void Initialise()
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
        
        var fillCanvasButton = new Button
        {
            Text = "Fill canvas area",
            Y = Pos.Top(serverActionsContainer) + 2
        };
        
        var changeChatCooldownButton = new Button
        {
            Text = "Edit chat cooldown",
            Y = Pos.Top(serverActionsContainer) + 3
        };
        changeChatCooldownButton.Clicked += () =>
        {
            var cooldownWizard = new Wizard("")
            {
                Modal = false,
                Width = 32,
                Height = 4,
            };
            
            var firstStep = new Wizard.WizardStep("Edit chat message cooldown");
            var cooldownField = new TextField(Program.Config.ChatCooldown.ToString())
            {
                Width = Dim.Fill(),
            };
            firstStep.Add(cooldownField);

            cooldownWizard.AddStep(firstStep);
            cooldownWizard.Finished += _ =>
            {
                Application.Top.Remove(cooldownWizard);
                Application.RequestStop();
            };
            
            Application.Top.Add(cooldownWizard);
            Application.Run(Application.Top);
        };

        var broadcastChatButton = new Button
        {
            Text = "Send chat message",
            Y = Pos.Top(serverActionsContainer) + 4
        };
        broadcastChatButton.Clicked += () =>
        {
            var chatWizard = new Wizard("")
            {
                Modal = false,
                Width = 32,
                Height = 6,
            };
            
            var firstStep = new Wizard.WizardStep("Broadcast chat message");
            var textInput = new TextField("Message from the server")
            {
                Width = Dim.Fill(),
            };
            var channelInput = new TextField("en")
            {
                Width = Dim.Fill(),
                Y = 1
            };
            firstStep.Add(textInput, channelInput);

            chatWizard.AddStep(firstStep);
            chatWizard.Finished += _ =>
            {
                Application.Top.Remove(chatWizard);
                Application.RequestStop();
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
            var cooldownField = new TextField(Program.Config.Cooldown.ToString())
            {
                Width = Dim.Fill(),
            };
            firstStep.Add(cooldownField);

            cooldownWizard.AddStep(firstStep);
            cooldownWizard.Finished += _ =>
            {
                Application.Top.Remove(cooldownWizard);
                Application.RequestStop();
            };
            
            Application.Top.Add(cooldownWizard);
            Application.Run(Application.Top);
        };

        var saveCanvasButton = new Button
        {
            Text = "Save canvas to disk",
            Y = Pos.Top(serverActionsContainer) + 6
        };
        saveCanvasButton.Clicked += async () =>
        {
            await Program.Server.WebServer.SaveCanvasBackup();
        };
        
        var stopServerButton = new Button
        {
            Text = "Gracefully stop server",
            Y = Pos.Top(serverActionsContainer) + 7
        };
        stopServerButton.Clicked += async () =>
        {
            await Program.Server.StopAsync();
            Application.Shutdown();
        };

        serverActionsContainer.Add(expandCanvasButton, fillCanvasButton, changeChatCooldownButton,
            broadcastChatButton, changeGameCooldownButton, saveCanvasButton, stopServerButton);
        // End server actions stack panel container
        
        // Server actions panel, provides nice border around container
        var serverActionsPanel = new PanelView
        {
            Border = new Border
            {
                BorderBrush = Color.White,
                BorderStyle = BorderStyle.Rounded,
                Title = "Server actions"
            },
            ColorScheme = Colors.Base,
            Child = serverActionsContainer
        };
        
        // Clients panel
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
        };
        clientsPanel.Child = new ListView(new Rect(0, 0, 64, 16), // TODO: Fix this
            new List<string>{ "No clients are connected yet... Invite some friends!" })
        {
            Width = Dim.Fill(),
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
            Text = "Game chat webhook URL: " + (Program.Config.WebhookUrl ?? "No webhook URL set"),
            Y = Pos.Bottom(serverBackupPathLabel),
            X = Pos.Center()
        };

        // Statistics log panel
        var serverLogs = new List<string>();
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
            serverBackupPathLabel, serverWebhookUrlLabel, serverLogPanel);
        
        // Server uptime timer
        var elapsedSeconds = 0;
        var uptimeTimer = new System.Timers.Timer
        {
            AutoReset = true,
            Interval = TimeSpan.FromSeconds(1).TotalMilliseconds
        };
        uptimeTimer.Elapsed += (_, _) =>
        {
            // TODO: Timer refuses for elapse for some unknown reason when I use the datetime. Use timespan seconds for now.
            serverUptimeLabel.Text = "Server uptime: " + TimeSpan.FromSeconds(elapsedSeconds);
            serverUptimeLabel.SetNeedsDisplay(serverUptimeLabel.Bounds);
            elapsedSeconds++;
        };
        uptimeTimer.Start();
        
        // Server logs panel
        if (Program.Config.LogToConsole)
        {
            Program.Server.SocketServer.Logger = message =>
            {
                serverLogs.Add("[SocketServer " + DateTime.Now.ToString("hh:mm:ss") + "]: " + message);
            };

            Program.Server.WebServer.Logger = message =>
            {
                serverLogs.Add("[WebServer " + DateTime.Now.ToString("hh:mm:ss") + "]: " + message);
            };
            
            serverLogs.Add("[TKOfficial " + DateTime.Now.ToString("hh:mm:ss") + "]: " + "Server software started");
        }
        else
        {
            serverLogs.Add("[TKOfficial " + DateTime.Now.ToString("hh:mm:ss") + "]:" +
                           "Server logging to console has been disabled. Logs will not appear here.");
        }

        // Update clients panel with a list of all currently connected clients
        Program.Server.SocketServer.PlayerConnected += (_, _) =>
        {
            ((ListView) clientsPanel.Child).SetSource(Program.Server.GameData.Clients
                .Select(pair => pair.Key.IpPort)
                .ToList());
        };
        Program.Server.SocketServer.PlayerDisconnected += (_, _) =>
        {
            ((ListView) clientsPanel.Child).SetSource(Program.Server.GameData.Clients
                .Select(pair => pair.Key.IpPort)
                .ToList());
        };
    }
}