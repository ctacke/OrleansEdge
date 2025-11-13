using Meadow;
using Meadow.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans.Configuration;
using System.Net;

namespace OrleansEdge.Node;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .UseMeadow<RaspberryPi>((device) =>
            {
                Resolver.Log.Info("Initializing MeadowApplication");

                var onboardLed = device.Pins.ACT_LED.CreateDigitalOutputPort();
                var outputService = new OutputService(
                    device.Pins.Pin22,   // R
                    device.Pins.Pin24,  // G
                    device.Pins.Pin26,  // B
                    device.Pins.Pin18); // Active LED

                Resolver.Services.Add(outputService);
            })
            .UseOrleans((ctx, siloBuilder) =>
            {
                // Get configuration values
                var siloPort = int.Parse(ctx.Configuration["Orleans:SiloPort"] ?? "11111");
                var gatewayPort = int.Parse(ctx.Configuration["Orleans:GatewayPort"] ?? "30000");
                var advertisedIp = ctx.Configuration["Orleans:AdvertisedIPAddress"];

                // Configure cluster identity
                siloBuilder.Configure<ClusterOptions>(opts =>
                {
                    opts.ClusterId = "edge-cluster";
                    opts.ServiceId = "led-service";
                });

                // Configure endpoints for this silo
                if (!string.IsNullOrEmpty(advertisedIp))
                {
                    // Use specific advertised IP for multi-homed systems
                    siloBuilder.ConfigureEndpoints(
                        advertisedIP: IPAddress.Parse(advertisedIp),
                        siloPort: siloPort,
                        gatewayPort: gatewayPort,
                        listenOnAnyHostAddress: true);
                }
                else
                {
                    // Auto-detect IP (development mode)
                    siloBuilder.ConfigureEndpoints(
                        siloPort: siloPort,
                        gatewayPort: gatewayPort,
                        listenOnAnyHostAddress: true);
                }

                // Use ADO.NET clustering with PostgreSQL for production-ready cluster membership
                var postgresConnectionString = ctx.Configuration["Orleans:PostgresConnectionString"]
                    ?? "Host=localhost;Database=orleans;Username=orleans_user;Password=orleans_dev;";

                siloBuilder.UseAdoNetClustering(options =>
                {
                    options.Invariant = "Npgsql";
                    options.ConnectionString = postgresConnectionString;
                });

                // Use ADO.NET grain storage with PostgreSQL for persistent state
                siloBuilder.AddAdoNetGrainStorage("ledStorage", options =>
                {
                    options.Invariant = "Npgsql";
                    options.ConnectionString = postgresConnectionString;
                    //options.UseJsonFormat = true;  // PostgreSQL has excellent JSON support
                });
            })
            .ConfigureLogging((ctx, loggingBuilder) =>
            {
                loggingBuilder.AddConsole();
            })
            .Build();

        await host.RunAsync();
    }
}