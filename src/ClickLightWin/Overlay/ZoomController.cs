using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using ClickLightWin.Interop;
using DrawingPoint = System.Drawing.Point;
using Screen = System.Windows.Forms.Screen;

namespace ClickLightWin.Overlay;

/// <summary>
/// Toggles the ZoomIt-style full-screen zoom: on activate it snapshots the monitor under
/// the cursor and shows it in a <see cref="ZoomWindow"/> that scales and pans around the
/// cursor. Esc, right-click, or the hotkey again closes it.
/// </summary>
public sealed class ZoomController : IDisposable
{
    private ZoomWindow? _window;

    public bool Active => _window is not null;

    public void Toggle()
    {
        if (Active) Close();
        else Open();
    }

    private void Open()
    {
        if (!NativeMethods.GetCursorPos(out var pt)) return;
        var screen = Screen.FromPoint(new DrawingPoint(pt.X, pt.Y));
        var window = new ZoomWindow(screen, CaptureScreen(screen)); // capture before showing
        window.ExitRequested += Close;
        _window = window;
        window.Show();
        window.Activate();
    }

    private void Close()
    {
        if (_window is null) return;
        _window.ExitRequested -= Close;
        _window.Close();
        _window = null;
    }

    private static BitmapSource CaptureScreen(Screen screen)
    {
        var b = screen.Bounds;
        using var bmp = new System.Drawing.Bitmap(b.Width, b.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
            g.CopyFromScreen(b.X, b.Y, 0, 0, new System.Drawing.Size(b.Width, b.Height));

        var hbitmap = bmp.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(
                hbitmap, 0, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            NativeMethods.DeleteObject(hbitmap);
        }
    }

    public void Dispose() => Close();
}
