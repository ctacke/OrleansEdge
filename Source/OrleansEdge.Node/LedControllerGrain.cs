using Meadow;

namespace OrleansEdge.Node;

// State that will be persisted
[Serializable]
public class LedState
{
    public LedColor CurrentColor { get; set; } = LedColor.Off;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public string? ControllingNodeId { get; set; }
}

// Grain implementation with persistent state and autonomous failover via reminders
public class LedControllerGrain : Grain, ILedControllerGrain//, IRemindable
{
    private readonly IPersistentState<LedState> _state;

    public LedControllerGrain(
        [PersistentState("ledState", "ledStorage")] IPersistentState<LedState> state)
    {
        _state = state;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        // Log when grain is activated and show current state
        Console.WriteLine($"LedControllerGrain activated. Current color: {_state.State.CurrentColor}");

        // Sync hardware LED with persisted state (critical for failover)
        var outputService = Resolver.Services.Get<OutputService>();
        if (outputService != null)
        {
            Console.WriteLine($"Applying loaded state to hardware: {_state.State.CurrentColor}");
            outputService.SetLedColor(_state.State.CurrentColor);
        }
        else
        {
            Console.WriteLine("WARNING: OutputService not available - cannot control hardware!");
        }

        // Register reminder for autonomous failover and health monitoring
        // Reminder survives grain deactivation and fires on whichever silo the grain activates on
        // NOTE: Reminders require additional Orleans.Reminders.AdoNet configuration
        // await this.RegisterOrUpdateReminder("HealthCheck", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        await SetLedColor(LedColor.Off);

        await base.OnDeactivateAsync(reason, cancellationToken);
    }

    public async Task SetLedColor(LedColor color)
    {
        Console.WriteLine($"Setting LED color to {color}");

        // Update state
        _state.State.CurrentColor = color;
        _state.State.LastUpdated = DateTime.UtcNow;
        _state.State.ControllingNodeId = this.GetPrimaryKeyString();

        // Persist to PostgreSQL
        await _state.WriteStateAsync();

        // Actually control the hardware LED via Meadow
        Resolver.Services.Get<OutputService>()?.SetLedColor(color);

        Console.WriteLine($"LED color set to {color} and persisted to database");
    }

    public Task<LedColor> GetCurrentColor()
    {
        return Task.FromResult(_state.State.CurrentColor);
    }

    // IRemindable implementation for autonomous failover
    // Commented out until reminder storage is configured
    /*
    public Task ReceiveReminder(string reminderName, TickStatus status)
    {
        Console.WriteLine($"Reminder '{reminderName}' triggered. Refreshing hardware state.");

        // Periodically refresh hardware state to ensure LED stays in sync
        // This ensures that if a silo fails and grain moves, hardware will auto-sync
        Resolver.Services.Get<OutputService>()?.SetLedColor(_state.State.CurrentColor);

        return Task.CompletedTask;
    }
    */
}
