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

    public event Action? ToggleRequested;
    public event Action? QuitRequested;

    public TrayIcon(bool initialEnabled)
    {
        var menu = new ContextMenuStrip();
        var toggle = new ToolStripMenuItem("Enabled") { Checked = initialEnabled, CheckOnClick = true };
        toggle.Click += (_, _) => ToggleRequested?.Invoke();
        menu.Items.Add(toggle);
        menu.Items.Add(new ToolStripSeparator());
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

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _iconImage.Dispose();
    }
}
