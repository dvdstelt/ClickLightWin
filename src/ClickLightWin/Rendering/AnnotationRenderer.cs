using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace ClickLightWin.Rendering;

/// <summary>
/// Draws persistent annotations on one monitor's overlay. Arrows are the first
/// (and currently only) shape; boxes slot in later behind the same begin/update/
/// commit/clear flow. A live preview follows the drag; committed shapes stay until
/// <see cref="Clear"/>. Mirrors the arrow drawing in ClickOverlayView.swift.
/// </summary>
public sealed class AnnotationRenderer(Canvas canvas)
{
    private static readonly Brush OutlineBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x99, 0, 0, 0)));

    private readonly List<UIElement> _committed = [];
    private readonly List<UIElement> _preview = [];
    private Point _start;

    public void Begin(Point start) => _start = start;

    public void Update(Point end, Settings settings)
    {
        RemovePreview();
        foreach (var shape in BuildArrow(_start, end, settings))
        {
            _preview.Add(shape);
            canvas.Children.Add(shape);
        }
    }

    public void Commit(Point end, Settings settings)
    {
        RemovePreview();
        if (Distance(_start, end) < settings.ArrowMinLengthDips) return; // ignore tiny drags
        foreach (var shape in BuildArrow(_start, end, settings))
        {
            _committed.Add(shape);
            canvas.Children.Add(shape);
        }
    }

    public void Clear()
    {
        RemovePreview();
        foreach (var shape in _committed) canvas.Children.Remove(shape);
        _committed.Clear();
    }

    private void RemovePreview()
    {
        foreach (var shape in _preview) canvas.Children.Remove(shape);
        _preview.Clear();
    }

    private static IEnumerable<Shape> BuildArrow(Point start, Point end, Settings settings)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = Math.Max(Math.Sqrt(dx * dx + dy * dy), 0.001);
        var ux = dx / length;
        var uy = dy / length;
        var nx = -uy; // unit normal
        var ny = ux;

        var headLen = Math.Min(settings.ArrowHeadLength, length); // don't overrun short arrows
        var headW = settings.ArrowHeadWidth;
        var shaftEnd = new Point(end.X - ux * headLen * 0.85, end.Y - uy * headLen * 0.85);
        var baseCenter = new Point(end.X - ux * headLen, end.Y - uy * headLen);
        var left = new Point(baseCenter.X + nx * headW / 2, baseCenter.Y + ny * headW / 2);
        var right = new Point(baseCenter.X - nx * headW / 2, baseCenter.Y - ny * headW / 2);

        var color = settings.ArrowColor;
        var fill = new SolidColorBrush(color);
        var t = settings.ArrowThickness;

        // Dark outline underneath for contrast on any background.
        yield return Shaft(start, shaftEnd, OutlineBrush, t + 3);
        yield return Head(end, left, right, OutlineBrush, OutlineBrush, 0);
        // Colored arrow on top.
        yield return Shaft(start, shaftEnd, fill, t);
        yield return Head(end, left, right, fill, OutlineBrush, 1);
    }

    private static Line Shaft(Point a, Point b, Brush brush, double thickness) => new()
    {
        X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y,
        Stroke = brush,
        StrokeThickness = thickness,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        IsHitTestVisible = false
    };

    private static Polygon Head(Point tip, Point left, Point right, Brush fill, Brush stroke, double strokeThickness) => new()
    {
        Points = [tip, left, right],
        Fill = fill,
        Stroke = stroke,
        StrokeThickness = strokeThickness,
        StrokeLineJoin = PenLineJoin.Round,
        IsHitTestVisible = false
    };

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static Brush Freeze(SolidColorBrush b)
    {
        b.Freeze();
        return b;
    }
}
