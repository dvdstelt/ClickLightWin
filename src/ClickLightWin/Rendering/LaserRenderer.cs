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
/// Draws the laser-pointer cursor on one monitor: a glowing halo (concentric
/// solid discs with a small bright center) that eases along just behind the
/// cursor and fades after movement stops. Matches ClickOverlayView.swift.
/// </summary>
public sealed class LaserRenderer : IDisposable
{
    private readonly Canvas _canvas;
    private Grid _glow;
    private Color _builtOuter, _builtMid, _builtCore; // colors the current glow was built from
    private readonly DispatcherTimer _idle;

    // Cursor easing: the glow chases the latest target a fraction each frame,
    // producing the slight trailing lag the macOS laser has.
    private const double FollowFactor = 0.28;
    private Point _target;
    private Point _current;
    private bool _visible;
    private bool _rendering;

    public LaserRenderer(Canvas canvas, Settings settings)
    {
        _canvas = canvas;
        _glow = BuildGlow(settings);
        RememberColors(settings);
        _glow.Visibility = Visibility.Collapsed;
        _canvas.Children.Add(_glow);

        _idle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(settings.LaserIdleFadeMs) };
        _idle.Tick += (_, _) => FadeGlowOut();
    }

    /// <summary>Aim the halo at the cursor; a per-frame loop eases it into place.</summary>
    public void UpdateCursor(Point center, Settings settings)
    {
        // Colors can change live (settings draft); rebuild the glow if they did.
        if (settings.LaserColor != _builtOuter || settings.LaserMidColor != _builtMid
            || settings.LaserCoreColor != _builtCore)
            RebuildGlow(settings);

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

    private void RememberColors(Settings s)
    {
        _builtOuter = s.LaserColor;
        _builtMid = s.LaserMidColor;
        _builtCore = s.LaserCoreColor;
    }

    private void RebuildGlow(Settings settings)
    {
        var half = _glow.Width / 2;
        _canvas.Children.Remove(_glow);
        _glow = BuildGlow(settings);
        RememberColors(settings);
        _glow.Visibility = _visible ? Visibility.Visible : Visibility.Collapsed;
        _canvas.Children.Add(_glow);
        Canvas.SetLeft(_glow, _current.X - half);
        Canvas.SetTop(_glow, _current.Y - half);
    }

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

    /// <summary>
    /// Detach from CompositionTarget.Rendering (a static event that would otherwise
    /// keep this renderer, its canvas, and a closed overlay window alive forever)
    /// and stop the idle timer. Must be called when the owning overlay closes,
    /// e.g. on a display-settings rebuild.
    /// </summary>
    public void Dispose()
    {
        _idle.Stop();
        if (_rendering)
        {
            CompositionTarget.Rendering -= OnRendering;
            _rendering = false;
        }
    }

    private static Color WithAlpha(Color c, double a) => Color.FromArgb((byte)(a * 255), c.R, c.G, c.B);
}
