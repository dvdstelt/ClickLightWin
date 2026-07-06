using System.Windows.Interop;
using ClickLightWin.Interop;

namespace ClickLightWin;

/// <summary>
/// Registers a system-wide toggle hotkey (Ctrl+Alt+L) on a message-only window
/// and raises <see cref="TogglePressed"/> when it fires. The Windows analogue of
/// HotKeyManager.swift, which uses Carbon RegisterEventHotKey on macOS.
/// </summary>
public sealed class HotKeyManager : IDisposable
{
    private const int ToggleHotKeyId = 1;
    private const uint VkL = 0x4C; // virtual-key code for 'L'

    private HwndSource? _source;

    public event Action? TogglePressed;

    /// <summary>Whether the hotkey registered successfully (false if another app owns it).</summary>
    public bool IsRegistered { get; private set; }

    public void Register()
    {
        // A message-only window receives WM_HOTKEY without a visible or taskbar window.
        var parameters = new HwndSourceParameters("ClickLightWinHotKeyWindow")
        {
            Width = 0,
            Height = 0,
            ParentWindow = NativeMethods.HWND_MESSAGE
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);

        IsRegistered = NativeMethods.RegisterHotKey(
            _source.Handle, ToggleHotKeyId,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT,
            VkL);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && (int)wParam == ToggleHotKeyId)
        {
            TogglePressed?.Invoke();
            handled = true;
        }
        return 0;
    }

    public void Dispose()
    {
        if (_source is null) return;
        if (IsRegistered)
            NativeMethods.UnregisterHotKey(_source.Handle, ToggleHotKeyId);
        _source.RemoveHook(WndProc);
        _source.Dispose();
        _source = null;
    }
}
