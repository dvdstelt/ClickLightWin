using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace ClickLightWin.Rendering;

/// <summary>
/// Spawns and animates click pulses on an overlay canvas. Each pulse is an
/// expanding, fading ring, colored per button. Mirrors ClickOverlayView.swift
/// (simplified for v0.1: a single grow-and-fade ring rather than the reference's
/// per-button glow/ring/dot/crosshair/diamond composition).
/// </summary>
public sealed class PulseRenderer(Canvas canvas)
{
    public void Spawn(Point center, ClickEvent click, Settings settings)
    {
        var color = settings.ColorFor(click.Button);
        var baseDiameter = settings.BaseDiameterDips;   // e.g. 28
        var maxScale = settings.MaxScale;               // e.g. 2.2
        var duration = settings.PulseDuration;          // e.g. 450ms

        var ring = new Ellipse
        {
            Width = baseDiameter,
            Height = baseDiameter,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = settings.StrokeThickness, // e.g. 3
            Fill = new SolidColorBrush(Color.FromArgb(60, color.R, color.G, color.B)),
            IsHitTestVisible = false,
            RenderTransformOrigin = new Point(0.5, 0.5)
        };

        // Position so the ellipse is centered on the click point.
        Canvas.SetLeft(ring, center.X - baseDiameter / 2);
        Canvas.SetTop(ring, center.Y - baseDiameter / 2);

        var scale = new ScaleTransform(1, 1);
        ring.RenderTransform = scale;
        canvas.Children.Add(ring);

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var grow = new DoubleAnimation(1, maxScale, duration) { EasingFunction = ease };
        var fade = new DoubleAnimation(1, 0, duration) { EasingFunction = ease };
        fade.Completed += (_, _) => canvas.Children.Remove(ring);

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        ring.BeginAnimation(UIElement.OpacityProperty, fade);
    }
}
