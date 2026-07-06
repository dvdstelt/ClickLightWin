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

    /// <summary>Route a laser event (physical pixels) to the overlay that contains it.</summary>
    public void DispatchLaser(ClickEvent click) => Find(click)?.Laser(click, _settings);

    // The overlay that owns the in-progress annotation gesture, so its update and
    // commit stay on the monitor where the drag began even if the cursor crosses monitors.
    private OverlayWindow? _annotatingOverlay;

    public void DispatchAnnotation(AnnotationEvent evt)
    {
        if (evt.Phase == AnnotationPhase.Begin)
            _annotatingOverlay = FindByPoint(evt.ScreenX, evt.ScreenY);
        _annotatingOverlay?.Annotate(evt, _settings);
        if (evt.Phase == AnnotationPhase.Commit)
            _annotatingOverlay = null;
    }

    public void ClearAnnotations()
    {
        foreach (var overlay in _overlays) overlay.ClearAnnotations();
    }

    /// <summary>Show a keyboard shortcut on the monitor that currently holds the cursor.</summary>
    public void DispatchShortcut(IReadOnlyList<string> keys)
    {
        var overlay = CursorOverlay() ?? (_overlays.Count > 0 ? _overlays[0] : null);
        overlay?.ShowShortcut(keys, _settings);
    }

    private OverlayWindow? CursorOverlay()
    {
        if (!Interop.NativeMethods.GetCursorPos(out var pt)) return null;
        return FindByPoint(pt.X, pt.Y);
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
        foreach (var o in _overlays) o.Close();
        _overlays.Clear();

        foreach (var screen in Screen.AllScreens)
        {
            var overlay = new OverlayWindow(screen);
            overlay.Show(); // shown but transparent + click-through; never activated
            _overlays.Add(overlay);
        }
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaysChanged;
        foreach (var o in _overlays) o.Close();
        _overlays.Clear();
    }
}
