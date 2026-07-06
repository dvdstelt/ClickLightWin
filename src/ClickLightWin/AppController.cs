using ClickLightWin.Overlay;
using ClickLightWin.Tray;
using Application = System.Windows.Application;

namespace ClickLightWin;

/// <summary>
/// Owns app lifetime and wires the pieces together. Milestone 1 wires the tray;
/// Milestone 3 installs the mouse hook; Milestone 4 routes each press to a fading
/// pulse; Milestone 5 fans overlays out across every monitor via OverlayManager.
/// All settings live in a single observable <see cref="Settings"/> that the
/// overlays read live and the store persists.
/// </summary>
public sealed class AppController : IDisposable
{
    private readonly LowLevelMouseHook _hook = new();
    private readonly HotKeyManager _hotKeys = new();
    private readonly SettingsStore _settingsStore = new();
    private readonly LaunchAtLoginController _launchAtLogin = new();
    private Settings _settings = new();
    private TrayIcon? _tray;
    private OverlayManager? _overlays;

    public void Start()
    {
        _settings = _settingsStore.Load();

        _tray = new TrayIcon(_settings.Enabled, _launchAtLogin.IsEnabled);
        _tray.ToggleRequested += ToggleEnabled;
        _tray.LaunchAtLoginRequested += ToggleLaunchAtLogin;
        _tray.QuitRequested += () => Application.Current.Shutdown();

        // One overlay per monitor; rebuilds itself on display changes.
        _overlays = new OverlayManager(_settings);

        // Global toggle hotkey (Ctrl+Alt+L). Fires on the UI thread.
        _hotKeys.TogglePressed += ToggleEnabled;
        _hotKeys.Register();

        // Install the system-wide mouse hook on the UI thread so the callback fires
        // here and can touch the overlays without cross-thread marshaling.
        _hook.ClickDetected += OnClick;
        _hook.Install();
    }

    private void OnClick(ClickEvent click)
    {
        if (!_settings.Enabled) return;
        // Press draws a ring; drag draws a fading trail (when enabled). Release has
        // no visual yet (separate press/release visuals are a later M7 item).
        if (click.Phase == ClickPhase.Up) return;
        if (click.Phase == ClickPhase.Drag && !_settings.ShowDrag) return;
        _overlays?.Dispatch(click);
    }

    // Single toggle path for both the tray menu and the global hotkey, so the
    // enabled state, persisted setting, and menu checkmark never diverge.
    private void ToggleEnabled()
    {
        _settings.Enabled = !_settings.Enabled;
        _settingsStore.Save(_settings);
        _tray?.SetEnabled(_settings.Enabled);
    }

    private void ToggleLaunchAtLogin()
    {
        var enabled = !_launchAtLogin.IsEnabled;
        _launchAtLogin.SetEnabled(enabled);
        _tray?.SetLaunchAtLogin(_launchAtLogin.IsEnabled);
    }

    public void Dispose()
    {
        _settingsStore.Save(_settings);
        _hook.Dispose();
        _hotKeys.Dispose();
        _overlays?.Dispose();
        _tray?.Dispose();
    }
}
