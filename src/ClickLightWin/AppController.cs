using System.Diagnostics;
using ClickLightWin.Tray;
using Application = System.Windows.Application;

namespace ClickLightWin;

/// <summary>
/// Owns app lifetime and wires the pieces together. Milestone 1 wires only the
/// tray; the mouse hook and overlays land in Milestones 3-4.
/// </summary>
public sealed class AppController : IDisposable
{
    private readonly Settings _settings = Settings.Default;
    private TrayIcon? _tray;
    private bool _enabled = true;

    public void Start()
    {
        _tray = new TrayIcon();
        _tray.ToggleRequested += OnToggle;
        _tray.QuitRequested += () => Application.Current.Shutdown();
    }

    private void OnToggle()
    {
        _enabled = !_enabled;
        Debug.WriteLine($"[ClickLight] Enabled = {_enabled}");
    }

    public void Dispose()
    {
        _tray?.Dispose();
    }
}
