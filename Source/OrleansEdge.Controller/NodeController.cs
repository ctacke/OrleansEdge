using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans.Configuration;
using System.Net;

namespace OrleansEdge.Controller;

/// <summary>
/// Manages Orleans cluster connection and grain interactions
/// </summary>
public class NodeController : IDisposable
{
    private readonly IHost _host;
    private IClusterClient? _client;
    private ILedControllerGrain? _led;
    private CancellationTokenSource? _pollingCancellation;
    private bool _disposed;

    public event Action<string>? OnLog;
    public event Action<LedColor>? OnColorChanged;
    public event Action<bool>? OnConnectionStatusChanged;

    public bool IsConnected => _client != null && _led != null;
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    public NodeController(string[] gatewayEndpoints)
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                // Clear default console logging to prevent output from interfering with Terminal.Gui
                logging.ClearProviders();
            })
            .UseOrleansClient((ctx, clientBuilder) =>
            {
                var gateways = gatewayEndpoints.Select(g =>
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
    }

    /// <summary>
    /// Connect to Orleans cluster and start polling
    /// </summary>
    public async Task<bool> ConnectAsync()
    {
        try
        {
            Log("Connecting to Orleans cluster...");
            await _host.StartAsync();

            _client = _host.Services.GetRequiredService<IClusterClient>();
            _led = _client.GetGrain<ILedControllerGrain>("led");

            // Check current state
            var currentColor = await _led.GetCurrentColor();

            Log($"Connected to cluster. Current color: {currentColor}");
            OnConnectionStatusChanged?.Invoke(true);
            OnColorChanged?.Invoke(currentColor);

            // Start background polling
            StartPolling();

            Log($"Started polling grain state every {PollingInterval.TotalSeconds} seconds for automatic failover...");

            return true;
        }
        catch (Exception ex)
        {
            Log($"Failed to connect to Orleans: {ex.Message}");
            OnConnectionStatusChanged?.Invoke(false);
            return false;
        }
    }

    /// <summary>
    /// Set LED color on the remote grain
    /// </summary>
    public async Task<bool> SetLedColorAsync(LedColor color)
    {
        if (_led == null)
        {
            Log("Error: Not connected to Orleans cluster. Please wait or check connection.");
            return false;
        }

        try
        {
            await _led.SetLedColor(color);
            Log($"LED color set to: {color}");
            OnColorChanged?.Invoke(color);
            return true;
        }
        catch (Exception ex)
        {
            Log($"Error setting color: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get current LED color from the grain
    /// </summary>
    public async Task<LedColor?> GetCurrentColorAsync()
    {
        if (_led == null)
        {
            return null;
        }

        try
        {
            return await _led.GetCurrentColor();
        }
        catch (Exception ex)
        {
            Log($"Error getting color: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parse color name to LedColor enum
    /// </summary>
    public static LedColor? ParseColor(string colorName)
    {
        return colorName.ToLower() switch
        {
            "off" or "black" => LedColor.Off,
            "red" => LedColor.Red,
            "green" => LedColor.Green,
            "blue" => LedColor.Blue,
            "yellow" => LedColor.Yellow,
            "cyan" => LedColor.Cyan,
            "magenta" => LedColor.Magenta,
            "white" => LedColor.White,
            _ => null
        };
    }

    private void StartPolling()
    {
        _pollingCancellation = new CancellationTokenSource();
        _ = Task.Run(() => PollGrainStateAsync(_pollingCancellation.Token));
    }

    private async Task PollGrainStateAsync(CancellationToken cancellationToken)
    {
        var lastColor = LedColor.Off;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(PollingInterval, cancellationToken);

                if (_led == null)
                {
                    // Not connected yet, skip this poll
                    continue;
                }

                // Poll grain state - this keeps grain alive and triggers activation after failover
                var currentColor = await _led.GetCurrentColor();

                // Only notify if color changed (avoid event spam)
                if (currentColor != lastColor)
                {
                    Log($"LED state changed: {lastColor} -> {currentColor}");
                    OnColorChanged?.Invoke(currentColor);
                    lastColor = currentColor;
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                // Grain might be temporarily unavailable during failover
                Log($"Polling error: {ex.Message} (will retry in {PollingInterval.TotalSeconds}s)");
            }
        }
    }

    private void Log(string message)
    {
        OnLog?.Invoke(message);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _pollingCancellation?.Cancel();
        _pollingCancellation?.Dispose();

        _host?.StopAsync().GetAwaiter().GetResult();
        _host?.Dispose();

        _disposed = true;
    }
}
