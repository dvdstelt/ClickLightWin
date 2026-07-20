using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Color = System.Windows.Media.Color;
using Panel = System.Windows.Controls.Panel;

namespace ClickLightWin.Rendering;

/// <summary>
/// A transient mode-switch toast (ClickLight / Laser Pointer / Off) shown when the
/// toggle hotkey cycles. It reuses the live shortcut display's pill layout, but sits
/// at the top-center with a color-accented border so it still reads as its own thing.
/// </summary>
public sealed class ModeIndicatorRenderer(Panel host)
{
    public void Show(string text, Color accent)
    {
        host.Children.Clear(); // only the latest switch is shown

        var pill = ShortcutStackRenderer.BuildPill([text], 16);
        pill.BorderBrush = new SolidColorBrush(accent);
        pill.BorderThickness = new Thickness(2);
        host.Children.Add(pill);

        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300))
        {
            BeginTime = TimeSpan.FromMilliseconds(1100)
        };
        fade.Completed += (_, _) => host.Children.Remove(pill);
        pill.BeginAnimation(UIElement.OpacityProperty, fade);
    }
}
