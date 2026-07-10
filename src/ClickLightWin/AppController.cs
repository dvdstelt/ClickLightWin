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
    private readonly LowLevelKeyboardHook _keyboardHook = new();
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
        _hook.EmitMoves = MovesActive; // only track the move stream when the laser needs it
        _hook.AnnotationsEnabled = AnnotationsActive;
        // React to settings changes: move tracking follows the laser (and Enabled);
        // the annotation gesture is only armed while the tool is on.
        _settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(Settings.Enabled) or nameof(Settings.ShowLaserPointer))
                _hook.EmitMoves = MovesActive;
            if (e.PropertyName is nameof(Settings.Enabled) or nameof(Settings.EnableAnnotations))
                _hook.AnnotationsEnabled = AnnotationsActive;
            if (e.PropertyName is nameof(Settings.Enabled) or nameof(Settings.ShowShortcuts))
                UpdateKeyboardHook();
        };
        _keyboardHook.ShortcutDetected += keys => _overlays?.DispatchShortcut(keys);

        // The tray menu mutates the shared settings directly and refreshes its
        // checkmarks on open, so no per-item sync is needed here.
        _tray = new TrayIcon(_settings, _launchAtLogin,
            persist: () => _settingsStore.Save(_settings),
            openSettings: ShowSettings,
            clearAnnotations: ClearAnnotations,
            quit: () => Application.Current.Shutdown());

        // One overlay per monitor; rebuilds itself on display changes.
        _overlays = new OverlayManager(_settings);

        // Global hotkeys (Ctrl+Shift+L toggle, Ctrl+Shift+C clear). Fire on the UI thread.
        _hotKeys.TogglePressed += ToggleEnabled;
        _hotKeys.ClearPressed += ClearAnnotations;
        _hotKeys.Register();

        // Install the system-wide mouse hook on the UI thread so the callback fires
        // here and can touch the overlays without cross-thread marshaling.
        _hook.ClickDetected += OnClick;
        _hook.AnnotationDetected += OnAnnotation;
        _hook.Install();

        UpdateKeyboardHook(); // installs the keyboard hook only if the shortcut display is on
    }

    // Privacy: the keyboard hook only runs while the shortcut display is enabled
    // (and the tool is on). It is uninstalled entirely otherwise.
    private void UpdateKeyboardHook()
    {
        var active = _settings.Enabled && _settings.ShowShortcuts;
        if (active && !_keyboardHook.IsInstalled) _keyboardHook.Install();
        else if (!active && _keyboardHook.IsInstalled) _keyboardHook.Uninstall();
    }

    // Ctrl+Shift annotation gestures are armed only while the tool is enabled.
    private bool AnnotationsActive => _settings.Enabled && _settings.EnableAnnotations;

    // The high-frequency move stream is only worth processing while the laser can draw.
    private bool MovesActive => _settings.Enabled && _settings.ShowLaserPointer;

    // Left-drag draws an arrow, right-drag a box; the hook tags each with its tool.
    private void OnAnnotation(AnnotationEvent evt) => _overlays?.DispatchAnnotation(evt);

    public void ClearAnnotations() => _overlays?.ClearAnnotations();

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
        _keyboardHook.Dispose();
        _hotKeys.Dispose();
        _overlays?.Dispose();
        _tray?.Dispose();
    }
}
