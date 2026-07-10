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
/// clear flow. The live preview builds its shapes once per gesture and mutates
/// their geometry on each drag point (rather than recreating them per mouse-move),
/// and the commit promotes those same shapes to the committed set. Committed
/// shapes stay until <see cref="Clear"/>. Mirrors ClickOverlayView.swift.
/// </summary>
public sealed class AnnotationRenderer(Canvas canvas)
{
    // A faint dark edge for contrast on any background, shared by arrows and boxes.
    private static readonly Brush OutlineBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x33, 0, 0, 0)));

    private readonly List<UIElement> _committed = [];
    private Shape[] _preview = [];
    private AnnotationTool _tool;
    private Point _start;

    public void Begin(AnnotationTool tool, Point start)
    {
        RemovePreview();
        _tool = tool;
        _start = start;
    }

    public void Update(Point end, Settings settings)
    {
        EnsurePreview(settings);
        UpdateGeometry(_preview, _start, end, settings);
    }

    public void Commit(Point end, Settings settings)
    {
        if (Distance(_start, end) < settings.AnnotationMinLengthDips)
        {
            RemovePreview(); // ignore tiny accidental drags
            return;
        }

        EnsurePreview(settings);
        UpdateGeometry(_preview, _start, end, settings);
        _committed.AddRange(_preview);
        _preview = [];
    }

    public void Clear()
    {
        RemovePreview();
        foreach (var shape in _committed) canvas.Children.Remove(shape);
        _committed.Clear();
    }

    private void EnsurePreview(Settings settings)
    {
        if (_preview.Length != 0) return;
        _preview = _tool == AnnotationTool.Box ? BuildBox(settings) : BuildArrow(settings);
        foreach (var shape in _preview) canvas.Children.Add(shape);
    }

    private void RemovePreview()
    {
        foreach (var shape in _preview) canvas.Children.Remove(shape);
        _preview = [];
    }

    // ---- Shape construction (styling only; geometry is applied by UpdateGeometry) ----

    private static Shape[] BuildArrow(Settings settings)
    {
        var fill = new SolidColorBrush(settings.AnnotationColor);
        var t = settings.ArrowThickness;
        return
        [
            NewLine(OutlineBrush, t + 1.5),      // faint outline shaft
            NewTriangle(OutlineBrush, OutlineBrush, 0), // faint outline head
            NewLine(fill, t),                    // colored shaft
            NewTriangle(fill, OutlineBrush, 1)   // colored head
        ];
    }

    private static Shape[] BuildBox(Settings settings)
    {
        var color = settings.AnnotationColor;
        var t = settings.BoxThickness;
        var faintFill = new SolidColorBrush(Color.FromArgb(0x12, color.R, color.G, color.B));
        return
        [
            NewRect(OutlineBrush, t + 1.5, null, settings),                 // faint outline
            NewRect(new SolidColorBrush(color), t, faintFill, settings)    // colored border + wash
        ];
    }

    private static void UpdateGeometry(Shape[] shapes, Point start, Point end, Settings settings)
    {
        if (shapes.Length == 4) UpdateArrow(shapes, start, end, settings);
        else UpdateBox(shapes, start, end);
    }

    private static void UpdateArrow(Shape[] shapes, Point start, Point end, Settings settings)
    {
        var (shaftEnd, left, right) = ArrowGeometry(start, end, settings);
        SetLine((Line)shapes[0], start, shaftEnd);
        SetTriangle((Polygon)shapes[1], end, left, right);
        SetLine((Line)shapes[2], start, shaftEnd);
        SetTriangle((Polygon)shapes[3], end, left, right);
    }

    private static void UpdateBox(Shape[] shapes, Point start, Point end)
    {
        var x = Math.Min(start.X, end.X);
        var y = Math.Min(start.Y, end.Y);
        var w = Math.Abs(end.X - start.X);
        var h = Math.Abs(end.Y - start.Y);
        foreach (var shape in shapes)
        {
            Canvas.SetLeft(shape, x);
            Canvas.SetTop(shape, y);
            shape.Width = w;
            shape.Height = h;
        }
    }

    private static (Point ShaftEnd, Point Left, Point Right) ArrowGeometry(Point start, Point end, Settings settings)
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
        return (shaftEnd, left, right);
    }

    private static void SetLine(Line line, Point a, Point b)
    {
        line.X1 = a.X;
        line.Y1 = a.Y;
        line.X2 = b.X;
        line.Y2 = b.Y;
    }

    private static void SetTriangle(Polygon polygon, Point tip, Point left, Point right)
    {
        var points = polygon.Points;
        if (points.Count == 3)
        {
            points[0] = tip;
            points[1] = left;
            points[2] = right;
        }
        else
        {
            points.Clear();
            points.Add(tip);
            points.Add(left);
            points.Add(right);
        }
    }

    private static Line NewLine(Brush brush, double thickness) => new()
    {
        Stroke = brush,
        StrokeThickness = thickness,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        IsHitTestVisible = false
    };

    private static Polygon NewTriangle(Brush fill, Brush stroke, double strokeThickness) => new()
    {
        Fill = fill,
        Stroke = stroke,
        StrokeThickness = strokeThickness,
        StrokeLineJoin = PenLineJoin.Round,
        IsHitTestVisible = false
    };

    private static Rectangle NewRect(Brush stroke, double thickness, Brush? fill, Settings settings) => new()
    {
        Stroke = stroke,
        StrokeThickness = thickness,
        RadiusX = settings.BoxCornerRadius,
        RadiusY = settings.BoxCornerRadius,
        Fill = fill,
        IsHitTestVisible = false
    };

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static Brush Freeze(SolidColorBrush b)
    {
        b.Freeze();
        return b;
    }
}
