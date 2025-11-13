using Microsoft.Extensions.Configuration;
using Terminal.Gui;

namespace OrleansEdge.Controller;

internal class Program
{
    private static NodeController? _controller;
    private static TextView? _logView;
    private static Label? _statusLabel;

    private static async Task Main(string[] args)
    {
        // Initialize Terminal.Gui FIRST so UI shows even if Orleans fails
        Application.Init();

        // Read configuration
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .Build();

        var gatewayList = config.GetSection("Orleans:Gateways").Get<string[]>()
            ?? new[] { "127.0.0.1:30000" };

        // Create controller
        _controller = new NodeController(gatewayList);

        // Wire up controller events
        _controller.OnLog += (message) => Log(message);
        _controller.OnColorChanged += (color) =>
        {
            Application.MainLoop.Invoke(() =>
            {
                if (_statusLabel != null)
                {
                    _statusLabel.Text = $"Current LED Color: {color}";
                }
            });
        };
        _controller.OnConnectionStatusChanged += (connected) =>
        {
            Application.MainLoop.Invoke(() =>
            {
                if (_statusLabel != null && !connected)
                {
                    _statusLabel.Text = "Current LED Color: Connection Failed";
                }
            });
        };

        // Build UI
        BuildUI();

        // Connect to Orleans in background
        _ = Task.Run(async () => await _controller.ConnectAsync());

        Application.Run();
        Application.Shutdown();

        // Cleanup
        _controller?.Dispose();
    }

    private static void BuildUI()
    {
        var top = Application.Top;

        // Title window
        var win = new Window("Orleans Edge LED Controller")
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        top.Add(win);

        // Menu bar
        var menu = new MenuBar(new MenuBarItem[]
        {
            new MenuBarItem("_File", new MenuItem[]
            {
                new MenuItem("_Quit", "", () => Application.RequestStop())
            })
        });
        top.Add(menu);

        // Status label
        _statusLabel = new Label("Current LED Color: Connecting...")
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(),
            Height = 1
        };
        win.Add(_statusLabel);

        // Command input
        var commandLabel = new Label("Command:")
        {
            X = 1,
            Y = 3,
            Width = 10
        };
        win.Add(commandLabel);

        var commandInput = new TextField("")
        {
            X = Pos.Right(commandLabel) + 1,
            Y = 3,
            Width = Dim.Fill(1)
        };
        win.Add(commandInput);

        // Help text
        var helpLabel = new Label("Type: color <red|green|blue|yellow|cyan|magenta|white|off|black>")
        {
            X = 1,
            Y = 5,
            Width = Dim.Fill()
        };
        win.Add(helpLabel);

        // Log view
        var logLabel = new Label("Log:")
        {
            X = 1,
            Y = 7,
            Width = Dim.Fill()
        };
        win.Add(logLabel);

        _logView = new TextView()
        {
            X = 1,
            Y = 8,
            Width = Dim.Fill(1),
            Height = Dim.Fill(1),
            ReadOnly = true
        };
        win.Add(_logView);

        // Handle command input
        commandInput.KeyPress += (e) =>
        {
            if (e.KeyEvent.Key == Key.Enter)
            {
                var command = commandInput.Text.ToString()?.Trim();
                if (!string.IsNullOrEmpty(command))
                {
                    Task.Run(async () => await ProcessCommand(command));
                    commandInput.Text = "";
                }
                e.Handled = true;
            }
        };
    }

    private static async Task ProcessCommand(string command)
    {
        if (_controller == null || !_controller.IsConnected)
        {
            Log("Error: Not connected to Orleans cluster. Please wait or check connection.");
            return;
        }

        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            Log("Invalid command. Use: color <colorname>");
            return;
        }

        if (parts[0].ToLower() != "color")
        {
            Log($"Unknown command: {parts[0]}");
            return;
        }

        var colorName = parts[1];
        var color = NodeController.ParseColor(colorName);

        if (color == null)
        {
            Log($"Unknown color: {colorName}");
            return;
        }

        await _controller.SetLedColorAsync(color.Value);
    }

    private static void Log(string message)
    {
        Application.MainLoop.Invoke(() =>
        {
            if (_logView != null)
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                _logView.Text += $"[{timestamp}] {message}\n";
                _logView.MoveEnd();
            }
        });
    }
}
