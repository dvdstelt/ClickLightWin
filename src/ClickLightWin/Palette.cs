using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace ClickLightWin;

/// <summary>One selectable color, carrying its hex (persisted), a display name, and a frozen brush.</summary>
public sealed class ColorSwatch
{
    public string Hex { get; }
    public string Name { get; }
    public Brush Brush { get; }

    public ColorSwatch(string hex, string name)
    {
        Hex = hex;
        Name = name;
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        Brush = brush;
    }
}

/// <summary>
/// The fixed palette of pulse colors offered per button in the settings window.
/// The first three entries are the default left/right/middle colors, so a fresh
/// install shows a selected swatch in each dropdown.
/// </summary>
public static class Palette
{
    public static readonly ColorSwatch[] Colors =
    [
        new("#3B82F6", "Blue"),   // default left
        new("#F97316", "Orange"), // default right
        new("#22C55E", "Green"),  // default middle
        new("#EF4444", "Red"),
        new("#A855F7", "Purple"),
        new("#06B6D4", "Cyan")
    ];
}
