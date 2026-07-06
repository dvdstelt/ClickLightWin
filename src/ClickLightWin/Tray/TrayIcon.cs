using System.Windows.Forms; // NotifyIcon, ContextMenuStrip

namespace ClickLightWin.Tray;

/// <summary>
/// System-tray presence and menu. WinForms NotifyIcon is the least-ceremony
/// option and needs no extra package. Maps to StatusController.swift /
/// StatusMenuConfiguration.swift in the macOS original.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly System.Drawing.Icon _iconImage;
    private readonly ToolStripMenuItem _toggleItem;
    private readonly ToolStripMenuItem _launchItem;

    public event Action? ToggleRequested;
    public event Action? LaunchAtLoginRequested;
    public event Action? SettingsRequested;
    public event Action? QuitRequested;

    public TrayIcon(bool initialEnabled, bool initialLaunchAtLogin)
    {
        var menu = new ContextMenuStrip();
        // No CheckOnClick: AppController owns these states and drives the
        // checkmarks via SetEnabled/SetLaunchAtLogin, so the hotkey and the menu
        // stay in sync.
        _toggleItem = new ToolStripMenuItem("Enabled") { Checked = initialEnabled };
        _toggleItem.Click += (_, _) => ToggleRequested?.Invoke();
        menu.Items.Add(_toggleItem);

        _launchItem = new ToolStripMenuItem("Launch at login") { Checked = initialLaunchAtLogin };
        _launchItem.Click += (_, _) => LaunchAtLoginRequested?.Invoke();
        menu.Items.Add(_launchItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings...", null, (_, _) => SettingsRequested?.Invoke());
        menu.Items.Add("Quit", null, (_, _) => QuitRequested?.Invoke());

        _iconImage = AppIconFactory.CreatePulseIcon();
        _icon = new NotifyIcon
        {
            Icon = _iconImage,
            Text = "ClickLight",
            Visible = true,
            ContextMenuStrip = menu
        };
    }

    /// <summary>Reflect the current enabled state in the menu checkmark.</summary>
    public void SetEnabled(bool enabled) => _toggleItem.Checked = enabled;

    /// <summary>Reflect the current launch-at-login state in the menu checkmark.</summary>
    public void SetLaunchAtLogin(bool enabled) => _launchItem.Checked = enabled;

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _iconImage.Dispose();
    }
}
