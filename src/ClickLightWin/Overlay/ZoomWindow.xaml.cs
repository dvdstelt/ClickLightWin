using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using ClickLightWin.Interop;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using Screen = System.Windows.Forms.Screen;

namespace ClickLightWin.Overlay;

/// <summary>
/// A ZoomIt-style full-screen zoom: a frozen snapshot of one monitor shown scaled and
/// panned around the cursor. Because it is a static image with a render transform, the
/// zoom/pan is GPU-cheap and stays smooth. Scroll changes zoom; the point under the
/// cursor stays fixed. Esc or right-click exits.
/// </summary>
public partial class ZoomWindow : Window
{
    private const double ZoomStep = 0.25;
    private const double MinZoom = 1.0;
    private const double MaxZoom = 6.0;

    private readonly Screen _screen;
    private double _zoom = 2.0;

    public event Action? ExitRequested;

    public ZoomWindow(Screen screen, BitmapSource capture)
    {
        InitializeComponent();
        _screen = screen;
        Frozen.Source = capture;
        Loaded += (_, _) =>
        {
            Activate();
            Focus();
            UpdateTransform(Mouse.GetPosition(this));
        };
    }

    protected override void OnMouseMove(MouseEventArgs e) => UpdateTransform(e.GetPosition(this));

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        e.Handled = true;
        var next = _zoom + (e.Delta > 0 ? ZoomStep : -ZoomStep);
        if (next <= MinZoom)
        {
            ExitRequested?.Invoke(); // zoomed all the way out = leave zoom mode
            return;
        }
        _zoom = Math.Min(next, MaxZoom);
        UpdateTransform(e.GetPosition(this));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape) ExitRequested?.Invoke();
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e) => ExitRequested?.Invoke();

    private void UpdateTransform(Point cursor)
    {
        Scale.ScaleX = Scale.ScaleY = _zoom;
        var w = ActualWidth;
        var h = ActualHeight;
        // Keep the point under the cursor fixed and magnify around it, clamped so the
        // image always covers the window (no black edges).
        Trans.X = Math.Clamp(cursor.X * (1 - _zoom), w * (1 - _zoom), 0);
        Trans.Y = Math.Clamp(cursor.Y * (1 - _zoom), h * (1 - _zoom), 0);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;

        // Hide from alt-tab but stay activatable so it can take keyboard focus (Esc).
        var ex = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, (nint)((long)ex | NativeMethods.WS_EX_TOOLWINDOW));

        var b = _screen.Bounds;
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, b.X, b.Y, b.Width, b.Height,
            NativeMethods.SWP_SHOWWINDOW);
    }
}
