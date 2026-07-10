using System.Windows;
using Point = System.Windows.Point;
using Rectangle = System.Drawing.Rectangle;

namespace ClickLightWin.Rendering;

/// <summary>
/// Converts physical virtual-screen pixel coordinates (as delivered by the
/// low-level mouse hook) into device-independent units local to a specific
/// monitor's overlay window. This is the Windows analogue of the macOS
/// CoordinateMapper, which reconciles Quartz and AppKit coordinate spaces.
/// Takes the monitor's physical bounds (rather than a Screen, which cannot be
/// constructed in tests) so the math stays unit-testable.
/// </summary>
public static class CoordinateMapper
{
    public static Point PhysicalToLocalDips(int physX, int physY, Rectangle screenBounds, DpiScale dpi)
    {
        // Offset into the monitor, in physical pixels.
        var offsetX = physX - screenBounds.X;
        var offsetY = physY - screenBounds.Y;

        // Physical pixels -> DIPs. dpi.DpiScaleX is 1.0 at 96 DPI, 1.5 at 144, etc.
        return new Point(offsetX / dpi.DpiScaleX, offsetY / dpi.DpiScaleY);
    }
}
