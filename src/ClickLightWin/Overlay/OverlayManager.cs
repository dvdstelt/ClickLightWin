using Microsoft.Win32; // SystemEvents
using Screen = System.Windows.Forms.Screen;

namespace ClickLightWin.Overlay;

/// <summary>
/// Owns one <see cref="OverlayWindow"/> per monitor and routes each click to the
/// monitor whose physical bounds contain it. Rebuilds the overlays when the
/// display arrangement changes (resolution, scale, or monitors added/removed).
/// Maps to OverlayCoordinator.swift in the macOS original.
/// </summary>
public sealed class OverlayManager : IDisposable
{
    private readonly List<OverlayWindow> _overlays = new();
    private readonly Settings _settings;

    public OverlayManager(Settings settings)
    {
        _settings = settings;
        Rebuild();
        SystemEvents.DisplaySettingsChanged += OnDisplaysChanged;
    }

    /// <summary>Route a click (physical pixels) to the overlay that contains it.</summary>
    public void Dispatch(ClickEvent click) => Find(click)?.ShowPulse(click, _settings);

    // The overlay whose laser stroke is in progress. If the cursor crosses to
    // another monitor mid-stroke, the stroke is completed on the old overlay and a
    // fresh one starts on the new, so no stroke is ever left behind un-faded.
    private OverlayWindow? _laserOverlay;

    /// <summary>Route a laser event (physical pixels) to the overlay that contains it.</summary>
    public void DispatchLaser(ClickEvent click)
    {
        var target = Find(click);
        if (target is null) return;

        if (_laserOverlay is not null && !ReferenceEquals(_laserOverlay, target)
            && click.Phase is ClickPhase.Drag or ClickPhase.Up)
        {
            // Stroke crossed monitors: finish it where it started...
            _laserOverlay.Laser(click with { Phase = ClickPhase.Up }, _settings);
            // ...and continue drawing on the monitor the cursor is on now.
            if (click.Phase == ClickPhase.Drag)
                target.Laser(click with { Phase = ClickPhase.Down }, _settings);
        }

        _laserOverlay = click.Phase switch
        {
            ClickPhase.Down or ClickPhase.Drag => target,
            ClickPhase.Up => null,
            _ => _laserOverlay
        };
        target.Laser(click, _settings);
    }

    // The overlay that owns the in-progress annotation gesture, so its update and
    // commit stay on the monitor where the drag began even if the cursor crosses monitors.
    private OverlayWindow? _annotatingOverlay;
    private (int X, int Y)? _annotationStart;

    // Committed annotations in physical pixels, so they can be re-rendered onto
    // fresh overlays (at the then-current DPI) after a display-settings rebuild.
    // Gestures the renderer rejected as too small are re-filtered on replay.
    private readonly record struct CommittedAnnotation(AnnotationTool Tool, int StartX, int StartY, int EndX, int EndY);
    private readonly List<CommittedAnnotation> _committed = [];

    public void DispatchAnnotation(AnnotationEvent evt)
    {
        if (evt.Phase == AnnotationPhase.Begin)
        {
            _annotatingOverlay = FindByPoint(evt.ScreenX, evt.ScreenY);
            _annotationStart = (evt.ScreenX, evt.ScreenY);
        }
        _annotatingOverlay?.Annotate(evt, _settings);
        if (evt.Phase == AnnotationPhase.Commit)
        {
            if (_annotatingOverlay is not null && _annotationStart is { } start)
                _committed.Add(new(evt.Tool, start.X, start.Y, evt.ScreenX, evt.ScreenY));
            _annotatingOverlay = null;
            _annotationStart = null;
        }
    }

    public void ClearAnnotations()
    {
        _committed.Clear();
        foreach (var overlay in _overlays) overlay.ClearAnnotations();
    }

    private bool _drawMode;

    /// <summary>Enter or leave the frozen drawing mode on every monitor.</summary>
    public void SetDrawMode(bool on)
    {
        _drawMode = on;
        foreach (var overlay in _overlays) overlay.SetDrawMode(on);
    }

    /// <summary>Show a keyboard shortcut on the monitor that currently holds the cursor,
    /// passing the cursor position so the near-pointer placement can use it.</summary>
    public void DispatchShortcut(IReadOnlyList<string> keys)
    {
        Interop.NativeMethods.GetCursorPos(out var pt);
        var overlay = FindByPoint(pt.X, pt.Y) ?? (_overlays.Count > 0 ? _overlays[0] : null);
        overlay?.ShowShortcut(keys, _settings, pt.X, pt.Y);
    }

    private OverlayWindow? Find(ClickEvent click) => FindByPoint(click.ScreenX, click.ScreenY);

    private OverlayWindow? FindByPoint(int x, int y)
    {
        foreach (var overlay in _overlays)
            if (overlay.ScreenBounds.Contains(x, y))
                return overlay;
        return null;
    }

    private void OnDisplaysChanged(object? sender, EventArgs e)
    {
        // SystemEvents may raise off the UI thread; overlay windows are UI objects,
        // so marshal the rebuild onto the WPF dispatcher.
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
            dispatcher.Invoke(Rebuild);
        else
            Rebuild();
    }

    private void Rebuild()
    {
        // Any in-progress gesture belongs to a window that is about to close;
        // drop the owner references so events don't route to a dead overlay.
        _annotatingOverlay = null;
        _annotationStart = null;
        _laserOverlay = null;

        foreach (var o in _overlays) o.Close();
        _overlays.Clear();

        foreach (var screen in Screen.AllScreens)
        {
            var overlay = new OverlayWindow(screen);
            overlay.Show(); // shown but transparent + click-through; never activated
            if (_drawMode) overlay.SetDrawMode(true); // keep drawing mode across a rebuild
            _overlays.Add(overlay);
        }

        // Re-render committed annotations onto the fresh overlays. Each replays as a
        // begin+commit on the monitor containing its start point; annotations on a
        // monitor that is currently unplugged are skipped and reappear when it returns.
        foreach (var a in _committed)
        {
            var overlay = FindByPoint(a.StartX, a.StartY);
            overlay?.Annotate(new(a.Tool, AnnotationPhase.Begin, a.StartX, a.StartY), _settings);
            overlay?.Annotate(new(a.Tool, AnnotationPhase.Commit, a.EndX, a.EndY), _settings);
        }
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaysChanged;
        foreach (var o in _overlays) o.Close();
        _overlays.Clear();
    }
}
