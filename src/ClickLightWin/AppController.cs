using ClickLightWin.Overlay;
using ClickLightWin.Tray;
using Application = System.Windows.Application;

namespace ClickLightWin;

/// <summary>
/// Owns app lifetime and wires the pieces together. Milestone 1 wires the tray;
/// Milestone 3 installs the mouse hook; Milestone 4 routes each press to a fading
/// pulse; Milestone 5 fans overlays out across every monitor via OverlayManager.
/// </summary>
public sealed class AppController : IDisposable
{
    private readonly Settings _settings = Settings.Default;
    private readonly LowLevelMouseHook _hook = new();
    private TrayIcon? _tray;
    private OverlayManager? _overlays;
    private bool _enabled = true;

    public void Start()
    {
        _tray = new TrayIcon();
        _tray.ToggleRequested += OnToggle;
        _tray.QuitRequested += () => Application.Current.Shutdown();

        // One overlay per monitor; rebuilds itself on display changes.
        _overlays = new OverlayManager(_settings);

        // Install the system-wide mouse hook on the UI thread so the callback fires
        // here and can touch the overlays without cross-thread marshaling.
        _hook.ClickDetected += OnClick;
        _hook.Install();
    }

    private void OnClick(ClickEvent click)
    {
        if (!_enabled) return;
        // v0.1: one pulse per press. Drop Up/Drag; separate release visuals are M7,
        // laser-pointer drag is M7 too. Both use the phases the hook already emits.
        if (click.Phase != ClickPhase.Down) return;
        _overlays?.Dispatch(click);
    }

    private void OnToggle()
    {
        _enabled = !_enabled;
        Console.WriteLine($"[ClickLight] Enabled = {_enabled}");
    }

    public void Dispose()
    {
        _hook.Dispose();
        _overlays?.Dispose();
        _tray?.Dispose();
    }
}
