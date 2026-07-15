using System.Drawing;
using System.Windows.Forms; // NotifyIcon, ContextMenuStrip
using ClickLightWin.Interop;

namespace ClickLightWin.Tray;

/// <summary>
/// System-tray presence and its context menu. A dark, richer menu (feature
/// toggles, Size/Duration preset submenus, launch-at-login, Settings, About,
/// Quit) driven by the shared <see cref="Settings"/> model. Checkmarks refresh
/// each time the menu opens, so it stays in sync with the settings window and
/// the global hotkey. Maps to StatusMenuConfiguration.swift.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly Icon _iconImage;
    private readonly ContextMenuStrip _menu;
    private readonly Font _menuFont;
    private readonly Font _updateFont;
    private readonly Settings _settings;
    private readonly LaunchAtLoginController _launchAtLogin;
    private readonly Action _persist;

    private readonly ToolStripMenuItem _updateItem;
    private readonly ToolStripSeparator _updateSeparator;
    private readonly ToolStripMenuItem _enabledItem;
    private readonly ToolStripMenuItem _laserItem;
    private readonly ToolStripMenuItem _releaseItem;
    private readonly ToolStripMenuItem _dragItem;
    private readonly ToolStripMenuItem _shortcutsItem;
    private readonly ToolStripMenuItem _launchItem;
    private readonly List<(ToolStripMenuItem Item, double Value)> _sizeItems = [];
    private readonly List<(ToolStripMenuItem Item, double Value)> _durationItems = [];

    // Every configurable item, keyed by id. The visible menu is (re)built from
    // Settings.MenuLayout each time it opens, so ordering and hiding take effect live.
    private readonly Dictionary<TrayMenuItem, ToolStripItem> _items;
    private bool _updateAvailable;

    public TrayIcon(Settings settings, LaunchAtLoginController launchAtLogin,
                    Action persist, Action openSettings, Action clearAnnotations,
                    Action applyUpdate, Action quit)
    {
        _settings = settings;
        _launchAtLogin = launchAtLogin;
        _persist = persist;

        _menuFont = new Font("Segoe UI", 9f);
        var menu = _menu = new ContextMenuStrip
        {
            Renderer = new DarkMenuRenderer(),
            ForeColor = Color.White,
            Font = _menuFont
        };
        menu.Opening += (_, _) => { RefreshChecks(); RebuildMenu(); };

        // Update prompt, pinned above the configured items when an update is found.
        _updateFont = new Font(_menuFont, FontStyle.Bold);
        _updateItem = Item("Restart to update", applyUpdate);
        _updateItem.Font = _updateFont;
        _updateSeparator = new ToolStripSeparator();

        _enabledItem = Check("Enabled", () => Toggle(s => s.Enabled = !s.Enabled));
        _laserItem = Check("Laser pointer mode", () => Toggle(s => s.ShowLaserPointer = !s.ShowLaserPointer));
        _releaseItem = Check("Show release ring", () => Toggle(s => s.ShowRelease = !s.ShowRelease));
        _dragItem = Check("Show drag trail", () => Toggle(s => s.ShowDrag = !s.ShowDrag));
        _shortcutsItem = Check("Show keyboard shortcuts", () => Toggle(s => s.ShowShortcuts = !s.ShowShortcuts));
        _launchItem = Check("Launch at login", () => _launchAtLogin.SetEnabled(!_launchAtLogin.IsEnabled));

        _items = new Dictionary<TrayMenuItem, ToolStripItem>
        {
            [TrayMenuItem.Enabled] = _enabledItem,
            [TrayMenuItem.LaserPointer] = _laserItem,
            [TrayMenuItem.ReleaseRing] = _releaseItem,
            [TrayMenuItem.DragTrail] = _dragItem,
            [TrayMenuItem.KeyboardShortcuts] = _shortcutsItem,
            [TrayMenuItem.Size] = PresetSubmenu("Size", Presets.Sizes, _sizeItems, v => _settings.BaseDiameterDips = v),
            [TrayMenuItem.Duration] = PresetSubmenu("Duration", Presets.Durations, _durationItems, v => _settings.PulseDurationMs = v),
            [TrayMenuItem.ClearAnnotations] = Item("Clear annotations", clearAnnotations),
            [TrayMenuItem.LaunchAtLogin] = _launchItem,
            [TrayMenuItem.Settings] = Item("Settings...", openSettings),
            [TrayMenuItem.About] = Item("About ClickLight", ShowAbout),
            [TrayMenuItem.Quit] = Item("Quit", quit),
        };

        RebuildMenu();

        _iconImage = AppIconFactory.CreatePulseIcon();
        _icon = new NotifyIcon
        {
            Icon = _iconImage,
            Text = "ClickLight",
            Visible = true
        };

        // Show the menu ourselves, opening upward from the cursor. WinForms' automatic
        // placement doesn't reliably flip a tall tray menu above a bottom taskbar, so
        // lower items (Quit) can fall behind it. ToolStripDropDown still clamps to the
        // screen, so this stays correct for other taskbar positions too.
        _icon.MouseUp += (_, e) =>
        {
            if (e.Button != MouseButtons.Right) return;
            // Give our process the foreground first; without it a hand-shown tray
            // menu won't dismiss when you click elsewhere (you'd be stuck picking an
            // item to close it). See NativeMethods.SetForegroundWindow.
            NativeMethods.SetForegroundWindow(menu.Handle);
            menu.Show(Cursor.Position, ToolStripDropDownDirection.AboveLeft);
        };
    }

    private ToolStripMenuItem Check(string text, Action onClick)
    {
        var item = new ToolStripMenuItem(text) { ForeColor = Color.White };
        item.Click += (_, _) => onClick();
        return item;
    }

    private static ToolStripMenuItem Item(string text, Action onClick)
    {
        var item = new ToolStripMenuItem(text) { ForeColor = Color.White };
        item.Click += (_, _) => onClick();
        return item;
    }

    private ToolStripMenuItem PresetSubmenu(
        string title, NumericPreset[] presets,
        List<(ToolStripMenuItem, double)> track, Action<double> apply)
    {
        var parent = new ToolStripMenuItem(title) { ForeColor = Color.White };
        foreach (var preset in presets)
        {
            var value = preset.Value;
            var item = new ToolStripMenuItem(preset.Title) { ForeColor = Color.White };
            item.Click += (_, _) => { apply(value); _persist(); };
            parent.DropDownItems.Add(item);
            track.Add((item, value));
        }
        parent.DropDown.Renderer = new DarkMenuRenderer();
        parent.DropDown.BackColor = Color.FromArgb(0x25, 0x25, 0x26);
        parent.DropDown.ForeColor = Color.White;
        return parent;
    }

    /// <summary>Show a transient warning balloon from the tray icon.</summary>
    public void ShowWarning(string title, string text) =>
        _icon.ShowBalloonTip(5000, title, text, ToolTipIcon.Warning);

    /// <summary>Show a transient informational balloon from the tray icon.</summary>
    public void ShowInfo(string title, string text) =>
        _icon.ShowBalloonTip(5000, title, text, ToolTipIcon.Info);

    /// <summary>
    /// Reveal the "Restart to update" prompt (top of the menu) and nudge the user
    /// with a balloon. Clicking the item runs the applyUpdate callback.
    /// </summary>
    public void ShowUpdateAvailable(string version)
    {
        _updateItem.Text = $"Restart to update to v{version}";
        _updateAvailable = true; // the next menu open rebuilds with the prompt on top
        ShowInfo("ClickLight update available",
            $"Version {version} is ready. Right-click the tray icon and choose \"Restart to update\".");
    }

    // Rebuild the menu from the configured layout: the update prompt (if any), then the
    // visible items in their saved order. Quit is always present and never hidden.
    private void RebuildMenu()
    {
        _menu.Items.Clear();
        if (_updateAvailable)
        {
            _menu.Items.Add(_updateItem);
            _menu.Items.Add(_updateSeparator);
        }
        foreach (var entry in _settings.MenuLayout)
        {
            if (!entry.Visible && entry.Item != TrayMenuItem.Quit) continue;
            if (_items.TryGetValue(entry.Item, out var item)) _menu.Items.Add(item);
        }
        if (!_menu.Items.Contains(_items[TrayMenuItem.Quit]))
            _menu.Items.Add(_items[TrayMenuItem.Quit]); // safety net; Quit is never hidden
    }

    private void Toggle(Action<Settings> mutate)
    {
        mutate(_settings);
        _persist();
    }

    private void RefreshChecks()
    {
        _enabledItem.Checked = _settings.Enabled;
        _laserItem.Checked = _settings.ShowLaserPointer;
        _releaseItem.Checked = _settings.ShowRelease;
        _dragItem.Checked = _settings.ShowDrag;
        _shortcutsItem.Checked = _settings.ShowShortcuts;
        _launchItem.Checked = _launchAtLogin.IsEnabled;
        foreach (var (item, value) in _sizeItems) item.Checked = Near(_settings.BaseDiameterDips, value);
        foreach (var (item, value) in _durationItems) item.Checked = Near(_settings.PulseDurationMs, value);
    }

    private static bool Near(double a, double b) => Math.Abs(a - b) < 0.5;

    private static void ShowAbout() => MessageBox.Show(
        "ClickLight for Windows\n\nHighlights your mouse clicks on screen during demos and "
        + "screen sharing.\n\nCtrl+Shift+L  toggle\nCtrl+Shift+D  frozen drawing mode"
        + "\nCtrl+Shift + left-drag  draw an arrow\nCtrl+Shift + right-drag  draw a box"
        + "\nCtrl+Shift+C  clear annotations",
        "About ClickLight", MessageBoxButtons.OK, MessageBoxIcon.Information);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _iconImage.Dispose();
        // Items not currently in the menu (hidden ones, the update prompt) are not owned
        // by it, so dispose every item we created explicitly.
        foreach (var item in _items.Values) item.Dispose();
        _updateItem.Dispose();
        _updateSeparator.Dispose();
        _menu.Dispose(); // NotifyIcon does not own its ContextMenuStrip
        _menuFont.Dispose();
        _updateFont.Dispose();
    }
}
