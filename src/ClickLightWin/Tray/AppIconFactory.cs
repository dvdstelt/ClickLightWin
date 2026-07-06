using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using ClickLightWin.Interop;

namespace ClickLightWin.Tray;

/// <summary>
/// Builds the tray icon at runtime: a blue pulse ring with a center dot, echoing
/// the on-screen click pulse. Avoids shipping a binary asset; a designed multi-
/// resolution .ico can replace this at packaging time.
/// </summary>
internal static class AppIconFactory
{
    public static Icon CreatePulseIcon()
    {
        const int size = 32;
        var blue = Color.FromArgb(0x3B, 0x82, 0xF6);

        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var ringPen = new Pen(blue, 3f);
            g.DrawEllipse(ringPen, 6, 6, 19, 19); // expanding ring

            using var dotBrush = new SolidBrush(blue);
            g.FillEllipse(dotBrush, 13, 13, 6, 6); // click point
        }

        // GetHicon returns an unmanaged handle we do not own; clone into a managed
        // Icon (which owns its own handle) and free the original immediately.
        var hicon = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hicon);
            return (Icon)temp.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(hicon);
        }
    }
}
