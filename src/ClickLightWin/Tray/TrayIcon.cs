using System.Drawing;
using System.Windows.Forms; // NotifyIcon, ContextMenuStrip

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
    private readonly Settings _settings;
    private readonly LaunchAtLoginController _launchAtLogin;
    private readonly Action _persist;

    private readonly ToolStripMenuItem _enabledItem;
    private readonly ToolStripMenuItem _laserItem;
    private readonly ToolStripMenuItem _releaseItem;
    private readonly ToolStripMenuItem _dragItem;
    private readonly ToolStripMenuItem _launchItem;
    private readonly List<(ToolStripMenuItem Item, double Value)> _sizeItems = [];
    private readonly List<(ToolStripMenuItem Item, double Value)> _durationItems = [];

    public TrayIcon(Settings settings, LaunchAtLoginController launchAtLogin,
                    Action persist, Action openSettings, Action quit)
    {
        _settings = settings;
        _launchAtLogin = launchAtLogin;
        _persist = persist;

        var menu = new ContextMenuStrip
        {
            Renderer = new DarkMenuRenderer(),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9f)
        };
        menu.Opening += (_, _) => RefreshChecks();

        _enabledItem = Check("Enabled", () => Toggle(s => s.Enabled = !s.Enabled));
        _laserItem = Check("Laser pointer mode", () => Toggle(s => s.ShowLaserPointer = !s.ShowLaserPointer));
        _releaseItem = Check("Show release ring", () => Toggle(s => s.ShowRelease = !s.ShowRelease));
        _dragItem = Check("Show drag trail", () => Toggle(s => s.ShowDrag = !s.ShowDrag));
        _launchItem = Check("Launch at login", () => _launchAtLogin.SetEnabled(!_launchAtLogin.IsEnabled));

        menu.Items.Add(_enabledItem);
        menu.Items.Add(_laserItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_releaseItem);
        menu.Items.Add(_dragItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(PresetSubmenu("Size", Presets.Sizes, _sizeItems, v => _settings.BaseDiameterDips = v));
        menu.Items.Add(PresetSubmenu("Duration", Presets.Durations, _durationItems, v => _settings.PulseDurationMs = v));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_launchItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Item("Settings...", openSettings));
        menu.Items.Add(Item("About ClickLight", ShowAbout));
        menu.Items.Add(Item("Quit", quit));

        _iconImage = AppIconFactory.CreatePulseIcon();
        _icon = new NotifyIcon
        {
            Icon = _iconImage,
            Text = "ClickLight",
            Visible = true,
            ContextMenuStrip = menu
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
        _launchItem.Checked = _launchAtLogin.IsEnabled;
        foreach (var (item, value) in _sizeItems) item.Checked = Near(_settings.BaseDiameterDips, value);
        foreach (var (item, value) in _durationItems) item.Checked = Near(_settings.PulseDurationMs, value);
    }

    private static bool Near(double a, double b) => Math.Abs(a - b) < 0.5;

    private static void ShowAbout() => MessageBox.Show(
        "ClickLight for Windows\n\nHighlights your mouse clicks on screen during demos and "
        + "screen sharing.\nToggle highlighting with Ctrl+Alt+L.",
        "About ClickLight", MessageBoxButtons.OK, MessageBoxIcon.Information);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _iconImage.Dispose();
    }
}
