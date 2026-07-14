using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace ClickLightWin.Rendering;

/// <summary>
/// Persistent freehand strokes for the Ctrl+Shift+D drawing mode. Each stroke is a
/// layered polyline (soft glow, mid, bright core) that stays on screen until
/// <see cref="Clear"/> (called when the mode is left). Unlike the laser stroke,
/// nothing fades. Coordinates are already in this overlay's DIPs (WPF mouse input).
/// </summary>
public sealed class DrawStrokeRenderer(Canvas canvas)
{
    private static readonly Color StrokeColor = Color.FromRgb(0xFF, 0x29, 0x05); // red, matches the laser
    private static readonly Color CoreColor = Color.FromRgb(0xFF, 0xF0, 0xEE);

    private readonly List<UIElement> _strokes = [];
    private Polyline[] _active = [];

    public void Begin(Point start)
    {
        _active =
        [
            MakeLine(StrokeColor, 13, 0.28), // wide soft glow
            MakeLine(StrokeColor, 6, 0.95),  // mid
            MakeLine(CoreColor, 2.5, 1.0)    // bright core
        ];
        foreach (var line in _active)
        {
            line.Points.Add(start);
            _strokes.Add(line);
            canvas.Children.Add(line);
        }
    }

    public void Append(Point p)
    {
        foreach (var line in _active) line.Points.Add(p);
    }

    public void Complete() => _active = [];

    public void Clear()
    {
        foreach (var stroke in _strokes) canvas.Children.Remove(stroke);
        _strokes.Clear();
        _active = [];
    }

    public bool HasStrokes => _strokes.Count > 0;

    private static Polyline MakeLine(Color color, double thickness, double opacity) => new()
    {
        Stroke = new SolidColorBrush(color),
        StrokeThickness = thickness,
        Opacity = opacity,
        StrokeLineJoin = PenLineJoin.Round,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        IsHitTestVisible = false,
        Points = []
    };
}
