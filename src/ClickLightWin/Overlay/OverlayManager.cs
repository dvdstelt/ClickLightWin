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

    private OverlayWindow? Find(ClickEvent click)
    {
        foreach (var overlay in _overlays)
            if (overlay.ScreenBounds.Contains(click.ScreenX, click.ScreenY))
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
