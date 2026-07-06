using System.Windows;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;

namespace ClickLightWin;

/// <summary>
/// Visual and behavioral settings for click pulses. Minimal for Milestone 1;
/// expand later into the full model from ClickSettingOptions.swift / SettingsStore.swift.
/// </summary>
public sealed class Settings
{
    public double BaseDiameterDips { get; init; } = 28;
    public double MaxScale { get; init; } = 2.2;
    public double StrokeThickness { get; init; } = 3;
    public Duration PulseDuration { get; init; } = new(TimeSpan.FromMilliseconds(450));

    public Color ColorFor(ClickButton button) => button switch
    {
        ClickButton.Left => Color.FromRgb(0x3B, 0x82, 0xF6),   // blue
        ClickButton.Right => Color.FromRgb(0xF9, 0x73, 0x16),  // orange
        ClickButton.Middle => Color.FromRgb(0x22, 0xC5, 0x5E), // green
        _ => Colors.White
    };

    public static Settings Default => new();
}
