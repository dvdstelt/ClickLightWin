using ClickLightWin.Overlay;
using ClickLightWin.Tray;
using ClickLightWin.Views;
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
    private SettingsWindow? _settingsWindow;

    public void Start()
    {
        _settings = _settingsStore.Load();
        _hook.EmitMoves = _settings.ShowLaserPointer; // only track moves when the laser needs them
        // Start/stop move tracking whenever the laser toggles (from tray or window).
        _settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Settings.ShowLaserPointer))
                _hook.EmitMoves = _settings.ShowLaserPointer;
        };

        // The tray menu mutates the shared settings directly and refreshes its
        // checkmarks on open, so no per-item sync is needed here.
        _tray = new TrayIcon(_settings, _launchAtLogin,
            persist: () => _settingsStore.Save(_settings),
            openSettings: ShowSettings,
            quit: () => Application.Current.Shutdown());

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

    // When each button went down, to measure hold time for the release ring.
    private readonly Dictionary<ClickButton, long> _pressTicks = new();

    private void OnClick(ClickEvent click)
    {
        if (!_settings.Enabled) return;
        switch (click.Phase)
        {
            case ClickPhase.Down:
                _pressTicks[click.Button] = Environment.TickCount64;
                _overlays?.Dispatch(click); // expanding press ring
                if (_settings.ShowLaserPointer) _overlays?.DispatchLaser(click); // start a stroke
                break;

            case ClickPhase.Move:
                if (_settings.ShowLaserPointer) _overlays?.DispatchLaser(click); // cursor halo
                break;

            case ClickPhase.Drag:
                // In laser mode the drag becomes a freehand stroke instead of the dot trail.
                if (_settings.ShowLaserPointer) _overlays?.DispatchLaser(click);
                else if (_settings.ShowDrag) _overlays?.Dispatch(click);
                break;

            case ClickPhase.Up:
                // Only show the contracting release ring if the button was held past
                // the threshold, so quick clicks stay clean.
                var held = _pressTicks.TryGetValue(click.Button, out var down)
                           && Environment.TickCount64 - down >= _settings.ReleaseMinHoldMs;
                _pressTicks.Remove(click.Button);
                if (_settings.ShowLaserPointer) _overlays?.DispatchLaser(click); // finish the stroke
                if (_settings.ShowRelease && held) _overlays?.Dispatch(click);
                break;
        }
    }

    // The global hotkey path. The tray menu toggles Enabled on its own; both just
    // mutate the shared settings, and the tray refreshes its checkmarks on open.
    private void ToggleEnabled()
    {
        _settings.Enabled = !_settings.Enabled;
        _settingsStore.Save(_settings);
    }

    private void ShowSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settings);
        _settingsWindow.Closed += (_, _) =>
        {
            _settingsWindow = null;
            _settingsStore.Save(_settings); // persist any edits made in the window
        };
        _settingsWindow.Show();
        _settingsWindow.Activate();
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
