using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace ClickLightWin.Rendering;

/// <summary>
/// Draws persistent annotations on one monitor's overlay. The mouse button picks
/// the shape (left = arrow, right = box); both follow the same begin/update/commit/
/// clear flow. A live preview follows the drag; committed shapes stay until
/// <see cref="Clear"/>. Mirrors the arrow/box drawing in ClickOverlayView.swift.
/// </summary>
public sealed class AnnotationRenderer(Canvas canvas)
{
    // A faint dark edge for contrast on any background, shared by arrows and boxes.
    private static readonly Brush OutlineBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x33, 0, 0, 0)));

    private readonly List<UIElement> _committed = [];
    private readonly List<UIElement> _preview = [];
    private AnnotationTool _tool;
    private Point _start;

    public void Begin(AnnotationTool tool, Point start)
    {
        _tool = tool;
        _start = start;
    }

    public void Update(Point end, Settings settings)
    {
        RemovePreview();
        AddShapes(_preview, Build(_start, end, settings));
    }

    public void Commit(Point end, Settings settings)
    {
        RemovePreview();
        if (Distance(_start, end) < settings.AnnotationMinLengthDips) return; // ignore tiny drags
        AddShapes(_committed, Build(_start, end, settings));
    }

    public void Clear()
    {
        RemovePreview();
        foreach (var shape in _committed) canvas.Children.Remove(shape);
        _committed.Clear();
    }

    private void AddShapes(List<UIElement> track, IEnumerable<Shape> shapes)
    {
        foreach (var shape in shapes)
        {
            track.Add(shape);
            canvas.Children.Add(shape);
        }
    }

    private void RemovePreview()
    {
        foreach (var shape in _preview) canvas.Children.Remove(shape);
        _preview.Clear();
    }

    private IEnumerable<Shape> Build(Point start, Point end, Settings settings) =>
        _tool == AnnotationTool.Box ? BuildBox(start, end, settings) : BuildArrow(start, end, settings);

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

        var fill = new SolidColorBrush(settings.AnnotationColor);
        var t = settings.ArrowThickness;

        // Faint outline underneath for contrast, colored arrow on top.
        yield return Shaft(start, shaftEnd, OutlineBrush, t + 1.5);
        yield return Head(end, left, right, OutlineBrush, OutlineBrush, 0);
        yield return Shaft(start, shaftEnd, fill, t);
        yield return Head(end, left, right, fill, OutlineBrush, 1);
    }

    private static IEnumerable<Shape> BuildBox(Point start, Point end, Settings settings)
    {
        var x = Math.Min(start.X, end.X);
        var y = Math.Min(start.Y, end.Y);
        var w = Math.Abs(end.X - start.X);
        var h = Math.Abs(end.Y - start.Y);
        var color = settings.AnnotationColor;
        var t = settings.BoxThickness;
        var faintFill = new SolidColorBrush(Color.FromArgb(0x12, color.R, color.G, color.B));

        yield return Rect(x, y, w, h, OutlineBrush, t + 1.5, null, settings);
        yield return Rect(x, y, w, h, new SolidColorBrush(color), t, faintFill, settings);
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

    private static Rectangle Rect(double x, double y, double w, double h, Brush stroke, double thickness, Brush? fill, Settings settings)
    {
        var rect = new Rectangle
        {
            Width = w,
            Height = h,
            Stroke = stroke,
            StrokeThickness = thickness,
            RadiusX = settings.BoxCornerRadius,
            RadiusY = settings.BoxCornerRadius,
            Fill = fill,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        return rect;
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static Brush Freeze(SolidColorBrush b)
    {
        b.Freeze();
        return b;
    }
}
