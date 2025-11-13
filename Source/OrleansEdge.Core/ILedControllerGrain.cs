using Orleans;

namespace OrleansEdge;

public interface ILedControllerGrain : IGrainWithStringKey
{
    Task SetLedColor(LedColor color);
    Task<LedColor> GetCurrentColor();
}