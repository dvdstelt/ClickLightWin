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
    // Last drag-dot position on this overlay, to throttle the trail by distance.
    private Point? _lastDragPoint;

    public void Spawn(Point center, ClickEvent click, Settings settings)
    {
        switch (click.Phase)
        {
            case ClickPhase.Drag:
                SpawnDragDot(center, settings);
                return;
            case ClickPhase.Up:
                SpawnReleaseRing(center, click, settings);
                return;
            default:
                // A fresh press ends any previous drag trail.
                _lastDragPoint = null;
                SpawnPressRing(center, click, settings);
                return;
        }
    }

    private void SpawnReleaseRing(Point center, ClickEvent click, Settings settings)
    {
        var color = settings.ColorFor(click.Button);
        var baseDiameter = settings.BaseDiameterDips;
        var duration = settings.ReleaseDuration;

        var ring = new Ellipse
        {
            Width = baseDiameter,
            Height = baseDiameter,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = settings.StrokeThickness * 0.7,
            IsHitTestVisible = false,
            RenderTransformOrigin = new Point(0.5, 0.5)
        };
        Canvas.SetLeft(ring, center.X - baseDiameter / 2);
        Canvas.SetTop(ring, center.Y - baseDiameter / 2);

        var scale = new ScaleTransform(settings.ReleaseStartScale, settings.ReleaseStartScale);
        ring.RenderTransform = scale;
        canvas.Children.Add(ring);

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        // Contract inward (wide -> narrow) while fading out.
        var shrink = new DoubleAnimation(settings.ReleaseStartScale, settings.ReleaseEndScale, duration) { EasingFunction = ease };
        var fade = new DoubleAnimation(0.9, 0, duration) { EasingFunction = ease };
        fade.Completed += (_, _) => canvas.Children.Remove(ring);

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);
        ring.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    private void SpawnDragDot(Point center, Settings settings)
    {
        // Throttle: skip dots too close to the previous one so slow drags do not
        // pile up hundreds of overlapping ellipses.
        if (_lastDragPoint is { } last)
        {
            var dx = center.X - last.X;
            var dy = center.Y - last.Y;
            var minSq = settings.DragMinSpacingDips * settings.DragMinSpacingDips;
            if (dx * dx + dy * dy < minSq) return;
        }
        _lastDragPoint = center;

        var d = settings.DragDotDiameter;
        var dot = new Ellipse
        {
            Width = d,
            Height = d,
            Fill = new SolidColorBrush(settings.DragColor),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(dot, center.X - d / 2);
        Canvas.SetTop(dot, center.Y - d / 2);
        canvas.Children.Add(dot);

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var fade = new DoubleAnimation(0.85, 0, settings.DragDuration) { EasingFunction = ease };
        fade.Completed += (_, _) => canvas.Children.Remove(dot);
        dot.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    private void SpawnPressRing(Point center, ClickEvent click, Settings settings)
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
