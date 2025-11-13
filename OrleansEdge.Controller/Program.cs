using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Configuration;
using Terminal.Gui;

namespace OrleansEdge.Controller;

internal class Program
{
    private static IClusterClient? _client;
    private static ILedControllerGrain? _led;
    private static TextView? _logView;

    private static async Task Main(string[] args)
    {
        // Initialize Terminal.Gui FIRST so UI shows even if Orleans fails
        Application.Init();

        // Build Orleans client host (don't start yet)
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                // Clear default console logging to prevent output from interfering with Terminal.Gui
                logging.ClearProviders();
                // Optionally add custom logger that writes to _logView instead
            })
            .UseOrleansClient((ctx, clientBuilder) =>
            {
                // Read gateway endpoints from configuration
                var gatewayList = ctx.Configuration.GetSection("Orleans:Gateways").Get<string[]>()
                    ?? new[] { "127.0.0.1:30000" };

                var gateways = gatewayList.Select(g =>
                {
                    var parts = g.Split(':');
                    return new IPEndPoint(
                        IPAddress.Parse(parts[0]),
                        int.Parse(parts[1]));
                }).ToArray();

                clientBuilder.UseStaticClustering(gateways);
                clientBuilder.Configure<ClusterOptions>(opts =>
                {
                    opts.ClusterId = "edge-cluster";
                    opts.ServiceId = "led-service";
                });
            })
            .Build();

        var top = Application.Top;

        // Title
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

        // Status label (initially shows "Connecting...")
        var statusLabel = new Label("Current LED Color: Connecting...")
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(),
            Height = 1
        };
        win.Add(statusLabel);

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
                    // Queue the async work to avoid multiple executions
                    Task.Run(async () => await ProcessCommand(command, statusLabel));
                    commandInput.Text = "";
                }
                e.Handled = true;
            }
        };

        // Connect to Orleans asynchronously in background AFTER UI is shown
        Task.Run(async () =>
        {
            try
            {
                Log("Connecting to Orleans cluster...");
                await host.StartAsync();

                _client = host.Services.GetRequiredService<IClusterClient>();
                _led = _client.GetGrain<ILedControllerGrain>("led");

                // Check current state
                var currentColor = await _led.GetCurrentColor();

                Application.MainLoop.Invoke(() =>
                {
                    statusLabel.Text = $"Current LED Color: {currentColor}";
                    Log($"Connected to cluster. Current color: {currentColor}");
                });
            }
            catch (Exception ex)
            {
                Application.MainLoop.Invoke(() =>
                {
                    statusLabel.Text = "Current LED Color: Connection Failed";
                    Log($"Failed to connect to Orleans: {ex.Message}");
                    Log("Retrying in 5 seconds...");
                });

                // Retry after 5 seconds
                await Task.Delay(5000);
                // TODO: Add retry loop if needed
            }
        });

        Application.Run();
        Application.Shutdown();

        if (_client != null)
        {
            await host.StopAsync();
            host.Dispose();
        }
    }

    private static async Task ProcessCommand(string command, Label statusLabel)
    {
        if (_led == null)
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

        var colorName = parts[1].ToLower();

        // Map color names to LedColor enum
        LedColor color = colorName switch
        {
            "off" or "black" => LedColor.Off,
            "red" => LedColor.Red,
            "green" => LedColor.Green,
            "blue" => LedColor.Blue,
            "yellow" => LedColor.Yellow,
            "cyan" => LedColor.Cyan,
            "magenta" => LedColor.Magenta,
            "white" => LedColor.White,
            _ => (LedColor)(-1) // Invalid
        };

        if ((int)color == -1)
        {
            Log($"Unknown color: {colorName}");
            return;
        }

        try
        {
            await _led.SetLedColor(color);
            Log($"LED color set to: {color}");
            statusLabel.Text = $"Current LED Color: {color}";
        }
        catch (Exception ex)
        {
            Log($"Error setting color: {ex.Message}");
        }
    }

    private static void Log(string message)
    {
        if (_logView != null)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            _logView.Text += $"[{timestamp}] {message}\n";
            // Scroll to bottom
            _logView.MoveEnd();
        }
    }
}