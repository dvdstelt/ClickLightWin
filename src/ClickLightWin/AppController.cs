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
    private readonly HotKeyManager _hotKeys = new();
    private readonly PreferencesStore _preferencesStore = new();
    private Preferences _preferences = new();
    private TrayIcon? _tray;
    private OverlayManager? _overlays;
    private bool _enabled = true;

    public void Start()
    {
        _preferences = _preferencesStore.Load();
        _enabled = _preferences.Enabled;

        _tray = new TrayIcon(_enabled);
        _tray.ToggleRequested += ToggleEnabled;
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
        if (!_enabled) return;
        // v0.1: one pulse per press. Drop Up/Drag; separate release visuals are M7,
        // laser-pointer drag is M7 too. Both use the phases the hook already emits.
        if (click.Phase != ClickPhase.Down) return;
        _overlays?.Dispatch(click);
    }

    // Single toggle path for both the tray menu and the global hotkey, so the
    // enabled state, persisted preference, and menu checkmark never diverge.
    private void ToggleEnabled()
    {
        _enabled = !_enabled;
        _preferences.Enabled = _enabled;
        _preferencesStore.Save(_preferences);
        _tray?.SetEnabled(_enabled);
    }

    public void Dispose()
    {
        _hook.Dispose();
        _hotKeys.Dispose();
        _overlays?.Dispose();
        _tray?.Dispose();
    }
}
