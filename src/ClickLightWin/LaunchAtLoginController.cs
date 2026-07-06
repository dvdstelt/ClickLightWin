using Microsoft.Win32;

namespace ClickLightWin;

/// <summary>
/// Toggles launch-at-login by writing the executable path under the per-user
/// HKCU Run key. The registry is the source of truth, so state survives without
/// being mirrored into settings.json. Maps to LaunchAtLoginController.swift,
/// which uses SMAppService on macOS.
/// </summary>
public sealed class LaunchAtLoginController
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ClickLightWin";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string;
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (key is null) return;

        if (enabled)
        {
            var exe = Environment.ProcessPath;
            if (exe is not null)
                key.SetValue(ValueName, $"\"{exe}\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
