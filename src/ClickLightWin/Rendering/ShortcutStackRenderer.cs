using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;

namespace ClickLightWin.Rendering;

/// <summary>
/// A bottom-center stack of key-cap pills for the live shortcut display. Each
/// pressed shortcut adds a pill that holds briefly, then fades and is reaped;
/// the newest sits at the bottom and the stack grows upward (Carnac-style).
/// Mirrors LiveShortcutLabel in ClickOverlayView.swift.
/// </summary>
public sealed class ShortcutStackRenderer(StackPanel host)
{
    private static readonly Brush PillBackground = Freeze(new SolidColorBrush(Color.FromArgb(0xE6, 0x20, 0x21, 0x24)));
    private static readonly Brush CapBackground = Freeze(new SolidColorBrush(Color.FromArgb(0xFF, 0x3A, 0x3A, 0x40)));
    private static readonly Brush TextBrush = Freeze(new SolidColorBrush(Colors.White));
    private static readonly Brush PlusBrush = Freeze(new SolidColorBrush(Color.FromArgb(0xFF, 0x9A, 0x9A, 0xA2)));

    public void Show(IReadOnlyList<string> keys, Settings settings)
    {
        var pill = BuildPill(keys, settings.ShortcutFontSize);
        host.Children.Add(pill);
        while (host.Children.Count > settings.ShortcutStackMax)
            host.Children.RemoveAt(0);

        // Hold at full opacity, then fade out and reap.
        var fade = new DoubleAnimation(1, 0, settings.ShortcutFade) { BeginTime = settings.ShortcutHold.TimeSpan };
        fade.Completed += (_, _) => host.Children.Remove(pill);
        pill.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    /// <summary>Build a key-cap pill (shared by the bottom stack and the near-pointer path).</summary>
    public static Border BuildPill(IReadOnlyList<string> keys, double fontSize)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        for (var i = 0; i < keys.Count; i++)
        {
            if (i > 0)
                row.Children.Add(new TextBlock
                {
                    Text = "+",
                    Foreground = PlusBrush,
                    FontSize = fontSize,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(5, 0, 5, 0)
                });
            row.Children.Add(Cap(keys[i], fontSize));
        }

        return new Border
        {
            Background = PillBackground,
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 5, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = row
        };
    }

    private static Border Cap(string text, double fontSize) => new()
    {
        Background = CapBackground,
        CornerRadius = new CornerRadius(5),
        Padding = new Thickness(fontSize * 0.6, fontSize * 0.2, fontSize * 0.6, fontSize * 0.2),
        Child = new TextBlock
        {
            Text = text,
            Foreground = TextBrush,
            FontSize = fontSize,
            FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI")
        }
    };

    private static Brush Freeze(SolidColorBrush b)
    {
        b.Freeze();
        return b;
    }
}
