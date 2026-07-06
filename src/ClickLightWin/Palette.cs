using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace ClickLightWin;

/// <summary>One selectable color, carrying both its hex (persisted) and a frozen brush (for the swatch).</summary>
public sealed class ColorSwatch
{
    public string Hex { get; }
    public Brush Brush { get; }

    public ColorSwatch(string hex)
    {
        Hex = hex;
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        Brush = brush;
    }
}

/// <summary>
/// The fixed palette of pulse colors offered per button in the settings window.
/// The first three entries are the default left/right/middle colors, so a fresh
/// install shows a selected swatch in each row.
/// </summary>
public static class Palette
{
    public static readonly ColorSwatch[] Colors =
    [
        new("#3B82F6"), // blue   (default left)
        new("#F97316"), // orange (default right)
        new("#22C55E"), // green  (default middle)
        new("#EF4444"), // red
        new("#A855F7"), // purple
        new("#06B6D4")  // cyan
    ];
}
