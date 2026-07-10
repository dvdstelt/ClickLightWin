using System.Drawing;
using System.Windows;
using ClickLightWin.Rendering;
using Point = System.Windows.Point;

namespace ClickLightWin.Tests;

public class CoordinateMapperTests
{
    [Fact]
    public void Maps_a_primary_monitor_point_unchanged_at_100_percent()
    {
        var bounds = new Rectangle(0, 0, 1920, 1080);
        var p = CoordinateMapper.PhysicalToLocalDips(100, 200, bounds, new DpiScale(1.0, 1.0));
        Assert.Equal(new Point(100, 200), p);
    }

    [Fact]
    public void Divides_by_the_dpi_scale_at_150_percent()
    {
        var bounds = new Rectangle(0, 0, 2880, 1620);
        var p = CoordinateMapper.PhysicalToLocalDips(300, 300, bounds, new DpiScale(1.5, 1.5));
        Assert.Equal(new Point(200, 200), p);
    }

    [Fact]
    public void Subtracts_the_monitor_origin_for_a_monitor_left_of_primary()
    {
        var bounds = new Rectangle(-1920, 0, 1920, 1080); // negative virtual-screen origin
        var p = CoordinateMapper.PhysicalToLocalDips(-1820, 50, bounds, new DpiScale(1.0, 1.0));
        Assert.Equal(new Point(100, 50), p);
    }

    [Fact]
    public void Combines_origin_offset_and_scale_for_a_mixed_dpi_secondary()
    {
        // 150%-scaled monitor to the right of a 1920-wide primary.
        var bounds = new Rectangle(1920, 0, 2880, 1620);
        var p = CoordinateMapper.PhysicalToLocalDips(1920 + 300, 150, bounds, new DpiScale(1.5, 1.5));
        Assert.Equal(new Point(200, 100), p);
    }

    [Fact]
    public void Supports_different_horizontal_and_vertical_scales()
    {
        var bounds = new Rectangle(0, 0, 1920, 1080);
        var p = CoordinateMapper.PhysicalToLocalDips(200, 200, bounds, new DpiScale(2.0, 1.25));
        Assert.Equal(new Point(100, 160), p);
    }
}
