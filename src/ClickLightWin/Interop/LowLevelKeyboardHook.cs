using System.Runtime.InteropServices;
using System.Windows.Threading;
using ClickLightWin.Interop;

namespace ClickLightWin;

/// <summary>
/// System-wide low-level keyboard hook for the live shortcut display. It reacts
/// ONLY to modifier combinations (Ctrl / Alt / Win + a key) and never to plain
/// typing, so it cannot capture passwords or ordinary text. Nothing is stored or
/// transmitted; each combo becomes a transient on-screen label. Installed only
/// while the feature is enabled. Maps to the keyDown branch of ClickEventTap.swift.
/// </summary>
public sealed class LowLevelKeyboardHook : IDisposable
{
    private readonly NativeMethods.LowLevelMouseProc _proc; // same (nCode, wParam, lParam) signature
    private readonly HashSet<int> _down = [];
    private nint _hookHandle;
    private Dispatcher? _dispatcher;

    /// <summary>Raised on the UI thread with the key tokens, e.g. ["Ctrl", "Shift", "P"].</summary>
    public event Action<IReadOnlyList<string>>? ShortcutDetected;

    public LowLevelKeyboardHook() => _proc = HookCallback;

    public bool IsInstalled => _hookHandle != 0;

    public void Install()
    {
        if (_hookHandle != 0) return;
        _dispatcher = Dispatcher.CurrentDispatcher;
        var hMod = NativeMethods.GetModuleHandleW(null);
        _hookHandle = NativeMethods.SetWindowsHookExW(NativeMethods.WH_KEYBOARD_LL, _proc, hMod, 0);
        if (_hookHandle == 0)
            throw new InvalidOperationException(
                $"SetWindowsHookEx (keyboard) failed (Win32 error {Marshal.GetLastWin32Error()}).");
    }

    public void Uninstall()
    {
        if (_hookHandle == 0) return;
        NativeMethods.UnhookWindowsHookEx(_hookHandle);
        _hookHandle = 0;
        _down.Clear();
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            var msg = (int)wParam;
            var data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            var vk = (int)data.vkCode;

            if (msg is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN)
            {
                // _down.Add returns false if already present, so auto-repeat is ignored.
                if (!IsModifier(vk) && _down.Add(vk))
                {
                    var keys = BuildShortcut(vk);
                    // Queue instead of invoking inline: UI work must never run inside
                    // the OS hook callback (latency + hook-timeout eviction risk).
                    if (keys is not null) _dispatcher?.InvokeAsync(() => ShortcutDetected?.Invoke(keys));
                }
            }
            else if (msg is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP)
            {
                _down.Remove(vk);
            }
        }
        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private static bool IsModifier(int vk) =>
        vk is NativeMethods.VK_SHIFT or NativeMethods.VK_CONTROL or NativeMethods.VK_MENU
            or NativeMethods.VK_LWIN or NativeMethods.VK_RWIN
            or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5; // L/R Shift, Ctrl, Alt

    private static IReadOnlyList<string>? BuildShortcut(int vk) => BuildShortcut(
        vk,
        ctrl: NativeMethods.IsDown(NativeMethods.VK_CONTROL),
        alt: NativeMethods.IsDown(NativeMethods.VK_MENU),
        shift: NativeMethods.IsDown(NativeMethods.VK_SHIFT),
        win: NativeMethods.IsDown(NativeMethods.VK_LWIN) || NativeMethods.IsDown(NativeMethods.VK_RWIN),
        rightAlt: NativeMethods.IsDown(NativeMethods.VK_RMENU));

    /// <summary>Pure shortcut-building logic, separated from live key state for testability.</summary>
    internal static IReadOnlyList<string>? BuildShortcut(int vk, bool ctrl, bool alt, bool shift, bool win, bool rightAlt)
    {
        // AltGr (right Alt) arrives as Ctrl+Alt on international layouts (e.g. US
        // International accents like AltGr+A). That is plain typing, not a
        // shortcut, so it must never be shown.
        if (ctrl && rightAlt) return null;

        // Privacy filter: require Ctrl/Alt/Win. Shift-only (e.g. capital letters) is
        // plain typing and is never shown.
        if (!ctrl && !alt && !win) return null;

        var name = KeyName(vk);
        if (name is null) return null;

        var keys = new List<string>(5);
        if (ctrl) keys.Add("Ctrl");
        if (alt) keys.Add("Alt");
        if (shift) keys.Add("Shift");
        if (win) keys.Add("Win");
        keys.Add(name);
        return keys;
    }

    internal static string? KeyName(int vk) => vk switch
    {
        >= 0x41 and <= 0x5A => ((char)vk).ToString(),              // A-Z
        >= 0x30 and <= 0x39 => ((char)vk).ToString(),              // 0-9
        >= 0x60 and <= 0x69 => "Num" + (char)('0' + (vk - 0x60)),  // numpad 0-9
        >= 0x70 and <= 0x87 => "F" + (vk - 0x6F),                  // F1-F24
        0x0D => "Enter",
        0x1B => "Esc",
        0x09 => "Tab",
        0x20 => "Space",
        0x08 => "Back",
        0x2E => "Del",
        0x2D => "Ins",
        0x24 => "Home",
        0x23 => "End",
        0x21 => "PgUp",
        0x22 => "PgDn",
        0x25 => "←", // left
        0x26 => "↑", // up
        0x27 => "→", // right
        0x28 => "↓", // down
        0xBB => "+",
        0xBD => "-",
        0xBC => ",",
        0xBE => ".",
        0xBF => "/",
        0xBA => ";",
        0xC0 => "`",
        0xDB => "[",
        0xDD => "]",
        0xDC => "\\",
        0xDE => "'",
        _ => null
    };

    public void Dispose() => Uninstall();
}
