using System.Windows;
using System.Windows.Interop;
using ClickLightWin.Interop;
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

    public OverlayWindow(Screen screen)
    {
        InitializeComponent();
        _screen = screen;
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
