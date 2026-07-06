using System.Diagnostics;
using ClickLightWin.Overlay;
using ClickLightWin.Tray;
using Application = System.Windows.Application;
using Screen = System.Windows.Forms.Screen;

namespace ClickLightWin;

/// <summary>
/// Owns app lifetime and wires the pieces together. Milestone 1 wires the tray;
/// Milestone 2 adds a single primary-monitor overlay (replaced by OverlayManager
/// in Milestone 5). The mouse hook and pulses land in Milestones 3-4.
/// </summary>
public sealed class AppController : IDisposable
{
    private readonly Settings _settings = Settings.Default;
    private TrayIcon? _tray;
    private OverlayWindow? _overlay;
    private bool _enabled = true;

    public void Start()
    {
        _tray = new TrayIcon();
        _tray.ToggleRequested += OnToggle;
        _tray.QuitRequested += () => Application.Current.Shutdown();

        // Milestone 2: one overlay on the primary monitor to verify placement and
        // click-through. Milestone 5 replaces this with OverlayManager (one per screen).
        _overlay = new OverlayWindow(Screen.PrimaryScreen!);
        _overlay.Show();
    }

    private void OnToggle()
    {
        _enabled = !_enabled;
        Debug.WriteLine($"[ClickLight] Enabled = {_enabled}");
    }

    public void Dispose()
    {
        _overlay?.Close();
        _tray?.Dispose();
    }
}
