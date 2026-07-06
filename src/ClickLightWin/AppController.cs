using ClickLightWin.Overlay;
using ClickLightWin.Tray;
using Application = System.Windows.Application;
using Screen = System.Windows.Forms.Screen;

namespace ClickLightWin;

/// <summary>
/// Owns app lifetime and wires the pieces together. Milestone 1 wires the tray;
/// Milestone 2 adds a single primary-monitor overlay (replaced by OverlayManager
/// in Milestone 5); Milestone 3 installs the mouse hook; Milestone 4 routes each
/// press to a fading pulse on the overlay.
/// </summary>
public sealed class AppController : IDisposable
{
    private readonly Settings _settings = Settings.Default;
    private readonly LowLevelMouseHook _hook = new();
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

        // Milestone 3: install the system-wide mouse hook on the UI thread so the
        // callback fires here. For now we only log; Milestone 4 routes to the overlay.
        _hook.ClickDetected += OnClick;
        _hook.Install();
    }

    private void OnClick(ClickEvent click)
    {
        if (!_enabled) return;
        // v0.1: one pulse per press. Drop Up/Drag; separate release visuals are M7,
        // laser-pointer drag is M7 too. Both use the phases the hook already emits.
        if (click.Phase != ClickPhase.Down) return;
        _overlay?.ShowPulse(click, _settings);
    }

    private void OnToggle()
    {
        _enabled = !_enabled;
        Console.WriteLine($"[ClickLight] Enabled = {_enabled}");
    }

    public void Dispose()
    {
        _hook.Dispose();
        _overlay?.Close();
        _tray?.Dispose();
    }
}
