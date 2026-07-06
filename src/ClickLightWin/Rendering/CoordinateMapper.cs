using System.Windows;
using Point = System.Windows.Point;
using Screen = System.Windows.Forms.Screen;

namespace ClickLightWin.Rendering;

/// <summary>
/// Converts physical virtual-screen pixel coordinates (as delivered by the
/// low-level mouse hook) into device-independent units local to a specific
/// monitor's overlay window. This is the Windows analogue of the macOS
/// CoordinateMapper, which reconciles Quartz and AppKit coordinate spaces.
/// </summary>
public static class CoordinateMapper
{
    public static Point PhysicalToLocalDips(int physX, int physY, Screen screen, DpiScale dpi)
    {
        // Offset into the monitor, in physical pixels.
        var offsetX = physX - screen.Bounds.X;
        var offsetY = physY - screen.Bounds.Y;

        // Physical pixels -> DIPs. dpi.DpiScaleX is 1.0 at 96 DPI, 1.5 at 144, etc.
        return new Point(offsetX / dpi.DpiScaleX, offsetY / dpi.DpiScaleY);
    }
}
