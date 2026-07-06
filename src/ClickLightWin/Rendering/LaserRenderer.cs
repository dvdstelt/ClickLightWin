using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Color = System.Windows.Media.Color;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Point = System.Windows.Point;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ClickLightWin.Rendering;

/// <summary>
/// Draws the laser-pointer overlay on one monitor: a glowing halo that eases
/// along just behind the cursor and fades after movement stops, plus a fading
/// freehand stroke while a button is dragged. The stroke is drawn as three
/// layered lines (wide soft glow, mid, thin near-white core) so it reads as a
/// glowing red line with a bright center, matching ClickOverlayView.swift.
/// </summary>
public sealed class LaserRenderer
{
    private readonly Canvas _canvas;
    private readonly Grid _glow;
    private readonly DispatcherTimer _idle;

    // Cursor easing: the glow chases the latest target a fraction each frame,
    // producing the slight trailing lag the macOS laser has.
    private const double FollowFactor = 0.28;
    private Point _target;
    private Point _current;
    private bool _visible;
    private bool _rendering;

    // Active stroke: three overlaid polylines sharing the same points.
    private Canvas? _strokeLayer;
    private Polyline[] _strokeLines = [];
    private Point? _lastStrokePoint;

    public LaserRenderer(Canvas canvas, Settings settings)
    {
        _canvas = canvas;
        _glow = BuildGlow(settings);
        _glow.Visibility = Visibility.Collapsed;
        _canvas.Children.Add(_glow);

        _idle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(settings.LaserIdleFadeMs) };
        _idle.Tick += (_, _) => FadeGlowOut();
    }

    /// <summary>Aim the halo at the cursor; a per-frame loop eases it into place.</summary>
    public void UpdateCursor(Point center, Settings settings)
    {
        _target = center;
        if (!_visible)
        {
            _current = center; // snap on first appearance, then ease afterwards
            _visible = true;
        }

        _glow.BeginAnimation(UIElement.OpacityProperty, null); // cancel any running fade
        _glow.Opacity = 1;
        _glow.Visibility = Visibility.Visible;
        EnsureRendering();
        _idle.Stop();
        _idle.Start();
    }

    public void BeginStroke()
    {
        _strokeLayer = null;
        _strokeLines = [];
        _lastStrokePoint = null;
    }

    public void AppendPoint(Point p, Settings settings)
    {
        var minSq = settings.LaserMinSpacingDips * settings.LaserMinSpacingDips;
        if (_lastStrokePoint is { } last && Distance2(last, p) < minSq) return;

        if (_strokeLayer is null)
        {
            _strokeLayer = new Canvas { IsHitTestVisible = false };
            var mid = settings.LaserStrokeThickness;
            _strokeLines =
            [
                MakeLine(settings.LaserColor, mid * 2.6, 0.28), // wide soft glow
                MakeLine(settings.LaserColor, mid, 0.95),       // mid red
                MakeLine(settings.LaserCoreColor, mid * 0.45, 1.0) // thin bright core
            ];
            foreach (var line in _strokeLines) _strokeLayer.Children.Add(line);
            _canvas.Children.Add(_strokeLayer);
        }

        foreach (var line in _strokeLines) line.Points.Add(p);
        _lastStrokePoint = p;
    }

    public void CompleteStroke(Settings settings)
    {
        if (_strokeLayer is null) return;
        var layer = _strokeLayer;
        _strokeLayer = null;
        _strokeLines = [];
        _lastStrokePoint = null;

        var fade = new DoubleAnimation(1, 0, settings.LaserStrokeFade)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fade.Completed += (_, _) => _canvas.Children.Remove(layer);
        layer.BeginAnimation(UIElement.OpacityProperty, fade);
    }

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

    private void EnsureRendering()
    {
        if (_rendering) return;
        _rendering = true;
        CompositionTarget.Rendering += OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        _current = new Point(
            _current.X + (_target.X - _current.X) * FollowFactor,
            _current.Y + (_target.Y - _current.Y) * FollowFactor);

        var half = _glow.Width / 2;
        Canvas.SetLeft(_glow, _current.X - half);
        Canvas.SetTop(_glow, _current.Y - half);
    }

    private void FadeGlowOut()
    {
        _idle.Stop();
        var fade = new DoubleAnimation(_glow.Opacity, 0, new Duration(TimeSpan.FromMilliseconds(250)));
        fade.Completed += (_, _) =>
        {
            if (_glow.Opacity != 0) return;
            _glow.Visibility = Visibility.Collapsed;
            _visible = false;
            _rendering = false;
            CompositionTarget.Rendering -= OnRendering;
        };
        _glow.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    private static Grid BuildGlow(Settings settings)
    {
        var glowD = settings.LaserGlowDiameter;

        // Soft outer aura beyond the solid discs.
        var halo = new Ellipse
        {
            Width = glowD,
            Height = glowD,
            IsHitTestVisible = false,
            Fill = new RadialGradientBrush(
            [
                new GradientStop(WithAlpha(settings.LaserColor, 0.55), 0.0),
                new GradientStop(WithAlpha(settings.LaserColor, 0.0), 1.0)
            ])
        };

        var grid = new Grid { Width = glowD, Height = glowD, IsHitTestVisible = false };
        grid.Children.Add(halo);
        // Concentric solid discs so red dominates with a small bright center.
        grid.Children.Add(Disc(settings.LaserRedDiameter, settings.LaserColor));
        grid.Children.Add(Disc(settings.LaserMidDiameter, settings.LaserMidColor));
        grid.Children.Add(Disc(settings.LaserCoreDiameter, settings.LaserCoreColor));
        return grid;
    }

    private static Ellipse Disc(double diameter, Color color) => new()
    {
        Width = diameter,
        Height = diameter,
        Fill = new SolidColorBrush(color),
        IsHitTestVisible = false,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };

    private static Color WithAlpha(Color c, double a) => Color.FromArgb((byte)(a * 255), c.R, c.G, c.B);

    private static double Distance2(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }
}
