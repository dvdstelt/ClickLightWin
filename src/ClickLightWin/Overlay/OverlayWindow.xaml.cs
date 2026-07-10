using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using ClickLightWin.Interop;
using ClickLightWin.Rendering;
using Point = System.Windows.Point;
using Screen = System.Windows.Forms.Screen;

namespace ClickLightWin.Overlay;

/// <summary>
/// A transparent, click-through, topmost overlay covering exactly one monitor.
/// Placement is done in physical pixels via SetWindowPos so WPF's DIP-based
/// Left/Top do not fight per-monitor DPI. Maps to ClickOverlayWindow.swift.
/// </summary>
public partial class OverlayWindow : Window
{
    private readonly Screen _screen;
    private readonly PulseRenderer _renderer;
    private LaserRenderer? _laser;
    private AnnotationRenderer? _annotations;
    private ShortcutStackRenderer? _shortcuts;

    public OverlayWindow(Screen screen)
    {
        InitializeComponent();
        _screen = screen;
        _renderer = new PulseRenderer(PulseCanvas);
    }

    /// <summary>Spawn a pulse. The click point is in physical virtual-screen pixels.</summary>
    public void ShowPulse(ClickEvent click, Settings settings)
    {
        _renderer.Spawn(ToLocal(click), click, settings);
    }

    /// <summary>Drive the laser cursor/stroke for one event (physical pixels).</summary>
    public void Laser(ClickEvent click, Settings settings)
    {
        _laser ??= new LaserRenderer(PulseCanvas, settings);
        var local = ToLocal(click);
        switch (click.Phase)
        {
            case ClickPhase.Down: _laser.BeginStroke(); break;
            case ClickPhase.Move: _laser.UpdateCursor(local, settings); break;
            case ClickPhase.Drag: _laser.UpdateCursor(local, settings); _laser.AppendPoint(local, settings); break;
            case ClickPhase.Up: _laser.CompleteStroke(settings); break;
        }
    }

    /// <summary>Drive an annotation (arrow) gesture for one event (physical pixels).</summary>
    public void Annotate(AnnotationEvent evt, Settings settings)
    {
        _annotations ??= new AnnotationRenderer(PulseCanvas);
        var local = ToLocal(evt.ScreenX, evt.ScreenY);
        switch (evt.Phase)
        {
            case AnnotationPhase.Begin: _annotations.Begin(evt.Tool, local); break;
            case AnnotationPhase.Update: _annotations.Update(local, settings); break;
            case AnnotationPhase.Commit: _annotations.Commit(local, settings); break;
        }
    }

    /// <summary>Remove all committed annotations on this overlay.</summary>
    public void ClearAnnotations() => _annotations?.Clear();

    /// <summary>Show a shortcut in this monitor's bottom-center stack.</summary>
    public void ShowShortcut(IReadOnlyList<string> keys, Settings settings)
    {
        _shortcuts ??= new ShortcutStackRenderer(ShortcutStack);
        _shortcuts.Show(keys, settings);
    }

    protected override void OnClosed(EventArgs e)
    {
        // The laser subscribes to the static CompositionTarget.Rendering event;
        // detach or the closed window is kept alive (and rendered to) forever.
        _laser?.Dispose();
        base.OnClosed(e);
    }

    private Point ToLocal(ClickEvent click) => ToLocal(click.ScreenX, click.ScreenY);

    private Point ToLocal(int physX, int physY)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        return CoordinateMapper.PhysicalToLocalDips(physX, physY, _screen, dpi);
    }

    /// <summary>Physical-pixel bounds of this overlay's monitor, for hit-testing.</summary>
    public System.Drawing.Rectangle ScreenBounds => _screen.Bounds;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;

        // Make the window click-through and hidden from alt-tab / taskbar.
        var ex = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
        var newEx = (nint)((long)ex
            | NativeMethods.WS_EX_LAYERED
            | NativeMethods.WS_EX_TRANSPARENT
            | NativeMethods.WS_EX_TOOLWINDOW
            | NativeMethods.WS_EX_NOACTIVATE);
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, newEx);

        // Place the window on its monitor in PHYSICAL pixels. Screen.Bounds is
        // already physical. This sidesteps WPF interpreting Left/Top in DIPs.
        var b = _screen.Bounds;
        NativeMethods.SetWindowPos(
            hwnd, NativeMethods.HWND_TOPMOST,
            b.X, b.Y, b.Width, b.Height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }
}
