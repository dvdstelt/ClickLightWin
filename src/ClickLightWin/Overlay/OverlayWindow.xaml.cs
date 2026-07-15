using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using ClickLightWin.Interop;
using ClickLightWin.Rendering;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
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
    private DrawStrokeRenderer? _drawStrokes;
    private bool _drawing;
    private nint _hwnd;

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

    /// <summary>Move the laser cursor glow to follow the pointer (physical pixels).</summary>
    public void Laser(ClickEvent click, Settings settings)
    {
        _laser ??= new LaserRenderer(PulseCanvas, settings);
        _laser.UpdateCursor(ToLocal(click), settings);
    }

    /// <summary>Drive an annotation gesture for one event (physical pixels).</summary>
    public void Annotate(AnnotationEvent evt, Settings settings)
    {
        var local = ToLocal(evt.ScreenX, evt.ScreenY);
        _annotations ??= new AnnotationRenderer(PulseCanvas);
        switch (evt.Phase)
        {
            case AnnotationPhase.Begin: _annotations.Begin(evt.Tool, local); break;
            case AnnotationPhase.Update: _annotations.Update(local, settings); break;
            case AnnotationPhase.Commit: _annotations.Commit(local, settings); break;
        }
    }

    /// <summary>Remove all committed annotations on this overlay.</summary>
    public void ClearAnnotations() => _annotations?.Clear();

    /// <summary>
    /// Enter or leave the frozen drawing mode. In draw mode the overlay stops being
    /// click-through and captures the mouse to draw persistent freehand strokes; on
    /// exit, click-through is restored and the strokes are cleared.
    /// </summary>
    private static readonly Brush DrawTint = Freeze(Color.FromArgb(0x1A, 0, 0, 0)); // faint frozen dim
    private static readonly Brush TransparentBrush = Freeze(Color.FromArgb(0, 0, 0, 0));

    public void SetDrawMode(bool on)
    {
        if (_drawModeOn == on) return;
        _drawModeOn = on;

        var ex = (long)NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE);
        ex = on ? ex & ~NativeMethods.WS_EX_TRANSPARENT   // capture the mouse
                : ex | NativeMethods.WS_EX_TRANSPARENT;   // click-through again
        NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE, (nint)ex);
        NativeMethods.SetWindowPos(_hwnd, 0, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER
            | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED); // flush the style change

        // Both the window and canvas must be hit-testable, and a layered window passes
        // clicks through fully-transparent pixels regardless of WS_EX_TRANSPARENT, so a
        // faint tint is needed to actually capture the mouse (it also cues the freeze).
        IsHitTestVisible = on;
        PulseCanvas.IsHitTestVisible = on;
        PulseCanvas.Background = on ? DrawTint : TransparentBrush;
        DrawModeBorder.Visibility = on ? Visibility.Visible : Visibility.Collapsed;

        if (on)
        {
            _drawStrokes ??= new DrawStrokeRenderer(PulseCanvas);
            PulseCanvas.MouseLeftButtonDown += OnDrawDown;
            PulseCanvas.MouseMove += OnDrawMove;
            PulseCanvas.MouseLeftButtonUp += OnDrawUp;
        }
        else
        {
            PulseCanvas.MouseLeftButtonDown -= OnDrawDown;
            PulseCanvas.MouseMove -= OnDrawMove;
            PulseCanvas.MouseLeftButtonUp -= OnDrawUp;
            _drawing = false;
            _drawStrokes?.Clear(); // strokes live only while the mode is active
        }
    }

    private bool _drawModeOn;

    private void OnDrawDown(object sender, MouseButtonEventArgs e)
    {
        _drawing = true;
        PulseCanvas.CaptureMouse();
        _drawStrokes?.Begin(e.GetPosition(PulseCanvas));
    }

    private void OnDrawMove(object sender, MouseEventArgs e)
    {
        if (_drawing) _drawStrokes?.Append(e.GetPosition(PulseCanvas));
    }

    private void OnDrawUp(object sender, MouseButtonEventArgs e)
    {
        if (!_drawing) return;
        _drawing = false;
        PulseCanvas.ReleaseMouseCapture();
        _drawStrokes?.Complete();
    }

    private static Brush Freeze(Color c)
    {
        var brush = new SolidColorBrush(c);
        brush.Freeze();
        return brush;
    }

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
        return CoordinateMapper.PhysicalToLocalDips(physX, physY, _screen.Bounds, dpi);
    }

    /// <summary>Physical-pixel bounds of this overlay's monitor, for hit-testing.</summary>
    public System.Drawing.Rectangle ScreenBounds => _screen.Bounds;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = _hwnd = new WindowInteropHelper(this).Handle;

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
