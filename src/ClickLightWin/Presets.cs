namespace ClickLightWin;

/// <summary>A named numeric choice shown as a segment in the settings window.</summary>
public sealed record NumericPreset(string Title, double Value);

/// <summary>
/// The discrete size and duration choices offered in the settings window, in the
/// spirit of ClickSettingOptions.swift. Values are in this app's own units (DIPs
/// for size, milliseconds for duration) rather than the macOS values.
/// </summary>
public static class Presets
{
    public static readonly NumericPreset[] Sizes =
    [
        new("Small", 22),
        new("Medium", 32),
        new("Large", 44),
        new("Huge", 60)
    ];

    public static readonly NumericPreset[] Durations =
    [
        new("Snappy", 280),
        new("Normal", 480),
        new("Long", 720),
        new("Very Long", 1000)
    ];
}
