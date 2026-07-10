using System.Windows.Interop;
using ClickLightWin.Interop;

namespace ClickLightWin;

/// <summary>
/// Registers the system-wide hotkeys on a message-only window: Ctrl+Shift+L to
/// toggle ClickLight and Ctrl+Shift+C to clear annotations. The Windows analogue
/// of HotKeyManager.swift, which uses Carbon RegisterEventHotKey on macOS.
/// </summary>
public sealed class HotKeyManager : IDisposable
{
    private const int ToggleHotKeyId = 1;
    private const int ClearHotKeyId = 2;
    private const uint VkL = 0x4C; // 'L'
    private const uint VkC = 0x43; // 'C'

    private HwndSource? _source;

    public event Action? TogglePressed;
    public event Action? ClearPressed;

    /// <summary>False when another application already owns the combination.</summary>
    public bool ToggleRegistered { get; private set; }

    /// <summary>False when another application already owns the combination.</summary>
    public bool ClearRegistered { get; private set; }

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

        const uint mod = NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT;
        ToggleRegistered = NativeMethods.RegisterHotKey(_source.Handle, ToggleHotKeyId, mod, VkL);
        ClearRegistered = NativeMethods.RegisterHotKey(_source.Handle, ClearHotKeyId, mod, VkC);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg != NativeMethods.WM_HOTKEY) return 0;
        switch ((int)wParam)
        {
            case ToggleHotKeyId: TogglePressed?.Invoke(); handled = true; break;
            case ClearHotKeyId: ClearPressed?.Invoke(); handled = true; break;
        }
        return 0;
    }

    public void Dispose()
    {
        if (_source is null) return;
        if (ToggleRegistered) NativeMethods.UnregisterHotKey(_source.Handle, ToggleHotKeyId);
        if (ClearRegistered) NativeMethods.UnregisterHotKey(_source.Handle, ClearHotKeyId);
        _source.RemoveHook(WndProc);
        _source.Dispose();
        _source = null;
    }
}
