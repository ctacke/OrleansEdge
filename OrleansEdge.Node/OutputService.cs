using Meadow;
using Meadow.Foundation.Leds;
using Meadow.Hardware;
using Meadow.Peripherals.Leds;

namespace OrleansEdge.Node;

internal class OutputService
{
    private readonly RgbLed _colorLed;
    private readonly Led _activeLed;

    public OutputService(
        IPin redPin,
        IPin greenPin,
        IPin bluePin,
        IPin activePin)
    {
        _colorLed = new RgbLed(
            redPin,
            greenPin,
            bluePin);

        _activeLed = new Led(activePin);
        _activeLed.IsOn = true; // Indicate service is active
    }

    public void SetLedColor(LedColor color)
    {
        Resolver.Log.Info($"OutputService: Setting LED color to {color}");

        if (color == LedColor.Off)
        {
            _colorLed.IsOn = false;
            return;
        }

        var c = color switch
        {
            LedColor.Red => RgbLedColors.Red,
            LedColor.Green => RgbLedColors.Green,
            LedColor.Blue => RgbLedColors.Blue,
            LedColor.Yellow => RgbLedColors.Yellow,
            LedColor.Cyan => RgbLedColors.Cyan,
            LedColor.Magenta => RgbLedColors.Magenta,
            LedColor.White => RgbLedColors.White,
            _ => RgbLedColors.Red
        };

        _colorLed.SetColor(c);
    }
}
