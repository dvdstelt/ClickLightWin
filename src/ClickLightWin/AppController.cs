using System.Windows.Threading;
using ClickLightWin.Overlay;
using ClickLightWin.Tray;
using ClickLightWin.Update;
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
    private readonly ProfileStore _profileStore = new();
    private readonly ActivityStore _activity = new();
    private readonly ZoomController _zoom = new();
    private readonly UpdateService _updates = new();
    private Settings _settings = new();
    private TrayIcon? _tray;
    private OverlayManager? _overlays;
    private SettingsWindow? _settingsWindow;
    private DispatcherTimer? _updateTimer;
    private DispatcherTimer? _activitySaveTimer;
    private ClickButton? _dragCounted; // which button's current drag gesture already counted

    public void Start()
    {
        _settings = _settingsStore.Load();
        _hook.EmitMoves = LaserActive; // only track the move stream when the laser glow needs it
        _hook.AnnotationsEnabled = AnnotationsActive;
        // React to settings changes: the glow move stream follows the laser (and
        // Enabled); annotations follow their own toggle.
        _settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(Settings.Enabled) or nameof(Settings.ShowLaserPointer))
                _hook.EmitMoves = LaserActive;
            if (e.PropertyName is nameof(Settings.Enabled) or nameof(Settings.EnableAnnotations))
                _hook.AnnotationsEnabled = AnnotationsActive;
            if (e.PropertyName is nameof(Settings.Enabled) or nameof(Settings.ShowShortcuts))
                UpdateKeyboardHook();
        };
        _keyboardHook.ShortcutDetected += keys => { if (!OverlaysSuspended) _overlays?.DispatchShortcut(keys); };

        // The tray menu mutates the shared settings directly and refreshes its
        // checkmarks on open, so no per-item sync is needed here.
        _tray = new TrayIcon(_settings, _launchAtLogin,
            persist: () => _settingsStore.Save(_settings),
            openSettings: ShowSettings,
            clearAnnotations: ClearAnnotations,
            applyUpdate: () => _ = ApplyUpdateAsync(),
            quit: () => Application.Current.Shutdown());

        // One overlay per monitor; rebuilds itself on display changes.
        _overlays = new OverlayManager(_settings);

        // Global hotkeys (defaults Ctrl+Shift+L/C/D; user-configurable in settings).
        _hotKeys.TogglePressed += CycleMode;
        _hotKeys.ClearPressed += ClearAnnotations;
        _hotKeys.DrawModePressed += ToggleDrawMode;
        _hotKeys.ShortcutsPressed += ToggleShortcuts;
        _hotKeys.ZoomPressed += () => _zoom.Toggle();
        _hotKeys.Start();
        ConfigureHotkeys();

        // Install the system-wide mouse hook on the UI thread so the callback fires
        // here and can touch the overlays without cross-thread marshaling.
        _hook.ClickDetected += OnClick;
        _hook.AnnotationDetected += OnAnnotation;
        _hook.Install();

        UpdateKeyboardHook(); // installs the keyboard hook only if the shortcut display is on

        // Persist the click tallies periodically (they are cheap in-memory otherwise).
        _activitySaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _activitySaveTimer.Tick += (_, _) => _activity.Save();
        _activitySaveTimer.Start();

        InitializeUpdates();
    }

    // Auto-check for a newer release on startup and every few hours. Only the
    // installed (Setup.exe) build is Velopack-managed, so this quietly no-ops for
    // the portable exe and `dotnet run`. Updates are never applied silently: a
    // found update only reveals the "Restart to update" tray prompt.
    private void InitializeUpdates()
    {
        if (!_updates.IsSupported) return;
        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(6) };
        _updateTimer.Tick += (_, _) => _ = CheckForUpdatesAsync();
        _updateTimer.Start();
        _ = CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var version = await _updates.CheckAsync();
            if (version is not null) _tray?.ShowUpdateAvailable(version);
        }
        catch
        {
            // A failed check (offline, GitHub hiccup) is not worth bothering the
            // user about; the next scheduled check will try again.
        }
    }

    private async Task ApplyUpdateAsync()
    {
        try
        {
            _tray?.ShowInfo("ClickLight", "Downloading the update. The app will restart when it is ready.");
            await _updates.DownloadAndRestartAsync(); // does not return on success
        }
        catch
        {
            _tray?.ShowWarning("Update failed", "The update could not be downloaded. Please try again later.");
        }
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

    // The laser features (glow move stream, Ctrl+drag stroke) run only while the laser is on.
    private bool LaserActive => _settings.Enabled && _settings.ShowLaserPointer;

    // While the settings window is open the user edits a draft and previews in its pad,
    // so the live overlay is paused: the real screen stays quiet (no laser following the
    // cursor over the window, no double pulse when clicking in the pad) until OK/Cancel.
    private bool OverlaysSuspended => _settingsWindow is not null;

    private void ConfigureHotkeys()
    {
        _hotKeys.Configure(_settings.ToggleHotKey, _settings.ClearHotKey, _settings.DrawModeHotKey,
            _settings.ShortcutsHotKey, _settings.ZoomHotKey);
        WarnAboutUnavailableHotkeys();
    }

    // If a binding is invalid or another app owns it, RegisterHotKey fails silently;
    // tell the user once so a dead shortcut is not mistaken for a ClickLight bug.
    private void WarnAboutUnavailableHotkeys()
    {
        var taken = new List<string>();
        if (!_hotKeys.ToggleRegistered) taken.Add($"{_settings.ToggleHotKey.Display} (toggle)");
        if (!_hotKeys.ClearRegistered) taken.Add($"{_settings.ClearHotKey.Display} (clear annotations)");
        if (!_hotKeys.DrawModeRegistered) taken.Add($"{_settings.DrawModeHotKey.Display} (drawing mode)");
        if (!_hotKeys.ShortcutsRegistered) taken.Add($"{_settings.ShortcutsHotKey.Display} (keyboard shortcuts)");
        if (!_hotKeys.ZoomRegistered) taken.Add($"{_settings.ZoomHotKey.Display} (zoom)");
        if (taken.Count == 0) return;
        _tray?.ShowWarning("ClickLight shortcut unavailable",
            $"{string.Join(" and ", taken)} could not be registered (already in use or invalid).");
    }

    // Left-drag draws an arrow, right-drag a box; the hook tags each with its tool.
    private void OnAnnotation(AnnotationEvent evt)
    {
        if (OverlaysSuspended) return;
        _overlays?.DispatchAnnotation(evt);
    }

    public void ClearAnnotations() => _overlays?.ClearAnnotations();

    // Ctrl+Shift+D: a frozen drawing mode. The overlay stops being click-through so
    // it captures the mouse for freehand strokes; the screen is "frozen" for apps
    // until toggled off. Resets to off each launch (runtime state, not persisted).
    private bool _drawMode;

    private void ToggleDrawMode()
    {
        _drawMode = !_drawMode;
        _overlays?.SetDrawMode(_drawMode);
    }

    // When each button went down, to measure hold time for the release ring.
    private readonly Dictionary<ClickButton, long> _pressTicks = new();

    private void OnClick(ClickEvent click)
    {
        RecordActivity(click); // count real clicks regardless of Enabled / settings being open
        if (!_settings.Enabled || OverlaysSuspended) return;
        switch (click.Phase)
        {
            case ClickPhase.Down:
                _pressTicks[click.Button] = Environment.TickCount64;
                _overlays?.Dispatch(click); // expanding press ring
                break;

            case ClickPhase.Move:
                if (_settings.ShowLaserPointer) _overlays?.DispatchLaser(click); // cursor halo
                break;

            case ClickPhase.Drag:
                // A plain drag shows the dot trail. The laser stroke is the explicit
                // Ctrl+drag gesture, handled (and swallowed) via the annotation path.
                if (_settings.ShowDrag) _overlays?.Dispatch(click);
                break;

            case ClickPhase.Up:
                // Only show the contracting release ring if the button was held past
                // the threshold, so quick clicks stay clean.
                var held = _pressTicks.TryGetValue(click.Button, out var down)
                           && Environment.TickCount64 - down >= _settings.ReleaseMinHoldMs;
                _pressTicks.Remove(click.Button);
                if (_settings.ShowRelease && held) _overlays?.Dispatch(click);
                break;
        }
    }

    // Tally clicks for the Activity view: each press counts for its button, and each
    // drag gesture counts once (not once per move event).
    private void RecordActivity(ClickEvent click)
    {
        switch (click.Phase)
        {
            case ClickPhase.Down:
                _dragCounted = null;
                _activity.RecordClick(click.Button);
                break;
            case ClickPhase.Drag when _dragCounted != click.Button:
                _dragCounted = click.Button;
                _activity.RecordDrag();
                break;
            case ClickPhase.Up:
                _dragCounted = null;
                break;
        }
    }

    // The toggle hotkey cycles through three modes: Off -> ClickLight (pulses only)
    // -> Laser Pointer (pulses + laser glow) -> Off. The tray still toggles Enabled and
    // the laser independently; the tray refreshes its checkmarks on open.
    private void CycleMode()
    {
        if (!_settings.Enabled)
        {
            _settings.ShowLaserPointer = false; // Off -> ClickLight
            _settings.Enabled = true;
        }
        else if (!_settings.ShowLaserPointer)
        {
            _settings.ShowLaserPointer = true;  // ClickLight -> Laser Pointer
        }
        else
        {
            _settings.Enabled = false;          // Laser Pointer -> Off
        }
        _settingsStore.Save(_settings);
        AnnounceMode();
    }

    // Show a transient badge naming the mode we just switched to, color-coded so it
    // stands apart from the live keyboard-shortcut pills.
    private void AnnounceMode()
    {
        var (text, color) = !_settings.Enabled
            ? ("Off", System.Windows.Media.Color.FromRgb(0x9A, 0x9A, 0xA2))
            : !_settings.ShowLaserPointer
                ? ("ClickLight", System.Windows.Media.Color.FromRgb(0x3B, 0x82, 0xF6))
                : ("Laser Pointer", System.Windows.Media.Color.FromRgb(0xFF, 0x29, 0x05));
        _overlays?.ShowMode(text, color);
    }

    // Toggle the live keyboard-shortcut display. Since it is otherwise invisible until
    // the next shortcut is pressed, announce the new state with a toast.
    private void ToggleShortcuts()
    {
        _settings.ShowShortcuts = !_settings.ShowShortcuts;
        _settingsStore.Save(_settings);
        var (text, color) = _settings.ShowShortcuts
            ? ("Keyboard Shortcuts On", System.Windows.Media.Color.FromRgb(0x3B, 0x82, 0xF6))
            : ("Keyboard Shortcuts Off", System.Windows.Media.Color.FromRgb(0x9A, 0x9A, 0xA2));
        _overlays?.ShowMode(text, color);
    }

    private void ShowSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        // Suspend the global hotkeys while configuring, so pressing a combo in the
        // shortcut recorder neither fires an action nor collides with the OS registration.
        _hotKeys.Suspend();

        // Edit a detached draft; the live settings (and overlays) are untouched until
        // the user commits with OK. Cancel just discards the draft.
        var draft = _settings.Clone();
        var window = _settingsWindow = new SettingsWindow(draft, _profileStore, _activity);
        _settingsWindow.Closed += (_, _) =>
        {
            _settingsWindow = null;
            if (window.Committed)
            {
                _settings.CopyFrom(draft);       // apply the draft to the overlays
                _settingsStore.Save(_settings);  // and persist it
            }
            ConfigureHotkeys(); // re-register from the (possibly updated) live settings and re-enable
        };
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    public void Dispose()
    {
        _settingsStore.Save(_settings);
        _updateTimer?.Stop();
        _activitySaveTimer?.Stop();
        _activity.Save();
        _hook.Dispose();
        _keyboardHook.Dispose();
        _hotKeys.Dispose();
        _overlays?.Dispose();
        _zoom.Dispose();
        _tray?.Dispose();
    }
}
