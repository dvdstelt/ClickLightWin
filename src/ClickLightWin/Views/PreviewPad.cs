using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClickLightWin.Rendering;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace ClickLightWin.Views;

/// <summary>
/// The interactive Preview Pad in the settings window: a small canvas that draws
/// real click effects (press ring, drag trail, release ring, laser glow) as you
/// click and drag inside it, using the live <see cref="Settings"/> from its
/// DataContext. Reuses the same renderers the overlays use, so the preview matches
/// what appears on screen. Mirrors the macOS InteractiveClickPreviewView.
/// </summary>
public sealed class PreviewPad : Canvas
{
    private PulseRenderer? _pulses;
    private LaserRenderer? _laser;

    public PreviewPad()
    {
        ClipToBounds = true;
        // A canvas only receives mouse input where it has a non-null background.
        Background = new SolidColorBrush(Color.FromRgb(0x15, 0x15, 0x17));
        Cursor = Cursors.Cross;
        Unloaded += (_, _) => _laser?.Dispose();
    }

    private Settings? Model => DataContext as Settings;

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        if (Model is not { } settings || Map(e.ChangedButton) is not { } button) return;
        _pulses ??= new PulseRenderer(this);
        CaptureMouse(); // so a drag that leaves the small pad still tracks
        _pulses.Spawn(e.GetPosition(this), new ClickEvent(button, ClickPhase.Down, 0, 0), settings);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (Model is not { } settings) return;
        var p = e.GetPosition(this);

        if (settings.ShowLaserPointer)
        {
            _laser ??= new LaserRenderer(this, settings);
            _laser.UpdateCursor(p, settings);
            return; // the laser replaces the drag trail, matching the overlay behavior
        }

        // Drag trail: driven by the button actually held during the move, so it does
        // not depend on capture or a separate dragging flag staying in sync.
        if (Held(e) is { } button && settings.ShowDrag)
        {
            _pulses ??= new PulseRenderer(this);
            _pulses.Spawn(p, new ClickEvent(button, ClickPhase.Drag, 0, 0), settings);
        }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        if (Model is not { } settings || Map(e.ChangedButton) is not { } button) return;
        if (IsMouseCaptured) ReleaseMouseCapture();
        if (settings.ShowRelease)
        {
            _pulses ??= new PulseRenderer(this);
            _pulses.Spawn(e.GetPosition(this), new ClickEvent(button, ClickPhase.Up, 0, 0), settings);
        }
    }

    private static ClickButton? Held(MouseEventArgs e) =>
        e.LeftButton == MouseButtonState.Pressed ? ClickButton.Left :
        e.RightButton == MouseButtonState.Pressed ? ClickButton.Right :
        e.MiddleButton == MouseButtonState.Pressed ? ClickButton.Middle : null;

    private static ClickButton? Map(MouseButton button) => button switch
    {
        MouseButton.Left => ClickButton.Left,
        MouseButton.Right => ClickButton.Right,
        MouseButton.Middle => ClickButton.Middle,
        _ => null
    };
}
